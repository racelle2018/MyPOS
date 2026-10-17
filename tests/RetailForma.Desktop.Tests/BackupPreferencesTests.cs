using System.IO;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Desktop;
using Xunit;

namespace RetailForma.Desktop.Tests;

public sealed class BackupPreferencesTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "retailforma-passphrase-test-" + Guid.NewGuid().ToString("N"));
    private readonly RetailFormaDbContext db;
    private readonly User owner = new() { Username = "owner", Role = UserRole.Admin, FullName = "Test Owner", PasswordHash = "test-only-hash" };
    private const string Old = "Old Test Recovery Passphrase!";
    private const string New = "New Test Recovery Passphrase!";
    private static Dictionary<string, string> Empty => new();
    public BackupPreferencesTests()
    {
        Directory.CreateDirectory(folder);
        db = new(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite($"Data Source={Path.Combine(folder, "retailforma.db")};Pooling=False").Options);
        db.Database.EnsureCreated(); db.Users.Add(owner); db.SaveChanges();
    }
    private BackupPreferencesService Service => new(db);
    private string Secret => db.Settings.AsNoTracking().Single(s => s.Key == "BackupRecoverySecret").Value;
    private void Configure() => Service.Save(owner.Id, Empty, "", Old, Old);

    [Theory]
    [InlineData("")]
    [InlineData("wrong")]
    public void Verification_rejects_invalid_current_without_replacing_secret(string entered)
    {
        Configure(); var before = Secret; var state = Service.ReadPassphraseState(owner.Id);
        Assert.True(state.Configured);
        Assert.Throws<ArgumentException>(() => Service.VerifyCurrent(owner.Id, entered, state.Version));
        Assert.Equal(before, Secret);
    }

    [Fact]
    public void Verification_alone_does_not_replace_secret_and_save_rechecks_current()
    {
        Configure(); var before = Secret; var state = Service.ReadPassphraseState(owner.Id);
        Service.VerifyCurrent(owner.Id, Old, state.Version);
        Assert.Equal(before, Secret);
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, "wrong", New, New, state.Version));
        Assert.Equal(before, Secret);
        Assert.True(Service.Save(owner.Id, Empty, Old, New, New, state.Version).PassphraseChanged);
    }

    [Fact]
    public void Verified_dialog_cannot_replace_a_subsequently_changed_secret_even_with_new_current()
    {
        Configure(); var state = Service.ReadPassphraseState(owner.Id);
        Service.VerifyCurrent(owner.Id, Old, state.Version);
        Service.Save(owner.Id, Empty, Old, New, New);
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, New, "Third recovery passphrase!", "Third recovery passphrase!", state.Version));
        Assert.Equal(New, EncryptedBackup.UnprotectPassword(Secret));
    }

    [Fact]
    public void Set_dialog_cannot_overwrite_a_passphrase_configured_since_it_opened()
    {
        var state = Service.ReadPassphraseState(owner.Id); Assert.False(state.Configured);
        Configure();
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, Old, New, New, state.Version));
        Assert.Equal(Old, EncryptedBackup.UnprotectPassword(Secret));
    }

    [Fact]
    public void Verification_rechecks_persisted_role()
    {
        Configure(); var state = Service.ReadPassphraseState(owner.Id);
        owner.Role = UserRole.Cashier; db.SaveChanges();
        Assert.Throws<UnauthorizedAccessException>(() => Service.VerifyCurrent(owner.Id, Old, state.Version));
        Assert.Equal(Old, EncryptedBackup.UnprotectPassword(Secret));
    }
    public void Dispose() { db.Dispose(); Directory.Delete(folder, true); }

    [Fact]
    public void First_setup_requires_matching_new_passphrase_and_audits_no_values()
    {
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, "", New, "mismatch"));
        Assert.False(db.Settings.Any());
        var saved = Service.Save(owner.Id, Empty, "", New, New);
        Assert.True(saved.Changed); Assert.True(saved.PassphraseChanged);
        Assert.Equal(New, EncryptedBackup.UnprotectPassword(Secret));
        Assert.Contains(db.AuditLogs, a => a.Action == "BackupPassphraseConfigured");
        Assert.All(db.AuditLogs, a => { Assert.DoesNotContain(New, a.Details ?? ""); Assert.DoesNotContain(Secret, a.Details ?? ""); });
    }

    [Theory]
    [InlineData("")]
    [InlineData("incorrect current phrase")]
    [InlineData("old test recovery passphrase!")]
    public void Missing_wrong_or_recased_current_passphrase_cannot_change_secret_or_any_other_preferences(string current)
    {
        Configure(); var original = Secret;
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id,
            new Dictionary<string, string> { ["Backup.Dropbox.Enabled"] = "true" }, current, New, New));
        Assert.Equal(original, Secret); Assert.False(db.Settings.Any(s => s.Key == "Backup.Dropbox.Enabled"));
        Assert.Contains(db.AuditLogs, a => a.Action == "BackupPassphraseChangeDenied");
        db.SaveChanges(); Assert.Equal(original, Secret); // No failed tracked replacement may leak through a later save.
        Assert.All(db.AuditLogs, a => { Assert.DoesNotContain(Old, a.Details ?? ""); Assert.DoesNotContain(New, a.Details ?? ""); });
    }

    [Fact]
    public void Correct_current_changes_secret_and_refreshes_previously_tracked_value()
    {
        Configure(); var cached = db.Settings.Single(s => s.Key == "BackupRecoverySecret");
        var result = Service.Save(owner.Id, Empty, Old, New, New);
        Assert.True(result.PassphraseChanged); Assert.Equal(New, EncryptedBackup.UnprotectPassword(cached.Value));
        Assert.Contains(db.AuditLogs, a => a.Action == "BackupPassphraseChanged");
    }

    [Theory]
    [InlineData("New Test Recovery Passphrase!", "not matching")]
    [InlineData("short", "short")]
    public void New_confirmation_and_length_are_checked_after_current_verification(string next, string confirmation)
    {
        Configure(); var original = Secret;
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, Old, next, confirmation));
        Assert.Equal(original, Secret);
    }

    [Fact]
    public void Blank_new_fields_keep_secret_and_allow_unrelated_backup_preferences()
    {
        Configure(); var original = Secret;
        var result = Service.Save(owner.Id, new Dictionary<string, string> { ["Backup.Dropbox.Enabled"] = "true" }, "", "", "");
        Assert.True(result.Changed); Assert.False(result.PassphraseChanged); Assert.Equal(original, Secret);
        Assert.Equal("true", db.Settings.Single(s => s.Key == "Backup.Dropbox.Enabled").Value);
    }

    [Fact]
    public void Same_passphrase_is_no_change_but_still_requires_current_verification()
    {
        Configure(); var original = Secret;
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, "wrong", Old, Old));
        var result = Service.Save(owner.Id, Empty, Old, Old, Old);
        Assert.False(result.Changed); Assert.Equal(original, Secret);
    }

    [Fact]
    public void Unreadable_saved_secret_cannot_be_silently_replaced()
    {
        db.Settings.Add(new Setting { Key = "BackupRecoverySecret", Value = "unreadable-protected-secret" }); db.SaveChanges();
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, Old, New, New));
        Assert.Equal("unreadable-protected-secret", Secret);
    }

    [Fact]
    public void Stale_ui_secret_does_not_authorize_overwriting_a_newer_saved_passphrase()
    {
        Configure(); var stale = db.Settings.Single(s => s.Key == "BackupRecoverySecret");
        using (var other = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite(db.Database.GetDbConnection(), false).Options))
        { other.Settings.Single(s => s.Key == "BackupRecoverySecret").Value = EncryptedBackup.ProtectPassword(New); other.SaveChanges(); }
        Assert.Equal(Old, EncryptedBackup.UnprotectPassword(stale.Value));
        Assert.Throws<ArgumentException>(() => Service.Save(owner.Id, Empty, Old, "Third recovery passphrase!", "Third recovery passphrase!"));
        Assert.Equal(New, EncryptedBackup.UnprotectPassword(Secret));
    }

    [Theory]
    [InlineData(UserRole.Cashier, true, false)]
    [InlineData(UserRole.Admin, false, false)]
    [InlineData(UserRole.SystemAdministrator, true, true)]
    public void Persisted_active_administrator_role_is_required_even_when_calling_service_directly(UserRole role, bool active, bool allowed)
    {
        owner.Role = role; owner.IsActive = active; db.SaveChanges();
        if (allowed) Assert.True(Service.Save(owner.Id, Empty, "", New, New).PassphraseChanged);
        else
        {
            Assert.Throws<UnauthorizedAccessException>(() => Service.Save(owner.Id, Empty, "", New, New));
            Assert.False(db.Settings.Any()); Assert.Contains(db.AuditLogs, a => a.Action == "PermissionDenied");
        }
    }

    [Fact]
    public void Failed_persistence_rolls_back_secret_preferences_and_success_audits_without_leaking_pending_changes()
    {
        Configure(); var original = Secret;
        db.Database.ExecuteSqlRaw("CREATE TRIGGER RejectSecret BEFORE UPDATE ON Settings WHEN NEW.Key='BackupRecoverySecret' BEGIN SELECT RAISE(ABORT,'test failure'); END;");
        Assert.Throws<DbUpdateException>(() => Service.Save(owner.Id,
            new Dictionary<string, string> { ["Backup.Dropbox.Enabled"] = "true" }, Old, New, New));
        Assert.Equal(original, Secret); Assert.False(db.Settings.Any(s => s.Key == "Backup.Dropbox.Enabled"));
        Assert.False(db.AuditLogs.Any(a => a.Action == "BackupPassphraseChanged"));
        db.Database.ExecuteSqlRaw("DROP TRIGGER RejectSecret;"); db.SaveChanges(); Assert.Equal(original, Secret);
    }

    [Fact]
    public void Independent_settings_save_does_not_commit_unrelated_pending_product_edits()
    {
        var pending = new Product { Name = "PENDING PRODUCT", Barcode = "PENDING-ONLY" }; db.Products.Add(pending);
        Assert.True(Service.Save(owner.Id, Empty, "", New, New).Changed);
        Assert.Empty(db.Products.AsNoTracking().ToArray()); Assert.Equal(EntityState.Added, db.Entry(pending).State);
    }

    [Fact]
    public void Existing_archives_keep_original_password_and_future_archives_use_new_password()
    {
        Configure(); var originalZip = Path.Combine(folder, "old.zip");
        EncryptedBackup.Create(Path.Combine(folder, "retailforma.db"), originalZip, Old); var bytes = File.ReadAllBytes(originalZip);
        Service.Save(owner.Id, Empty, Old, New, New);
        EncryptedBackup.Verify(originalZip, Old); Assert.ThrowsAny<Exception>(() => EncryptedBackup.Verify(originalZip, New));
        Assert.Equal(bytes, File.ReadAllBytes(originalZip));
        var nextZip = Path.Combine(folder, "new.zip"); EncryptedBackup.Create(Path.Combine(folder, "retailforma.db"), nextZip, New);
        EncryptedBackup.Verify(nextZip, New); Assert.ThrowsAny<Exception>(() => EncryptedBackup.Verify(nextZip, Old));
    }

    [Fact]
    public void Restore_staging_accepts_original_archive_passphrase_and_rejects_current_replacement()
    {
        Configure(); var archive = Path.Combine(folder, "restore-test.zip");
        EncryptedBackup.Create(Path.Combine(folder, "retailforma.db"), archive, Old);
        Service.Save(owner.Id, Empty, Old, New, New);
        Assert.ThrowsAny<Exception>(() => BackupService.StageEncryptedRestore(archive, New, folder));
        Assert.False(File.Exists(Path.Combine(folder, "restore-pending.db")));
        Assert.Equal(New, EncryptedBackup.UnprotectPassword(Secret));
        BackupService.StageEncryptedRestore(archive, Old, folder);
        BackupService.Verify(Path.Combine(folder, "restore-pending.db"));
        Assert.Equal(New, EncryptedBackup.UnprotectPassword(Secret));
    }
}
