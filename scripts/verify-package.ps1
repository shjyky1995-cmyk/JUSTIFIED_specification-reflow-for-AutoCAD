[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BundlePath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$bundleRoot = (Resolve-Path -LiteralPath $BundlePath).Path.TrimEnd('\', '/')
$prefix = $bundleRoot + [IO.Path]::DirectorySeparatorChar
$manifest = Get-Content -LiteralPath (Join-Path $bundleRoot 'SHA256.json') -Raw | ConvertFrom-Json
if (@($manifest).Count -eq 0) { throw 'Empty package manifest.' }
$seen = @{}
foreach ($entry in $manifest) {
    if ([IO.Path]::IsPathRooted($entry.File)) { throw 'Absolute manifest path rejected.' }
    $filePath = [IO.Path]::GetFullPath((Join-Path $bundleRoot $entry.File))
    if (-not $filePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes bundle.' }
    if ($seen.ContainsKey($filePath)) { throw "Duplicate manifest entry: $($entry.File)" }
    $seen[$filePath] = $true
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Missing package file: $($entry.File)" }
    if ((Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash -ne $entry.Hash) { throw "Hash mismatch: $($entry.File)" }
}
foreach ($file in Get-ChildItem -LiteralPath $bundleRoot -Recurse -File) {
    if ($file.FullName -eq (Join-Path $bundleRoot 'SHA256.json')) { continue }
    if (-not $seen.ContainsKey($file.FullName)) { throw "Unexpected package file: $($file.FullName)" }
    if ($file.Name -match '^(acmgd|acdbmgd|accoremgd)\.dll$' -or $file.Extension -in '.shx','.otf') {
        throw "SDK/CAD font must not be distributed in this package: $($file.Name)"
    }
    if ($file.Extension -eq '.ttf' -and $file.FullName -ne (Join-Path $bundleRoot 'Contents/Windows/fonts/NotoSansSC-VF.ttf')) {
        throw "Unexpected UI font: $($file.FullName)"
    }
}
$required = @('DocumentFormat.OpenXml.dll','DocumentFormat.OpenXml.Framework.dll','Newtonsoft.Json.dll')
$required += @('Contracts','DocumentCore','DocxAdapter','Standards','LayoutEngine','Application','AutoCadAdapter','PluginHost') |
    ForEach-Object { "Justified.SpecificationReflow.AutoCAD.$_.dll" }
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $bundleRoot "Contents/Windows/$name") -PathType Leaf)) { throw "Missing runtime dependency: $name" }
}
$uiFont = Join-Path $bundleRoot 'Contents/Windows/fonts/NotoSansSC-VF.ttf'
if ((-not (Test-Path -LiteralPath $uiFont -PathType Leaf)) -or
    ((Get-FileHash -LiteralPath $uiFont -Algorithm SHA256).Hash -ne '763146584CF0710223441356B4395E279021B0806C196614377A7A0174AE074A')) {
    throw 'Bundled Noto Sans SC font is missing or differs from the approved file.'
}
if (-not (Test-Path -LiteralPath (Join-Path $bundleRoot 'third-party/NotoSansSC-OFL.txt') -PathType Leaf)) {
    throw 'Bundled font license is missing.'
}
[xml]$xml = Get-Content -LiteralPath (Join-Path $bundleRoot 'PackageContents.xml') -Raw
$commands = @($xml.ApplicationPackage.Components.ComponentEntry.Commands.Command | ForEach-Object { $_.Global })
foreach ($command in @('DN_DIAG','DN_NOTE_SET','DN_NOTE')) {
    if ($command -notin $commands) { throw "Missing autoload command: $command" }
}
Write-Output "PACKAGE_VERIFY_OK files=$(@($manifest).Count)"
