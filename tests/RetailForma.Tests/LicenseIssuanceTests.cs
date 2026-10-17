using System.Security.Cryptography;
using RetailForma.Core.Services;
namespace RetailForma.Tests;
public sealed class LicenseIssuanceTests
{
    [Fact]
    public void Offline_paid_issuance_has_no_expiry_and_replacement_keeps_entitlement()
    {
        using var key = RSA.Create(3072); var id = Guid.NewGuid();
        var oldRequest = new LicenseRequest(1, "Test Company", new string('A', 64));
        var old = LicenseIssuance.Lifetime(oldRequest, id, key, DateTimeOffset.UtcNow);
        var newRequest = oldRequest with { Fingerprint = new string('B', 64) };
        var replacement = LicenseIssuance.Lifetime(newRequest, id, key, DateTimeOffset.UtcNow, ownerPortal: true);
        Assert.Null(LicenseProtocol.VerifyLicense(old, key, oldRequest.Fingerprint).ExpiresUtc);
        var claims = LicenseProtocol.VerifyLicense(replacement, key, newRequest.Fingerprint);
        Assert.Equal(id, claims.LicenseId); Assert.Equal(LicenseKind.Lifetime, claims.Kind); Assert.Contains("OwnerPortal", claims.Features);
        Assert.Throws<InvalidDataException>(() => LicenseProtocol.VerifyLicense(old, key, newRequest.Fingerprint));
    }
}
