. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
Push-Location $projectRoot
try {
    & npm.cmd run build --workspaces --if-present
    $commandExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $commandExit
