param(
    [Parameter(Mandatory = $true)][ValidateSet('Backup', 'Verify', 'RestoreDrill')][string]$Mode,
    [string]$BackupDirectory,
    [ValidateSet('Native', 'Docker')][string]$Transport = 'Native',
    [string]$DockerExecutable = 'docker',
    [string]$DockerContainer = 'shuttlebook-postgres-1',
    [string]$DockerDatabaseUser = 'shuttlebook',
    [string]$SourceDatabase = 'shuttlebook'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$localRoot = Join-Path $projectRoot '.local'
$backupRoot = Join-Path $localRoot 'backups'
$restoreRoot = Join-Path $localRoot 'restore-drill'
$mediaSource = Join-Path $projectRoot 'backend/src/ShuttleBook.Api/.media-local'

if ($Mode -ne 'Verify' -and $Transport -eq 'Native') {
    foreach ($name in @('PGHOST', 'PGPORT', 'PGUSER', 'PGDATABASE', 'PGPASSWORD')) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Missing process environment variable $name. Do not put credentials in the command line."
        }
    }
}
$commands = if ($Mode -eq 'Verify' -or $Transport -eq 'Docker') { @() } elseif ($Mode -eq 'Backup') { @('pg_dump', 'psql') } else { @('pg_restore', 'createdb', 'psql') }
foreach ($name in $commands) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "Required PostgreSQL CLI is unavailable: $name" }
}
function Assert-DockerReady {
    if (-not (Get-Command $DockerExecutable -ErrorAction SilentlyContinue)) { throw 'Docker CLI is unavailable.' }
    foreach ($value in @($DockerContainer, $DockerDatabaseUser, $SourceDatabase)) {
        if ($value -cnotmatch '^[a-zA-Z][a-zA-Z0-9_-]*$') { throw 'Docker container, role and source database must use simple names.' }
    }
    $containerImage = & $DockerExecutable inspect --format '{{.Config.Image}}' $DockerContainer
    if ($LASTEXITCODE -ne 0 -or $containerImage -notmatch '^postgis/postgis:') { throw 'Expected a local postgis/postgis container.' }
    $containerState = & $DockerExecutable inspect --format '{{.State.Running}}' $DockerContainer
    if ($LASTEXITCODE -ne 0 -or $containerState -ne 'true') { throw 'PostGIS container is not running.' }
}

$countSql = "SELECT json_build_object('bookings',(SELECT count(*) FROM bookings),'payments',(SELECT count(*) FROM payments),'series',(SELECT count(*) FROM booking_series),'allocations',(SELECT count(*) FROM court_allocations),'evidence',(SELECT count(*) FROM payment_evidence),'media',(SELECT count(*) FROM media_uploads))"
function Get-Counts([string]$database) {
    if ($Transport -eq 'Docker') {
        $result = & $DockerExecutable exec --user postgres $DockerContainer psql -U $DockerDatabaseUser --no-psqlrc --quiet --tuples-only --no-align --dbname=$database --command=$countSql
    } else {
        $result = & psql --no-psqlrc --quiet --tuples-only --no-align --dbname=$database --command=$countSql
    }
    if ($LASTEXITCODE -ne 0 -or @($result).Count -ne 1) { throw 'Could not read verification counts.' }
    return ($result | ConvertFrom-Json)
}
function Invoke-DockerDump([string]$destination) {
    $containerDump = '/tmp/sb_backup_' + [guid]::NewGuid().ToString('N') + '.dump'
    try {
        & $DockerExecutable exec --user postgres $DockerContainer pg_dump -U $DockerDatabaseUser --format=custom --no-owner --no-acl --file=$containerDump $SourceDatabase
        if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; incomplete backup must not be used.' }
        & $DockerExecutable cp "${DockerContainer}:$containerDump" $destination
        if ($LASTEXITCODE -ne 0) { throw 'Could not copy database dump from container.' }
    } finally {
        & $DockerExecutable exec --user root $DockerContainer rm -f $containerDump | Out-Null
    }
}
function Invoke-DockerRestore([string]$database, [string]$dumpPath) {
    $containerDump = '/tmp/sb_restore_' + [guid]::NewGuid().ToString('N') + '.dump'
    try {
        & $DockerExecutable cp $dumpPath "${DockerContainer}:$containerDump"
        if ($LASTEXITCODE -ne 0) { throw 'Could not copy verified dump into container.' }
        & $DockerExecutable exec --user postgres $DockerContainer createdb -U $DockerDatabaseUser --maintenance-db=postgres --template=template0 $database
        if ($LASTEXITCODE -ne 0) { throw 'Could not create dedicated restore database.' }
        & $DockerExecutable exec --user postgres $DockerContainer pg_restore -U $DockerDatabaseUser --exit-on-error --no-owner --no-acl --dbname=$database $containerDump
        if ($LASTEXITCODE -ne 0) { throw "Restore failed; inspect dedicated test database $database. No existing database was modified." }
    } finally {
        & $DockerExecutable exec --user root $DockerContainer rm -f $containerDump | Out-Null
    }
}
function Assert-Inside([string]$path, [string]$parent) {
    $base = [IO.Path]::GetFullPath($parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath($path)
    if (-not $full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw "Path must stay inside $parent" }
}
function Get-Entries([string]$directory) {
    $items = @()
    foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($directory.Length + 1).Replace('\', '/')
        $items += [pscustomobject]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    return $items
}

if ($Mode -eq 'Backup') {
    if ($Transport -eq 'Docker') { Assert-DockerReady }
    if (-not (Test-Path -LiteralPath $mediaSource -PathType Container)) { throw 'Local media directory is missing; verify Media:Mode=Local and its storage path.' }
    $runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [guid]::NewGuid().ToString('N')
    $target = Join-Path $backupRoot $runId
    Assert-Inside $target $backupRoot
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $dump = Join-Path $target 'database.dump'
    if ($Transport -eq 'Docker') {
        Invoke-DockerDump $dump
    } else {
        & pg_dump --format=custom --no-owner --no-acl --file=$dump
        if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; incomplete backup must not be used.' }
    }
    Copy-Item -LiteralPath $mediaSource -Destination (Join-Path $target 'media') -Recurse
    $database = if ($Transport -eq 'Docker') { $SourceDatabase } else { $env:PGDATABASE }
    $counts = Get-Counts $database
    $manifest = [ordered]@{ version = 1; createdUtc = [DateTime]::UtcNow.ToString('o'); counts = $counts; files = @(Get-Entries $target) }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $target 'manifest.json') -Encoding utf8
    Write-Host "Backup saved: $target"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) { throw "$Mode requires -BackupDirectory." }
$source = (Resolve-Path -LiteralPath $BackupDirectory).Path
Assert-Inside $source $backupRoot
$manifestPath = Join-Path $source 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Backup manifest is missing.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
if ($manifest.version -ne 1) { throw 'Unsupported backup manifest version.' }
$expected = @($manifest.files | Sort-Object path)
$actual = @(Get-Entries $source | Where-Object { $_.path -ne 'manifest.json' } | Sort-Object path)
if (($expected | ConvertTo-Json -Depth 5 -Compress) -ne ($actual | ConvertTo-Json -Depth 5 -Compress)) {
    throw 'Backup checksum or file inventory mismatch; no restore database was created.'
}
if ($Mode -eq 'Verify') { Write-Host "Backup checksum verification passed: $source"; exit 0 }
if ($Transport -eq 'Docker') { Assert-DockerReady }
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '_' + [guid]::NewGuid().ToString('N')
$database = 'sb_restore_' + $runId
$target = Join-Path $restoreRoot $runId
Assert-Inside $target $restoreRoot
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'media') -Destination (Join-Path $target 'media') -Recurse
if ($Transport -eq 'Docker') {
    Invoke-DockerRestore $database (Join-Path $source 'database.dump')
} else {
    & createdb --maintenance-db=postgres --template=template0 $database
    if ($LASTEXITCODE -ne 0) { throw 'Could not create dedicated restore database.' }
    & pg_restore --exit-on-error --no-owner --no-acl --dbname=$database (Join-Path $source 'database.dump')
    if ($LASTEXITCODE -ne 0) { throw "Restore failed; inspect dedicated test database $database. No existing database was modified." }
}
$counts = Get-Counts $database
if (($manifest.counts | ConvertTo-Json -Compress) -ne ($counts | ConvertTo-Json -Compress)) { throw 'Restored row counts differ from backup manifest.' }
Write-Host "Restore drill passed. Test database: $database; private media copy: $target"
