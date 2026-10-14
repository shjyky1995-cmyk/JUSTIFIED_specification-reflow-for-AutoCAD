[CmdletBinding()]
param([string]$AutoCadDir, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    if (-not $SkipBuild) {
        & (Join-Path $PSScriptRoot 'build.ps1') -Target All -AutoCadDir $AutoCadDir
    }
    $version = '0.2.0-table-preview.2'
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
    $changes = @(& git diff --name-only --ignore-space-at-eol HEAD -- src schemas scripts standards packaging tests docs)
    $newInputs = @(& git ls-files --others --exclude-standard -- src schemas scripts standards packaging)
    if ($changes.Count -gt 0 -or $newInputs.Count -gt 0) { throw 'Commit candidate inputs before packaging.' }
    $output = Join-Path $taskRoot ('artifacts/t28-candidate/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $bundle = Join-Path $output 'JUSTIFIED_specification-reflow-for-AutoCAD.bundle'
    $contents = Join-Path $bundle 'Contents/Windows'
    New-Item -ItemType Directory -Path $contents -Force | Out-Null
    Copy-Item -LiteralPath 'packaging/JUSTIFIED_specification-reflow-for-AutoCAD.bundle/PackageContents.xml' -Destination $bundle
    [xml]$package = Get-Content -LiteralPath (Join-Path $bundle 'PackageContents.xml') -Raw
    $package.ApplicationPackage.AppVersion = '0.2.0'
    $package.Save((Join-Path $bundle 'PackageContents.xml'))
    $binaryDir = 'src/Justified.SpecificationReflow.AutoCAD.PluginHost/bin/Release/net48'
    Get-ChildItem -LiteralPath $binaryDir -Filter *.dll | ForEach-Object {
        if ($_.Name -match '^(AcMgd|AcDbMgd|AcCoreMgd)\.dll$') { throw 'SDK DLL must not be distributed.' }
        Copy-Item -LiteralPath $_.FullName -Destination $contents
    }
    Copy-Item -LiteralPath (Join-Path $binaryDir 'fonts') -Destination $contents -Recurse
    Copy-Item -LiteralPath 'docs/INSTALL.md','docs/THIRD_PARTY.md','docs/CAD_TABLES_ACCEPTANCE.md','docs/CAD_TABLES_AUTOMATED_CHECKS.md' -Destination $bundle
    Copy-Item -LiteralPath 'third-party','schemas' -Destination $bundle -Recurse
    Copy-Item -LiteralPath 'scripts/verify-package.ps1' -Destination $bundle
    $published = Join-Path $contents 'standards/published'
    New-Item -ItemType Directory -Path $published -Force | Out-Null
    Copy-Item -Path 'standards/candidates/t28/*' -Destination $published -Recurse
    $setupBin = 'src/Justified.SpecificationReflow.AutoCAD.Setup/bin/Release/net48'
    foreach ($name in @('Setup.exe','Justified.SpecificationReflow.AutoCAD.SetupCore.dll','Newtonsoft.Json.dll')) {
        Copy-Item -LiteralPath (Join-Path $setupBin $name) -Destination $output
    }
    $metadata = [ordered]@{
        classification = 'candidate'; version = $version; sourceCommit = $commit; workingTreeDirty = $false
        builtUtc = [DateTime]::UtcNow.ToString('o'); supportedHost = 'AutoCAD 2021 R24.0 / Windows x64 / .NET Framework 4.8'
        productionReady = $false
        acceptanceBasis = 'T28 190/190 per framework; safe-margin template 1.2.1; offscreen UI checks; recorded AutoCAD TEXTBOX geometry and prepared-DXF plots. Full DSS host checks pending.'
        pending = @('CAD 内真实 DPI 与窗口核对','A1/A2/A3 的 DSS 导入及一次撤销','实际工程图框与打印核对')
    }
    $metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $bundle 'BUILD.json') -Encoding UTF8
    Get-ChildItem -LiteralPath $bundle -Recurse -File | Get-FileHash -Algorithm SHA256 |
        Select-Object @{n='File';e={$_.Path.Substring($bundle.Length + 1)}},Hash |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'SHA256.json') -Encoding UTF8
    & (Join-Path $PSScriptRoot 'verify-package.ps1') -BundlePath $bundle
    $zip = Join-Path $output "CAD-tables-$version-$($commit.Substring(0,7)).zip"
    $items = @($bundle) + @('Setup.exe','Justified.SpecificationReflow.AutoCAD.SetupCore.dll','Newtonsoft.Json.dll' | ForEach-Object { Join-Path $output $_ })
    Compress-Archive -LiteralPath $items -DestinationPath $zip
    (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash | Set-Content -LiteralPath "$zip.sha256" -Encoding ASCII
    # 只新增候选交付目录，不替换已发布包或当前 CAD 安装。
    $handoff = Join-Path $taskRoot '测试文件/CAD表格试用'
    New-Item -ItemType Directory -Path $handoff -Force | Out-Null
    Copy-Item -LiteralPath $zip -Destination $handoff
    Copy-Item -LiteralPath 'docs/CAD_TABLES_ACCEPTANCE.md' -Destination $handoff
    Write-Output "TABLE_CANDIDATE_OK $zip"
}
finally { Pop-Location }
