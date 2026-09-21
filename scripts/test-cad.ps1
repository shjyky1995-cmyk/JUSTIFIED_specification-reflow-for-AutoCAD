[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AutoCadDir,
    [Parameter(Mandatory=$true)][string]$PluginPath,
    [ValidateRange(5,120)][int]$TimeoutSeconds = 45
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path $PSScriptRoot -Parent
$console = Join-Path $AutoCadDir 'accoreconsole.exe'
if (-not (Test-Path -LiteralPath $console)) { throw 'AutoCAD Core Console not found.' }
$plugin = (Resolve-Path -LiteralPath $PluginPath).Path
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($console)
if ($version.FileMajorPart -ne 24 -or $version.FileMinorPart -ne 0) { throw 'Only AutoCAD 2021 is supported.' }
$output = Join-Path $taskRoot ('artifacts/cad-smoke/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$script = Join-Path $output 'diagnose.scr'
# No input drawing: Core Console opens a disposable drawing. Never save or lower SECURELOAD.
@('_.NETLOAD', ('"' + $plugin.Replace('\','/') + '"'), 'DN_DIAG', '_.QUIT', '_N', '') |
    Set-Content -LiteralPath $script -Encoding ASCII
$stdout = Join-Path $output 'stdout.log'
$stderr = Join-Path $output 'stderr.log'
$process = Start-Process -FilePath $console -ArgumentList @('/s', ('"' + $script + '"')) -WorkingDirectory $AutoCadDir -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    throw "CAD smoke timed out; inspect $output. Only the spawned process was stopped."
}
$process.Refresh()
$text = [IO.File]::ReadAllText($stdout, [Text.Encoding]::Unicode)
$text | Write-Output
if ($process.ExitCode -ne 0 -or $text -notmatch 'DN_DIAG_OK:' -or $text -match 'DN_DIAG_FAILED:') { throw "CAD diagnostic did not pass (exit $($process.ExitCode)); inspect $output" }
Write-Host "CAD dependency smoke passed. Evidence: $output"
