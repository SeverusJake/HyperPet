# Exercise the test entry point through a child process, including the Actions
# pwsh trailer. Direct invocation alone does not check the process exit status.
$ErrorActionPreference = 'Stop'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$processSandbox = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ('HyperPet.ReleaseProcess-' + [guid]::NewGuid().ToString('N'))))
if ((Split-Path $processSandbox -Parent) -ne $temporaryRoot.TrimEnd('\', '/')) { throw 'Unsafe process-test sandbox.' }
New-Item -ItemType Directory (Join-Path $processSandbox 'tests'), (Join-Path $processSandbox 'scripts') | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../scripts/ReleaseTools.psm1') -Destination (Join-Path $processSandbox 'scripts/ReleaseTools.psm1')
    $original = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ReleaseScripts.Tests.ps1') -Raw
    $cases = @(
        @{ Name = 'success'; Script = $original; ExpectedSuccess = $true },
        @{ Name = 'assertion failure'; Script = $original.Replace('if (-not $Condition) { throw $Message }', "throw 'Injected assertion failure'"); ExpectedSuccess = $false },
        @{ Name = 'cleanup failure'; Script = $original.Replace('Remove-Item -LiteralPath $sandbox -Recurse -Force', "throw 'Injected cleanup failure'"); ExpectedSuccess = $false }
    )
    foreach ($case in $cases) {
        if (-not $case.ExpectedSuccess -and $case.Script -eq $original) { throw 'Failure injection did not change the fixture.' }
        $entryPoint = Join-Path $processSandbox 'tests/ReleaseScripts.Tests.ps1'
        Set-Content -LiteralPath $entryPoint -Value $case.Script -Encoding utf8
        $quotedEntry = $entryPoint.Replace("'", "''")
        $quotedTemp = $processSandbox.Replace("'", "''")
        $command = @"
`$ErrorActionPreference = 'Stop'
`$env:TEMP = '$quotedTemp'
`$env:TMP = '$quotedTemp'
& '$quotedEntry'
if (Test-Path variable:\LASTEXITCODE) { exit `$LASTEXITCODE }
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        # Windows PowerShell 5.1 turns redirected native stderr into a terminating
        # error under 'Stop'; the expected-failure cases write to stderr.
        $ErrorActionPreference = 'Continue'
        try {
            $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -EncodedCommand $encoded 2>&1
            $processExit = $LASTEXITCODE
        } finally { $ErrorActionPreference = 'Stop' }
        if ($case.ExpectedSuccess) {
            if ($processExit -ne 0) { throw "Successful suite returned process exit $processExit under the Actions wrapper." }
        } elseif ($processExit -eq 0) { throw "$($case.Name) was incorrectly reported as success." }
        Write-Host "PASS process contract: $($case.Name)"
    }
} finally {
    Remove-Item -LiteralPath $processSandbox -Recurse -Force
}
Write-Host '3 process-exit regression cases passed.'
exit 0
