param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RunDirectory).Path
@{ hostChecksPassed=$false; status='pending-or-failed'; releaseAccepted=$false } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $root 'result.json') -Encoding UTF8
$manifest = Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
function CountAt([string]$name) {
    $path = Join-Path $root $name
    if (!(Test-Path -LiteralPath $path)) { throw "Pending: missing $name" }
    $value = (Get-Content -LiteralPath $path -Raw).Trim()
    if ($value -notmatch '^\d+$') { throw "Invalid count: $name" }
    return [int]$value
}
$before = CountAt 'before.txt'
if ($before -ne 0) { throw 'Invalid run: original drawing was not empty.' }
$generated = CountAt 'generated.txt'
if ($generated -le 0) { throw 'Normal generation did not produce text.' }
foreach ($file in @('undo.txt','missing.txt','after-cancel.txt')) {
    if ((CountAt $file) -ne 0) { throw "Residual TEXT: $file" }
}
$normal = @(Get-ChildItem -LiteralPath (Join-Path $root 'reports-normal') -Filter *.json | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
if ($normal.Count -ne 2) { throw 'Expected exactly two reports for normal and missing input.' }
$success = @($normal | Where-Object { $_.Success -eq $true -and $_.Committed -eq $true -and $_.Objects -eq $generated -and $_.Errors.Count -eq 0 })
$missing = @($normal | Where-Object { $_.Success -eq $false -and $_.Committed -eq $false -and $_.Objects -eq 0 -and ($_.Errors -join ' ') -match 'E_DOCX_READ' })
if ($success.Count -ne 1 -or $missing.Count -ne 1) { throw 'Normal/missing-input report did not match expectation.' }
if ($success[0].InputHash -ne $manifest.normalSha256) { throw 'Normal report input hash differs from prepared sample.' }
$cancel = @(Get-ChildItem -LiteralPath (Join-Path $root 'reports-cancel') -Filter *.json | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$matched = @($cancel | Where-Object {
    $_.Success -eq $false -and $_.Committed -eq $false -and $_.Objects -eq 0 -and
    ($_.Errors -join ' ') -match 'CANCELLED' -and ($_.ParseMilliseconds + $_.LayoutMilliseconds + $_.RenderMilliseconds) -gt 0
})
if ($cancel.Count -ne 1 -or $matched.Count -ne 1) { throw 'Pending: need exactly one cancellation during processing, not at point selection. Keep reports for diagnosis.' }
[ordered]@{ hostChecksPassed=$true; generatedObjects=$generated; cancellationDuringProcessing=$true; cancellationLatencyAccepted=$false; offlineAccepted=$false; releaseAccepted=$false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'result.json') -Encoding UTF8
Write-Host 'T11_MANUAL_CHECK_OK (latency/offline/release still pending)'
