param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$run = (Resolve-Path -LiteralPath $RunDirectory).Path
$resultPath = Join-Path $run 'result.json'
@{ reportChecksPassed = $false; visualChecksPending = $true; printPending = $true; releaseAccepted = $false } |
    ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
$manifest = Get-Content -LiteralPath (Join-Path $run 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$audit = Get-Content -LiteralPath (Join-Path $run 'audit.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.classification -ne 'local-test-only' -or $manifest.releaseAccepted -ne $false) {
    throw 'Run directory is not a local test package.'
}
if ($manifest.sourceSampleSha256 -ne $manifest.copiedSampleSha256 -or
    (Get-FileHash -LiteralPath (Join-Path $run 'sample.docx') -Algorithm SHA256).Hash -ne $manifest.copiedSampleSha256) {
    throw 'Sample hash changed after preparation.'
}
if (@($audit.documents).Count -ne 1 -or -not $audit.documents[0].parseSuccess -or
    -not $audit.package.standardSuccess -or
    @($audit.package.templates | Where-Object { -not $_.success }).Count -ne 0) {
    throw 'Core DOCX/package audit did not pass.'
}
$rows = @()
foreach ($paper in @('A1','A2','A3')) {
    $directory = Join-Path $run "reports/$paper"
    $files = @(Get-ChildItem -LiteralPath $directory -Filter '*.json' -File)
    if ($files.Count -eq 0) { throw "Pending: no $paper host report." }
    $latest = $files | Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    $report = Get-Content -LiteralPath $latest.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $template = switch ($paper) { 'A1' { 'jsr-A1-three-column' } 'A2' { 'jsr-A2-three-column' } 'A3' { 'jsr-A3-two-column' } }
    if ($report.Success -ne $true -or $report.Committed -ne $true -or
        $report.InputHash -ne $manifest.copiedSampleSha256 -or
        $report.PaperCode -ne $paper -or $report.TemplateId -ne $template -or
        $report.StandardId -ne 'jsr-note' -or $report.StandardVersion -ne '1.0.0' -or
        $report.UnitScale -ne 1 -or $report.Pages -lt 2 -or $report.Objects -le 0 -or
        @($report.Errors).Count -ne 0 -or @($report.Warnings).Count -ne 0 -or
        @($report.TextBounds).Count -ne $report.Pages) {
        throw "$paper host report does not meet the two-page business sample checks."
    }
    $rows += [ordered]@{ paper = $paper; pages = $report.Pages; objects = $report.Objects; lines = $report.Lines; reportFile = $latest.Name }
}
[ordered]@{
    reportChecksPassed = $true
    papers = $rows
    visualChecksPending = $true
    printPending = $true
    externalMachinePending = $true
    releaseAccepted = $false
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding UTF8
Write-Output 'T12_PAPER_REPORTS_OK (visual, print, external machine and release checks still pending)'
