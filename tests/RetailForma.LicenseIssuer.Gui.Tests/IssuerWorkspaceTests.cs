using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RetailForma.Core.Services;
using RetailForma.LicenseIssuer.Gui.Backend;
using Xunit;

namespace RetailForma.LicenseIssuer.Gui.Tests;

public sealed class IssuerWorkspaceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "retailforma-issuer-fixture-" + Guid.NewGuid().ToString("N"));
    private const string Password = "TEST-ONLY-passphrase-2026";
    public IssuerWorkspaceTests() => Directory.CreateDirectory(folder);
    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    private string Keys()
    {
        var keys = Path.Combine(folder, "paid"); IssuerWorkspace.CreateKeys(keys, Password, Password, SigningPurpose.Paid);
        return Path.Combine(keys, "paid-private.encrypted.pem");
    }
    private string Request(char fingerprint)
    {
        var path = Path.Combine(folder, fingerprint + ".request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new LicenseRequest(1, "TEST COMPANY", new string(fingerprint, 64))));
        return path;
    }
    [Fact]
    public void Separate_encrypted_keys_do_not_overwrite_existing_folders_or_allow_repository_storage()
    {
        var paid = Keys(); var trial = Path.Combine(folder, "trial");
        IssuerWorkspace.CreateKeys(trial, Password, Password, SigningPurpose.Trial);
        Assert.Contains("ENCRYPTED PRIVATE KEY", File.ReadAllText(paid));
        Assert.NotEqual(File.ReadAllText(Path.Combine(folder, "paid", "paid-public.pem")), File.ReadAllText(Path.Combine(trial, "trial-public.pem")));
        Assert.Throws<IOException>(() => IssuerWorkspace.CreateKeys(Path.GetDirectoryName(paid)!, Password, Password, SigningPurpose.Paid));
        Directory.CreateDirectory(Path.Combine(folder, "repository", ".git"));
        Assert.Throws<ArgumentException>(() => IssuerWorkspace.CreateKeys(Path.Combine(folder, "repository", "keys"), Password, Password, SigningPurpose.Paid));
        var client = Path.Combine(folder, "client"); Directory.CreateDirectory(client); File.WriteAllText(Path.Combine(client, "RetailForma.Desktop.exe"), "fixture");
        Assert.Throws<ArgumentException>(() => IssuerWorkspace.CreateKeys(Path.Combine(client, "keys"), Password, Password, SigningPurpose.Paid));
        var brandedClient = Path.Combine(folder, "retailforma-client"); Directory.CreateDirectory(brandedClient);
        File.WriteAllText(Path.Combine(brandedClient, "RetailForma.exe"), "fixture");
        Assert.Throws<ArgumentException>(() => IssuerWorkspace.CreateKeys(Path.Combine(brandedClient, "keys"), Password, Password, SigningPurpose.Paid));
        var legacyClient = Path.Combine(folder, "legacy-client"); Directory.CreateDirectory(legacyClient);
        File.WriteAllText(Path.Combine(legacyClient, "MyPos.Desktop.exe"), "fixture");
        Assert.Throws<ArgumentException>(() => IssuerWorkspace.CreateKeys(Path.Combine(legacyClient, "keys"), Password, Password, SigningPurpose.Paid));
    }
    [Fact]
    public void Issuance_and_explicit_replacement_share_entitlement_and_record_retired_fingerprint()
    {
        var key = Keys(); var id = Guid.NewGuid(); var output = Path.Combine(folder, "license.json");
        var first = IssuerWorkspace.IssueLifetime(key, Request('A'), output, id, Password);
        using var publicKey = RSA.Create(); publicKey.ImportFromPem(File.ReadAllText(Path.Combine(folder, "paid", "paid-public.pem")));
        Assert.Null(LicenseProtocol.VerifyLicense(first, publicKey, new string('A', 64)).ExpiresUtc);
        Assert.Throws<IOException>(() => IssuerWorkspace.IssueLifetime(key, Request('A'), output, id, Password));
        var replacement = Path.Combine(folder, "replacement.json"); var newRequest = Request('B');
        Assert.Throws<InvalidOperationException>(() => IssuerWorkspace.IssueLifetime(key, newRequest, replacement, id, Password));
        Assert.False(File.Exists(replacement));
        IssuerWorkspace.IssueLifetime(key, newRequest, replacement, id, Password, replace: true);
        var history = IssuerWorkspace.History(key); Assert.Equal(2, history.Count);
        Assert.Equal(new string('A', 64), history[1].RetiredFingerprint); Assert.Equal(id, history[1].EntitlementId);
        Assert.DoesNotContain(Password, File.ReadAllText(Path.Combine(folder, "paid", "issuer-journal.jsonl")));
    }
    [Fact]
    public void Invalid_passphrase_and_changed_preview_never_publish_license()
    {
        var key = Keys(); var requestPath = Request('A'); var preview = IssuerWorkspace.ReadRequest(requestPath); var output = Path.Combine(folder, "license.json");
        Assert.ThrowsAny<CryptographicException>(() => IssuerWorkspace.IssueLifetime(key, requestPath, output, Guid.NewGuid(), "wrong"));
        File.WriteAllText(requestPath, JsonSerializer.Serialize(preview with { Company = "CHANGED" }));
        Assert.Throws<InvalidOperationException>(() => IssuerWorkspace.IssueLifetime(key, requestPath, output, Guid.NewGuid(), Password, expectedRequest: preview));
        Assert.False(File.Exists(output)); Assert.Empty(IssuerWorkspace.History(key));
    }
    [Fact]
    public void Replacement_without_history_and_weak_key_passphrases_are_denied()
    {
        var key = Keys(); var output = Path.Combine(folder, "replacement.json");
        Assert.Throws<InvalidOperationException>(() => IssuerWorkspace.IssueLifetime(key, Request('A'), output, Guid.NewGuid(), Password, replace: true));
        Assert.False(File.Exists(output));
        Assert.Throws<ArgumentException>(() => IssuerWorkspace.CreateKeys(Path.Combine(folder, "weak"), "short", "short", SigningPurpose.Paid));
        Assert.False(Directory.Exists(Path.Combine(folder, "weak")));
    }
}
