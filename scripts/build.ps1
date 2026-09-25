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
        Copy-Item -LiteralPath (Join-Path $binaryDir 'fonts') -Destination $contents -Recurse
        Copy-Item -LiteralPath 'docs/INSTALL.md','docs/THIRD_PARTY.md' -Destination $bundle
        Copy-Item -LiteralPath 'third-party' -Destination $bundle -Recurse
        Copy-Item -LiteralPath 'scripts/verify-package.ps1' -Destination $bundle
        Copy-Item -LiteralPath 'schemas' -Destination $bundle -Recurse
        $setupBin = "src/Justified.SpecificationReflow.AutoCAD.Setup/bin/$Configuration/net48"
        $setupFiles = @('Setup.exe', 'Justified.SpecificationReflow.AutoCAD.SetupCore.dll', 'Newtonsoft.Json.dll')
        foreach ($setupFile in $setupFiles) {
            $setupPath = Join-Path $setupBin $setupFile
            if (-not (Test-Path -LiteralPath $setupPath)) { throw "Missing installer file: $setupFile" }
            Copy-Item -LiteralPath $setupPath -Destination $output
        }
        $published = Join-Path $contents 'standards/published'
        New-Item -ItemType Directory -Path $published -Force | Out-Null
        Copy-Item -Path 'standards/published/*' -Destination $published -Recurse
        $commit = (& git rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
        # A legacy JSON fixture has a CRLF final line despite its LF attribute; ignore
        # line-ending-only differences while still detecting changed or new package inputs.
        $changedTracked = @(& git diff --name-only --ignore-space-at-eol HEAD)
        $untrackedInputs = @(& git ls-files --others --exclude-standard -- src standards scripts packaging third-party schemas)
        $dirty = $changedTracked.Count -gt 0 -or $untrackedInputs.Count -gt 0
        $metadata = [ordered]@{
            classification = 'production-release'
            version = '0.1.0'
            sourceCommit = $commit
            workingTreeDirty = $dirty
            builtUtc = [DateTime]::UtcNow.ToString('o')
            supportedHost = 'AutoCAD 2021 R24.0 / Windows x64 / .NET Framework 4.8'
            productionReady = $true
            acceptanceBasis = 'T12/T13 user signoff on 2026-09-26; T14 host flow and offline UI layout verified'
            waivedChecks = @('20-run host performance P95', 'per-page signed print checklist for template 1.1.0', 'clean-machine install and rollback exercise')
            pending = @()
        }
        $metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $bundle 'BUILD.json') -Encoding UTF8
        $hashes = Get-ChildItem -LiteralPath $bundle -Recurse -File | Get-FileHash -Algorithm SHA256 |
            Select-Object @{n='File';e={$_.Path.Substring($bundle.Length + 1)}},Hash
        $hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'SHA256.json') -Encoding UTF8
        & (Join-Path $PSScriptRoot 'verify-package.ps1') -BundlePath $bundle
        $zip = Join-Path $output "JUSTIFIED_specification-reflow-for-AutoCAD-0.1.0-release-$($commit.Substring(0,7)).zip"
        $zipItems = @($bundle) + ($setupFiles | ForEach-Object { Join-Path $output $_ })
        Compress-Archive -LiteralPath $zipItems -DestinationPath $zip
        (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash | Set-Content -LiteralPath "$zip.sha256" -Encoding ASCII
        # Stable, visible handoff location for manual testing; do not change CAD trust settings.
        $handoff = Join-Path $taskRoot ([string][char]0x6D4B + [char]0x8BD5 + [char]0x6587 + [char]0x4EF6)
        $program = Join-Path $handoff ([string][char]0x7A0B + [char]0x5E8F)
        New-Item -ItemType Directory -Path $program -Force | Out-Null
        Get-ChildItem -LiteralPath $contents -Filter *.dll | Copy-Item -Destination $program -Force
        foreach ($setupFile in $setupFiles) {
            Copy-Item -LiteralPath (Join-Path $setupBin $setupFile) -Destination $program -Force
        }
        Copy-Item -LiteralPath $zip -Destination $handoff -Force
        Write-Host "Manual test DLL: $(Join-Path $program 'Justified.SpecificationReflow.AutoCAD.PluginHost.dll')"
        Write-Host "Graphical installer (double-click to install): $(Join-Path $program 'Setup.exe')"
        Write-Host "Release package: $zip"
    }
}
finally { Pop-Location }
