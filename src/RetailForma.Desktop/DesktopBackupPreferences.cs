using RetailForma.ServiceApplication;

namespace RetailForma.Desktop;

public sealed class DesktopBackupPreferences
{
    private BackupPreferencesService? Local => App.Service.TestDatabase is { } db ? new(db) : null;
    public BackupPreferencesService.PassphraseState ReadPassphraseState(Guid actor)
    {
        if (Local is { } local) return local.ReadPassphraseState(actor);
        var state = App.Service.Call<BackupPhraseState>(Operation.BackupPhraseState); return new(state.Configured, state.Version);
    }
    public void VerifyCurrent(Guid actor, string current, string version)
    { if (Local is { } local) local.VerifyCurrent(actor, current, version); else App.Service.Call(Operation.VerifyBackupPhrase, new VerifyBackupPhraseInput(current, version)); }
    public BackupPreferencesService.Result Save(Guid actor, IReadOnlyDictionary<string, string> preferences, string current, string next, string confirmation, string? expectedVersion = null)
    {
        if (Local is { } local) return local.Save(actor, preferences, current, next, confirmation, expectedVersion);
        if (next.Length > 0 || confirmation.Length > 0)
        {
            if (preferences.Count > 0) throw new ArgumentException("Change the passphrase separately from destination preferences.");
            App.Service.Call(Operation.ChangeBackupPhrase, new ChangeBackupPhraseInput(current, next, confirmation,
                expectedVersion ?? throw new ArgumentException("Reopen the passphrase dialog."))); return new(true, true);
        }
        var saved = App.Service.Settings();
        var changed = preferences.Any(p => saved.GetValueOrDefault(p.Key, p.Key.EndsWith(".Enabled") ? "false" : "") != p.Value);
        if (changed) App.Service.SaveSettings(preferences.ToDictionary(p => p.Key, p => p.Value));
        return new(changed, false);
    }
}
