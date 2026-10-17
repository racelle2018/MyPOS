; Compile through tools/Build-Installer.ps1. Never point PayloadRoot at live company data.
#ifndef PayloadRoot
  #error PayloadRoot is required
#endif
#ifndef ProductVersion
  #error ProductVersion is required
#endif

[Setup]
AppId={{2FB081AF-362B-4998-A86B-2BBEAF0905C1}
AppName=RetailForma
AppVersion={#ProductVersion}
AppPublisher=RetailForma Ltd.
AppSupportURL=mailto:ellecarpacquiaban@gmail.com
DefaultDirName={autopf}\RetailForma
DefaultGroupName=RetailForma
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputBaseFilename=RetailFormaSetup
SetupIconFile={#PayloadRoot}\Assets\Branding\retailforma.ico
UninstallDisplayIcon={app}\RetailForma.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
AppMutex=Local\RetailFormaDesktop,Local\MyPosDesktop
SetupMutex=Local\RetailFormaInstaller
CloseApplications=no
RestartApplications=no
RestartIfNeededByRun=no
UsePreviousAppDir=yes
Uninstallable=yes
UninstallDisplayName=RetailForma
VersionInfoVersion={#ProductVersion}
InfoBeforeFile=InstallNotes.txt
LicenseFile=TermsAndConditions.txt

[Messages]
WelcomeLabel2=RetailForma brings sales, inventory, cash-drawer management and reports together in a configurable retail system.%n%nDesigned to fit your business workflow, with offline operation under a paid lifetime license.
WizardLicense=Terms and Conditions
LicenseAccepted=I accept the terms and conditions
LicenseNotAccepted=I do not accept the terms and conditions

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadRoot}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Configure-DataService.ps1"; Flags: dontcopy

[Icons]
Name: "{group}\RetailForma"; Filename: "{app}\RetailForma.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\RetailForma"; Filename: "{app}\RetailForma.exe"; WorkingDir: "{app}"; Tasks: desktopicon

; Never start the POS/migrate a database automatically or erase AppData/company backups.
[Code]
function InitializeSetup(): Boolean;
var
  ParameterIndex: Integer;
  TermsAccepted: Boolean;
begin
  Result := True;
  if WizardSilent then
  begin
    TermsAccepted := False;
    for ParameterIndex := 1 to ParamCount do
      if CompareText(ParamStr(ParameterIndex), '/ACCEPTTERMS') = 0 then
        TermsAccepted := True;
    if not TermsAccepted then
    begin
      Log('Silent installation requires explicit terms acceptance using /ACCEPTTERMS.');
      Result := False;
      Exit;
    end;
  end;
  if RegKeyExists(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{2FB081AF-362B-4998-A86B-2BBEAF0905C1}_is1') then
  begin
    MsgBox('Uninstall the earlier per-user RetailForma installation before continuing. Company data will be retained.', mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstalledVersion, InstallerVersion: Int64;
  ExitCode: Integer;
begin
  Result := '';
  if CheckForMutexes('Local\RetailFormaDesktop,Local\MyPosDesktop') then
    Result := 'Close RetailForma normally before installing or updating. Unsaved work must be completed first.';
  if GetPackedVersion(ExpandConstant('{app}\RetailForma.exe'), InstalledVersion) and
     StrToVersion('{#ProductVersion}', InstallerVersion) then
  begin
    if ComparePackedVersion(InstalledVersion, InstallerVersion) > 0 then
      Result := 'A newer RetailForma version is installed. Downgrading is blocked to protect database compatibility.';
  end;
  if Result = '' then
  begin
    ExtractTemporaryFile('Configure-DataService.ps1');
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Configure-DataService.ps1') +
      '" -AppDirectory "' + ExpandConstant('{app}') + '" -StopForUpgrade', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'Could not stop the protected service for upgrade. No company conversion was performed.'
    else if ExitCode <> 0 then
      Result := 'Close RetailForma in all Windows sessions and verify its existing service registration before updating.';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var ExitCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Configure-DataService.ps1') +
      '" -AppDirectory "' + ExpandConstant('{app}') + '"', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      RaiseException('The protected data service could not be configured. Run installer repair; no company was created.')
    else if ExitCode <> 0 then
      RaiseException('Protected service setup failed. Existing company data was preserved. Review the service and storage permissions before starting RetailForma.');
  end;
end;

function InitializeUninstall(): Boolean;
var ExitCode: Integer;
begin
  Result := False;
  if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Configure-DataService.ps1') +
    '" -AppDirectory "' + ExpandConstant('{app}') + '" -CheckUninstall', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := ExitCode = 0;
  if not Result then MsgBox('Close RetailForma in every Windows session and verify the service before uninstalling. Company data and backups have not been deleted.', mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var ExitCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Configure-DataService.ps1') +
      '" -AppDirectory "' + ExpandConstant('{app}') + '" -Uninstall', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      RaiseException('Could not remove the protected service; uninstall stopped without deleting company data.')
    else if ExitCode <> 0 then
      RaiseException('Service removal was refused. Close RetailForma normally before uninstalling.');
  end;
end;
