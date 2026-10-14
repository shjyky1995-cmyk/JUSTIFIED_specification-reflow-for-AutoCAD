[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AutoCadDir,
    [string]$EvidenceDirectory = 'artifacts/t28-visual',
    [ValidateSet('simulated','host-textbox')][string]$LayoutMeasurement = 'simulated'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath((Join-Path $projectRoot $EvidenceDirectory))
$console = Join-Path $AutoCadDir 'accoreconsole.exe'
if (-not (Test-Path -LiteralPath $console)) { throw 'AutoCAD Core Console not found.' }
$sizes = @(@('A1',841,594), @('A2',594,420), @('A3',420,297))
foreach ($size in $sizes) {
    $paper = $size[0]; $width = $size[1]; $height = $size[2]
    $source = Join-Path $output "$paper-layout-inspection.dxf"
    $pdf = Join-Path $output "$paper-core-font-inspection.pdf"
    if (Test-Path -LiteralPath $pdf) { throw "Use a fresh evidence directory: $pdf exists." }
    $script = Join-Path $output "$paper-plot.scr"
    # 读取核心测试准备的 DXF，使用真实 SHX 出图；不加载插件、不修改安全设置。
    # 此路径只验证字体与图面，不能冒充 DSS 的测量、事务及撤销验收。
    $media = "ISO full bleed $paper ($width.00 x $height.00 " + [char]0x6BEB + [char]0x7C73 + ')'
    $lines = @(
        '(princ (strcat "T28_FONT_PATHS " (findfile "tssdeng.shx") " | " (findfile "tssdchn.shx")))',
        '_.-PLOT','_Y','Model','DWG To PDF.pc3',$media,'_M','_L','_N','_W',
        "0,-$height", "$width,0",'_F','_C','_Y','monochrome.ctb','_Y','_A',
        ('"' + $pdf.Replace('\','/') + '"'),'_N','_Y','_.QUIT','_Y',''
    )
    [IO.File]::WriteAllLines($script, $lines, [Text.Encoding]::GetEncoding(936))
    $stdout = Join-Path $output "$paper-core-stdout.log"
    $stderr = Join-Path $output "$paper-core-stderr.log"
    $arguments = '/i "' + $source + '" /s "' + $script + '"'
    $process = Start-Process -FilePath $console -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (-not $process.WaitForExit(45000)) {
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        throw "CAD plot timed out: $paper. Only the spawned process was stopped."
    }
    $log = [IO.File]::ReadAllText($stdout, [Text.Encoding]::Unicode)
    if (-not (Test-Path -LiteralPath $pdf) -or (Get-Item -LiteralPath $pdf).Length -lt 1000 -or $log -notmatch 'T28_FONT_PATHS') {
        throw "CAD plot did not produce verified output: $paper; inspect $stdout"
    }
    Write-Output "CAD_FONT_PLOT_OK $paper $pdf (prepared DXF, measurement=$LayoutMeasurement; not DSS)"
}
