# pack.ps1 — build the Velopack release and versioned Setup/Portable files.
# Usage: ./pack.ps1 -Version 0.5.4
param(
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = 'Stop'

$packDir = "publish/velopack/$Version"
if (-not (Test-Path $packDir)) {
    throw "Pack dir not found: $packDir. Publish the app to that folder first."
}

vpk pack `
    --packId HyperPet `
    --packVersion $Version `
    --packDir $packDir `
    --mainExe HyperPet.exe `
    --icon "src/HyperPet.App/Assets/HyperPet.ico"

if ($LASTEXITCODE -ne 0) { throw "vpk pack failed." }

foreach ($artifact in @('Setup.exe', 'Portable.zip')) {
    $source = "Releases/HyperPet-win-$artifact"
    $destination = "Releases/HyperPet-v$Version-win-$artifact"
    if (-not (Test-Path $source)) { throw "Release artifact not found: $source" }
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

Write-Host "Packed HyperPet $Version. Output in Releases/." -ForegroundColor Green
