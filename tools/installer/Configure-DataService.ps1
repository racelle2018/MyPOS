[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AppDirectory,
    [switch]$Uninstall,
    [switch]$StopForUpgrade,
    [switch]$CheckUninstall
)
$ErrorActionPreference = 'Stop'
$serviceName = 'RetailFormaData'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    if (!(New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Service installation requires Windows administrator approval.'
    }
} finally { $identity.Dispose() }
$applicationRoot = [IO.Path]::GetFullPath($AppDirectory).TrimEnd('\')
$programsRoot = [IO.Path]::GetFullPath([Environment]::GetFolderPath('ProgramFiles')).TrimEnd('\') + '\'
if (!$applicationRoot.StartsWith($programsRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($applicationRoot) -ne 'RetailForma') {
    throw 'Install the service only under the protected Program Files RetailForma directory.'
}
function Assert-NoLinks([string]$Path) {
    $entry = [IO.DirectoryInfo]::new($Path)
    while ($null -ne $entry) {
        if ($entry.Exists -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Service paths cannot traverse filesystem links.' }
        $entry = $entry.Parent
    }
}
Assert-NoLinks $applicationRoot
if (Get-Process -Name RetailForma -ErrorAction SilentlyContinue) { throw 'Close RetailForma normally in every Windows session before installing, updating or uninstalling.' }
$sc = Join-Path ([Environment]::GetFolderPath('System')) 'sc.exe'
function Invoke-ServiceCommand([string[]]$Arguments) {
    $result = & $sc @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('Service configuration failed: ' + ($result -join ' ')) }
}
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    # Never replace a different service which happens to use the same name.
    $registered = Get-CimInstance Win32_Service -Filter "Name='RetailFormaData'"
    $expected = Join-Path $applicationRoot 'Service\RetailForma.DataService.exe'
    if ($registered.PathName.Trim('"') -ne $expected -or $registered.StartName -ne 'NT SERVICE\RetailFormaData') {
        throw 'An unexpected service registration exists. Review it before replacing anything.'
    }
    if (!$CheckUninstall -and $existing.Status -ne 'Stopped') { Stop-Service -Name $serviceName; $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(45)) }
}
if ($CheckUninstall) { Write-Output 'Uninstall preflight passed; no service or company changes were made.'; exit 0 }
if ($StopForUpgrade) { Write-Output 'Existing service stopped safely for binary replacement; company data untouched.'; exit 0 }
if ($Uninstall) {
    if ($existing) { Invoke-ServiceCommand -Arguments @('delete', $serviceName) }
    Write-Output 'Service removed. ProgramData company/key/recovery files and AppData backups were preserved.'
    exit 0
}
$executable = Join-Path $applicationRoot 'Service\RetailForma.DataService.exe'
if (!(Test-Path -LiteralPath $executable -PathType Leaf) -or !(Test-Path -LiteralPath (Join-Path $applicationRoot 'Service\sqlcipher.dll') -PathType Leaf)) {
    throw 'The verified service and SQLCipher payload are missing.'
}
& $executable --self-check
if ($LASTEXITCODE -ne 0) { throw 'The installed service public-trust/native-provider check failed. Company data was not opened.' }
# Only these retired, generated plain-provider binaries under the validated application
# directory are removed. No wildcard, recursive operation, data, backup or key path is used.
foreach ($retiredName in @('e_sqlite3.dll', 'SQLitePCLRaw.batteries_v2.dll', 'SQLitePCLRaw.provider.e_sqlite3.dll')) {
    $retiredPath = [IO.Path]::GetFullPath((Join-Path $applicationRoot $retiredName))
    if (!$retiredPath.StartsWith($applicationRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe retired-binary path refused.' }
    if (Test-Path -LiteralPath $retiredPath -PathType Leaf) { Remove-Item -LiteralPath $retiredPath -Force }
}
if (!$existing) {
    Invoke-ServiceCommand -Arguments @('create', $serviceName, 'binPath=', ('"' + $executable + '"'), 'start=', 'auto', 'obj=', 'NT SERVICE\RetailFormaData', 'DisplayName=', 'RetailForma Protected Data')
}
Invoke-ServiceCommand -Arguments @('sidtype', $serviceName, 'unrestricted')
Invoke-ServiceCommand -Arguments @('description', $serviceName, 'Owns the encrypted RetailForma company database and authenticates local POS operations.')
Invoke-ServiceCommand -Arguments @('failure', $serviceName, 'reset=', '86400', 'actions=', 'restart/10000/restart/30000/none/0')
if (![Diagnostics.EventLog]::SourceExists($serviceName)) {
    [Diagnostics.EventLog]::CreateEventSource($serviceName, 'Application')
}
$serviceSid = ([Security.Principal.NTAccount]::new('NT SERVICE', $serviceName)).Translate([Security.Principal.SecurityIdentifier])
$storageRoot = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'RetailForma'
Assert-NoLinks $storageRoot
if (Test-Path -LiteralPath $storageRoot) {
    # Do not recursively seize an unknown folder or silently relax existing permissions.
    $currentAcl = Get-Acl -LiteralPath $storageRoot
    $allowed = @($serviceSid.Value, 'S-1-5-18', 'S-1-5-32-544')
    if (!$currentAcl.AreAccessRulesProtected -or $allowed -notcontains $currentAcl.GetOwner([Security.Principal.SecurityIdentifier]).Value) {
        throw 'An unrecognized ProgramData storage folder exists. Review/migrate it explicitly; no ACL takeover was performed.'
    }
    foreach ($rule in $currentAcl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.AccessControlType -eq 'Allow' -and $allowed -notcontains $rule.IdentityReference.Value) {
            throw 'Existing storage grants access outside the service boundary. Repair it through authorized maintenance.'
        }
    }
} else {
    $descriptor = [Security.AccessControl.DirectorySecurity]::new()
    $descriptor.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    $descriptor.SetAccessRuleProtection($true, $false)
    foreach ($sidValue in @($serviceSid.Value, 'S-1-5-18', 'S-1-5-32-544')) {
        $descriptor.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new($sidValue), [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit', [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow))
    }
    # Create with its restrictive descriptor, not an initially cashier-readable directory.
    [IO.DirectoryInfo]::new($storageRoot).Create($descriptor)
}
Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(45))
Write-Output 'Protected service installed and started; no company was created and no existing company was migrated.'
