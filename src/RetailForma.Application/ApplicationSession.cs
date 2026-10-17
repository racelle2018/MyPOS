using System.Security.Cryptography;
using RetailForma.Core.Entities;

namespace RetailForma.ServiceApplication;

/// <summary>Owned by one authenticated local pipe connection; never deserialized from a client.</summary>
public sealed class ApplicationSession(string windowsSid)
{
    public string WindowsSid { get; } = !string.IsNullOrWhiteSpace(windowsSid) ? windowsSid : throw new ArgumentException("Windows identity is required.");
    internal Guid? UserId;
    internal byte[]? CredentialStamp;
    internal bool Locked;
    internal long LastActivity;
    internal Guid AuthorityEpoch;
    public bool IsAuthenticated => UserId.HasValue;
    public void Clear()
    {
        UserId = null; Locked = false; LastActivity = 0; AuthorityEpoch = Guid.Empty;
        if (CredentialStamp != null) CryptographicOperations.ZeroMemory(CredentialStamp);
        CredentialStamp = null;
    }
    internal void SignIn(User user, TimeProvider clock, Guid authorityEpoch)
    {
        Clear(); UserId = user.Id; AuthorityEpoch = authorityEpoch;
        CredentialStamp = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.PasswordHash));
        LastActivity = clock.GetTimestamp();
    }
    internal bool Matches(User user) => CredentialStamp != null && CryptographicOperations.FixedTimeEquals(
        CredentialStamp, SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.PasswordHash)));
}
