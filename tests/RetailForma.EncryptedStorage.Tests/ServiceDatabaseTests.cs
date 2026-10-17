using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailForma.ServiceApplication;
using RetailForma.DataService;
using RetailForma.Core.Entities;
using RetailForma.EncryptedStorage;
using RetailForma.Security;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class ServiceDatabaseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "RetailForma-private-service-test-" + Guid.NewGuid().ToString("N"));
    private readonly RSA recovery = RSA.Create(3072);
    private const string Password = "Individual technical test password!";
    private static TechnicalProvisionInput Provision => new("technician", "Test Technician", Password, Password, "ISOLATED TEST COMPANY");
    public ServiceDatabaseTests() => Directory.CreateDirectory(root);
    private ServiceDatabase Store() => new(root, recovery.ExportSubjectPublicKeyInfoPem(), () => { }, Security);
    private static DirectorySecurity Security()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var security = new DirectorySecurity(); security.SetOwner(identity.User!); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    [Fact]
    public void ProvisionIsExplicitAtomicEncryptedAndNeverOverwritesAnExistingCompany()
    {
        using (var store = Store())
        {
            Assert.False(store.Configured); Assert.False(Directory.Exists(Path.Combine(root, "Data")));
            Assert.Throws<InvalidOperationException>(() => store.Open());
            store.ProvisionNew(Provision, "TEST-OPERATOR");
            Assert.True(store.Status().TechnicalAccountExists); Assert.Equal("ISOLATED TEST COMPANY", store.Status().Company);
            Assert.Throws<InvalidOperationException>(() => store.ProvisionNew(Provision, "TEST-OPERATOR"));
            using var db = store.Open(); Assert.Single(db.Users.Where(u => u.Role == UserRole.SystemAdministrator));
            Assert.DoesNotContain(db.Users, u => u.Username == "admin");
        }
        using var reopened = Store(); Assert.True(reopened.Configured);
        var bytes = File.ReadAllBytes(reopened.DatabasePath);
        Assert.True(bytes.AsSpan().IndexOf("SQLite format 3"u8) < 0);
        Assert.True(bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(Password)) < 0);
    }

    [Fact]
    public void DeveloperEnvelopeRecoversTheActualDatabaseKeyWithoutTheServiceDpapiSecret()
    {
        using var store = Store(); store.ProvisionNew(Provision, "TEST-OPERATOR");
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "Data", "key-state.json")));
        var data = json.RootElement;
        var context = new RecoveryContext(data.GetProperty("InstallationId").GetGuid(), data.GetProperty("ResourceId").GetGuid(), RecoveryPurpose.DatabaseKey);
        var key = DeveloperRecoveryEnvelope.Open(Convert.FromBase64String(data.GetProperty("RecoveryEnvelope").GetString()!), context, recovery);
        try { using var db = SqlCipherConnections.OpenExisting(store.DatabasePath, key); SqlCipherConnections.VerifyIntegrity(db); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    [Fact]
    public void RecoveryRequiresDeveloperProofPreservesRoleAndInvalidatesAllPriorSessions()
    {
        using var store = Store(); store.ProvisionNew(Provision, "TEST-OPERATOR");
        var operations = new ApplicationOperations(store.Open, () => { }); var session = new ApplicationSession("TEST-WINDOWS-SID");
        operations.Execute(session, Operation.Login, WireJson.Element(new LoginInput("technician", Password)));
        var request = store.IssueReset("technician"); var challenge = Convert.FromBase64String(request.Challenge);
        var response = new AccountRecoveryResponse(1, request.Challenge,
            Convert.ToBase64String(recovery.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
        var next = Password + " temporary";
        using var other = RSA.Create(3072);
        Assert.Throws<CryptographicException>(() => store.ResetAccount(new(response with { Signature = Convert.ToBase64String(other.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) }, next, next), "TEST-OPERATOR"));
        store.ResetAccount(new(response, next, next), "TEST-OPERATOR");
        Assert.Throws<UnauthorizedAccessException>(() => operations.Execute(session, Operation.Products, WireJson.Element(new { })));
        Assert.Throws<CryptographicException>(() => store.ResetAccount(new(response, next, next), "TEST-OPERATOR"));
        var fresh = new ApplicationSession("TEST-WINDOWS-SID");
        var actor = Assert.IsType<ClientUser>(operations.Execute(fresh, Operation.Login, WireJson.Element(new LoginInput("technician", next))));
        Assert.True(actor.MustChangePassword); Assert.Equal(UserRole.SystemAdministrator, actor.Role);
        Assert.Throws<UnauthorizedAccessException>(() => operations.Execute(fresh, Operation.Products, WireJson.Element(new { })));
        operations.Execute(fresh, Operation.ChangePassword, WireJson.Element(new ChangePasswordInput(next, Password, Password)));
        using var db = store.Open();
        var audit = Assert.Single(db.AuditLogs.Where(a => a.Action == "DeveloperAccountRecovery"));
        Assert.DoesNotContain(next, audit.Details!); Assert.DoesNotContain(response.Signature, audit.Details!);
    }

    [Fact]
    public void MissingStateOrChangedRecoveryTrustRefusesBlankFallback()
    {
        string path;
        using (var store = Store()) { store.ProvisionNew(Provision, "TEST-OPERATOR"); path = store.DatabasePath; }
        var before = File.ReadAllBytes(path);
        using (var other = RSA.Create(3072))
            Assert.Throws<InvalidDataException>(() => new ServiceDatabase(root, other.ExportSubjectPublicKeyInfoPem(), () => { }, Security));
        File.Move(Path.Combine(root, "Data", "key-state.json"), Path.Combine(root, "preserved-key-state.json"));
        Assert.Throws<InvalidDataException>(() => Store()); Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void InitialImportIsConnectionBoundVerifiedPreservesAccountsAndCannotOverwriteACompany()
    {
        var sourceRoot = Path.Combine(root, "source-installation"); Directory.CreateDirectory(sourceRoot);
        using var source = new ServiceDatabase(sourceRoot, recovery.ExportSubjectPublicKeyInfoPem(), () => { }, Security);
        source.ProvisionNew(Provision, "TEST-OPERATOR");
        var work = Path.Combine(root, "source-work"); Directory.CreateDirectory(work);
        var archive = Path.Combine(root, "original.zip");
        source.CreatePortableBackup(archive, work, Password, false); var original = File.ReadAllBytes(archive);
        var request = new RestoreUploadInput(original.Length, Convert.ToHexString(SHA256.HashData(original)));
        using var destination = Store(); using var importer = new CompanyImport(destination);
        var session = new ApplicationSession("TEST-TECHNICIAN"); var other = new ApplicationSession("OTHER-TECHNICIAN");
        var id = Assert.IsType<Guid>(importer.Execute(session, Operation.BeginCompanyImport, WireJson.Element(request)));
        Assert.Throws<UnauthorizedAccessException>(() => importer.Execute(other, Operation.CancelCompanyImport, WireJson.Element(new IdInput(id))));
        Assert.Throws<InvalidOperationException>(() => importer.Execute(session, Operation.CommitCompanyImport, WireJson.Element(new IdInput(id))));
        for (var offset = 0; offset < original.Length;)
        {
            var count = Math.Min(ServiceBackups.ChunkBytes, original.Length - offset);
            importer.Execute(session, Operation.WriteCompanyImportChunk, WireJson.Element(new RestoreChunkInput(id, offset, original.AsSpan(offset, count).ToArray()))); offset += count;
        }
        Assert.Throws<ArgumentException>(() => importer.Execute(session, Operation.PreviewCompanyImport, WireJson.Element(new PrepareRestoreInput(id, Password + "wrong"))));
        Assert.False(destination.Configured);
        var preview = Assert.IsType<RestorePreview>(importer.Execute(session, Operation.PreviewCompanyImport, WireJson.Element(new PrepareRestoreInput(id, Password))));
        Assert.Equal("ISOLATED TEST COMPANY", preview.Company);
        var status = Assert.IsType<ServiceStatusInfo>(importer.Execute(session, Operation.CommitCompanyImport, WireJson.Element(new IdInput(id))));
        Assert.True(status.Configured); Assert.NotEqual(source.Status().InstallationId, status.InstallationId);
        Assert.Equal(original, File.ReadAllBytes(archive));
        using var db = destination.Open(); Assert.Single(db.Users); Assert.Single(db.Users.Where(u => u.Username == "technician"));
        Assert.Single(db.AuditLogs.Where(a => a.Action == "ProtectedCompanyImported"));
        Assert.True(File.Exists(Path.Combine(destination.DataDirectory, "import-safety.zip")));
        var service = new ApplicationOperations(destination.Open, () => { }); var authenticated = new ApplicationSession("TEST-SIGNIN");
        service.Execute(authenticated, Operation.Login, WireJson.Element(new LoginInput("technician", Password)));
        using var backups = new ServiceBackups(destination, service);
        Assert.True(Assert.IsType<BackupPhraseState>(backups.Execute(authenticated, Operation.BackupPhraseState, WireJson.Element(new { }))).Configured);
        Assert.Throws<InvalidOperationException>(() => importer.Execute(session, Operation.BeginCompanyImport, WireJson.Element(request)));
    }

    public void Dispose() { recovery.Dispose(); Directory.Delete(root, true); }
}
