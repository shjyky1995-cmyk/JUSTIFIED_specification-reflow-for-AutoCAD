param(
    [Parameter(Mandatory=$true)][string]$BundlePath,
    [Parameter(Mandatory=$true)][string]$TestStandardRoot,
    [Parameter(Mandatory=$true)][string]$SampleDocx
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$bundle = (Resolve-Path -LiteralPath $BundlePath).Path
& (Join-Path $PSScriptRoot 'verify-package.ps1') -BundlePath $bundle
$standard = (Resolve-Path -LiteralPath $TestStandardRoot).Path
$sample = (Resolve-Path -LiteralPath $SampleDocx).Path
$run = Join-Path $workspace ('artifacts/t11-manual/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
Copy-Item -LiteralPath $standard -Destination (Join-Path $run 'test-package') -Recurse
Copy-Item -LiteralPath $sample -Destination (Join-Path $run 'normal.docx')
Copy-Item -LiteralPath $sample -Destination (Join-Path $run 'cancel.docx')
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::Open((Join-Path $run 'cancel.docx'), [IO.Compression.ZipArchiveMode]::Update)
try {
    $archive.GetEntry('word/document.xml').Delete()
    $entry = $archive.CreateEntry('word/document.xml')
    $writer = New-Object IO.StreamWriter($entry.Open(), (New-Object Text.UTF8Encoding($false)))
    try {
        $writer.Write('<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>')
        $text = ([string][char]0x7532) * 999 + [char]0x3002
        for ($i = 0; $i -lt 190; $i++) {
            $writer.Write('<w:p><w:pPr><w:pStyle w:val="Normal"/></w:pPr><w:r><w:t>' + $text + '</w:t></w:r></w:p>')
        }
        $writer.Write('</w:body></w:document>')
    } finally { $writer.Dispose() }
} finally { $archive.Dispose() }
# Scripts only touch unique local copies. No fonts, trust settings or business drawings are changed.
$r = $run.Replace('\','/')
$dll = (Join-Path $bundle 'Contents/Windows/Justified.SpecificationReflow.AutoCAD.PluginHost.dll').Replace('\','/')
function SettingsLines([string]$name, [string]$reports) {
    @('DN_NOTE_SET', "$r/test-package", 'A1', '1', '1', ('"' + "$r/$name.docx" + '"'), "$r/$reports")
}
$lines = @(
    '(setq t11fd (getvar "FILEDIA"))',
    '(setvar "FILEDIA" 0)',
    'NETLOAD', ('"' + $dll + '"'),
    '(defun t11count (path / f s) (setq f (open path "w") s (ssget "_X" (list (cons 0 "TEXT") (cons 410 "Model")))) (prin1 (if s (sslength s) 0) f) (close f) (princ))',
    ('(t11count "' + "$r/before.txt" + '")')
)
$lines += SettingsLines 'normal' 'reports-normal'
$lines += @('DN_NOTE','0,0,0', ('(t11count "' + "$r/generated.txt" + '")'),
    '(if (ssget "_X" (list (cons 0 "TEXT") (cons 410 "Model"))) (command "_.U"))',
    ('(t11count "' + "$r/undo.txt" + '")'),
    ('(vl-file-rename "' + "$r/normal.docx" + '" "' + "$r/normal.hidden" + '")'),
    'DN_NOTE', ('(t11count "' + "$r/missing.txt" + '")'),
    ('(vl-file-rename "' + "$r/normal.hidden" + '" "' + "$r/normal.docx" + '")'))
$lines += SettingsLines 'cancel' 'reports-cancel'
$lines += @('(setvar "FILEDIA" t11fd)', '(princ "\nT11_AUTO_FINISHED: run DN_NOTE, click a point, then hold Esc.\n")')
$lines | Set-Content -LiteralPath (Join-Path $run '01-auto.scr') -Encoding UTF8
@(('(t11count "' + "$r/after-cancel.txt" + '")'), '(princ "\nT11_COLLECT_FINISHED\n")') |
    Set-Content -LiteralPath (Join-Path $run '02-collect.scr') -Encoding UTF8
@('(if t11fd (setvar "FILEDIA" t11fd))',
    ('(if (findfile "' + "$r/normal.hidden" + '") (vl-file-rename "' + "$r/normal.hidden" + '" "' + "$r/normal.docx" + '"))'),
    '(princ "\nT11_LOCAL_STATE_RESTORED\n")') |
    Set-Content -LiteralPath (Join-Path $run '99-restore.scr') -Encoding UTF8
# Standalone recovery when a run is interrupted before FILEDIA is restored: SCRIPT this file in CAD.
@('(setvar "FILEDIA" 1)', '(princ "\nFILEDIA_RESTORED\n")') |
    Set-Content -LiteralPath (Join-Path $run 'restore-filedia.scr') -Encoding UTF8
[ordered]@{
    classification='local-test-only'; preparedUtc=[DateTime]::UtcNow.ToString('o')
    bundle=$bundle; dllSha256=(Get-FileHash -LiteralPath $dll).Hash
    normalSha256=(Get-FileHash -LiteralPath (Join-Path $run 'normal.docx')).Hash
    cancellationCharacters=190000; cancellationParagraphs=190
    cancellationLatencyAccepted=$false; offlineAccepted=$false
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'manifest.json') -Encoding UTF8
Write-Host "T11_MANUAL_READY $run"
