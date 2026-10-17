[CmdletBinding()]
param(
    [ValidateSet('All', 'OpenSSL', 'Amalgamation', 'SqlCipher')][string]$Stage = 'All',
    [string]$PerlPath = 'C:\Strawberry\perl\bin\perl.exe'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cache = Join-Path $repository 'artifacts\native-security'
$output = Join-Path $cache 'output'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Install Microsoft C++ Build Tools first.' }
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'The x64 Microsoft C++ toolchain is not installed.' }
$vcvars = Join-Path ($vs | Select-Object -First 1) 'VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars)) { throw 'The C++ installation is incomplete; vcvars64.bat is missing.' }
# Only initialize this build process. No filesystem operation is delegated across shells.
$environmentLines = & cmd.exe /d /c ('call "{0}" >nul && set' -f $vcvars)
if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the x64 compiler environment.' }
foreach ($line in $environmentLines) {
    $separator = $line.IndexOf('=')
    if ($separator -gt 0) { [Environment]::SetEnvironmentVariable($line.Substring(0, $separator), $line.Substring($separator + 1), 'Process') }
}
if ($env:VSCMD_ARG_TGT_ARCH -ne 'x64') { throw 'Only the verified Windows x64 build is supported.' }
$env:LC_ALL = 'C'
$env:LC_CTYPE = 'C'
New-Item -ItemType Directory -Path $cache, $output -Force | Out-Null

function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed (exit $LASTEXITCODE). No customer package was updated." }
}
function Get-Source([string]$Name, [string]$Url, [string]$Tag, [string]$Commit) {
    $directory = Join-Path $cache $Name
    if (-not (Test-Path -LiteralPath $directory)) {
        Invoke-Checked git @('clone', '--depth', '1', '--branch', $Tag, $Url, $directory)
    }
    $actual = & git -C $directory rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actual -ne $Commit) { throw "$Name source does not match the reviewed upstream commit (expected $Commit; received $actual; git exit $LASTEXITCODE)." }
    $changes = & git -C $directory diff --name-only HEAD
    if ($LASTEXITCODE -ne 0 -or $changes) { throw "$Name has modified tracked source; review it before building." }
    return $directory
}

if ($Stage -in @('All', 'OpenSSL')) {
    if (-not (Test-Path -LiteralPath $PerlPath)) { throw 'Install native Windows Strawberry Perl first.' }
    $openssl = Get-Source 'openssl' 'https://github.com/openssl/openssl.git' 'openssl-3.5.9' '45e844fa2a14ec92d146bd8f5778ac130b6625fb'
    Push-Location $openssl
    try {
        Invoke-Checked $PerlPath @('Configure', 'VC-WIN64A', 'no-shared', 'no-asm', 'no-module')
        Invoke-Checked nmake @('/nologo', 'build_libs')
    }
    finally { Pop-Location }
}

if ($Stage -in @('All', 'Amalgamation', 'SqlCipher')) {
    $cipher = Get-Source 'sqlcipher' 'https://github.com/sqlcipher/sqlcipher.git' 'v4.19.0' 'c4b275a47932888216bade83aff2bbc73df0ff85'
    $openssl = Join-Path $cache 'openssl'
    if ($Stage -ne 'Amalgamation' -and -not (Test-Path -LiteralPath (Join-Path $openssl 'libcrypto.lib'))) { throw 'Build OpenSSL first.' }
    Push-Location $cipher
    try {
        # SQLCipher's reviewed source builds its own Jim interpreter; no separate Tcl install is needed.
        Invoke-Checked nmake @('/nologo', '/f', 'Makefile.msc', 'DEBUG=0', 'USE_CRT_DLL=0', 'PLATFORM=x64', 'sqlite3.c')
        if ($Stage -eq 'Amalgamation') { return }
        Invoke-Checked cl @('/nologo', '/O2', '/MT', '/LD', '/guard:cf', '/DSQLITE_API=__declspec(dllexport)',
            '/DSQLITE_HAS_CODEC', '/DSQLITE_EXTRA_INIT=sqlcipher_extra_init', '/DSQLITE_EXTRA_SHUTDOWN=sqlcipher_extra_shutdown',
            '/DSQLITE_TEMP_STORE=2', '/DSQLITE_THREADSAFE=1', '/DSQLITE_OMIT_LOAD_EXTENSION', '/DSQLITE_SECURE_DELETE=1',
            '/DSQLCIPHER_CRYPTO_OPENSSL', "/I$openssl\include", 'sqlite3.c', "$openssl\libcrypto.lib",
            'ws2_32.lib', 'gdi32.lib', 'advapi32.lib', 'crypt32.lib', 'user32.lib', 'bcrypt.lib', '/link', '/DYNAMICBASE', '/NXCOMPAT',
            '/GUARD:CF', "/OUT:$output\sqlcipher.dll", "/IMPLIB:$output\sqlcipher.lib")
        Copy-Item -LiteralPath (Join-Path $cipher 'LICENSE.md') -Destination (Join-Path $output 'SQLCipher-LICENSE.md')
        Copy-Item -LiteralPath (Join-Path $openssl 'LICENSE.txt') -Destination (Join-Path $output 'OpenSSL-LICENSE.txt')
        Write-Host "Native build ready for isolated tests: $output"
        Get-FileHash -LiteralPath (Join-Path $output 'sqlcipher.dll') -Algorithm SHA256
    }
    finally { Pop-Location }
}
# Do not copy this build into the POS or installer. Native/integration acceptance is a separate gate.
