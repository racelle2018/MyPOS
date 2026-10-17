namespace RetailForma.Desktop;

// Read-only migration/old-backup compatibility. Never use these for new data or licenses.
internal static class LegacyMigrationIdentity
{
    internal const string Folder = "MyPos";
    internal const string Database = "mypos.db";
    internal const string Mutex = @"Local\MyPosDesktop";
}
