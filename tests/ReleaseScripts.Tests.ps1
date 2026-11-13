# Dependency-free regression checks; exits nonzero on the first failed assertion.
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../scripts/ReleaseTools.psm1') -Force
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('HyperPet.ReleaseTest-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $sandbox | Out-Null
try {
    git -C $sandbox init --quiet
    Set-Content (Join-Path $sandbox 'tracked.txt') 'original'
    git -C $sandbox add tracked.txt
    git -C $sandbox -c user.name=Tests -c user.email=tests@example.invalid commit --quiet -m baseline
    $commit = Assert-CleanReleaseSource -Repository $sandbox
    Assert-True ($commit -match '^[0-9a-f]{40}$') 'Clean source must resolve a commit.'
    Set-Content (Join-Path $sandbox 'tracked.txt') 'changed'
    $message = ''
    try { Assert-CleanReleaseSource -Repository $sandbox } catch { $message = $_.Exception.Message }
    Assert-True ($message -match 'dirty') 'Dirty source must be rejected before building.'
    Write-Host 'PASS clean/dirty source guard'

    $artifact = Join-Path $sandbox 'file.zip'; Set-Content $artifact 'payload'
    Write-ReleaseManifest -Directory $sandbox -Version '1.2.3' -CommitSha 'abc123' -Artifacts @($artifact)
    $manifest = Get-Content (Join-Path $sandbox 'build-manifest.json') -Raw | ConvertFrom-Json
    Assert-True ($manifest.CommitSha -eq 'abc123') 'Manifest must identify source.'
    Assert-True (@($manifest.Artifacts).Count -eq 1) 'Manifest must contain exactly supplied assets.'
    Assert-True ($manifest.Artifacts[0].SHA256 -eq (Get-FileHash $artifact -Algorithm SHA256).Hash) 'Artifact checksum must match payload.'
    $checksums = Get-Content (Join-Path $sandbox 'SHA256SUMS.txt') -Raw
    Assert-True ($checksums -match 'file.zip' -and $checksums -match 'build-manifest.json') 'Checksums must cover payload and manifest.'
    Write-Host 'PASS checksum manifest'

    $message = ''
    try { Invoke-ReleaseCommand -Command $env:ComSpec -Arguments @('/c', 'exit', '7') } catch { $message = $_.Exception.Message }
    Assert-True ($message -match 'exit 7') 'A failing native command must stop release.'
    Write-Host 'PASS native exit-code guard'
} finally {
    # Only this explicitly created, random temporary folder is removed.
    if ((Split-Path $sandbox -Parent) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath())) { throw 'Unsafe test cleanup target.' }
    Remove-Item -LiteralPath $sandbox -Recurse -Force
}
Write-Host '3 PowerShell regression groups passed.'
# Expected native failures leave LASTEXITCODE nonzero. Report success only
# after every assertion and sandbox cleanup has completed.
exit 0
