using System.Buffers.Binary;
using System.Security.Cryptography;

namespace RetailForma.Security;

/// <summary>
/// Wraps one random resource key to a developer public key using RSA-OAEP-SHA256.
/// This is confidentiality and context binding, NOT authorization or proof of the sender.
/// No licensing keys, disk I/O, password reset, or private-key persistence belongs here.
/// </summary>
public static class DeveloperRecoveryEnvelope
{
    private static ReadOnlySpan<byte> Magic => "RFDKEY01"u8;
    private const int HeaderBytes = 8 + 32 + 2;

    public static string PublicKeyId(RSA developerKey)
    {
        ValidateRsa(developerKey);
        return Convert.ToHexString(SHA256.HashData(developerKey.ExportSubjectPublicKeyInfo()));
    }

    public static byte[] Seal(ReadOnlySpan<byte> resourceKey, RecoveryContext context, RSA developerPublicKey)
    {
        RecoveryContext.RequireKey(resourceKey);
        ValidateRsa(developerPublicKey);
        var binding = context.Encode();
        var plain = new byte[binding.Length + 32];
        try
        {
            binding.CopyTo(plain, 0);
            resourceKey.CopyTo(plain.AsSpan(binding.Length));
            var wrapped = developerPublicKey.Encrypt(plain, RSAEncryptionPadding.OaepSHA256);
            var output = new byte[HeaderBytes + wrapped.Length];
            Magic.CopyTo(output);
            SHA256.HashData(developerPublicKey.ExportSubjectPublicKeyInfo()).CopyTo(output, 8);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(40), checked((ushort)wrapped.Length));
            wrapped.CopyTo(output, HeaderBytes);
            return output;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    /// <summary>Caller owns the returned secret and must zero it after use.</summary>
    public static byte[] Open(ReadOnlySpan<byte> envelope, RecoveryContext expectedContext, RSA developerPrivateKey)
    {
        ValidateRsa(developerPrivateKey);
        var binding = expectedContext.Encode();
        // Bound lengths before copying or invoking RSA. Do not support arbitrary algorithms from input.
        if (envelope.Length != HeaderBytes + developerPrivateKey.KeySize / 8 ||
            !envelope[..8].SequenceEqual(Magic) ||
            BinaryPrimitives.ReadUInt16BigEndian(envelope.Slice(40, 2)) != developerPrivateKey.KeySize / 8 ||
            !CryptographicOperations.FixedTimeEquals(envelope.Slice(8, 32),
                SHA256.HashData(developerPrivateKey.ExportSubjectPublicKeyInfo())))
            throw RecoveryContext.Rejected();
        byte[] plain;
        try { plain = developerPrivateKey.Decrypt(envelope[HeaderBytes..].ToArray(), RSAEncryptionPadding.OaepSHA256); }
        catch (CryptographicException) { throw RecoveryContext.Rejected(); }
        try
        {
            if (plain.Length != binding.Length + 32 ||
                !CryptographicOperations.FixedTimeEquals(plain.AsSpan(0, binding.Length), binding))
                throw RecoveryContext.Rejected();
            return plain.AsSpan(binding.Length, 32).ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static void ValidateRsa(RSA key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.KeySize is < 3072 or > 8192 || key.KeySize % 8 != 0)
            throw new ArgumentException("Use a separate RSA recovery key of 3072–8192 bits.");
    }
}
