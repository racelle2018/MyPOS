using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RetailForma.Core.Services;

public enum LicenseKind { Trial = 1, Lifetime = 2 }
public sealed record LicenseClaims(int Format, Guid LicenseId, string Fingerprint, LicenseKind Kind,
    DateTimeOffset IssuedUtc, DateTimeOffset? ExpiresUtc, string[] Features,
    [property: System.Text.Json.Serialization.JsonRequired] string Product = ProductBrand.LicenseProduct);
public sealed record TrialValidation(int Format, Guid LicenseId, string Fingerprint, string Challenge,
    DateTimeOffset ServerUtc, bool Active,
    [property: System.Text.Json.Serialization.JsonRequired] string Product = ProductBrand.LicenseProduct);
public sealed record SignedLicenseMessage(string Payload, string Signature);

/// <summary>Protocol primitives only. No private keys, hardware reads, network access or application lockout.</summary>
public static class LicenseProtocol
{
    private const int MaximumPayloadBytes = 16384;

    public static string Fingerprint(string motherboardSerial, string cpuId, string windowsMachineGuid)
    {
        static string Normalize(string value)
        {
            var normalized = value.Trim().ToUpperInvariant();
            if (normalized.Length == 0 || normalized is "UNKNOWN" or "NONE" or "DEFAULT STRING" or "TO BE FILLED BY O.E.M." or "0")
                throw new ArgumentException("A stable hardware identifier is unavailable; do not silently activate with a generic fingerprint.");
            return normalized;
        }
        if (!Guid.TryParse(windowsMachineGuid, out var machineGuid) || machineGuid == Guid.Empty)
            throw new ArgumentException("A valid Windows Machine GUID is required.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { Normalize(motherboardSerial), Normalize(cpuId), machineGuid.ToString("D") });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static string NewChallenge() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    // Only a separately controlled issuer/server holding the private key can call Sign successfully.
    public static SignedLicenseMessage Sign<T>(T claims, RSA issuerPrivateKey)
    {
        RequireKey(issuerPrivateKey);
        var payload = JsonSerializer.SerializeToUtf8Bytes(claims);
        if (payload.Length > MaximumPayloadBytes) throw new ArgumentException("License payload is too large.");
        return new(Convert.ToBase64String(payload), Convert.ToBase64String(
            issuerPrivateKey.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
    }

    public static LicenseClaims VerifyLicense(SignedLicenseMessage message, RSA trustedIssuerPublicKey, string deviceFingerprint)
    {
        var claims = Verify<LicenseClaims>(message, trustedIssuerPublicKey);
        RequireFingerprint(deviceFingerprint);
        if (claims.Product != ProductBrand.LicenseProduct || claims.Format != 1 || claims.LicenseId == Guid.Empty || !Enum.IsDefined(claims.Kind) ||
            !string.Equals(claims.Fingerprint, deviceFingerprint, StringComparison.Ordinal) ||
            claims.Features == null || claims.Features.Length == 0 || claims.Features.Length > 4 ||
            claims.Features.Distinct(StringComparer.Ordinal).Count() != claims.Features.Length ||
            claims.Features.Any(feature => feature is not ("Basic" or "OwnerPortal" or "LAN" or "MultiBranch")) ||
            !claims.Features.Contains("Basic", StringComparer.Ordinal))
            throw new InvalidDataException("License claims are incompatible with this device/product.");
        if (claims.Kind == LicenseKind.Lifetime && claims.ExpiresUtc != null)
            throw new InvalidDataException("A lifetime license must not contain an expiry.");
        if (claims.Kind == LicenseKind.Trial && claims.ExpiresUtc != claims.IssuedUtc.AddDays(7))
            throw new InvalidDataException("A trial must have a signed seven-day duration.");
        // Lifetime validity has no online/current-clock dependency after signature and device verification.
        return claims;
    }

    public static LicenseClaims ValidateTrialOnline(SignedLicenseMessage license, SignedLicenseMessage? serverResponse,
        RSA trustedIssuerPublicKey, string deviceFingerprint, string expectedChallenge)
    {
        var claims = VerifyLicense(license, trustedIssuerPublicKey, deviceFingerprint);
        if (claims.Kind != LicenseKind.Trial) throw new InvalidOperationException("Online trial validation is not a lifetime-license requirement.");
        RequireFingerprint(expectedChallenge); // Fresh 32-byte challenge, not a cached validation response.
        if (serverResponse == null) throw new InvalidOperationException("Trial validation requires an online server response.");
        var validation = Verify<TrialValidation>(serverResponse, trustedIssuerPublicKey);
        if (validation.Product != ProductBrand.LicenseProduct || validation.Format != 1 || validation.LicenseId != claims.LicenseId || validation.Fingerprint != claims.Fingerprint ||
            validation.Challenge != expectedChallenge || !validation.Active)
            throw new InvalidDataException("Trial validation was denied or does not match this request.");
        if (validation.ServerUtc < claims.IssuedUtc || validation.ServerUtc >= claims.ExpiresUtc)
            throw new InvalidOperationException("The trial is expired or not yet active; paid activation is required.");
        return claims;
    }

    private static T Verify<T>(SignedLicenseMessage message, RSA trustedKey)
    {
        RequireKey(trustedKey);
        if (message == null || string.IsNullOrEmpty(message.Payload) || message.Payload.Length > 24000 ||
            string.IsNullOrEmpty(message.Signature) || message.Signature.Length > 2048)
            throw new InvalidDataException("Invalid signed-license envelope.");
        try
        {
            var payload = Convert.FromBase64String(message.Payload);
            var signature = Convert.FromBase64String(message.Signature);
            if (payload.Length > MaximumPayloadBytes || !trustedKey.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new InvalidDataException("License signature verification failed.");
            return JsonSerializer.Deserialize<T>(payload) ?? throw new InvalidDataException("Missing license claims.");
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        { throw new InvalidDataException("Invalid signed-license message.", ex); }
    }

    private static void RequireKey(RSA key)
    {
        if (key.KeySize < 3072) throw new ArgumentException("Use an RSA issuer key of at least 3072 bits.");
    }
    private static void RequireFingerprint(string value)
    {
        if (value == null || value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)) || value != value.ToUpperInvariant())
            throw new ArgumentException("Expected a normalized SHA-256 fingerprint/challenge.");
    }

    public static byte[] EncryptLocalToken(SignedLicenseMessage message, byte[] protectedStorageKey)
    {
        if (protectedStorageKey.Length != 32) throw new ArgumentException("AES-256 requires a 32-byte storage key.");
        var clear = JsonSerializer.SerializeToUtf8Bytes(message);
        if (clear.Length > 32768) throw new ArgumentException("License token is too large.");
        var encrypted = new byte[1 + 12 + 16 + clear.Length];
        encrypted[0] = 2;
        RandomNumberGenerator.Fill(encrypted.AsSpan(1, 12));
        try
        {
            using var aes = new AesGcm(protectedStorageKey, 16);
            aes.Encrypt(encrypted.AsSpan(1, 12), clear, encrypted.AsSpan(29), encrypted.AsSpan(13, 16), "RETAILFORMA-LICENSE-2"u8);
            return encrypted;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public static SignedLicenseMessage DecryptLocalToken(byte[] encrypted, byte[] protectedStorageKey)
    {
        if (protectedStorageKey.Length != 32 || encrypted.Length is < 30 or > 32797 || encrypted[0] != 2)
            throw new InvalidDataException("Invalid encrypted-license package.");
        var clear = new byte[encrypted.Length - 29];
        try
        {
            using var aes = new AesGcm(protectedStorageKey, 16);
            aes.Decrypt(encrypted.AsSpan(1, 12), encrypted.AsSpan(29), encrypted.AsSpan(13, 16), clear, "RETAILFORMA-LICENSE-2"u8);
            return JsonSerializer.Deserialize<SignedLicenseMessage>(clear) ?? throw new InvalidDataException("Missing license token.");
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
}
