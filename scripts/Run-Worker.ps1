. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
Push-Location $projectRoot
try {
    & $dotnetExecutable run --project backend/src/ShuttleBook.Worker --no-launch-profile
    $commandExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $commandExit
