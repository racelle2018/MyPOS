using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RetailForma.Core.Services;

namespace RetailForma.LicenseIssuer.Gui.Backend;

public enum SigningPurpose { Paid, Trial, Recovery }
public sealed record IssuerEvent(Guid EntitlementId, string Company, string Fingerprint, string? RetiredFingerprint,
    DateTimeOffset IssuedUtc, string Operator, string SignedFileSha256);

// Backend of the programmer-only GUI. The customer POS must never reference this project.
public static class IssuerWorkspace
{
    public static string Absolute(string path) => Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) :
        throw new ArgumentException("Choose an absolute file or folder path.");
    public static void OutsideClientAndRepository(string path)
    {
        var resolved = Absolute(path);
        if (File.Exists(resolved) && (File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException("Choose a private key file, not a filesystem link.");
        var cursor = new DirectoryInfo(Directory.Exists(resolved) ? resolved : Path.GetDirectoryName(resolved)!);
        while (cursor != null)
        {
            if (cursor.Exists && (cursor.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Private key paths must not traverse filesystem links.");
            if (Directory.Exists(Path.Combine(cursor.FullName, ".git")) || File.Exists(Path.Combine(cursor.FullName, "RetailForma.slnx")) ||
                File.Exists(Path.Combine(cursor.FullName, "RetailForma.Desktop.exe")) || File.Exists(Path.Combine(cursor.FullName, "RetailForma.Desktop.dll")) ||
                File.Exists(Path.Combine(cursor.FullName, "RetailForma.exe")) ||
                File.Exists(Path.Combine(cursor.FullName, "MyPos.slnx")) ||
                File.Exists(Path.Combine(cursor.FullName, "MyPos.Desktop.exe")) ||
                File.Exists(Path.Combine(cursor.FullName, "MyPos.Desktop.dll")))
                throw new ArgumentException("Private signing keys must stay outside the repository and customer POS installation.");
            cursor = cursor.Parent;
        }
    }
    public static void CreateKeys(string folder, string password, string confirmation, SigningPurpose purpose)
    {
        folder = Absolute(folder); OutsideClientAndRepository(folder);
        if (!Enum.IsDefined(purpose)) throw new ArgumentException("Choose paid, trial or developer recovery keys.");
        if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Choose a new key folder. Existing folders are never overwritten.");
        if (password.Length is < 16 or > 256 || string.IsNullOrWhiteSpace(password) || password != confirmation)
            throw new ArgumentException("Passphrases must match and contain at least 16 characters.");
        using var key = RSA.Create(3072);
        var prefix = purpose switch { SigningPurpose.Paid => "paid", SigningPurpose.Trial => "trial", _ => "recovery" };
        var privatePem = key.ExportEncryptedPkcs8PrivateKeyPem(password,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 200000));
        Directory.CreateDirectory(folder);
        WriteNew(Path.Combine(folder, prefix + "-private.encrypted.pem"), privatePem);
        WriteNew(Path.Combine(folder, prefix + "-public.pem"), key.ExportSubjectPublicKeyInfoPem());
    }
    public static LicenseRequest ReadRequest(string path)
    {
        var request = JsonSerializer.Deserialize<LicenseRequest>(ReadBounded(Absolute(path), 32768)) ?? throw new InvalidDataException("Missing device request.");
        if (request.Format != 1 || request.Product != "RetailForma.Basic" || string.IsNullOrWhiteSpace(request.Company) || request.Company.Length > 200 ||
            request.Fingerprint == null || request.Fingerprint.Length != 64 || request.Fingerprint.Any(c => !Uri.IsHexDigit(c)) ||
            request.Fingerprint != request.Fingerprint.ToUpperInvariant())
            throw new ArgumentException("Use a valid named-company RetailForma device request with a normalized SHA-256 fingerprint.");
        return request;
    }
    public static IReadOnlyList<IssuerEvent> History(string privateKeyPath)
    {
        var path = LedgerPath(privateKeyPath);
        if (!File.Exists(path)) return [];
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadLedger(file);
    }
    public static SignedLicenseMessage IssueLifetime(string privateKeyPath, string requestPath, string outputPath,
        Guid entitlement, string password, bool ownerPortal = false, bool replace = false, LicenseRequest? expectedRequest = null)
    {
        privateKeyPath = Absolute(privateKeyPath); OutsideClientAndRepository(privateKeyPath); outputPath = Absolute(outputPath);
        if (File.Exists(outputPath) || Directory.Exists(outputPath)) throw new IOException("Output already exists. No license is overwritten.");
        if (entitlement == Guid.Empty) throw new ArgumentException("Enter a valid entitlement ID.");
        var request = ReadRequest(requestPath);
        if (expectedRequest != null && request != expectedRequest)
            throw new InvalidOperationException("The device request changed after preview. Reload and verify the company/fingerprint.");
        using var key = RSA.Create(); key.ImportFromEncryptedPem(ReadBounded(privateKeyPath, 131072), password);
        var signed = LicenseIssuance.Lifetime(request, entitlement, key, DateTimeOffset.UtcNow, ownerPortal);
        using var ledger = new FileStream(LedgerPath(privateKeyPath), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var prior = ReadLedger(ledger).LastOrDefault(entry => entry.EntitlementId == entitlement);
        if (prior != null && prior.Fingerprint != request.Fingerprint && !replace)
            throw new InvalidOperationException("This entitlement belongs to another device. Authorize replacement only after verifying ownership.");
        if (replace && prior == null) throw new InvalidOperationException("No prior entitlement exists in this issuer ledger. Verify the original record before replacing it.");
        var serialized = JsonSerializer.Serialize(signed); WriteNew(outputPath, serialized);
        ledger.Seek(0, SeekOrigin.End);
        using var writer = new StreamWriter(ledger, leaveOpen: true);
        writer.WriteLine(JsonSerializer.Serialize(new IssuerEvent(entitlement, request.Company, request.Fingerprint,
            prior != null && prior.Fingerprint != request.Fingerprint ? prior.Fingerprint : null, DateTimeOffset.UtcNow,
            Environment.UserName, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))))));
        writer.Flush(); ledger.Flush(flushToDisk: true); return signed;
    }
    private static string LedgerPath(string key) => Path.Combine(Path.GetDirectoryName(Absolute(key))!, "issuer-journal.jsonl");
    private static List<IssuerEvent> ReadLedger(Stream stream)
    {
        if (stream.Length > 10 * 1024 * 1024) throw new InvalidDataException("Issuer ledger requires archival review; do not discard its history.");
        var entries = new List<IssuerEvent>(); using var reader = new StreamReader(stream, leaveOpen: true);
        while (reader.ReadLine() is { } line)
            if (line.Length > 0) entries.Add(JsonSerializer.Deserialize<IssuerEvent>(line) ?? throw new InvalidDataException("Invalid issuer ledger."));
        return entries;
    }
    private static string ReadBounded(string path, long maximum)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > maximum) throw new InvalidDataException("Selected file is too large.");
        using var reader = new StreamReader(file); return reader.ReadToEnd();
    }
    private static void WriteNew(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream); writer.Write(text); writer.Flush(); stream.Flush(flushToDisk: true);
    }
}
