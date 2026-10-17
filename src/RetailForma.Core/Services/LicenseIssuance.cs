using System.Security.Cryptography;

namespace RetailForma.Core.Services;

public sealed record LicenseRequest(int Format, string Company, string Fingerprint, string Product = ProductBrand.LicenseProduct);
public static class LicenseIssuance
{
    public static SignedLicenseMessage Lifetime(LicenseRequest request, Guid entitlementId, RSA privateKey,
        DateTimeOffset issuedUtc, bool ownerPortal = false)
    {
        if (request.Format != 1 || request.Product != "RetailForma.Basic" || string.IsNullOrWhiteSpace(request.Company) ||
            request.Company.Length > 200 || entitlementId == Guid.Empty)
            throw new ArgumentException("Use a valid named-company RetailForma request and entitlement ID.");
        var claims = new LicenseClaims(1, entitlementId, request.Fingerprint, LicenseKind.Lifetime, issuedUtc, null,
            ownerPortal ? ["Basic", "OwnerPortal"] : ["Basic"]);
        var signed = LicenseProtocol.Sign(claims, privateKey);
        LicenseProtocol.VerifyLicense(signed, privateKey, request.Fingerprint);
        return signed;
    }
}
