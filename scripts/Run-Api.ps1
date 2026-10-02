. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
if (-not $env:ASPNETCORE_ENVIRONMENT -or -not $env:ASPNETCORE_URLS) {
    throw 'Set ASPNETCORE_ENVIRONMENT and ASPNETCORE_URLS in .env.'
}
Push-Location $projectRoot
try {
    & $dotnetExecutable run --project backend/src/ShuttleBook.Api --no-launch-profile
    $commandExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $commandExit
