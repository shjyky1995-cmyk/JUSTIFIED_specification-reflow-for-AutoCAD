param(
    [Parameter(Mandatory=$true)][string]$RunDirectory,
    [Parameter(Mandatory=$true)][string]$CandidateZip
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspace = Split-Path $PSScriptRoot -Parent
$run = (Resolve-Path -LiteralPath $RunDirectory).Path
$candidate = (Resolve-Path -LiteralPath $CandidateZip).Path
$manifest = Get-Content -LiteralPath (Join-Path $run 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.classification -ne 'local-test-only' -or $manifest.releaseAccepted -ne $false) { throw 'Only local test runs can be transferred.' }
if ((Get-FileHash -LiteralPath (Join-Path $run 'sample.docx') -Algorithm SHA256).Hash -ne $manifest.copiedSampleSha256) {
    throw 'Sample hash changed after preparation.'
}
$directory = Join-Path $workspace ('artifacts/t12-transfer/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $directory | Out-Null
Copy-Item -LiteralPath $candidate -Destination (Join-Path $directory 'plugin-candidate.zip')
Copy-Item -LiteralPath (Join-Path $run 'sample.docx') -Destination $directory
Copy-Item -LiteralPath (Join-Path $run 'local-test-package') -Destination $directory -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify-t12-transfer.ps1') -Destination (Join-Path $directory 'verify.ps1')
New-Item -ItemType Directory -Path (Join-Path $directory 'reports') | Out-Null
$instructions = @'
# T12 外机与断网试验包（仅本地测试）

此目录含用户业务样本，不要上传或转发给无关人员。包内标准仅供验收，不能复制到正式出图标准目录。

1. 在目标机 Windows PowerShell 中运行：`& '.\verify.ps1' -TransferDirectory '.'`，应显示 T12_TRANSFER_VERIFY_OK。
2. 先解压 plugin-candidate.zip，进入 .bundle 目录运行包内 verify-package.ps1，应显示 PACKAGE_VERIFY_OK。字体另由授权来源安装，本包不含字体或 Autodesk SDK。
3. 目标机须为 Windows x64 + AutoCAD 2021 + .NET Framework 4.8。退出 AutoCAD 后将完整 .bundle 放入当前用户 `%APPDATA%\Autodesk\ApplicationPlugins`，移开同名旧包，重启后执行 DN_DIAG，记录 DN_DIAG_OK 或完整错误。
4. 新建独立空白模型图，先明确测试图按 1 图形单位 = 1 mm。执行 DN_NOTE_SET：包根目录选此目录的 local-test-package，图幅 A1，模板序号 1，单位比例 1，文档 sample.docx，报告目录选此目录的 reports。执行 DN_NOTE，点一次位置；记录页数、对象数和截图。
5. 断网核对时，先由用户确认文件及插件已在目标机本地，再自行断开网络、重启 CAD、重复第 4 步。保留网络断开状态证据与新报告；随后恢复网络。不要更改 SECURELOAD 或信任路径。
6. 记录目标机 AutoCAD 版本、OS、字体文件名及 SHA256、插件 ZIP SHA256、样本 SHA256、图面观察、断网状态和复核人。报告含本地路径，外发前脱敏。

通过本步骤只证明外机与断网生成；真实打印、结构专业签收和正式发布仍另行验收。
'@
$instructions | Set-Content -LiteralPath (Join-Path $directory 'README.md') -Encoding UTF8

$files = @(Get-ChildItem -LiteralPath $directory -Recurse -File | ForEach-Object {
    [ordered]@{
        file = $_.FullName.Substring($directory.Length + 1).Replace('\','/')
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
[ordered]@{
    classification = 'local-test-only'
    releaseAccepted = $false
    sourceCandidateSha256 = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
    sampleSha256 = $manifest.copiedSampleSha256
    files = $files
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $directory 'SHA256.json') -Encoding UTF8
& (Join-Path $directory 'verify.ps1') -TransferDirectory $directory
if (-not $?) { throw 'Transfer verification failed.' }
$zip = $directory + '.zip'
Compress-Archive -Path (Join-Path $directory '*') -DestinationPath $zip
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
$zipHash | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII
Write-Output "T12_TRANSFER_READY $directory"
Write-Output "T12_TRANSFER_ZIP $zip SHA256=$zipHash"
