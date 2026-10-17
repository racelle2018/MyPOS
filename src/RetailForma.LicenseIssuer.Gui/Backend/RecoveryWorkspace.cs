using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RetailForma.ServiceApplication;
using RetailForma.Security;
using RetailForma.EncryptedStorage;

namespace RetailForma.LicenseIssuer.Gui.Backend;

public static class RecoveryWorkspace
{
    public static AccountRecoveryRequest ReadRequest(string path)
    {
        using var file = new FileStream(IssuerWorkspace.Absolute(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 32768) throw new InvalidDataException("The recovery request is too large.");
        var request = JsonSerializer.Deserialize<AccountRecoveryRequest>(file, WireJson.Options) ?? throw new InvalidDataException("Missing recovery request.");
        if (request.Format != 1 || request.KeyId == null || request.KeyId.Length != 64 || request.KeyId.Any(c => !Uri.IsHexDigit(c)) ||
            request.Username == null || request.Username.Length is < 1 or > 128 || request.Company == null || request.Company.Length > 200 ||
            request.Challenge == null || request.Challenge.Length > 1024)
            throw new ArgumentException("Use an exported RetailForma account recovery request.");
        (Guid InstallationId, Guid AccountId) context;
        try { context = DeveloperRecoveryAuthorization.Describe(Convert.FromBase64String(request.Challenge)); }
        catch (FormatException) { throw new ArgumentException("The recovery challenge is not correctly encoded."); }
        if (context.InstallationId != request.InstallationId || context.AccountId != request.AccountId)
            throw new ArgumentException("The request identity does not match its challenge.");
        return request;
    }

    public static void Authorize(string requestPath, string privateKeyPath, string passphrase, string outputPath, AccountRecoveryRequest preview)
    {
        IssuerWorkspace.OutsideClientAndRepository(privateKeyPath);
        var request = ReadRequest(requestPath);
        if (request != preview) throw new InvalidOperationException("The request changed after preview. Reload and verify it.");
        using var keyFile = new FileStream(IssuerWorkspace.Absolute(privateKeyPath), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (keyFile.Length > 131072) throw new InvalidDataException("The selected private key file is too large.");
        using var reader = new StreamReader(keyFile); using var key = RSA.Create();
        key.ImportFromEncryptedPem(reader.ReadToEnd(), passphrase);
        if (DeveloperRecoveryEnvelope.PublicKeyId(key) != request.KeyId)
            throw new ArgumentException("The private recovery key does not match this installation. Licensing keys cannot authorize recovery.");
        var challenge = Convert.FromBase64String(request.Challenge);
        var response = new AccountRecoveryResponse(1, request.Challenge,
            Convert.ToBase64String(key.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, WireJson.Options);
        using (var output = new FileStream(IssuerWorkspace.Absolute(outputPath), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes); output.Flush(true); }
        // Never store passphrases, the private key, temporary account passwords or raw challenge bytes in the ledger.
        var ledger = Path.Combine(Path.GetDirectoryName(IssuerWorkspace.Absolute(privateKeyPath))!, "recovery-journal.jsonl");
        using var history = new FileStream(ledger, FileMode.Append, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(history, Encoding.UTF8);
        writer.WriteLine(JsonSerializer.Serialize(new { Format = 1, Action = "AuthorizeAccountPasswordReset", request.InstallationId,
            request.AccountId, request.KeyId, Operator = Environment.UserName, IssuedUtc = DateTimeOffset.UtcNow,
            AuthorizationSha256 = Convert.ToHexString(SHA256.HashData(bytes)) })); writer.Flush(); history.Flush(true);
    }

    public static void ReprotectBackup(string archivePath, string outputPath, string privateKeyPath, string privateKeyPassphrase, string newOwnerPassphrase)
    {
        IssuerWorkspace.OutsideClientAndRepository(privateKeyPath);
        using var file = new FileStream(IssuerWorkspace.Absolute(privateKeyPath), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 131072) throw new InvalidDataException("The private key file is too large.");
        using var reader = new StreamReader(file); using var key = RSA.Create();
        try { key.ImportFromEncryptedPem(reader.ReadToEnd(), privateKeyPassphrase); }
        catch (CryptographicException) { throw new ArgumentException("The encrypted private recovery key was not unlocked. Verify its file and passphrase."); }
        var work = Path.Combine(Path.GetDirectoryName(IssuerWorkspace.Absolute(privateKeyPath))!, ".recovery-work-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            try { RecoveryBackupPackage.ReprotectWithDeveloper(IssuerWorkspace.Absolute(archivePath), IssuerWorkspace.Absolute(outputPath), work, key, newOwnerPassphrase); }
            catch (CryptographicException) { throw new ArgumentException("The selected recovery key did not authenticate this backup, or its recovery metadata was altered."); }
        }
        finally { Directory.Delete(work, recursive: false); } // Refuse to delete unexpected contents; never sweep private-key folders.
    }
}
