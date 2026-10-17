using System.Security.Cryptography;
using RetailForma.Security;

namespace RetailForma.Tests;

public sealed class RecoveryAuthorizationTests
{
    private sealed class Clock : TimeProvider
    {
        public long Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch; // Wall clock never drives expiry.
    }

    private static byte[] Sign(RSA key, byte[] challenge) =>
        key.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

    [Fact]
    public void RequiresPrivateKeyProofAndConsumesOnlyOnce()
    {
        using var key = RSA.Create(3072);
        using var other = RSA.Create(3072);
        using var gate = new DeveloperRecoveryAuthorization(Guid.NewGuid(), key.ExportSubjectPublicKeyInfo());
        var target = Guid.NewGuid();
        var challenge = gate.IssuePasswordReset(target);
        Assert.Throws<CryptographicException>(() => gate.ConsumePasswordReset(challenge, Sign(other, challenge), target));
        var signature = Sign(key, challenge);
        gate.ConsumePasswordReset(challenge, signature, target);
        Assert.Throws<CryptographicException>(() => gate.ConsumePasswordReset(challenge, signature, target));
    }

    [Fact]
    public void BindsTargetInstallationExactRequestAndProcessInstance()
    {
        using var key = RSA.Create(3072);
        var installation = Guid.NewGuid();
        using var gate = new DeveloperRecoveryAuthorization(installation, key.ExportSubjectPublicKeyInfo());
        using var restarted = new DeveloperRecoveryAuthorization(installation, key.ExportSubjectPublicKeyInfo());
        using var different = new DeveloperRecoveryAuthorization(Guid.NewGuid(), key.ExportSubjectPublicKeyInfo());
        var target = Guid.NewGuid();
        var challenge = gate.IssuePasswordReset(target);
        var signature = Sign(key, challenge);
        Assert.Throws<CryptographicException>(() => gate.ConsumePasswordReset(challenge, signature, Guid.NewGuid()));
        Assert.Throws<CryptographicException>(() => restarted.ConsumePasswordReset(challenge, signature, target));
        Assert.Throws<CryptographicException>(() => different.ConsumePasswordReset(challenge, signature, target));
        for (var i = 0; i < challenge.Length; i++)
        {
            var tampered = (byte[])challenge.Clone(); tampered[i] ^= 1;
            Assert.Throws<CryptographicException>(() => gate.ConsumePasswordReset(tampered, Sign(key, tampered), target));
        }
        gate.ConsumePasswordReset(challenge, signature, target);
    }

    [Fact]
    public void MonotonicExpiryAndPendingLimitDoNotUseComputerDate()
    {
        using var key = RSA.Create(3072);
        var clock = new Clock();
        using var gate = new DeveloperRecoveryAuthorization(Guid.NewGuid(), key.ExportSubjectPublicKeyInfo(), clock);
        var target = Guid.NewGuid();
        var expired = gate.IssuePasswordReset(target);
        clock.Ticks = TimeSpan.FromMinutes(5).Ticks;
        Assert.Throws<CryptographicException>(() => gate.ConsumePasswordReset(expired, Sign(key, expired), target));
        for (var i = 0; i < 32; i++) gate.IssuePasswordReset(target);
        Assert.Throws<InvalidOperationException>(() => gate.IssuePasswordReset(target));
        clock.Ticks += TimeSpan.FromMinutes(5).Ticks;
        var fresh = gate.IssuePasswordReset(target);
        gate.ConsumePasswordReset(fresh, Sign(key, fresh), target);
    }

    [Fact]
    public void RejectsMalformedInputsWeakTrustAndUseAfterDisposal()
    {
        using var weak = RSA.Create(2048);
        Assert.Throws<ArgumentException>(() => new DeveloperRecoveryAuthorization(Guid.NewGuid(), weak.ExportSubjectPublicKeyInfo()));
        using var key = RSA.Create(3072);
        Assert.Throws<ArgumentException>(() => new DeveloperRecoveryAuthorization(Guid.Empty, key.ExportSubjectPublicKeyInfo()));
        var gate = new DeveloperRecoveryAuthorization(Guid.NewGuid(), key.ExportSubjectPublicKeyInfo());
        Assert.Throws<ArgumentException>(() => gate.IssuePasswordReset(Guid.Empty));
        foreach (var size in new[] { 0, 1, 10000 })
            Assert.Throws<CryptographicException>(() => gate.ConsumePasswordReset(new byte[size], [], Guid.NewGuid()));
        gate.Dispose();
        Assert.Throws<ObjectDisposedException>(() => gate.IssuePasswordReset(Guid.NewGuid()));
    }
}
