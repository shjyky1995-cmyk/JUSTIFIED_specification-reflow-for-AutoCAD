param([Parameter(Mandatory=$true)][string]$TransferDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $TransferDirectory).Path.TrimEnd('\','/')
$prefix = $root + [IO.Path]::DirectorySeparatorChar
$manifest = Get-Content -LiteralPath (Join-Path $root 'SHA256.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (@($manifest.files).Count -eq 0 -or $manifest.classification -ne 'local-test-only') { throw 'Invalid transfer manifest.' }
$seen = @{}
foreach ($entry in $manifest.files) {
    if ([IO.Path]::IsPathRooted($entry.file)) { throw 'Absolute path in transfer manifest.' }
    $path = [IO.Path]::GetFullPath((Join-Path $root $entry.file))
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Transfer path escapes directory.' }
    if ($seen.ContainsKey($path)) { throw 'Duplicate transfer file.' }
    $seen[$path] = $true
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing transfer file: $($entry.file)" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Transfer hash mismatch: $($entry.file)" }
}
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
    if ($file.FullName -eq (Join-Path $root 'SHA256.json')) { continue }
    if (-not $seen.ContainsKey($file.FullName)) { throw "Unexpected transfer file: $($file.FullName)" }
}
Write-Output "T12_TRANSFER_VERIFY_OK files=$(@($manifest.files).Count)"
