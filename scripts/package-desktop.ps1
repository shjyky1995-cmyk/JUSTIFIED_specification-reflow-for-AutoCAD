[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [string]$ContentLibraryDirectory = '',
    [string]$BiddingFrameworkDirectory = '',
    [string]$BiddingSourceArchive = '',
    [string]$Dotnet10 = '',
    [switch]$Portable
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appDirectory = Join-Path $projectRoot 'desktop/app'
$workerDirectory = Join-Path $projectRoot 'desktop/worker'
$version = (Get-Content (Join-Path $appDirectory 'package.json') -Raw | ConvertFrom-Json).version
$revision = (& git -C $projectRoot rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw '无法读取 Git 版本。' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $projectRoot "artifacts/desktop-client-$version-$revision-$(Get-Date -Format yyyyMMdd-HHmmss)" }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw "输出目录已存在，避免覆盖：$outputRoot" }
if (!$Dotnet10) {
    $candidates = @((Join-Path $projectRoot 'artifacts/dotnet10/sdk/dotnet.exe'), (Join-Path $projectRoot '../../artifacts/dotnet10/sdk/dotnet.exe'))
    $Dotnet10 = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$Dotnet10) { throw '请通过 -Dotnet10 指定本机 .NET 10 SDK 的 dotnet.exe。' }
}
function Invoke-Build([string]$Directory, [string]$Command, [string[]]$Arguments) {
    Push-Location -LiteralPath $Directory
    try {
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) { throw "构建失败：$Command $Arguments" }
    } finally { Pop-Location }
}
Invoke-Build $appDirectory 'npm.cmd' @('run', 'build')
$workerOutput = Join-Path $projectRoot 'artifacts/desktop-worker-release'
Invoke-Build $workerDirectory $Dotnet10 @('publish', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=false', '-o', $workerOutput)
if (!$Portable) { Invoke-Build $projectRoot 'dotnet' @('build', 'desktop/setup/DesktopSetup.csproj', '-c', 'Release') }
$electronDirectory = Join-Path $appDirectory 'node_modules/electron/dist'
if (!(Test-Path -LiteralPath (Join-Path $electronDirectory 'electron.exe'))) { throw '缺少 Electron 运行时，请先 npm ci。' }
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$clientRoot = Join-Path $outputRoot 'client'
Copy-Item -LiteralPath $electronDirectory -Destination $clientRoot -Recurse
Rename-Item -LiteralPath (Join-Path $clientRoot 'electron.exe') -NewName 'EngiSpace.exe'
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/Justified.SpecificationReflow.AutoCAD.Setup/Assets/app.ico') -Destination (Join-Path $clientRoot 'product-icon.ico')
$runtimeApp = Join-Path $clientRoot 'resources/app'
New-Item -ItemType Directory -Path $runtimeApp | Out-Null
foreach ($folder in @('dist', 'dist-electron')) { Copy-Item -LiteralPath (Join-Path $appDirectory $folder) -Destination (Join-Path $runtimeApp $folder) -Recurse }
New-Item -ItemType Directory -Path (Join-Path $runtimeApp 'electron') | Out-Null
Copy-Item -LiteralPath (Join-Path $appDirectory 'electron/preload.cjs') -Destination (Join-Path $runtimeApp 'electron')
Copy-Item -LiteralPath (Join-Path $appDirectory 'package.json') -Destination $runtimeApp
Copy-Item -LiteralPath $workerOutput -Destination (Join-Path $clientRoot 'resources/worker') -Recurse
if ($BiddingFrameworkDirectory) {
    $frameworkRoot = [IO.Path]::GetFullPath($BiddingFrameworkDirectory)
    foreach ($required in @('EngiSpace-Bidding.exe', 'resources/app.asar', 'resources/openxml-tools/win32-x64/openxmlhelper.exe', 'resources/agent-tools/win32-x64/bin/rg.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $frameworkRoot $required))) { throw "投标模块缺少 $required" }
    }
    if (!$BiddingSourceArchive -or !(Test-Path -LiteralPath $BiddingSourceArchive)) { throw '投标移植候选需要同时提供对应源码ZIP。' }
    # 原框架有大量小文件；多线程复制减少便携包组装时间，不使用删除/镜像选项。
    & robocopy.exe $frameworkRoot (Join-Path $clientRoot 'resources/bidding-framework') /E /MT:16 /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "投标模块复制失败，robocopy退出码$LASTEXITCODE" }
    $frameworkNotice = Join-Path $clientRoot 'resources/bidding-framework/third-party'
    New-Item -ItemType Directory -Path $frameworkNotice -Force | Out-Null
    foreach ($file in @('LICENSE', 'NOTICE', 'UPSTREAM.json')) { Copy-Item -LiteralPath (Join-Path $projectRoot "desktop/bidding-framework/$file") -Destination $frameworkNotice }
    foreach ($file in @('NotoSansSC-OFL.txt', 'Open-XML-SDK-LICENSE.txt')) { Copy-Item -LiteralPath (Join-Path $projectRoot "third-party/$file") -Destination $frameworkNotice }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'desktop/bidding-framework/client/vendor/agent-tools/win32-x64/jq-COPYING') -Destination $frameworkNotice
    $helperAssets = Get-Content (Join-Path $projectRoot 'desktop/bidding-framework/openxmlhelper/src/OpenXmlHelper/obj/project.assets.json') -Raw | ConvertFrom-Json
    $helperNuget = $helperAssets.packageFolders.psobject.Properties.Name | Select-Object -First 1
    Copy-Item -LiteralPath (Join-Path $helperNuget 'htmltoopenxml.dll/3.5.0/LICENSE') -Destination (Join-Path $frameworkNotice 'HtmlToOpenXml-LICENSE.txt')
    Copy-Item -LiteralPath $BiddingSourceArchive -Destination (Join-Path $outputRoot '对应源码.zip')
}
$noticeDirectory = Join-Path $clientRoot 'third-party'
New-Item -ItemType Directory -Path $noticeDirectory | Out-Null
foreach ($file in @('NotoSansSC-OFL.txt', 'Open-XML-SDK-LICENSE.txt', 'Newtonsoft.Json-LICENSE.md', 'PdfPig-LICENSE.txt', 'PdfPig-ATTRIBUTION.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot "third-party/$file") -Destination $noticeDirectory }
foreach ($dependency in @('react', 'react-dom', 'lucide-react')) { Copy-Item -LiteralPath (Join-Path $appDirectory "node_modules/$dependency/LICENSE") -Destination (Join-Path $noticeDirectory "$dependency-LICENSE.txt") }
$assets = Get-Content (Join-Path $workerDirectory 'obj/project.assets.json') -Raw | ConvertFrom-Json
$runtimeDependency = $assets.project.frameworks.'net10.0'.downloadDependencies | Where-Object { $_.name -eq 'Microsoft.NETCore.App.Runtime.win-x64' } | Select-Object -First 1
$runtimeVersion = $runtimeDependency.version.Trim('[', ']').Split(',')[0].Trim()
$nugetRoot = $assets.packageFolders.psobject.Properties.Name | Select-Object -First 1
Copy-Item -LiteralPath (Join-Path $nugetRoot "microsoft.netcore.app.runtime.win-x64/$runtimeVersion/LICENSE.TXT") -Destination (Join-Path $noticeDirectory 'dotnet-runtime-LICENSE.txt')
$privateContent = [bool]$ContentLibraryDirectory
if ($privateContent) {
    $libraryRoot = [IO.Path]::GetFullPath($ContentLibraryDirectory)
    foreach ($required in @('catalog.json', 'source-layouts.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $libraryRoot $required))) { throw "资料库缺少 $required" }
    }
    $libraryTarget = Join-Path $clientRoot 'resources/content-library'
    New-Item -ItemType Directory -Path $libraryTarget | Out-Null
    foreach ($file in @('catalog.json', 'source-layouts.json')) { Copy-Item -LiteralPath (Join-Path $libraryRoot $file) -Destination $libraryTarget }
    '本包含本机旧工程候选资料，仅供本机试用，不上传公开仓库或公开发行。' | Set-Content -LiteralPath (Join-Path $outputRoot '本机资料版-请勿公开.txt') -Encoding utf8
}
'engispace-design-note-client-v1' | Set-Content -LiteralPath (Join-Path $clientRoot '.engispace-client') -Encoding ascii
$version | Set-Content -LiteralPath (Join-Path $clientRoot 'VERSION.txt') -Encoding ascii
if ($Portable) {
    'portable-v1' | Set-Content -LiteralPath (Join-Path $clientRoot 'resources/.engispace-portable') -Encoding ascii
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/BIDDING_CLIENT_GUIDE.md') -Destination (Join-Path $outputRoot '使用说明.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/BIDDING_LEGACY_CLIENT_GUIDE.md') -Destination $outputRoot
} else {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/DESKTOP_CLIENT_GUIDE.md') -Destination (Join-Path $outputRoot '使用说明.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'desktop/setup/bin/Release/net48/DesktopSetup.exe') -Destination $outputRoot
}
$manifest = Get-ChildItem -LiteralPath $clientRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($clientRoot, $_.FullName).Replace('\', '/')
    "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative"
}
$manifest | Set-Content -LiteralPath (Join-Path $outputRoot 'manifest.sha256') -Encoding ascii
$dirty = [bool](& git -C $projectRoot status --porcelain --untracked-files=no)
@{ version = $version; revision = $revision; candidate = $true; productionReady = $false; builtFromDirtyTree = $dirty; portable = [bool]$Portable; privateContent = $privateContent; biddingFramework = [bool]$BiddingFrameworkDirectory; builtAt = (Get-Date).ToString('o'); files = $manifest.Count } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'BUILD.json') -Encoding utf8
if (!$Portable) {
    $verification = Start-Process -FilePath (Join-Path $outputRoot 'DesktopSetup.exe') -ArgumentList '--verify-payload' -WindowStyle Hidden -Wait -PassThru
    if ($verification.ExitCode -ne 0) { throw '安装包完整性检查失败。' }
}
$zipPath = $outputRoot + '.zip'
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $outputRoot | Select-Object -ExpandProperty FullName) -DestinationPath $zipPath -CompressionLevel Optimal
Write-Output "DESKTOP_PACKAGE_OK files=$($manifest.Count) privateContent=$privateContent"
if (!$Portable) { Write-Output "安装入口：$(Join-Path $outputRoot 'DesktopSetup.exe')" }
Write-Output "免安装入口：$(Join-Path $clientRoot 'EngiSpace.exe')"
Write-Output "ZIP：$zipPath"
