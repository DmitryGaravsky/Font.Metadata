#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Executable,
    [string[]] $Prefix = @(),
    [Parameter(Mandatory)][string] $WorkDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null

function Invoke-ActionsShell([string] $CaseDirectory, [hashtable] $Settings) {
    New-Item -ItemType Directory -Path $CaseDirectory -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $CaseDirectory 'settings.json'), ($Settings | ConvertTo-Json -Depth 5))
    $wrapper = Join-Path $CaseDirectory 'actions-step.ps1'
    # Match the runner's preamble, dot-sourced -Command invocation and exit-code footer.
    # https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#exit-codes-and-error-action-preference
    [IO.File]::WriteAllText($wrapper, @'
$ErrorActionPreference = 'Stop'
$settings = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'settings.json') -Raw | ConvertFrom-Json -AsHashtable
& $settings.Script -Executable $settings.Executable -Prefix $settings.Prefix -WorkDirectory $settings.WorkDirectory
Write-Host 'Verification script returned to its caller.'
if(Test-Path -LiteralPath variable:\LASTEXITCODE) { exit $LASTEXITCODE }
'@)
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('-NoProfile')
    $start.ArgumentList.Add('-NonInteractive')
    $start.ArgumentList.Add('-Command')
    $start.ArgumentList.Add(". '" + $wrapper.Replace("'", "''") + "'")
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        [void] $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(60000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw 'Actions-shell verification exceeded 60 seconds.'
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $CaseDirectory 'stdout.txt'), $output)
        [IO.File]::WriteAllText((Join-Path $CaseDirectory 'stderr.txt'), $errorOutput)
        return @{ ExitCode = $process.ExitCode; Output = $output; Error = $errorOutput }
    }
    finally {
        $process.Dispose()
    }
}

$verification = Join-Path $PSScriptRoot 'verify-cli.ps1'
$success = Invoke-ActionsShell (Join-Path $WorkDirectory 'success') @{
    Script = $verification
    Executable = $Executable
    Prefix = $Prefix
    WorkDirectory = (Join-Path $WorkDirectory 'success/input')
}
if($success.ExitCode -ne 0 -or $success.Error.Length -ne 0 -or
    $success.Output -notmatch 'CLI plain/JSON, schema, license evidence, partial and I/O checks passed.' -or
    $success.Output -notmatch 'Verification script returned to its caller.') {
    throw ("Successful CLI verification failed in the Actions shell (exit {0}). See {1}." -f $success.ExitCode, $WorkDirectory)
}

# A genuine unexpected native failure must still fail verification.
$nativeFailure = Invoke-ActionsShell (Join-Path $WorkDirectory 'native-failure') @{
    Script = $verification
    Executable = $Executable
    Prefix = @($Prefix) + @('--unknown')
    WorkDirectory = (Join-Path $WorkDirectory 'native-failure/input')
}
if($nativeFailure.ExitCode -eq 0 -or $nativeFailure.Error -notmatch 'Installed/portable CLI plain inspection failed.') {
    throw 'An unexpected CLI exit code did not fail the Actions shell as expected.'
}

# Fail after the expected native exit 3, during schema validation. Only copies are altered.
$fixture = Join-Path $WorkDirectory 'invalid-example/repository'
New-Item -ItemType Directory -Path (Join-Path $fixture 'eng'), (Join-Path $fixture 'docs/examples') -Force | Out-Null
Copy-Item -LiteralPath $verification -Destination (Join-Path $fixture 'eng/verify-cli.ps1')
Copy-Item -LiteralPath (Join-Path $root 'docs/cli.schema.json') -Destination (Join-Path $fixture 'docs/cli.schema.json')
[IO.File]::WriteAllText((Join-Path $fixture 'docs/examples/invalid.json'), '{}')
$schemaFailure = Invoke-ActionsShell (Join-Path $WorkDirectory 'invalid-example') @{
    Script = (Join-Path $fixture 'eng/verify-cli.ps1')
    Executable = $Executable
    Prefix = $Prefix
    WorkDirectory = (Join-Path $WorkDirectory 'invalid-example/input')
}
if($schemaFailure.ExitCode -eq 0 -or $schemaFailure.Error -notmatch 'Test-Json') {
    throw 'A real schema validation failure did not fail the Actions shell as expected.'
}
Write-Host 'Actions-shell regression passed: success returns zero; native and schema failures remain nonzero.'
exit 0
