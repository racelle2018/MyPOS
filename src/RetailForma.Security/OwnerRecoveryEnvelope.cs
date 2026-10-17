using System.Security.Cryptography;
using System.Text;

namespace RetailForma.Security;

/// <summary>
/// Owner-passphrase route to the SAME random key as the developer envelope.
/// Version fixes KDF cost/algorithm; untrusted input cannot request unbounded iterations.
/// The payload containing these envelopes must independently authenticate its complete header/content.
/// </summary>
public static class OwnerRecoveryEnvelope
{
    private static ReadOnlySpan<byte> Magic => "RFOKEY01"u8;
    public const int Iterations = 600_000;
    private const int SaltBytes = 16, NonceBytes = 12, TagBytes = 16;
    private const int EnvelopeBytes = 8 + SaltBytes + NonceBytes + TagBytes + 32;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public static byte[] Seal(ReadOnlySpan<byte> resourceKey, RecoveryContext context, string passphrase)
    {
        RecoveryContext.RequireKey(resourceKey);
        ValidatePassphrase(passphrase);
        var binding = context.Encode();
        var output = new byte[EnvelopeBytes];
        Magic.CopyTo(output);
        RandomNumberGenerator.Fill(output.AsSpan(8, SaltBytes + NonceBytes));
        var wrappingKey = Derive(passphrase, output.AsSpan(8, SaltBytes));
        try
        {
            using var aes = new AesGcm(wrappingKey, TagBytes);
            aes.Encrypt(output.AsSpan(24, NonceBytes), resourceKey, output.AsSpan(52, 32),
                output.AsSpan(36, TagBytes), binding);
            return output;
        }
        finally { CryptographicOperations.ZeroMemory(wrappingKey); }
    }

    /// <summary>Caller owns the returned secret and must zero it after use.</summary>
    public static byte[] Open(ReadOnlySpan<byte> envelope, RecoveryContext expectedContext, string passphrase)
    {
        ValidatePassphrase(passphrase);
        var binding = expectedContext.Encode();
        if (envelope.Length != EnvelopeBytes || !envelope[..8].SequenceEqual(Magic))
            throw RecoveryContext.Rejected();
        var wrappingKey = Derive(passphrase, envelope.Slice(8, SaltBytes));
        var plain = new byte[32];
        try
        {
            using var aes = new AesGcm(wrappingKey, TagBytes);
            aes.Decrypt(envelope.Slice(24, NonceBytes), envelope.Slice(52, 32),
                envelope.Slice(36, TagBytes), plain, binding);
            return plain;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plain);
            throw RecoveryContext.Rejected();
        }
        finally { CryptographicOperations.ZeroMemory(wrappingKey); }
    }

    private static byte[] Derive(string passphrase, ReadOnlySpan<byte> salt)
    {
        var bytes = Utf8.GetBytes(passphrase);
        try { return Rfc2898DeriveBytes.Pbkdf2(bytes, salt, Iterations, HashAlgorithmName.SHA256, 32); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase) || passphrase.Length is < 16 or > 256)
            throw new ArgumentException("Use a unique recovery passphrase of 16–256 characters.");
        try { _ = Utf8.GetByteCount(passphrase); }
        catch (EncoderFallbackException) { throw new ArgumentException("Recovery passphrase contains invalid Unicode characters."); }
        // No trim/case conversion: the exact phrase is significant.
    }
}
