using System.Security.Cryptography;
using System.Text;
using RetailForma.Core.Services;

namespace RetailForma.Tests;

public sealed class LicenseProtocolTests : IDisposable
{
    private readonly RSA issuer = RSA.Create(3072);
    private readonly RSA verifier = RSA.Create();
    private readonly string fingerprint = LicenseProtocol.Fingerprint("BOARD-TEST", "CPU-TEST", "46a8b21e-9a50-4a79-b005-4b1d6aef91c1");
    private readonly DateTimeOffset issued = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
    public LicenseProtocolTests() => verifier.ImportParameters(issuer.ExportParameters(false));
    public void Dispose() { issuer.Dispose(); verifier.Dispose(); }
    private LicenseClaims Claims(LicenseKind kind) => new(1, Guid.NewGuid(), fingerprint, kind, issued,
        kind == LicenseKind.Trial ? issued.AddDays(7) : null, ["Basic"]);

    [Fact]
    public void Lifetime_requires_only_signature_and_matching_device_not_online_or_expiry()
    {
        var claims = Claims(LicenseKind.Lifetime);
        Assert.Equal(claims.LicenseId, LicenseProtocol.VerifyLicense(LicenseProtocol.Sign(claims, issuer), verifier, fingerprint).LicenseId);
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(
            LicenseProtocol.Sign(claims with { ExpiresUtc = issued.AddYears(100) }, issuer), verifier, fingerprint));
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(
            LicenseProtocol.Sign(claims, issuer), verifier, new string('A', 64)));
    }

    [Fact]
    public void Signed_product_identity_is_required_and_legacy_or_other_products_are_rejected()
    {
        var claims = Claims(LicenseKind.Lifetime);
        Assert.Equal("RetailForma.Basic", LicenseProtocol.VerifyLicense(LicenseProtocol.Sign(claims, issuer), verifier, fingerprint).Product);
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(
            LicenseProtocol.Sign(claims with { Product = "MyPos.Basic" }, issuer), verifier, fingerprint));
        var legacy = new { claims.Format, claims.LicenseId, claims.Fingerprint, claims.Kind, claims.IssuedUtc, claims.ExpiresUtc, claims.Features };
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(LicenseProtocol.Sign(legacy, issuer), verifier, fingerprint));
    }

    [Fact]
    public void Tampering_and_another_issuer_do_not_grant_a_license()
    {
        var token = LicenseProtocol.Sign(Claims(LicenseKind.Lifetime), issuer);
        var bytes = Convert.FromBase64String(token.Payload); bytes[10] ^= 1;
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(token with { Payload = Convert.ToBase64String(bytes) }, verifier, fingerprint));
        using var other = RSA.Create(3072);
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(token, other, fingerprint));
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(token with { Signature = "malformed" }, verifier, fingerprint));
    }

    [Fact]
    public void Trial_requires_fresh_matching_online_response_and_expires_at_seven_days()
    {
        var claims = Claims(LicenseKind.Trial); var token = LicenseProtocol.Sign(claims, issuer);
        var challenge = LicenseProtocol.NewChallenge();
        var response = new TrialValidation(1, claims.LicenseId, fingerprint, challenge, issued.AddDays(6), true);
        Assert.Equal(claims.LicenseId, LicenseProtocol.ValidateTrialOnline(token, LicenseProtocol.Sign(response, issuer), verifier, fingerprint, challenge).LicenseId);
        Assert.Throws<InvalidOperationException>(() => LicenseProtocol.ValidateTrialOnline(token, null, verifier, fingerprint, challenge));
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.ValidateTrialOnline(token, LicenseProtocol.Sign(response, issuer), verifier, fingerprint, LicenseProtocol.NewChallenge()));
        Assert.Throws<InvalidOperationException>(() => LicenseProtocol.ValidateTrialOnline(token, LicenseProtocol.Sign(response with { ServerUtc = issued.AddDays(7) }, issuer), verifier, fingerprint, challenge));
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.ValidateTrialOnline(token, LicenseProtocol.Sign(response with { Active = false }, issuer), verifier, fingerprint, challenge));
    }

    [Fact]
    public void Encrypted_storage_is_authenticated_and_decryption_does_not_replace_signature_verification()
    {
        var token = LicenseProtocol.Sign(Claims(LicenseKind.Lifetime), issuer); var key = RandomNumberGenerator.GetBytes(32);
        var package = LicenseProtocol.EncryptLocalToken(token, key);
        Assert.DoesNotContain(token.Payload, Encoding.UTF8.GetString(package));
        var restored = LicenseProtocol.DecryptLocalToken(package, key);
        Assert.Equal(token, restored); LicenseProtocol.VerifyLicense(restored, verifier, fingerprint);
        Assert.ThrowsAny<CryptographicException>(() => LicenseProtocol.DecryptLocalToken(package, RandomNumberGenerator.GetBytes(32)));
        package[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => LicenseProtocol.DecryptLocalToken(package, key));
    }

    [Fact]
    public void Fingerprint_normalizes_values_and_rejects_missing_generic_identifiers()
    {
        Assert.Equal(fingerprint, LicenseProtocol.Fingerprint(" board-test ", "cpu-test", "46A8B21E-9A50-4A79-B005-4B1D6AEF91C1"));
        Assert.Throws<ArgumentException>(() => LicenseProtocol.Fingerprint("DEFAULT STRING", "CPU", Guid.NewGuid().ToString()));
        Assert.Throws<ArgumentException>(() => LicenseProtocol.Fingerprint("BOARD", "CPU", ""));
    }
}
