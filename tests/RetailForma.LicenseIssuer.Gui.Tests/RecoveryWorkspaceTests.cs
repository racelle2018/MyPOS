using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RetailForma.ServiceApplication;
using RetailForma.LicenseIssuer.Gui.Backend;
using RetailForma.Security;
using Xunit;

namespace RetailForma.LicenseIssuer.Gui.Tests;

public sealed class RecoveryWorkspaceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "RetailForma-recovery-issuer-test-" + Guid.NewGuid().ToString("N"));
    private const string Passphrase = "Individual test recovery key phrase!";
    public RecoveryWorkspaceTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void GuiBackendCreatesSeparateRecoveryKeysAndOnlySignsThePreviewedInstallationRequest()
    {
        var keys = Path.Combine(folder, "keys"); IssuerWorkspace.CreateKeys(keys, Passphrase, Passphrase, SigningPurpose.Recovery);
        var privatePath = Path.Combine(keys, "recovery-private.encrypted.pem");
        using var publicKey = RSA.Create(); publicKey.ImportFromPem(File.ReadAllText(Path.Combine(keys, "recovery-public.pem")));
        Assert.Contains("ENCRYPTED PRIVATE KEY", File.ReadAllText(privatePath));
        var installation = Guid.NewGuid(); var account = Guid.NewGuid();
        using var gate = new DeveloperRecoveryAuthorization(installation, publicKey.ExportSubjectPublicKeyInfo());
        var challenge = gate.IssuePasswordReset(account);
        var request = new AccountRecoveryRequest(1, installation, account, "owner", "TEST COMPANY",
            DeveloperRecoveryEnvelope.PublicKeyId(publicKey), Convert.ToBase64String(challenge));
        var requestPath = Path.Combine(folder, "request.json"); File.WriteAllText(requestPath, JsonSerializer.Serialize(request));
        Assert.Equal(request, RecoveryWorkspace.ReadRequest(requestPath));
        var output = Path.Combine(folder, "authorization.json");
        Assert.ThrowsAny<CryptographicException>(() => RecoveryWorkspace.Authorize(requestPath, privatePath, "wrong phrase", output, request));
        Assert.False(File.Exists(output));
        RecoveryWorkspace.Authorize(requestPath, privatePath, Passphrase, output, request);
        var response = JsonSerializer.Deserialize<AccountRecoveryResponse>(File.ReadAllText(output))!;
        gate.ConsumePasswordReset(Convert.FromBase64String(response.Challenge), Convert.FromBase64String(response.Signature), account);
        Assert.Throws<CryptographicException>(() => gate.ConsumePasswordReset(challenge, Convert.FromBase64String(response.Signature), account));
        var history = File.ReadAllText(Path.Combine(keys, "recovery-journal.jsonl"));
        Assert.DoesNotContain(Passphrase, history); Assert.DoesNotContain("PRIVATE KEY", history); Assert.DoesNotContain(request.Challenge, history);
        Assert.Throws<IOException>(() => RecoveryWorkspace.Authorize(requestPath, privatePath, Passphrase, output, request));
        File.WriteAllText(requestPath, JsonSerializer.Serialize(request with { Company = "CHANGED AFTER PREVIEW" }));
        Assert.Throws<InvalidOperationException>(() => RecoveryWorkspace.Authorize(requestPath, privatePath, Passphrase, Path.Combine(folder, "different.json"), request));
    }

    [Fact]
    public void MalformedOrMismatchedRecoveryTargetsAreRejectedBeforePrivateKeyUse()
    {
        using var key = RSA.Create(3072); var installation = Guid.NewGuid(); var account = Guid.NewGuid();
        using var gate = new DeveloperRecoveryAuthorization(installation, key.ExportSubjectPublicKeyInfo());
        var request = new AccountRecoveryRequest(1, installation, account, "owner", "TEST", DeveloperRecoveryEnvelope.PublicKeyId(key),
            Convert.ToBase64String(gate.IssuePasswordReset(account)));
        var path = Path.Combine(folder, "request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request with { AccountId = Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() => RecoveryWorkspace.ReadRequest(path));
        File.WriteAllText(path, JsonSerializer.Serialize(request with { Challenge = "not a challenge" }));
        Assert.ThrowsAny<ArgumentException>(() => RecoveryWorkspace.ReadRequest(path));
    }
    public void Dispose() => Directory.Delete(folder, true);
}
