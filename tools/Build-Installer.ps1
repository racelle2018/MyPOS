[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PaidLicensePublicKey,
    [Parameter(Mandatory=$true)][string]$DeveloperRecoveryPublicKey,
    [string]$TrialLicensePublicKey = '',
    [string]$TrialLicenseServerUrl = '',
    [string]$CompilerPath = '',
    [switch]$ValidateOnly,
    [switch]$UpdateStablePackage
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($UpdateStablePackage -and (Get-Process -Name RetailForma -ErrorAction SilentlyContinue)) {
    throw 'Close RetailForma normally before updating its existing stable package.'
}
$compiler = ''
if (!$ValidateOnly) {
    if (!$CompilerPath) { throw 'Provide the installed Inno Setup ISCC.exe path, or use -ValidateOnly for payload checks.' }
    $compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
    # Early consistency check only; this does not replace Inno's own signature verification.
    # Refuse a known-mismatched compiler before building or updating any output package.
    $compilerDll = Join-Path (Split-Path -Parent $compiler) 'ISCmplr.dll'
    $compilerMetadata = $compilerDll + '.issig'
    if (!(Test-Path -LiteralPath $compilerDll) -or !(Test-Path -LiteralPath $compilerMetadata)) { throw 'The compiler installation is incomplete; repair it from the official installer.' }
    $compilerRecord = [IO.File]::ReadAllText($compilerMetadata)
    $expectedSize = [regex]::Match($compilerRecord, '(?m)^file-size (\d+)\r?$')
    $expectedHash = [regex]::Match($compilerRecord, '(?m)^file-hash ([0-9a-fA-F]{64})\r?$')
    if (!$expectedSize.Success -or !$expectedHash.Success -or
        (Get-Item -LiteralPath $compilerDll).Length -ne [long]$expectedSize.Groups[1].Value -or
        (Get-FileHash -LiteralPath $compilerDll -Algorithm SHA256).Hash -ne $expectedHash.Groups[1].Value) {
        throw 'Compiler DLL no longer matches its integrity metadata. Stop and investigate; do not bypass verification or distribute earlier outputs.'
    }
}
$paidPem = (Resolve-Path -LiteralPath $PaidLicensePublicKey).Path
function Assert-PublicKey([string]$Path) {
    $pem = [IO.File]::ReadAllText($Path)
    if ($pem.Contains('PRIVATE KEY') -or !$pem.Contains('-----BEGIN PUBLIC KEY-----')) { throw 'Only public SPKI PEMs may enter the POS installer.' }
    # RSA/embedded-trust validation runs under .NET 10 below, also on Windows PowerShell 5.1.
}
Assert-PublicKey $paidPem
$recoveryPem = (Resolve-Path -LiteralPath $DeveloperRecoveryPublicKey).Path
Assert-PublicKey $recoveryPem
if (($TrialLicensePublicKey.Length -gt 0) -ne ($TrialLicenseServerUrl.Length -gt 0)) { throw 'Trial trust and HTTPS endpoint must be supplied together, or both omitted for lifetime-only.' }
if ($TrialLicensePublicKey) {
    $TrialLicensePublicKey = (Resolve-Path -LiteralPath $TrialLicensePublicKey).Path
    Assert-PublicKey $TrialLicensePublicKey
    if ((Get-FileHash -LiteralPath $paidPem).Hash -eq (Get-FileHash -LiteralPath $TrialLicensePublicKey).Hash) { throw 'Paid and trial trust must be separate.' }
    $endpoint = [Uri]$TrialLicenseServerUrl
    if (!$endpoint.IsAbsoluteUri -or $endpoint.Scheme -ne 'https') { throw 'Trial endpoint must use HTTPS.' }
}
$project = Join-Path $repoRoot 'src\RetailForma.Desktop\RetailForma.Desktop.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ }
$version = [string]$version
$stageRoot = Join-Path ([IO.Path]::GetTempPath()) ('RetailForma-installer-' + [Guid]::NewGuid().ToString('N'))
$stageBoundary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
New-Item -ItemType Directory -Path $stageRoot | Out-Null
try {
    $payload = Join-Path $stageRoot 'payload'
    & dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --artifacts-path (Join-Path $stageRoot 'build') --output $payload '-p:PublishTrimmed=false' '-p:PublishSingleFile=false' "-p:PaidLicensePublicKey=$paidPem" "-p:TrialLicensePublicKey=$TrialLicensePublicKey" "-p:TrialLicenseServerUrl=$TrialLicenseServerUrl"
    if ($LASTEXITCODE -ne 0) { throw 'POS publish failed.' }
    $servicePayload = Join-Path $payload 'Service'
    & dotnet publish (Join-Path $repoRoot 'src\RetailForma.DataService\RetailForma.DataService.csproj') --configuration Release --runtime win-x64 --self-contained true --artifacts-path (Join-Path $stageRoot 'service-build') --output $servicePayload "-p:DeveloperRecoveryPublicKey=$recoveryPem" "-p:PaidLicensePublicKey=$paidPem" "-p:TrialLicensePublicKey=$TrialLicensePublicKey" "-p:TrialLicenseServerUrl=$TrialLicenseServerUrl"
    if ($LASTEXITCODE -ne 0) { throw 'Protected service publish failed.' }
    & (Join-Path $servicePayload 'RetailForma.DataService.exe') --self-check
    if ($LASTEXITCODE -ne 0) { throw 'Published SQLCipher/public-trust self-check failed.' }
    if (Get-ChildItem -LiteralPath $servicePayload -File -Recurse | Where-Object { $_.Name -match '^e_sqlite3\.' }) { throw 'Plain SQLite must not enter the encrypted service payload.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'installer\Configure-DataService.ps1') -Destination (Join-Path $payload 'Configure-DataService.ps1')
    if (!(Test-Path -LiteralPath (Join-Path $payload 'RetailForma.exe')) -or !(Test-Path -LiteralPath (Join-Path $payload 'coreclr.dll'))) { throw 'Expected self-contained POS payload is missing.' }
    $branding = Join-Path $payload 'Assets\Branding'
    New-Item -ItemType Directory -Force -Path $branding | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src\RetailForma.Desktop\Assets\Branding\retailforma.ico') -Destination (Join-Path $branding 'retailforma.ico')
    $forbidden = Get-ChildItem -LiteralPath $payload -File -Recurse | Where-Object {
        $_.Extension -in '.db','.zip','.rar','.pem','.pfx','.key','.jsonl' -or $_.Name -match 'LicenseIssuer|LicenseGenerator|TechnicianSetup|\.license\.json$' -or
        ($_.Extension -notin '.dll','.pdb' -and $_.Name -match 'private|secret')
    }
    if ($forbidden) { throw 'Installer payload contains a forbidden data/key/programmer-tool file.' }
    $trialKeyArgument = if($TrialLicensePublicKey) { $TrialLicensePublicKey } else { '-' }
    $trialEndpointArgument = if($TrialLicenseServerUrl) { $TrialLicenseServerUrl } else { '-' }
    & dotnet run --file (Join-Path $PSScriptRoot 'installer\VerifyPayload.cs') -- (Join-Path $payload 'RetailForma.Desktop.dll') $paidPem $trialKeyArgument $trialEndpointArgument
    if ($LASTEXITCODE -ne 0) { throw 'Release payload/public trust verification failed; installer was not built.' }
    if ($ValidateOnly) { Write-Output 'Installer payload validated. Compiler/install/upgrade/uninstall acceptance still required.'; return }
    $compiled = Join-Path $stageRoot 'compiled'
    & $compiler '/Q' "/DPayloadRoot=$payload" "/DProductVersion=$version" "/O$compiled" (Join-Path $PSScriptRoot 'installer\RetailForma.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $output = Join-Path $repoRoot 'artifacts\RetailFormaSetup.exe'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
    Copy-Item -LiteralPath (Join-Path $compiled 'RetailFormaSetup.exe') -Destination $output -Force
    if ($UpdateStablePackage) {
        $stable = Join-Path $repoRoot 'artifacts\RetailForma'
        New-Item -ItemType Directory -Force -Path $stable | Out-Null
        foreach ($item in Get-ChildItem -LiteralPath $payload) {
            Copy-Item -LiteralPath $item.FullName -Destination $stable -Recurse -Force
        }
        Write-Output "Updated existing stable POS/service package: $stable"
        Write-Output 'Unreferenced binaries from an older package are not treated as company data; the installer handles its fixed retired-provider list separately.'
    }
    Write-Output "Installer ready: $output"
    Write-Output 'Self-contained Release; company data untouched; no POS startup or installation performed.'
}
finally {
    $resolvedStage = [IO.Path]::GetFullPath($stageRoot)
    if (!$resolvedStage.StartsWith($stageBoundary, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedStage) -notlike 'RetailForma-installer-*') { throw 'Unsafe temporary cleanup path refused.' }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
