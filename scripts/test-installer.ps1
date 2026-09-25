[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ZipPath,
    [switch]$SkipUninstall
)
# Semi-automatic T13 acceptance helper: prepares a redirected install target,
# launches the graphical installer and uninstaller, waits while a human clicks
# through the wizard, and verifies every side effect automatically.
# ASCII-only source; Chinese labels are built with [char] casts, so no BOM is
# needed and any editor can touch this file.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$zip = (Resolve-Path -LiteralPath $ZipPath).Path
$root = Join-Path $env:TEMP ("t13-installer-" + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$extract = Join-Path $root 'package'
$plugins = Join-Path $root 'plugins'
$uninstaller = Join-Path $root 'uninstaller'
Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
$env:DN_SETUP_PLUGINS_ROOT = $plugins
$env:DN_SETUP_UNINSTALLER_ROOT = $uninstaller
$setup = Join-Path $extract 'Setup.exe'
if (-not (Test-Path -LiteralPath $setup)) { throw "ZIP root is missing Setup.exe: $zip" }
$bundle = Join-Path $plugins 'JUSTIFIED_specification-reflow-for-AutoCAD.bundle'
$pluginDll = Join-Path $bundle 'Contents/Windows/Justified.SpecificationReflow.AutoCAD.PluginHost.dll'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\JUSTIFIED_specification-reflow-for-AutoCAD'
$btnNext = [string][char]0x4E0B + [char]0x4E00 + [char]0x6B65
$btnInstall = [string][char]0x5F00 + [char]0x59CB + [char]0x5B89 + [char]0x88C5
$btnDone = [string][char]0x5B8C + [char]0x6210
$btnUninstall = [string][char]0x5378 + [char]0x8F7D
$btnClose = [string][char]0x5173 + [char]0x95ED

function Wait-Until([scriptblock]$Condition, [int]$TimeoutSec, [string]$Message) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 500
    }
    throw $Message
}

$failed = $false
Write-Host "== T13 acceptance helper =="
Write-Host "Package : $zip"
Write-Host "Sandbox : $root"
Write-Host "The live ApplicationPlugins candidate is never touched."

Write-Host ""
Write-Host "[1/2] Install test. In the wizard that opens now, click:"
Write-Host "        $btnNext  ->  $btnInstall  ->  $btnDone"
$proc = Start-Process -FilePath $setup -PassThru
try {
    Wait-Until { Test-Path -LiteralPath $pluginDll } 300 'Install check failed: PluginHost.dll never appeared.'
    Wait-Until { $null -ne (Get-ItemProperty -LiteralPath $uninstallKey -ErrorAction SilentlyContinue) } 30 'Install check failed: uninstall entry was not registered.'
    $key = Get-ItemProperty -LiteralPath $uninstallKey
    $registered = [System.IO.Path]::GetFullPath([string]$key.InstallLocation)
    $expected = [System.IO.Path]::GetFullPath($bundle)
    if ($registered -ne $expected) { throw "Install check failed: registered location is $registered (expected $expected)" }
    if (-not $key.UninstallString) { throw 'Install check failed: UninstallString is empty.' }
    $fileCount = @(Get-ChildItem -LiteralPath $bundle -Recurse -File).Count
    Write-Host "T13_INSTALL_OK bundle=$bundle files=$fileCount version=$($key.DisplayVersion)"
    Wait-Until { $proc.HasExited } 120 'The wizard did not close after Finish; close it manually.'
}
catch {
    $failed = $true
    Write-Host "T13_INSTALL_FAILED $($_.Exception.Message)"
}

if (-not $failed -and -not $SkipUninstall) {
    Write-Host ""
    Write-Host "[2/2] Uninstall test. In the wizard that opens now, click:"
    Write-Host "        $btnUninstall  ->  $btnClose"
    $proc2 = Start-Process -FilePath $setup -ArgumentList '--uninstall' -PassThru
    try {
        Wait-Until { -not (Test-Path -LiteralPath $bundle) -and $null -eq (Get-ItemProperty -LiteralPath $uninstallKey -ErrorAction SilentlyContinue) } 120 'Uninstall check failed: bundle or registry entry survived.'
        Write-Host "T13_UNINSTALL_OK"
        Wait-Until { $proc2.HasExited } 120 'The wizard did not close after Close; close it manually.'
    }
    catch {
        $failed = $true
        Write-Host "T13_UNINSTALL_FAILED $($_.Exception.Message)"
    }
}
elseif (-not $failed) {
    Write-Host "T13_UNINSTALL_SKIPPED"
}

if ($failed) {
    Write-Host "T13_ACCEPTANCE_FAILED sandbox kept for inspection: $root"
    exit 1
}
Write-Host "T13_ACCEPTANCE_OK sandbox kept for inspection: $root"
