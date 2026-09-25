[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReportDirectory,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [int]$MaxP95Milliseconds = 2000,
    [int]$MinSamples = 20
)
# CAL-08 cancellation latency: P95 of Esc-press to zero-residue exit, from run reports.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$reports = @(Get-ChildItem -LiteralPath $ReportDirectory -Filter 'dn-note-*.json' -File |
    Sort-Object Name | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
if ($reports.Count -eq 0) { throw 'No run reports found.' }
$cancelled = @($reports | Where-Object { $_.Cancelled -eq $true })
$committed = @($reports | Where-Object { $_.Committed -eq $true })
if ($committed.Count -ne 0) { throw "Report directory mixes committed runs; use a dedicated cancellation directory." }
if ($cancelled.Count -lt $MinSamples) { throw "Need at least $MinSamples cancelled runs for P95; got $($cancelled.Count)." }
foreach ($report in $cancelled) {
    if ($report.Success -eq $true) { throw "Cancelled report marked Success; check the run." }
    $latency = [double]$report.CancelToFinishMilliseconds
    if ([double]::IsNaN($latency) -or [latency -lt 0]) { throw "Invalid CancelToFinishMilliseconds in a report." }
}
$latencies = @($cancelled | ForEach-Object { [double]$_.CancelToFinishMilliseconds })
$ordered = @($latencies | Sort-Object)
$index = [int][Math]::Ceiling(0.95 * $ordered.Count) - 1
if ($index -lt 0) { $index = 0 }
if ($index -ge $ordered.Count) { $index = $ordered.Count - 1 }
$p95 = $ordered[$index]
$max = $ordered[$ordered.Count - 1]
$passed = $p95 -le $MaxP95Milliseconds
$result = [ordered]@{
    samples = $cancelled.Count
    p95Milliseconds = [Math]::Round($p95, 1)
    maxMilliseconds = [Math]::Round($max, 1)
    thresholdMilliseconds = $MaxP95Milliseconds
    passed = $passed
    zeroResiduePending = $true
    checkedUtc = [DateTime]::UtcNow.ToString('o')
}
$result | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding UTF8
if (-not $passed) { throw "P95 $p95 ms exceeds $MaxP95Milliseconds ms." }
Write-Output "T12_CANCEL_P95_OK samples=$($cancelled.Count) p95=$([Math]::Round($p95,1))ms max=$([Math]::Round($max,1))ms"
