using System.Reflection;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text.Json;
using RetailForma.ServiceApplication;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using RetailForma.Desktop.Licensing;
using RetailForma.EncryptedStorage;
using RetailForma.LocalTransport;

namespace RetailForma.DataService;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-check" }))
        {
            // Packaging probe only. No storage initialization, service installation, database access or real key generation.
            var trust = DataWindowsService.ReadAndValidateTrust();
            using var recovery = RSA.Create(); recovery.ImportFromPem(trust.Recovery);
            using var paid = RSA.Create(); paid.ImportFromPem(trust.Paid);
            Console.WriteLine(JsonSerializer.Serialize(new { Format = 1, SqlCipher = SqlCipherConnections.ProviderVersion,
                RecoveryKeyId = RetailForma.Security.DeveloperRecoveryEnvelope.PublicKeyId(recovery),
                PaidKeyId = RetailForma.Security.DeveloperRecoveryEnvelope.PublicKeyId(paid), TrialEnabled = trust.Trial != null }));
            return 0;
        }
        if (args.Length != 0) throw new ArgumentException("Only --self-check or normal Windows service startup is supported.");
        ServiceBase.Run(new DataWindowsService()); return 0;
    }
}

internal sealed class DataWindowsService : ServiceBase
{
    private CancellationTokenSource? stopping;
    private Task? listener;
    private ServiceDatabase? storage;
    private LicenseClient? licensing;
    private HttpClient? http;
    private ServiceBackups? backups;
    private CompanyImport? companyImport;
    public DataWindowsService() { ServiceName = ServiceStoragePolicy.ServiceName; CanStop = true; AutoLog = true; }
    protected override void OnStart(string[] args)
    {
        RequestAdditionalTime(180000); // Existing-data schema checks/safety snapshots may outlast SCM's default start window.
        if (args.Length != 0) throw new ArgumentException("The data service does not accept caller-selected storage paths.");
        var trust = ReadAndValidateTrust();
        var recovery = trust.Recovery; var paid = trust.Paid; var trial = trust.Trial;
        storage = new ServiceDatabase(recovery);
        var endpoint = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "RetailFormaTrialEndpoint")?.Value;
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        licensing = new LicenseClient(paid, trial, string.IsNullOrWhiteSpace(endpoint) ? null : new Uri(endpoint),
            new LicenseCache(Path.Combine(Path.GetDirectoryName(storage.DataDirectory)!, "licensing")), DeviceIdentity.ReadFingerprint, http);
        var operations = new ApplicationOperations(storage.Open, () => licensing.ValidateAsync().GetAwaiter().GetResult());
        backups = new ServiceBackups(storage, operations);
        companyImport = new CompanyImport(storage);
        var server = new LocalApplicationServer(PipeWire.PipeName, LocalPipeSecurity.ServiceSid, (session, elevated, operation, payload) =>
        {
            if (operation is Operation.BeginCompanyImport or Operation.WriteCompanyImportChunk or Operation.PreviewCompanyImport or Operation.CommitCompanyImport or Operation.CancelCompanyImport)
            {
                if (!elevated) throw new UnauthorizedAccessException("Initial import requires the elevated technician tool.");
                return companyImport.Execute(session, operation, payload);
            }
            if (operation is Operation.ServiceStatus or Operation.ProvisionNewCompany or Operation.ProvisionTechnicalAccount or
                Operation.IssueAccountRecovery or Operation.CompleteAccountRecovery)
            {
                if (!elevated) throw new UnauthorizedAccessException("Open the technician tool with Windows administrator approval.");
                switch (operation)
                {
                    case Operation.ServiceStatus: return storage.Status();
                    case Operation.ProvisionNewCompany: storage.ProvisionNew(WireJson.Read<TechnicalProvisionInput>(payload), session.WindowsSid); return storage.Status();
                    case Operation.ProvisionTechnicalAccount: storage.ProvisionTechnical(WireJson.Read<TechnicalProvisionInput>(payload), session.WindowsSid); return storage.Status();
                    case Operation.IssueAccountRecovery: return storage.IssueReset(WireJson.Read<UsernameInput>(payload).Username);
                    case Operation.CompleteAccountRecovery: storage.ResetAccount(WireJson.Read<AccountRecoveryInput>(payload), session.WindowsSid); session.Clear(); return null;
                }
            }
            if (!storage.Configured)
            {
                if (operation == Operation.PublicIdentity) return new Dictionary<string, string> { ["CompanyName"] = "Technician setup required", ["System.ServiceConfigured"] = "false" };
                throw new InvalidOperationException("Complete technician setup or migrate the existing company before signing in. No blank replacement database was created.");
            }
            if (operation is Operation.BackupPhraseState or Operation.VerifyBackupPhrase or Operation.ChangeBackupPhrase or Operation.CreateBackup or
                Operation.ReadBackupChunk or Operation.FinishBackupDownload or Operation.BeginRestoreUpload or Operation.WriteRestoreChunk or
                Operation.PrepareRestore or Operation.CommitRestore or Operation.CancelRestore)
                return backups.Execute(session, operation, payload);
            if (operation is Operation.LicenseRequest or Operation.ImportLicense or Operation.StartTrial or Operation.ValidateLicense)
            {
                var actor = (ClientUser)operations.Execute(session, Operation.CurrentUser, WireJson.Element(new { }))!;
                if (actor.Role != UserRole.SystemAdministrator) throw new UnauthorizedAccessException("Only the System Administrator can manage activation.");
                if (operation == Operation.LicenseRequest)
                {
                    using var db = storage.Open(); return licensing.Request(db.Settings.Find("CompanyName")?.Value ?? "");
                }
                if (operation == Operation.ValidateLicense) return licensing.ValidateAsync().GetAwaiter().GetResult();
                LicenseClaims claims;
                if (operation == Operation.ImportLicense) claims = licensing.ImportAsync(WireJson.Read<SignedLicenseMessage>(payload)).GetAwaiter().GetResult();
                else
                {
                    var start = WireJson.Read<TrialStartInput>(payload);
                    if (start.ActivationKey == null || start.ActivationKey.Length > 512) throw new ArgumentException("Invalid trial activation key.");
                    claims = licensing.StartTrialAsync(start.LicenseId, start.ActivationKey).GetAwaiter().GetResult();
                }
                using (var db = storage.Open())
                {
                    db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = actor.Id, Action = "LicenseActivated",
                        EntityName = "License", Details = $"{claims.Kind}; entitlement {claims.LicenseId}; validated by the local service." }); db.SaveChanges();
                }
                return claims;
            }
            return operations.Execute(session, operation, payload);
        }, details => System.Diagnostics.EventLog.WriteEntry(ServiceStoragePolicy.ServiceName,
            details.Length > 8000 ? details[..8000] : details, System.Diagnostics.EventLogEntryType.Error));
        stopping = new CancellationTokenSource();
        listener = Task.Run(() => server.RunAsync(stopping.Token));
        _ = listener.ContinueWith(_ => Environment.Exit(1), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }
    protected override void OnStop()
    {
        stopping?.Cancel(); listener?.GetAwaiter().GetResult();
        companyImport?.Dispose(); backups?.Dispose(); storage?.Dispose(); http?.Dispose(); stopping?.Dispose();
    }
    private static string? ReadTrust(string name, bool optional = false)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("Trust." + name + ".pem");
        if (stream == null)
        { if (optional) return null; throw new InvalidDataException("The service build is missing its " + name + " public trust key."); }
        using var reader = new StreamReader(stream); var pem = reader.ReadToEnd();
        if (pem.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new InvalidDataException("A private key must never enter the data service.");
        return pem;
    }

    internal sealed record Trust(string Recovery, string Paid, string? Trial);
    internal static Trust ReadAndValidateTrust()
    {
        var result = new Trust(ReadTrust("recovery")!, ReadTrust("paid")!, ReadTrust("trial", optional: true));
        using var recovery = RSA.Create(); recovery.ImportFromPem(result.Recovery);
        using var paid = RSA.Create(); paid.ImportFromPem(result.Paid);
        _ = RetailForma.Security.DeveloperRecoveryEnvelope.PublicKeyId(recovery);
        if (paid.KeySize < 3072 || CryptographicOperations.FixedTimeEquals(recovery.ExportSubjectPublicKeyInfo(), paid.ExportSubjectPublicKeyInfo()))
            throw new InvalidDataException("Recovery and paid licensing need separate RSA keys of at least 3072 bits.");
        var endpoint = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "RetailFormaTrialEndpoint")?.Value;
        if ((result.Trial != null) != !string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidDataException("Trial public trust and its HTTPS endpoint must be supplied together.");
        if (result.Trial != null)
        {
            using var trial = RSA.Create(); trial.ImportFromPem(result.Trial);
            if (trial.KeySize < 3072 || CryptographicOperations.FixedTimeEquals(recovery.ExportSubjectPublicKeyInfo(), trial.ExportSubjectPublicKeyInfo()) ||
                CryptographicOperations.FixedTimeEquals(paid.ExportSubjectPublicKeyInfo(), trial.ExportSubjectPublicKeyInfo()))
                throw new InvalidDataException("Trial, paid and recovery trust must be independent RSA keys of at least 3072 bits.");
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidDataException("Trial validation requires a credential-free HTTPS endpoint.");
        }
        return result;
    }
}
