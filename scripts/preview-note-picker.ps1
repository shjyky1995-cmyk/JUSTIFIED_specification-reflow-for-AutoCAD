[CmdletBinding()]
param(
    [string]$OutputPath = 'artifacts/ui-preview/note-picker.png'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$projectRoot = Split-Path $PSScriptRoot -Parent
$binaryDirectory = Join-Path $projectRoot 'src/Justified.SpecificationReflow.AutoCAD.PluginHost/bin/Release/net48'
$pluginPath = Join-Path $binaryDirectory 'Justified.SpecificationReflow.AutoCAD.PluginHost.dll'
if (-not (Test-Path -LiteralPath $pluginPath)) { throw 'Build the Release plugin before previewing.' }
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = (New-Object Reflection.AssemblyName($eventArgs.Name)).Name + '.dll'
    $candidate = Join-Path $binaryDirectory $name
    if (Test-Path -LiteralPath $candidate) { return [Reflection.Assembly]::LoadFrom($candidate) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $assembly = [Reflection.Assembly]::LoadFrom($pluginPath)
    $type = $assembly.GetType('Justified.SpecificationReflow.AutoCAD.PluginHost.NotePickerForm', $true)
    $root = Join-Path $projectRoot 'standards/published'
    $flags = [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::NonPublic
    $constructor = $type.GetConstructors($flags) | Where-Object { $_.GetParameters().Count -eq 2 } | Select-Object -First 1
    if ($null -eq $constructor) { throw 'NotePickerForm constructor not found.' }
    $arguments = New-Object 'object[]' 2
    $arguments[0] = [string]$root
    $arguments[1] = $null
    $form = $constructor.Invoke($arguments)
    try {
        $form.Show()
        [Windows.Forms.Application]::DoEvents()
        $image = New-Object Drawing.Bitmap($form.Width, $form.Height)
        try {
            $form.DrawToBitmap($image, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
            $fullPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($fullPath)) | Out-Null
            $image.Save($fullPath, [Drawing.Imaging.ImageFormat]::Png)
            Write-Host "UI_PREVIEW_OK $fullPath"
        }
        finally { $image.Dispose() }
    }
    finally { $form.Close(); $form.Dispose() }
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
