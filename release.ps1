# release.ps1 — full HyperPet release pipeline.
#
# Bumps the version, publishes, packs (Setup + nupkgs + portable via pack.ps1),
# commits + tags + pushes, then creates the GitHub release with EVERY asset
# (Setup.exe, full + delta nupkg, releases.win.json, and the portable zip —
# renamed to a versioned name so it never gets dropped again).
#
# Usage:
#   ./release.ps1 -Version 0.7.0
#   ./release.ps1 -Version 0.7.0 -NotesFile Releases/notes-0.7.0.md
#   ./release.ps1 -Version 0.7.0 -SkipGit      # build + pack + release, no commit/tag
#   ./release.ps1 -Version 0.7.0 -DryRun       # build + pack only, no git, no gh

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$NotesFile,
    [switch]$SkipGit,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be X.Y.Z (got '$Version')."
}

$csproj   = 'src/HyperPet.App/HyperPet.App.csproj'
$publishD = "publish/velopack/$Version"
$tag      = "v$Version"

Write-Host "=== Releasing HyperPet $Version ===" -ForegroundColor Cyan

# --- 1. Bump version in the csproj (all four fields) ---
$xml = Get-Content $csproj -Raw
$xml = $xml -replace '<Version>[\d.]+</Version>', "<Version>$Version</Version>"
$xml = $xml -replace '<AssemblyVersion>[\d.]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$xml = $xml -replace '<FileVersion>[\d.]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
$xml = $xml -replace '<InformationalVersion>[\d.]+</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>"
Set-Content $csproj -Value $xml -Encoding utf8
Write-Host "Bumped $csproj to $Version." -ForegroundColor Green

# --- 2. Publish (self-contained win-x64, plain folder) ---
dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishD -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# --- 3. Pack (Setup + nupkgs + portable, with Setup icon) ---
& "$PSScriptRoot/pack.ps1" -Version $Version

# --- 4. Version the portable zip so it isn't an unversioned generic name ---
$portableSrc = 'Releases/HyperPet-win-Portable.zip'
$portableDst = "Releases/HyperPet-v$Version-win-Portable.zip"
if (-not (Test-Path $portableSrc)) { throw "Portable zip not found: $portableSrc" }
Copy-Item $portableSrc $portableDst -Force
Write-Host "Portable -> $portableDst" -ForegroundColor Green

# --- 5. Collect the release assets (delta only exists when a prior version did) ---
$assets = @(
    'Releases/HyperPet-win-Setup.exe',
    "Releases/HyperPet-$Version-full.nupkg",
    'Releases/releases.win.json',
    $portableDst
)
$delta = "Releases/HyperPet-$Version-delta.nupkg"
if (Test-Path $delta) { $assets += $delta }

foreach ($a in $assets) {
    if (-not (Test-Path $a)) { throw "Missing release asset: $a" }
}
Write-Host "Assets:" -ForegroundColor Cyan
$assets | ForEach-Object { Write-Host "  $_" }

if ($DryRun) {
    Write-Host "DryRun: stopping before git + gh." -ForegroundColor Yellow
    return
}

# --- 6. Commit version bump + tag + push ---
if (-not $SkipGit) {
    git add $csproj
    git commit -m "release: HyperPet $Version"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }
    git push origin main
    if ($LASTEXITCODE -ne 0) { throw "git push failed." }
    git tag -a $tag -m "HyperPet $Version"
    git push origin $tag
    if ($LASTEXITCODE -ne 0) { throw "git push tag failed." }
}

# --- 7. Create the GitHub release with ALL assets ---
$notesArgs = @()
if ($NotesFile -and (Test-Path $NotesFile)) {
    $notesArgs = @('--notes-file', $NotesFile)
} else {
    $notesArgs = @('--generate-notes')
}

gh release create $tag @assets --title "HyperPet $Version" @notesArgs
if ($LASTEXITCODE -ne 0) { throw "gh release create failed." }

Write-Host "=== Released HyperPet $Version ===" -ForegroundColor Green
gh release view $tag --json assets --jq '.assets[].name'
