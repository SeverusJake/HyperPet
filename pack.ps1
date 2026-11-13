param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$PackDirectory,
    [string]$OutputDirectory,
    # Optional HyperPet-X.Y.Z-full.nupkg of the previous release. vpk builds a
    # delta package only when the previous full package is in its output.
    [string]$PreviousPackage
)
$ErrorActionPreference = 'Stop'
if (-not $PackDirectory) { $PackDirectory = Join-Path $PSScriptRoot "publish/velopack/$Version" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot ('publish/packed/' + $Version + '-' + [guid]::NewGuid().ToString('N')) }
$PackDirectory = [IO.Path]::GetFullPath($PackDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $PackDirectory 'HyperPet.exe'))) { throw 'Publish directory does not contain HyperPet.exe.' }
if ((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory -Force | Select-Object -First 1)) { throw 'Pack output must be empty; refusing to mix old release assets.' }
$exeVersion = (Get-Item -LiteralPath (Join-Path $PackDirectory 'HyperPet.exe')).VersionInfo.ProductVersion -split '\+'
if ($exeVersion[0] -ne $Version) { throw "Published binary version does not match $Version." }
$seededPackage = $null
if ($PreviousPackage) {
    if (-not (Test-Path -LiteralPath $PreviousPackage -PathType Leaf)) { throw 'Previous package does not exist.' }
    $previousName = [IO.Path]::GetFileName($PreviousPackage)
    if ($previousName -notmatch '^HyperPet-(\d+\.\d+\.\d+)-full\.nupkg$') { throw 'Previous package must be named HyperPet-X.Y.Z-full.nupkg.' }
    if ([version]$Matches[1] -ge [version]$Version) { throw "Previous package version must be lower than $Version." }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $seededPackage = Join-Path $OutputDirectory $previousName
    Copy-Item -LiteralPath $PreviousPackage -Destination $seededPackage
}
Push-Location $PSScriptRoot
try {
    dotnet tool run vpk -- pack --packId HyperPet --packVersion $Version --packDir $PackDirectory --mainExe HyperPet.exe --icon (Join-Path $PSScriptRoot 'src/HyperPet.App/Assets/HyperPet.ico') --outputDir $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed.' }
    if ($seededPackage) {
        if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory "HyperPet-$Version-delta.nupkg"))) { throw 'vpk did not produce a delta package from the previous release.' }
        # The previous package is an input, already published with its own release.
        Remove-Item -LiteralPath $seededPackage
    }
    foreach ($artifact in @('Setup.exe', 'Portable.zip')) {
        $source = Join-Path $OutputDirectory "HyperPet-win-$artifact"
        if (-not (Test-Path -LiteralPath $source)) { throw "Missing artifact: $source" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $OutputDirectory "HyperPet-v$Version-win-$artifact")
    }
} finally { Pop-Location }
