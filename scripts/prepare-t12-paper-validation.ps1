param([Parameter(Mandatory=$true)][string]$SampleDocx)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$workspace = Split-Path $PSScriptRoot -Parent
$sample = (Resolve-Path -LiteralPath $SampleDocx).Path
if ([IO.Path]::GetExtension($sample) -ne '.docx') { throw 'Sample must be a DOCX.' }
$run = Join-Path $workspace ('artifacts/t12-paper/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$package = Join-Path $run 'local-test-package'
$standardDir = Join-Path $package 'standards/jsr-note'
New-Item -ItemType Directory -Path $standardDir -Force | Out-Null
Copy-Item -LiteralPath $sample -Destination (Join-Path $run 'sample.docx')
foreach ($paper in @('A1','A2','A3')) {
    New-Item -ItemType Directory -Path (Join-Path $run "reports/$paper") -Force | Out-Null
}

$standard = Get-Content -LiteralPath (Join-Path $workspace 'standards/drafts/jsr-note-1.0.0.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$standard.classification = 'production'
$standard.symbolMapVersion = 'passthrough-1.0.0'
$standard.lineBreakRuleVersion = 'jsr-linebreak-1.0.0'
$standard.measurementTolerance = 0
foreach ($field in @{'superscriptScale'=0.7;'superscriptRise'=0.35;'subscriptScale'=0.7;'subscriptDrop'=0.2}.GetEnumerator()) {
    $standard | Add-Member -MemberType NoteProperty -Name $field.Key -Value $field.Value -Force
}
$standard | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $standardDir '1.0.0.json') -Encoding UTF8

foreach ($paper in @('A1','A2','A3')) {
    $id = switch ($paper) { 'A1' { 'jsr-A1-three-column' } 'A2' { 'jsr-A2-three-column' } 'A3' { 'jsr-A3-two-column' } }
    $template = Get-Content -LiteralPath (Join-Path $workspace "standards/drafts/$id-1.0.0.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $template.classification = 'production'
    $template.status = 'calibrated'
    $folder = Join-Path $package "templates/$id"
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $template | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $folder '1.0.0.json') -Encoding UTF8
}

[ordered]@{
    classification = 'local-test-only'
    releaseAccepted = $false
    sourceSampleSha256 = (Get-FileHash -LiteralPath $sample -Algorithm SHA256).Hash
    copiedSampleSha256 = (Get-FileHash -LiteralPath (Join-Path $run 'sample.docx') -Algorithm SHA256).Hash
    standardRule = 'jsr-note/1.0.0'
    templates = @('jsr-A1-three-column/1.0.0','jsr-A2-three-column/1.0.0','jsr-A3-two-column/1.0.0')
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'manifest.json') -Encoding UTF8

$auditProject = Join-Path $workspace 'tools/T12Audit/T12Audit.csproj'
& dotnet run --project $auditProject -- $run $package (Join-Path $run 'audit.json')
if ($LASTEXITCODE -ne 0) { throw 'T12 audit failed.' }
$audit = Get-Content -LiteralPath (Join-Path $run 'audit.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $audit.documents[0].parseSuccess -or -not $audit.package.standardSuccess -or
    @($audit.package.templates | Where-Object { -not $_.success }).Count -ne 0) {
    throw "Sample or local test package did not pass core parsing/validation. Inspect $run/audit.json"
}
Write-Output "T12_PAPER_TEST_READY $run"
