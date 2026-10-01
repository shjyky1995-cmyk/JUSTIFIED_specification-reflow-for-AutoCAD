[CmdletBinding()]
param(
    [string]$OutputPath = 'artifacts/ui-preview/note-picker.png',
    [ValidateRange(1, 3)][double]$Scale = 1,
    [string]$StandardsPath = 'standards/published'
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
    $root = Join-Path $projectRoot $StandardsPath
    $flags = [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::NonPublic
    $constructor = $type.GetConstructors($flags) | Where-Object { $_.GetParameters().Count -eq 2 } | Select-Object -First 1
    if ($null -eq $constructor) { throw 'NotePickerForm constructor not found.' }
    $arguments = New-Object 'object[]' 2
    $arguments[0] = [string]$root
    $arguments[1] = $null
    $form = $constructor.Invoke($arguments)
    try {
        # 离屏创建真实 WinForms 控件，避免抢占用户正在使用的 CAD 窗口。
        $form.CreateControl()
        $form.PerformAutoScale()
        $form.Scale((New-Object Drawing.SizeF($Scale, $Scale)))
        # 先收集原字体，再统一修改，避免继承字体被重复放大。
        $fontPlan = New-Object System.Collections.Generic.List[object]
        $queue = New-Object System.Collections.Queue
        $queue.Enqueue($form)
        while ($queue.Count -gt 0) {
            $control = $queue.Dequeue()
            $fontPlan.Add(@{ Control = $control; Font = $control.Font })
            foreach ($child in $control.Controls) { $queue.Enqueue($child) }
        }
        foreach ($entry in $fontPlan) {
            if ($Scale -ne 1) {
                $entry.Control.Font = New-Object Drawing.Font($entry.Font.FontFamily, ($entry.Font.SizeInPoints * $Scale), $entry.Font.Style, ([Drawing.GraphicsUnit]::Point))
            }
        }
        $flags = [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::NonPublic
        $type.GetField('_docx', $flags).GetValue($form).Text = ('G:\' + [char]0x8BD5 + [char]0x7528 + '\' + [char]0x590D + [char]0x6742 + [char]0x8868 + [char]0x683C + '.docx')
        $type.GetField('_scale', $flags).GetValue($form).Text = '100'
        $type.GetMethod('SelectPaper', $flags).Invoke($form, @('A3')) | Out-Null
        if ($form.SelectedTemplate.PaperCode -ne 'A3') { throw 'Paper selection did not update the actual template.' }
        $type.GetField('_scale', $flags).GetValue($form).Text = 'abc'
        $type.GetMethod('Confirm', $flags).Invoke($form, @()) | Out-Null
        if ($form.DialogResult -eq [Windows.Forms.DialogResult]::OK) { throw 'Invalid unit scale was accepted.' }
        $type.GetField('_scale', $flags).GetValue($form).Text = '100'
        $type.GetField('_docx', $flags).GetValue($form).Text = 'Z:\missing-test-note.docx'
        $type.GetMethod('Confirm', $flags).Invoke($form, @()) | Out-Null
        if ($form.DialogResult -eq [Windows.Forms.DialogResult]::OK) { throw 'Missing DOCX was accepted.' }
        $type.GetField('_docx', $flags).GetValue($form).Text = ('G:\' + [char]0x8BD5 + [char]0x7528 + '\' + [char]0x590D + [char]0x6742 + [char]0x8868 + [char]0x683C + '.docx')
        $type.GetMethod('SelectPaper', $flags).Invoke($form, @('A1')) | Out-Null
        $form.PerformLayout()
        # DrawToBitmap 需要创建顶层窗口句柄；透明窗口只用于后台绘制，不抢焦点。
        $form.Opacity = 0
        $form.Show()
        [Windows.Forms.Application]::DoEvents()
        $cards = @($form.Controls | Where-Object { $_.GetType().Name -eq 'PaperCard' })
        foreach ($card in $cards) {
            foreach ($other in $form.Controls) {
                if ($other -eq $card) { continue }
                if ($card.Bounds.IntersectsWith($other.Bounds)) { throw "Card overlaps another control: $($other.Text)" }
            }
        }
        $image = New-Object Drawing.Bitmap($form.Width, $form.Height)
        try {
            $form.DrawToBitmap($image, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
            $colors = New-Object 'System.Collections.Generic.HashSet[int]'
            for ($x = 0; $x -lt $image.Width; $x += 7) {
                for ($y = 0; $y -lt $image.Height; $y += 7) { $null = $colors.Add($image.GetPixel($x, $y).ToArgb()) }
            }
            if ($colors.Count -lt 8) { throw 'Preview is blank or missing its controls.' }
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
