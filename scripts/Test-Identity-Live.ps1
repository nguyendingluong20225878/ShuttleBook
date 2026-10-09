param([ValidateRange(1024, 65535)][int]$ApiPort = 5080,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
$ErrorActionPreference = 'Stop'
$adminContact = "admin-live-$([guid]::NewGuid().ToString('N'))@example.test"
$adminPassword = "Admin-$([guid]::NewGuid().ToString('N'))!Aa1"
$databaseName = "shuttlebook_f014_live_$([guid]::NewGuid().ToString('N'))"
if (-not $localConfig) { throw 'A local .env is required for disposable identity tests.' }
$env:ConnectionStrings__ShuttleBook = "Host=127.0.0.1;Port=$databasePort;Database=$databaseName;Username=$($localConfig.POSTGRES_USER);Password=$($localConfig.POSTGRES_PASSWORD);Timeout=15;Command Timeout=30"
$targetDatabase = [regex]::Match($env:ConnectionStrings__ShuttleBook, '(?i)(?:^|;)Database=([^;]+)').Groups[1].Value
if ($targetDatabase -cne $databaseName -or $databaseName -notmatch '^shuttlebook_f014_live_[0-9a-f]{32}$') {
    throw 'Temporary database target validation failed.'
}
$env:SHUTTLEBOOK_ADMIN_TEST_CONTACT = $adminContact
$env:SHUTTLEBOOK_ADMIN_TEST_PASSWORD = $adminPassword
$env:SHUTTLEBOOK_ADMIN_E2E_REAL = '1'
$env:SHUTTLEBOOK_E2E_REAL = '1'
$originalWebApiBase = $env:VITE_API_BASE_URL
$testApiBase = "http://localhost:$ApiPort"
$env:VITE_API_BASE_URL = $testApiBase
$env:ASPNETCORE_URLS = $testApiBase
$env:SHUTTLEBOOK_TEST_API_URL = $testApiBase
$env:Logging__EventLog__LogLevel__Default = 'None'
$localDir = Join-Path $projectRoot '.local'
[IO.Directory]::CreateDirectory($localDir) | Out-Null
$env:DataProtection__KeysPath = Join-Path $localDir ('identity-live-keys-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($env:DataProtection__KeysPath) | Out-Null
$apiProcess = $null
$workerProcess = $null
$databaseCreated = $false
$testExit = 1

function Assert-PortFree([int]$port) {
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $attempt = $client.BeginConnect('127.0.0.1', $port, $null, $null)
        if ($attempt.AsyncWaitHandle.WaitOne(250)) {
            try { $client.EndConnect($attempt); throw "Port $port is already in use." }
            catch [Net.Sockets.SocketException] { }
        }
    } finally { $client.Dispose() }
}

Push-Location $projectRoot
try {
    Assert-PortFree $ApiPort
    $migrator = Join-Path $projectRoot "backend/src/ShuttleBook.Migrator/bin/$Configuration/net10.0/ShuttleBook.Migrator.dll"
    $cli = Join-Path $projectRoot "backend/src/ShuttleBook.AdminCli/bin/$Configuration/net10.0/ShuttleBook.AdminCli.dll"
    $api = Join-Path $projectRoot "backend/src/ShuttleBook.Api/bin/$Configuration/net10.0/ShuttleBook.Api.dll"
    $worker = Join-Path $projectRoot "backend/src/ShuttleBook.Worker/bin/$Configuration/net10.0/ShuttleBook.Worker.dll"
    foreach ($file in @($migrator, $cli, $api, $worker)) {
        if (-not (Test-Path -LiteralPath $file)) { throw 'Build backend/ShuttleBook.slnx before the live Admin test.' }
    }
    $contextInfo = & $dotnetExecutable ef dbcontext info --no-build --configuration $Configuration `
        --project backend/src/ShuttleBook.Infrastructure `
        --startup-project backend/src/ShuttleBook.Migrator 2>&1
    if ($LASTEXITCODE -ne 0 -or $contextInfo -notcontains "Database name: $databaseName") {
        throw 'EF database target does not match the generated temporary name.'
    }
    if ($contextInfo -notcontains "Data source: tcp://127.0.0.1:$databasePort") {
        throw 'EF database host is not the local PostgreSQL port.'
    }
    & npm.cmd run build | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Local web build failed.' }
    & $dotnetExecutable $migrator | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Temporary database migration failed.' }
    $databaseCreated = $true
    @('email', $adminContact, $adminPassword) | & $dotnetExecutable $cli bootstrap | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Temporary Admin bootstrap failed.' }

    $apiProcess = Start-Process -FilePath $dotnetExecutable -ArgumentList @($api) `
        -WorkingDirectory (Join-Path $projectRoot 'backend/src/ShuttleBook.Api') `
        -RedirectStandardOutput (Join-Path $localDir 'admin-live-api.stdout.log') `
        -RedirectStandardError (Join-Path $localDir 'admin-live-api.stderr.log') `
        -WindowStyle Hidden -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 24; $attempt++) {
        if ($apiProcess.HasExited) { break }
        try {
            $response = Invoke-WebRequest -Uri "$testApiBase/health/ready" -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'Temporary API did not become ready.' }
    $workerProcess = Start-Process -FilePath $dotnetExecutable -ArgumentList @($worker) `
        -WorkingDirectory (Join-Path $projectRoot 'backend/src/ShuttleBook.Worker') `
        -RedirectStandardOutput (Join-Path $localDir 'f02-live-worker.stdout.log') `
        -RedirectStandardError (Join-Path $localDir 'f02-live-worker.stderr.log') `
        -WindowStyle Hidden -PassThru
    & (Join-Path $PSScriptRoot 'Test-Web.ps1') 'tests/web/admin-live.spec.ts' 'tests/web/customer-registration-live.spec.ts' 'tests/web/f02-live.spec.ts' '--workers=2' '--trace=off'
    $testExit = $LASTEXITCODE
} catch {
    Write-Error "Live identity test could not complete: $($_.Exception.Message)"
} finally {
    if ($workerProcess -and -not $workerProcess.HasExited) {
        Stop-Process -Id $workerProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($databaseCreated) {
        $dryRun = & $dotnetExecutable ef database drop --dry-run --no-build --configuration $Configuration `
            --project backend/src/ShuttleBook.Infrastructure `
            --startup-project backend/src/ShuttleBook.Migrator 2>&1
        if ($LASTEXITCODE -ne 0 -or ($dryRun -join ' ') -notlike "*'$databaseName'*") {
            Write-Error 'Temporary database cleanup target did not match the generated name. No database was dropped.'
        } else {
            & $dotnetExecutable ef database drop --force --no-build --configuration $Configuration `
                --project backend/src/ShuttleBook.Infrastructure `
                --startup-project backend/src/ShuttleBook.Migrator
            if ($LASTEXITCODE -ne 0) { Write-Error 'Temporary identity test database cleanup failed.' }
        }
    }
    # Restore the normal browser build after testing on a separate API port.
    if ($env:VITE_API_BASE_URL -ne $originalWebApiBase) {
        $env:VITE_API_BASE_URL = $originalWebApiBase
        & npm.cmd run build | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error 'Could not restore the normal web build after live tests.' }
    }
    Pop-Location
}
exit $testExit
