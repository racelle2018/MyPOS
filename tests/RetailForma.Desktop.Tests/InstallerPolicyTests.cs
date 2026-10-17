using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace RetailForma.Desktop.Tests;

public class InstallerPolicyTests
{
    private static string Root([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    [Fact]
    public void Protected_service_packaging_preserves_data_and_desktop_startup_has_no_plaintext_fallback()
    {
        var root = Root(); var installer = File.ReadAllText(Path.Combine(root, "tools", "installer", "RetailForma.iss"));
        var setup = File.ReadAllText(Path.Combine(root, "tools", "installer", "Configure-DataService.ps1"));
        var startup = File.ReadAllText(Path.Combine(root, "src", "RetailForma.Desktop", "App.xaml.cs"));
        Assert.Contains("-StopForUpgrade", installer); Assert.Contains("-CheckUninstall", installer); Assert.Contains("-Uninstall", installer);
        Assert.Contains("CurUninstallStep = usUninstall", installer);
        Assert.Contains("NT SERVICE\\RetailFormaData", setup);
        Assert.Contains("SetAccessRuleProtection($true, $false)", setup);
        Assert.Contains("$allowed -notcontains", setup);
        Assert.DoesNotContain("-Recurse", setup); Assert.DoesNotContain("Stop-Process", setup);
        Assert.DoesNotContain("UseSqlite(", startup); Assert.DoesNotContain("DbInitializer.Initialize", startup);
        Assert.DoesNotContain("ApplyPendingRestore", startup); Assert.DoesNotContain("AppDataMigration.Migrate", startup);
        Assert.Contains("Service.Settings(beforeLogin: true)", startup);
        foreach (var project in new[] { "RetailForma.Desktop", "RetailForma.Setup" })
            Assert.DoesNotContain("SQLitePCLRaw.bundle_e_sqlite3", File.ReadAllText(Path.Combine(root, "src", project, project + ".csproj")));
    }

    [Fact]
    public void Installer_preserves_profile_and_never_launches_or_force_closes_pos()
    {
        var script = File.ReadAllText(Path.Combine(Root(), "tools", "installer", "RetailForma.iss"));
        Assert.Contains("PrivilegesRequired=admin", script);
        Assert.Contains("DefaultDirName=C:\\RetailForma", script);
        Assert.Contains("RegKeyExists(HKCU64", script);
        Assert.Contains("Uninstall the earlier per-user RetailForma installation", script);
        Assert.Contains("AppMutex=Local\\RetailFormaDesktop,Local\\MyPosDesktop", script);
        Assert.Contains("CloseApplications=no", script);
        Assert.Contains("RestartApplications=no", script);
        Assert.Contains("UsePreviousAppDir=yes", script);
        Assert.Contains("ComparePackedVersion(InstalledVersion, InstallerVersion) > 0", script);
        Assert.DoesNotContain("[Run]", script);
        Assert.DoesNotContain("[InstallDelete]", script);
        Assert.DoesNotContain("[UninstallDelete]", script);
        Assert.DoesNotContain("DestDir: \"{userappdata}", script);
        Assert.DoesNotContain("DestDir: \"{commonappdata}", script);
    }

    [Fact]
    public void Build_uses_release_self_contained_payload_and_excludes_private_material()
    {
        var script = File.ReadAllText(Path.Combine(Root(), "tools", "Build-Installer.ps1"));
        Assert.Contains("--configuration Release", script);
        Assert.Contains("--self-contained true", script);
        Assert.Contains("PRIVATE KEY", script);
        Assert.Contains("VerifyPayload.cs", script);
        Assert.Contains("LicenseIssuer|LicenseGenerator|TechnicianSetup", script);
        Assert.Contains("'.db','.zip','.rar','.pem','.pfx','.key','.jsonl'", script);
        Assert.Contains("artifacts\\RetailFormaSetup.exe", script);
        Assert.Contains("$resolvedStage.StartsWith($stageBoundary", script);
        Assert.DoesNotContain("Stop-Process", script);
    }

    [Fact]
    public void Installer_shows_intro_and_requires_terms_acceptance()
    {
        var folder = Path.Combine(Root(), "tools", "installer");
        var script = File.ReadAllText(Path.Combine(folder, "RetailForma.iss"));
        Assert.Contains("DisableWelcomePage=no", script);
        Assert.Contains("WelcomeLabel2=RetailForma", script);
        Assert.Contains("AppPublisher=RetailForma Ltd.", script);
        Assert.Contains("AppSupportURL=mailto:ellecarpacquiaban@gmail.com", script);
        Assert.Contains("LicenseFile=TermsAndConditions.txt", script);
        Assert.Contains("LicenseAccepted=I accept the terms and conditions", script);
        Assert.Contains("if WizardSilent then", script);
        Assert.Contains("'/ACCEPTTERMS'", script);
        var terms = File.ReadAllText(Path.Combine(folder, "TermsAndConditions.txt"));
        Assert.Contains("one authorized device", terms);
        Assert.Contains("Company: RetailForma Ltd.", terms);
        Assert.DoesNotContain("Publisher:", terms);
        Assert.DoesNotContain("Racelle Horca", terms);
        Assert.Contains("Support: ellecarpacquiaban@gmail.com", terms);
        Assert.Contains("seven days", terms);
        Assert.Contains("retains company data and backups", terms);
        Assert.DoesNotContain("test installer", terms);
    }
}
