using System.Security.Cryptography;
using RetailForma.Security;

namespace RetailForma.Tests;

public sealed class RecoveryEnvelopeTests
{
    private static RecoveryContext Context() => new(Guid.NewGuid(), Guid.NewGuid(), RecoveryPurpose.BackupKey);
    private const string Phrase = "Owner test-only phrase 2026!";

    [Fact]
    public void IndependentOwnerAndDeveloperRoutesRecoverSameRandomKey()
    {
        using var developer = RSA.Create(3072);
        using var publicOnly = RSA.Create();
        publicOnly.ImportSubjectPublicKeyInfo(developer.ExportSubjectPublicKeyInfo(), out _);
        var context = Context();
        var key = RandomNumberGenerator.GetBytes(32);
        var ownerEnvelope = OwnerRecoveryEnvelope.Seal(key, context, Phrase);
        var developerEnvelope = DeveloperRecoveryEnvelope.Seal(key, context, publicOnly);
        Assert.Equal(key, OwnerRecoveryEnvelope.Open(ownerEnvelope, context, Phrase));
        // This path does not receive or depend on the owner's passphrase at all.
        Assert.Equal(key, DeveloperRecoveryEnvelope.Open(developerEnvelope, context, developer));
        Assert.Equal(DeveloperRecoveryEnvelope.PublicKeyId(developer), DeveloperRecoveryEnvelope.PublicKeyId(publicOnly));
        Assert.Throws<CryptographicException>(() => DeveloperRecoveryEnvelope.Open(developerEnvelope, context, publicOnly));
    }

    [Fact]
    public void RandomizedEnvelopesAndCaseSensitivePassphrase()
    {
        using var developer = RSA.Create(3072);
        var context = Context();
        var key = RandomNumberGenerator.GetBytes(32);
        Assert.NotEqual(DeveloperRecoveryEnvelope.Seal(key, context, developer), DeveloperRecoveryEnvelope.Seal(key, context, developer));
        var owner = OwnerRecoveryEnvelope.Seal(key, context, Phrase);
        Assert.NotEqual(owner, OwnerRecoveryEnvelope.Seal(key, context, Phrase));
        Assert.Throws<CryptographicException>(() => OwnerRecoveryEnvelope.Open(owner, context, Phrase.ToUpperInvariant()));
        Assert.Throws<CryptographicException>(() => OwnerRecoveryEnvelope.Open(owner, context, " " + Phrase));
    }

    [Fact]
    public void WrongDeveloperKeyAndEveryHeaderCiphertextRegionAreRejected()
    {
        using var developer = RSA.Create(3072);
        using var other = RSA.Create(3072);
        var context = Context();
        var envelope = DeveloperRecoveryEnvelope.Seal(RandomNumberGenerator.GetBytes(32), context, developer);
        Assert.Throws<CryptographicException>(() => DeveloperRecoveryEnvelope.Open(envelope, context, other));
        foreach (var offset in new[] { 0, 7, 8, 39, 40, 41, 42, envelope.Length - 1 })
        {
            var changed = (byte[])envelope.Clone();
            changed[offset] ^= 1;
            Assert.Throws<CryptographicException>(() => DeveloperRecoveryEnvelope.Open(changed, context, developer));
        }
        foreach (var length in new[] { 0, 1, 7, 40, envelope.Length - 1, envelope.Length + 1, 65536 })
            Assert.Throws<CryptographicException>(() => DeveloperRecoveryEnvelope.Open(new byte[length], context, developer));
    }

    [Fact]
    public void OwnerEnvelopeRejectsTamperingTruncationAndWrongPhrase()
    {
        var context = Context();
        var envelope = OwnerRecoveryEnvelope.Seal(RandomNumberGenerator.GetBytes(32), context, Phrase);
        foreach (var offset in new[] { 0, 8, 24, 36, 52, envelope.Length - 1 })
        {
            var changed = (byte[])envelope.Clone();
            changed[offset] ^= 1;
            Assert.Throws<CryptographicException>(() => OwnerRecoveryEnvelope.Open(changed, context, Phrase));
        }
        foreach (var length in new[] { 0, 7, envelope.Length - 1, envelope.Length + 1, 65536 })
            Assert.Throws<CryptographicException>(() => OwnerRecoveryEnvelope.Open(new byte[length], context, Phrase));
        Assert.Throws<CryptographicException>(() => OwnerRecoveryEnvelope.Open(envelope, context, "A different valid phrase"));
    }

    [Fact]
    public void CannotSwapInstallationResourceOrPurpose()
    {
        using var developer = RSA.Create(3072);
        var context = Context();
        var key = RandomNumberGenerator.GetBytes(32);
        var owner = OwnerRecoveryEnvelope.Seal(key, context, Phrase);
        var technical = DeveloperRecoveryEnvelope.Seal(key, context, developer);
        foreach (var wrong in new[] { context with { InstallationId = Guid.NewGuid() },
            context with { ResourceId = Guid.NewGuid() }, context with { Purpose = RecoveryPurpose.DatabaseKey } })
        {
            Assert.Throws<CryptographicException>(() => OwnerRecoveryEnvelope.Open(owner, wrong, Phrase));
            Assert.Throws<CryptographicException>(() => DeveloperRecoveryEnvelope.Open(technical, wrong, developer));
        }
    }

    [Fact]
    public void RejectsWeakKeysInvalidContextsAndInvalidPhrases()
    {
        using var weak = RSA.Create(2048);
        var context = Context();
        var key = RandomNumberGenerator.GetBytes(32);
        Assert.Throws<ArgumentException>(() => DeveloperRecoveryEnvelope.Seal(key, context, weak));
        Assert.Throws<ArgumentException>(() => OwnerRecoveryEnvelope.Seal(new byte[31], context, Phrase));
        Assert.Throws<ArgumentException>(() => OwnerRecoveryEnvelope.Seal(key, context with { InstallationId = Guid.Empty }, Phrase));
        Assert.Throws<ArgumentException>(() => OwnerRecoveryEnvelope.Seal(key, context with { ResourceId = Guid.Empty }, Phrase));
        Assert.Throws<ArgumentException>(() => OwnerRecoveryEnvelope.Seal(key, context with { Purpose = (RecoveryPurpose)99 }, Phrase));
        foreach (var invalid in new[] { "", "short", new string(' ', 20), new string('a', 257), new string('a', 16) + '\ud800' })
            Assert.Throws<ArgumentException>(() => OwnerRecoveryEnvelope.Seal(key, context, invalid));
    }
}
