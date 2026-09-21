[CmdletBinding()]
param(
    [ValidateSet('Core','All','Package')][string]$Target = 'Core',
    [string]$AutoCadDir,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$UpdateLocks
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $testProject = 'tests/Justified.SpecificationReflow.AutoCAD.Core.Tests/Justified.SpecificationReflow.AutoCAD.Core.Tests.csproj'
    $solution = 'JUSTIFIED_specification-reflow-for-AutoCAD.sln'
    $restoreTarget = $testProject
    $properties = @()
    if ($Target -ne 'Core') {
        if (-not $AutoCadDir) {
            $AutoCadDir = Get-ChildItem 'HKLM:\SOFTWARE\Autodesk\AutoCAD\R24.0' -ErrorAction SilentlyContinue |
                Get-ItemProperty | Where-Object { $_.PSObject.Properties.Name -contains 'AcadLocation' } |
                Select-Object -First 1 -ExpandProperty AcadLocation
        }
        if (-not $AutoCadDir) { throw 'AutoCAD 2021 not found. Pass -AutoCadDir.' }
        foreach ($name in @('acmgd.dll','acdbmgd.dll','accoremgd.dll')) {
            $dll = Join-Path $AutoCadDir $name
            if (-not (Test-Path -LiteralPath $dll)) { throw "Missing $dll" }
            $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll)
            if ($version.FileMajorPart -ne 24 -or $version.FileMinorPart -ne 0) { throw "Not AutoCAD 2021: $dll" }
        }
        $properties = @("-p:AutoCadDir=$AutoCadDir")
        $restoreTarget = $solution
    }
    $lockArgs = @('--locked-mode')
    if ($UpdateLocks) { $lockArgs = @('--force-evaluate') }
    & dotnet restore $restoreTarget @lockArgs @properties --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    & dotnet build $restoreTarget --no-restore -c $Configuration @properties --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & dotnet test $testProject --no-build --no-restore -c $Configuration --logger 'trx' --results-directory artifacts/test-results --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    if ($Target -eq 'Package') {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $output = Join-Path $taskRoot "artifacts/packages/$stamp"
        $bundle = Join-Path $output 'JUSTIFIED_specification-reflow-for-AutoCAD.bundle'
        $contents = Join-Path $bundle 'Contents/Windows'
        New-Item -ItemType Directory -Path $contents -Force | Out-Null
        Copy-Item -LiteralPath 'packaging/JUSTIFIED_specification-reflow-for-AutoCAD.bundle/PackageContents.xml' -Destination $bundle
        $binaryDir = "src/Justified.SpecificationReflow.AutoCAD.PluginHost/bin/$Configuration/net48"
        Get-ChildItem -LiteralPath $binaryDir -Filter *.dll | ForEach-Object {
            if ($_.Name -match '^(AcMgd|AcDbMgd|AcCoreMgd)\.dll$') { throw 'Autodesk DLL must not be packaged.' }
            Copy-Item -LiteralPath $_.FullName -Destination $contents
        }
        Copy-Item -LiteralPath 'docs/INSTALL.md','docs/THIRD_PARTY.md' -Destination $bundle
        Copy-Item -LiteralPath 'third-party' -Destination $bundle -Recurse
        $hashes = Get-ChildItem -LiteralPath $bundle -Recurse -File | Get-FileHash -Algorithm SHA256 |
            Select-Object @{n='File';e={$_.Path.Substring($bundle.Length + 1)}},Hash
        $hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'SHA256.json') -Encoding UTF8
        $zip = Join-Path $output 'JUSTIFIED_specification-reflow-for-AutoCAD-0.1.0-m0.zip'
        Compress-Archive -LiteralPath $bundle -DestinationPath $zip
        Write-Host "M0 diagnostic package: $zip"
    }
}
finally { Pop-Location }
