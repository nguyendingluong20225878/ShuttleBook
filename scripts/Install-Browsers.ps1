. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
$playwright = Join-Path $projectRoot 'node_modules/.bin/playwright.cmd'
if (-not (Test-Path -LiteralPath $playwright)) {
    throw 'Playwright is not installed. Run npm.cmd ci first.'
}
Push-Location $projectRoot
try {
    & $playwright install chromium
    $commandExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $commandExit
