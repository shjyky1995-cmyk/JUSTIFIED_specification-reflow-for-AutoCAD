[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReportDirectory,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$reports = @(Get-ChildItem -LiteralPath $ReportDirectory -Filter 'dn-note-*.json' -File |
    Sort-Object Name | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
if ($reports.Count -eq 0) { throw 'No run reports found.' }
$fields = @('InputHash','EngineVersion','StandardId','StandardVersion','TemplateId','TemplateVersion','UnitScale',
    'FirstRunInProcess','PreparationMilliseconds','UserWaitMilliseconds','RenderMilliseconds','EngineMilliseconds',
    'ReadMilliseconds','ParseMilliseconds','LayoutMilliseconds','PlacementMilliseconds','ElapsedMilliseconds')
foreach ($report in $reports) {
    foreach ($field in $fields) {
        if ($field -notin $report.PSObject.Properties.Name) { throw "Old/incomplete report: missing $field" }
    }
    if ([string]::IsNullOrWhiteSpace($report.InputHash)) { throw 'Input hash missing; do not mix unidentified samples.' }
    foreach ($field in $fields | Where-Object { $_ -like '*Milliseconds' }) {
        $value = [double]$report.$field
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -lt 0) { throw "Invalid timing: $field" }
    }
}
$groups = @($reports | Group-Object -Property InputHash,EngineVersion,StandardId,StandardVersion,TemplateId,TemplateVersion,UnitScale)
if ($groups.Count -ne 1) { throw 'Mixed input/configuration reports; select one benchmark directory.' }
$failed = @($reports | Where-Object { -not $_.Success -or -not $_.Committed })
$warm = @($reports | Where-Object { $_.Success -and $_.Committed -and -not $_.FirstRunInProcess })
if ($warm.Count -lt 20) { throw "Need at least 20 successful warm runs for P95; got $($warm.Count). Failed=$($failed.Count)." }
function Percentile95([double[]]$Values) {
    $ordered = @($Values | Sort-Object)
    return $ordered[[int][Math]::Ceiling(0.95 * $ordered.Count) - 1]
}
$prepare = Percentile95 @($warm | ForEach-Object { [double]$_.PreparationMilliseconds })
# 总时长扣除点选前准备和用户等待；包含点选后解析/排版、落图及命令开销。
$postAnchor = Percentile95 @($warm | ForEach-Object {
    [Math]::Max(0, [double]$_.ElapsedMilliseconds - [double]$_.PreparationMilliseconds - [double]$_.UserWaitMilliseconds)
})
$summary = [ordered]@{
    inputHash = $reports[0].InputHash
    totalRuns = $reports.Count
    failedRuns = $failed.Count
    warmRuns = $warm.Count
    firstCommandRuns = @($reports | Where-Object FirstRunInProcess).Count
    preparationP95Milliseconds = $prepare
    postAnchorP95Milliseconds = $postAnchor
    engineP95Milliseconds = Percentile95 @($warm | ForEach-Object { [double]$_.EngineMilliseconds })
    baselineTimingTargetsMet = ($failed.Count -eq 0 -and $prepare -le 3000 -and $postAnchor -le 1000)
    releaseAccepted = $false
    notes = 'Synthetic benchmark only. CAD startup/load costs, real sample calibration, cancellation latency and offline acceptance require separate evidence.'
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 5
