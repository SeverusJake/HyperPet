param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$PackDirectory,
    [string]$OutputDirectory
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
Push-Location $PSScriptRoot
try {
    dotnet tool run vpk -- pack --packId HyperPet --packVersion $Version --packDir $PackDirectory --mainExe HyperPet.exe --icon (Join-Path $PSScriptRoot 'src/HyperPet.App/Assets/HyperPet.ico') --outputDir $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed.' }
    foreach ($artifact in @('Setup.exe', 'Portable.zip')) {
        $source = Join-Path $OutputDirectory "HyperPet-win-$artifact"
        if (-not (Test-Path -LiteralPath $source)) { throw "Missing artifact: $source" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $OutputDirectory "HyperPet-v$Version-win-$artifact")
    }
} finally { Pop-Location }
