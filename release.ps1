# Build from a clean, versioned commit. Remote publication is explicit.
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$NotesFile,
    [Alias('DryRun')][switch]$BuildOnly,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
if ($BuildOnly -and $Publish) { throw 'Choose BuildOnly or Publish, not both.' }
Import-Module (Join-Path $PSScriptRoot 'scripts/ReleaseTools.psm1') -Force
Push-Location $PSScriptRoot
try {
    $commit = Assert-CleanReleaseSource -Repository $PSScriptRoot
    $project = Join-Path $PSScriptRoot 'src/HyperPet.App/HyperPet.App.csproj'
    [xml]$xml = Get-Content -LiteralPath $project -Raw
    if ($xml.Project.PropertyGroup.Version -ne $Version) { throw 'Version must already match the committed project version. This script never edits source.' }
    $tag = "v$Version"
    if ($Publish) {
        $tagCommit = & git rev-parse "$tag^{}"
        if ($LASTEXITCODE -ne 0 -or $tagCommit.Trim() -ne $commit) { throw 'The existing tag must identify the exact release commit.' }
        $remote = @(& git ls-remote --tags origin "refs/tags/$tag" "refs/tags/$tag^{}")
        if ($LASTEXITCODE -ne 0 -or -not ($remote | Where-Object { ($_ -split '\s+')[0] -eq $commit })) { throw 'Remote release tag does not identify this commit.' }
    }
    Invoke-ReleaseCommand -Command dotnet -Arguments @('test', 'HyperPet.sln', '-c', 'Release')
    $auditText = (& dotnet list HyperPet.sln package --vulnerable --include-transitive --format json) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Dependency audit failed.' }
    $audit = $auditText | ConvertFrom-Json
    foreach ($framework in $audit.projects.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if ($package.vulnerabilities.Count -gt 0) { throw "Dependency advisory: $($package.id)" }
        }
    }
    Invoke-ReleaseCommand -Command dotnet -Arguments @('tool', 'restore')
    $output = Join-Path $PSScriptRoot ("publish/verified/$Version-$($commit.Substring(0,12))-" + [guid]::NewGuid().ToString('N'))
    $appDirectory = Join-Path $output 'app'
    $releaseDirectory = Join-Path $output 'releases'
    Invoke-ReleaseCommand -Command dotnet -Arguments @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $appDirectory, "-p:SourceRevisionId=$commit")
    & (Join-Path $PSScriptRoot 'pack.ps1') -Version $Version -PackDirectory $appDirectory -OutputDirectory $releaseDirectory
    $assets = @(
        (Join-Path $releaseDirectory "HyperPet-v$Version-win-Setup.exe"),
        (Join-Path $releaseDirectory "HyperPet-v$Version-win-Portable.zip"),
        (Join-Path $releaseDirectory 'releases.win.json')
    ) + @(Get-ChildItem -LiteralPath $releaseDirectory -Filter "HyperPet-$Version-*.nupkg" | ForEach-Object FullName)
    Write-ReleaseManifest -Directory $releaseDirectory -Version $Version -CommitSha $commit -Artifacts $assets
    if ($Publish) {
        if ((Assert-CleanReleaseSource -Repository $PSScriptRoot) -ne $commit) { throw 'Source changed while building.' }
        $assets += Join-Path $releaseDirectory 'build-manifest.json'
        $assets += Join-Path $releaseDirectory 'SHA256SUMS.txt'
        $notes = @('--generate-notes')
        if ($NotesFile) {
            if (-not (Test-Path -LiteralPath $NotesFile -PathType Leaf)) { throw 'Notes file does not exist.' }
            $notes = @('--notes-file', (Resolve-Path -LiteralPath $NotesFile).Path)
        }
        Invoke-ReleaseCommand -Command gh -Arguments (@('release', 'create', $tag, '--verify-tag', '--title', "HyperPet $Version") + $assets + $notes)
    }
    Write-Host "Verified artifacts: $releaseDirectory"
    if (-not $Publish) { Write-Host 'Build only: no commit, tag, push, or GitHub publication performed.' }
} finally { Pop-Location }
