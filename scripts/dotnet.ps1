param([Parameter(ValueFromRemainingArguments = $true)][string[]]$DotnetArguments)
. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
Push-Location $projectRoot
try {
    & $dotnetExecutable @DotnetArguments
    $commandExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $commandExit
