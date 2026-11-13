function Invoke-ReleaseCommand {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed (exit $LASTEXITCODE)." }
}
function Assert-CleanReleaseSource {
    param([Parameter(Mandatory)][string]$Repository)
    $status = & git -C $Repository status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect release source.' }
    if ($status) { throw 'Release source is dirty. Commit or isolate the intended changes before building a release.' }
    $commit = & git -C $Repository rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve release commit.' }
    return $commit.Trim()
}
function Write-ReleaseManifest {
    param([string]$Directory, [string]$Version, [string]$CommitSha, [string[]]$Artifacts)
    $records = @($Artifacts | ForEach-Object {
        if (-not (Test-Path -LiteralPath $_ -PathType Leaf)) { throw "Missing artifact: $_" }
        [ordered]@{ Name = [IO.Path]::GetFileName($_); Size = (Get-Item -LiteralPath $_).Length; SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
    $manifestPath = Join-Path $Directory 'build-manifest.json'
    [ordered]@{ Version = $Version; CommitSha = $CommitSha; Artifacts = $records } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $lines = @($records | ForEach-Object { "$($_.SHA256.ToLowerInvariant())  $($_.Name)" })
    $lines += "$((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant())  build-manifest.json"
    $lines | Set-Content -LiteralPath (Join-Path $Directory 'SHA256SUMS.txt') -Encoding ascii
}
Export-ModuleMember -Function Invoke-ReleaseCommand, Assert-CleanReleaseSource, Write-ReleaseManifest
