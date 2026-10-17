using System.Security.Cryptography;
using System.Text;

namespace RetailForma.Desktop;

public static class BackupPassphrase
{
    // Pure validation/encryption only. Persistence must go through the authorized preferences action.
    public static string? PrepareChange(string savedSecret, string current, string next, string confirmation)
    {
        if (next.Length == 0 && confirmation.Length == 0) return null;
        var previous = savedSecret.Length > 0 ? RequireCurrent(savedSecret, current) : null;
        if (!string.Equals(next, confirmation, StringComparison.Ordinal))
            throw new ArgumentException("New recovery passphrases do not match.");
        EncryptedBackup.ValidatePassword(next);
        if (previous != null && string.Equals(previous, next, StringComparison.Ordinal)) return null;
        return EncryptedBackup.ProtectPassword(next);
    }

    public static void VerifyCurrent(string savedSecret, string current) => _ = RequireCurrent(savedSecret, current);

    private static string RequireCurrent(string savedSecret, string current)
    {
        if (savedSecret.Length == 0) throw new ArgumentException("No recovery passphrase is saved. Reopen the dialog to set one.");
        if (current.Length == 0) throw new ArgumentException("Enter the current recovery passphrase before changing it.");
        string previous;
        try { previous = EncryptedBackup.UnprotectPassword(savedSecret); }
        catch (InvalidOperationException)
        { throw new ArgumentException("The saved passphrase cannot be verified on this Windows profile. It was not replaced. Ask for authorized recovery assistance."); }
        var expectedBytes = Encoding.UTF8.GetBytes(previous);
        var enteredBytes = Encoding.UTF8.GetBytes(current);
        var expectedHash = SHA256.HashData(expectedBytes);
        var enteredHash = SHA256.HashData(enteredBytes);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, enteredHash))
                throw new ArgumentException("The current recovery passphrase is incorrect. No preferences were changed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes); CryptographicOperations.ZeroMemory(enteredBytes);
            CryptographicOperations.ZeroMemory(expectedHash); CryptographicOperations.ZeroMemory(enteredHash);
        }
        return previous;
    }
}
