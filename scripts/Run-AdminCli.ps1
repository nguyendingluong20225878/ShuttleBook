param([Parameter(Mandatory = $true)][ValidateSet('bootstrap', 'rotate', 'suspend', 'activate', 'revoke-sessions')][string]$Operation)
. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
Push-Location $projectRoot
try {
    & $dotnetExecutable run --project backend/src/ShuttleBook.AdminCli --no-launch-profile -- $Operation
    $commandExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $commandExit
