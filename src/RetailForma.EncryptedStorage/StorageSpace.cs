namespace RetailForma.EncryptedStorage;

public static class StorageSpace
{
    public const long ReserveBytes = 64L * 1024 * 1024;
    public static void Require(string trustedPath, long requiredBytes)
    {
        if (!Path.IsPathFullyQualified(trustedPath) || requiredBytes < 0) throw new ArgumentException("Invalid private storage size/path.");
        var root = Path.GetPathRoot(trustedPath)!;
        if (root.StartsWith("\\\\", StringComparison.Ordinal)) return; // Remote quotas are ultimately enforced by actual writes.
        if (new DriveInfo(root).AvailableFreeSpace < checked(requiredBytes + ReserveBytes))
            throw new IOException("Insufficient free space for verified backup/recovery and the 64 MB safety reserve. Free disk space and retry; existing company files were not replaced.");
    }
}
