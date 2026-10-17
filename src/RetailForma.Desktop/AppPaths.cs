using System.IO;

namespace RetailForma.Desktop;

public static class AppPaths
{
    public const string DatabaseName = "retailforma.db";
    public const string InstanceMutex = @"Local\RetailFormaDesktop";
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RetailForma");
    public static string LegacyFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyMigrationIdentity.Folder);
}
