using System.Security.Cryptography;
using System.Text;

namespace RetailForma.Security;

public enum RecoveryPurpose : byte { DatabaseKey = 1, BackupKey = 2 }

/// <summary>Non-secret binding. ResourceId changes for every new backup/key generation.</summary>
public readonly record struct RecoveryContext(Guid InstallationId, Guid ResourceId, RecoveryPurpose Purpose)
{
    internal byte[] Encode()
    {
        if (InstallationId == Guid.Empty || ResourceId == Guid.Empty ||
            Purpose is not (RecoveryPurpose.DatabaseKey or RecoveryPurpose.BackupKey))
            throw new ArgumentException("A valid installation, resource and recovery purpose are required.");
        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes("RetailForma.Recovery.v1\0"));
        output.WriteByte((byte)Purpose);
        output.Write(InstallationId.ToByteArray());
        output.Write(ResourceId.ToByteArray());
        return output.ToArray();
    }

    internal static void RequireKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32) throw new ArgumentException("A 256-bit random resource key is required.");
    }

    internal static CryptographicException Rejected() =>
        new("Recovery credentials or envelope authentication were not accepted. No data was changed.");
}
