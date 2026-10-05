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
$env:VITE_API_BASE_URL = 'http://localhost:5080'
$env:Logging__EventLog__LogLevel__Default = 'None'
$localDir = Join-Path $projectRoot '.local'
[IO.Directory]::CreateDirectory($localDir) | Out-Null
$env:DataProtection__KeysPath = Join-Path $localDir 'identity-live-keys'
[IO.Directory]::CreateDirectory($env:DataProtection__KeysPath) | Out-Null
$apiProcess = $null
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
    Assert-PortFree 5080
    $migrator = Join-Path $projectRoot 'backend/src/ShuttleBook.Migrator/bin/Debug/net10.0/ShuttleBook.Migrator.dll'
    $cli = Join-Path $projectRoot 'backend/src/ShuttleBook.AdminCli/bin/Debug/net10.0/ShuttleBook.AdminCli.dll'
    $api = Join-Path $projectRoot 'backend/src/ShuttleBook.Api/bin/Debug/net10.0/ShuttleBook.Api.dll'
    foreach ($file in @($migrator, $cli, $api)) {
        if (-not (Test-Path -LiteralPath $file)) { throw 'Build backend/ShuttleBook.slnx before the live Admin test.' }
    }
    $contextInfo = & $dotnetExecutable ef dbcontext info --no-build `
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
            $response = Invoke-WebRequest -Uri 'http://127.0.0.1:5080/health/ready' -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'Temporary API did not become ready.' }
    & (Join-Path $PSScriptRoot 'Test-Web.ps1') 'tests/web/admin-live.spec.ts' 'tests/web/customer-registration-live.spec.ts' '--workers=2' '--trace=off'
    $testExit = $LASTEXITCODE
} catch {
    Write-Error "Live identity test could not complete: $($_.Exception.Message)"
} finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($databaseCreated) {
        $dryRun = & $dotnetExecutable ef database drop --dry-run --no-build `
            --project backend/src/ShuttleBook.Infrastructure `
            --startup-project backend/src/ShuttleBook.Migrator 2>&1
        if ($LASTEXITCODE -ne 0 -or ($dryRun -join ' ') -notlike "*'$databaseName'*") {
            Write-Error 'Temporary database cleanup target did not match the generated name. No database was dropped.'
        } else {
            & $dotnetExecutable ef database drop --force --no-build `
                --project backend/src/ShuttleBook.Infrastructure `
                --startup-project backend/src/ShuttleBook.Migrator
            if ($LASTEXITCODE -ne 0) { Write-Error 'Temporary identity test database cleanup failed.' }
        }
    }
    Pop-Location
}
exit $testExit
