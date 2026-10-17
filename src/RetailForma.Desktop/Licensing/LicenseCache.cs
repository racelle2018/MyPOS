using System.IO;
using System.Security.Cryptography;
using RetailForma.Core.Services;
namespace RetailForma.Desktop.Licensing;
public sealed class LicenseCache(string folder)
{
    private string KeyPath => Path.Combine(folder, "storage-key.dpapi");
    private string TokenPath => Path.Combine(folder, "license.aes");
    public bool Exists => File.Exists(TokenPath);
    public SignedLicenseMessage Load()
    {
        var key = ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser);
        try { return LicenseProtocol.DecryptLocalToken(File.ReadAllBytes(TokenPath), key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public void SaveVerified(SignedLicenseMessage token, RSA issuerPublicKey, string fingerprint)
    {
        LicenseProtocol.VerifyLicense(token, issuerPublicKey, fingerprint);
        Directory.CreateDirectory(folder);
        byte[] key;
        if (File.Exists(KeyPath)) key = ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser);
        else
        {
            key = RandomNumberGenerator.GetBytes(32);
            try { WriteAtomic(KeyPath, ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser), overwrite: false); }
            catch { CryptographicOperations.ZeroMemory(key); throw; }
        }
        try { WriteAtomic(TokenPath, LicenseProtocol.EncryptLocalToken(token, key), overwrite: true); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    private static void WriteAtomic(string path, byte[] bytes, bool overwrite)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, overwrite); }
        finally { File.Delete(temporary); }
    }
}
