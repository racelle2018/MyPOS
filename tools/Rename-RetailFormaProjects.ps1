$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$quarantine = Join-Path $root ('artifacts/retired-mypos-builds-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$projects = @(
 'src/MyPos.Core', 'src/MyPos.Desktop', 'src/MyPos.Setup',
 'src/MyPos.LicenseIssuer.Gui', 'src/MyPos.TrialApi',
 'tests/MyPos.Tests', 'tests/MyPos.Desktop.Tests', 'tests/MyPos.LicenseIssuer.Gui.Tests'
)
function Assert-InWorkspace([string]$path) {
 $resolved = [System.IO.Path]::GetFullPath($path)
 if (-not $resolved.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Outside workspace: $resolved" }
 return $resolved
}
# Preflight every source/destination before moving anything. Never touch AppData or key folders.
foreach ($project in $projects) {
 $source = Assert-InWorkspace (Join-Path $root $project)
 $target = Assert-InWorkspace ($source.Replace('MyPos.', 'RetailForma.'))
 if (-not (Test-Path -LiteralPath $source)) { throw "Missing project: $source" }
 if (Test-Path -LiteralPath $target) { throw "Destination exists: $target" }
}
foreach ($project in $projects) {
 foreach ($generated in @('bin', 'obj')) {
  $source = Assert-InWorkspace (Join-Path (Join-Path $root $project) $generated)
  if (Test-Path -LiteralPath $source) {
   $target = Assert-InWorkspace (Join-Path $quarantine ($project + '/' + $generated))
   New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
   try { Move-Item -LiteralPath $source -Destination $target }
   catch { Write-Warning "Generated folder is locked; leaving it untouched: $source" }
  }
 }
}
$oldGenerator = Assert-InWorkspace (Join-Path $root 'artifacts/license-generator')
if (Test-Path -LiteralPath $oldGenerator) {
 $target = Assert-InWorkspace (Join-Path $quarantine 'license-generator')
 New-Item -ItemType Directory -Path $quarantine -Force | Out-Null
 Move-Item -LiteralPath $oldGenerator -Destination $target
}
$extensions = @('.cs','.xaml','.csproj','.slnx','.props','.targets','.md','.json','.mjs','.sql','.ps1')
$roots = @('src','tests','tools','.vscode')
$files = @(Get-ChildItem -LiteralPath $root -File)
foreach ($directory in $roots) { $files += Get-ChildItem -LiteralPath (Join-Path $root $directory) -File -Recurse }
$encoding = New-Object System.Text.UTF8Encoding $false
foreach ($file in $files) {
 if ($file.FullName -eq $PSCommandPath -or $file.FullName -match '[\\/](node_modules|bin|obj)[\\/]' -or $file.Extension -notin $extensions) { continue }
 $original = [System.IO.File]::ReadAllText($file.FullName)
 $updated = $original
 $protected = @('MyPos.Basic','Local\MyPosDesktop','"MyPos"','D:\MyPos','D:/MyPos')
 for ($i=0; $i -lt $protected.Count; $i++) { $updated = $updated.Replace($protected[$i], "__RETAILFORMA_COMPAT_$i`__") }
 $updated = $updated.Replace('MyPos', 'RetailForma')
 for ($i=0; $i -lt $protected.Count; $i++) { $updated = $updated.Replace("__RETAILFORMA_COMPAT_$i`__", $protected[$i]) }
 if ($updated -ne $original) { [System.IO.File]::WriteAllText($file.FullName, $updated, $encoding) }
}
foreach ($project in $projects) {
 $source = Assert-InWorkspace (Join-Path $root $project)
 Get-ChildItem -LiteralPath $source -File -Recurse | Where-Object {$_.Name.Contains('MyPos') -and $_.FullName -notmatch '[\\/](bin|obj|node_modules)[\\/]'} | ForEach-Object {
  $target = Assert-InWorkspace (Join-Path $_.DirectoryName ($_.Name.Replace('MyPos','RetailForma')))
  if (Test-Path -LiteralPath $target) { throw "File destination exists: $target" }
  Move-Item -LiteralPath $_.FullName -Destination $target
 }
 $destination = Assert-InWorkspace ($source.Replace('MyPos.', 'RetailForma.'))
 New-Item -ItemType Directory -Path $destination | Out-Null
 Get-ChildItem -LiteralPath $source -Force | Where-Object {$_.Name -notin @('bin','obj')} | ForEach-Object {
  Move-Item -LiteralPath $_.FullName -Destination (Join-Path $destination $_.Name)
 }
 if (@(Get-ChildItem -LiteralPath $source -Force).Count -eq 0) { Remove-Item -LiteralPath $source }
}
Move-Item -LiteralPath (Join-Path $root 'MyPos.slnx') -Destination (Join-Path $root 'RetailForma.slnx')
Write-Output "Renamed projects/namespaces. Recoverable generated-output quarantine: $quarantine"
