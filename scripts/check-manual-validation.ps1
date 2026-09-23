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
    if ((CountAt $file) -ne 0) {
        if ($file -eq 'after-cancel.txt') { throw 'Cancellation did not leave an empty drawing. If generation finished before Esc, close this test drawing without saving and use a fresh run directory.' }
        throw "Residual TEXT: $file"
    }
}
$normal = @(Get-ChildItem -LiteralPath (Join-Path $root 'reports-normal') -Filter *.json | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
if ($normal.Count -lt 2) { throw 'Pending: need normal and missing-input reports.' }
$success = @($normal | Where-Object { $_.Success -eq $true -and $_.Committed -eq $true -and $_.Objects -eq $generated -and $_.Errors.Count -eq 0 })
$missing = @($normal | Where-Object { $_.Success -eq $false -and $_.Committed -eq $false -and $_.Objects -eq 0 -and ($_.Errors -join ' ') -match 'E_DOCX_READ' })
if ($success.Count -lt 1 -or $missing.Count -lt 1 -or ($success.Count + $missing.Count) -ne $normal.Count) { throw 'Normal/missing-input reports did not match expectation.' }
if (@($success | Where-Object { $_.InputHash -ne $manifest.normalSha256 }).Count -gt 0) { throw 'Normal report input hash differs from prepared sample.' }
$cancel = @(Get-ChildItem -LiteralPath (Join-Path $root 'reports-cancel') -Filter *.json | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
if ($cancel.Count -lt 1) { throw 'Pending: no cancellation report. Keep reports for diagnosis.' }
$latestCancel = $cancel | Sort-Object FinishedUtc | Select-Object -Last 1
if ($latestCancel.Success -ne $false -or $latestCancel.Committed -ne $false -or $latestCancel.Objects -ne 0 -or
    ($latestCancel.Errors -join ' ') -notmatch 'CANCELLED' -or
    ($latestCancel.ParseMilliseconds + $latestCancel.LayoutMilliseconds + $latestCancel.RenderMilliseconds) -le 0) {
    throw 'Latest attempt did not cancel during processing. Use a fresh empty drawing and run directory.'
}
[ordered]@{ hostChecksPassed=$true; generatedObjects=$generated; cancellationDuringProcessing=$true; cancellationLatencyAccepted=$false; offlineAccepted=$false; releaseAccepted=$false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'result.json') -Encoding UTF8
Write-Host 'T11_MANUAL_CHECK_OK (latency/offline/release still pending)'
