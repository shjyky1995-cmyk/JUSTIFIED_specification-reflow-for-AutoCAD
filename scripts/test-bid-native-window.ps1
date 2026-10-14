param([int]$TargetProcessId, [int]$OwnerProcessId, [ValidateSet('Read','Minimize')][string]$Action='Read')
$ErrorActionPreference='Stop'
# 只检查由本次测试进程创建的模块，不操作用户实例。
$moduleProcess=Get-CimInstance Win32_Process -Filter "ProcessId=$TargetProcessId"
if (!$moduleProcess -or $moduleProcess.ParentProcessId -ne $OwnerProcessId) { throw '目标不属于本次自动检查，拒绝操作。' }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class BidWindowCheck {
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr window,int command);
}
'@
$nativeProcess=Get-Process -Id $TargetProcessId
$window=$nativeProcess.MainWindowHandle
if ($window -eq [IntPtr]::Zero) { throw '模块没有实际可见的原生窗口。' }
if ($Action -eq 'Minimize') { [void][BidWindowCheck]::ShowWindowAsync($window,6); Start-Sleep -Milliseconds 200 }
@{visible=[BidWindowCheck]::IsWindowVisible($window);minimized=[BidWindowCheck]::IsIconic($window);handle=$window.ToInt64()} | ConvertTo-Json -Compress
