# pack.ps1 — build the Velopack release (Setup.exe + nupkg) for a given version.
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

Write-Host "Packed HyperPet $Version. Output in Releases/." -ForegroundColor Green
