using System.Security.Cryptography;

namespace RetailForma.Security;

/// <summary>
/// Service-side, process-lifetime, one-use account-reset challenges. No password or role travels
/// in the signed request. The host must check local caller access before issuing a challenge,
/// recheck the target account, apply a transactional reset/audit, and revoke its sessions.
/// This gate does not itself expose an endpoint or mutate an account.
/// </summary>
public sealed class DeveloperRecoveryAuthorization : IDisposable
{
    private static ReadOnlySpan<byte> Domain => "RFPWRESET01"u8;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const int MaximumPending = 32;
    private readonly Guid _installationId;
    private readonly RSA _trustedPublicKey;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _pending = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Display/validation only. Parsing a request does not authorize it.</summary>
    public static (Guid InstallationId, Guid AccountId) Describe(ReadOnlySpan<byte> challenge)
    {
        if (challenge.Length != Domain.Length + 64 || !challenge[..Domain.Length].SequenceEqual(Domain))
            throw new ArgumentException("Invalid password recovery request.");
        var installation = new Guid(challenge.Slice(Domain.Length, 16)); var account = new Guid(challenge.Slice(Domain.Length + 16, 16));
        if (installation == Guid.Empty || account == Guid.Empty) throw new ArgumentException("Invalid recovery target.");
        return (installation, account);
    }

    public DeveloperRecoveryAuthorization(Guid installationId, ReadOnlySpan<byte> trustedRecoveryPublicKey,
        TimeProvider? clock = null)
    {
        if (installationId == Guid.Empty) throw new ArgumentException("Installation identity is required.");
        _trustedPublicKey = RSA.Create();
        try
        {
            _trustedPublicKey.ImportSubjectPublicKeyInfo(trustedRecoveryPublicKey, out var consumed);
            if (consumed != trustedRecoveryPublicKey.Length || _trustedPublicKey.KeySize is < 3072 or > 8192)
                throw new ArgumentException("Use a pinned RSA recovery public key of 3072–8192 bits.");
        }
        catch { _trustedPublicKey.Dispose(); throw; }
        _installationId = installationId;
        _clock = clock ?? TimeProvider.System;
    }

    public byte[] IssuePasswordReset(Guid targetAccountId)
    {
        if (targetAccountId == Guid.Empty) throw new ArgumentException("Target account is required.");
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Prune();
            if (_pending.Count >= MaximumPending)
                throw new InvalidOperationException("Too many pending recovery requests. Wait for them to expire.");
            var challenge = new byte[Domain.Length + 16 + 16 + 32];
            Domain.CopyTo(challenge);
            _installationId.TryWriteBytes(challenge.AsSpan(Domain.Length, 16));
            targetAccountId.TryWriteBytes(challenge.AsSpan(Domain.Length + 16, 16));
            RandomNumberGenerator.Fill(challenge.AsSpan(Domain.Length + 32, 32));
            _pending.Add(Id(challenge), _clock.GetTimestamp());
            return challenge;
        }
    }

    public void ConsumePasswordReset(ReadOnlySpan<byte> challenge, ReadOnlySpan<byte> signature, Guid targetAccountId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Prune();
            if (targetAccountId == Guid.Empty || challenge.Length != Domain.Length + 64 ||
                signature.Length != _trustedPublicKey.KeySize / 8 ||
                !challenge[..Domain.Length].SequenceEqual(Domain) ||
                new Guid(challenge.Slice(Domain.Length, 16)) != _installationId ||
                new Guid(challenge.Slice(Domain.Length + 16, 16)) != targetAccountId ||
                !_pending.ContainsKey(Id(challenge)) ||
                !_trustedPublicKey.VerifyData(challenge, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw RecoveryContext.Rejected();
            _pending.Remove(Id(challenge)); // Consume before any host mutation; retries need a new challenge.
        }
    }

    private void Prune()
    {
        var now = _clock.GetTimestamp();
        foreach (var item in _pending.ToArray())
        {
            var elapsed = _clock.GetElapsedTime(item.Value, now);
            if (elapsed < TimeSpan.Zero || elapsed >= Lifetime) _pending.Remove(item.Key);
        }
    }

    private static string Id(ReadOnlySpan<byte> challenge) => Convert.ToHexString(SHA256.HashData(challenge));

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear();
            _trustedPublicKey.Dispose();
        }
    }
}
