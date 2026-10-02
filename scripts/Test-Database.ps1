. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
if (-not $env:SHUTTLEBOOK_TEST_CONNECTION_STRING) {
    if (-not $localConfig) { throw 'Initialize local .env or set SHUTTLEBOOK_TEST_CONNECTION_STRING for a disposable local test server.' }
    $env:SHUTTLEBOOK_TEST_CONNECTION_STRING = "Host=localhost;Port=$databasePort;Database=postgres;Username=$($localConfig.POSTGRES_USER);Password=$($localConfig.POSTGRES_PASSWORD);Timeout=3;Command Timeout=5"
}
Write-Host 'Database integration tests require a disposable local PostgreSQL/PostGIS server with CREATE DATABASE permission.'
Push-Location $projectRoot
try {
    & $dotnetExecutable test backend/tests/ShuttleBook.Database.Tests --no-restore
    $commandExit = $LASTEXITCODE
} finally { Pop-Location }
exit $commandExit
