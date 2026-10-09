param(
    [Parameter(Mandatory = $true)][ValidateSet('Backup', 'Verify', 'RestoreDrill')][string]$Mode,
    [string]$BackupDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$localRoot = Join-Path $projectRoot '.local'
$backupRoot = Join-Path $localRoot 'backups'
$restoreRoot = Join-Path $localRoot 'restore-drill'
$mediaSource = Join-Path $projectRoot 'backend/src/ShuttleBook.Api/.media-local'

if ($Mode -ne 'Verify') {
    foreach ($name in @('PGHOST', 'PGPORT', 'PGUSER', 'PGDATABASE', 'PGPASSWORD')) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Missing process environment variable $name. Do not put credentials in the command line."
        }
    }
}
$commands = if ($Mode -eq 'Backup') { @('pg_dump', 'psql') } elseif ($Mode -eq 'RestoreDrill') { @('pg_restore', 'createdb', 'psql') } else { @() }
foreach ($name in $commands) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "Required PostgreSQL CLI is unavailable: $name" }
}

$countSql = "SELECT json_build_object('bookings',(SELECT count(*) FROM bookings),'payments',(SELECT count(*) FROM payments),'series',(SELECT count(*) FROM booking_series),'allocations',(SELECT count(*) FROM court_allocations),'evidence',(SELECT count(*) FROM payment_evidence),'media',(SELECT count(*) FROM media_uploads))"
function Get-Counts([string]$database) {
    $result = & psql --no-psqlrc --quiet --tuples-only --no-align --dbname=$database --command=$countSql
    if ($LASTEXITCODE -ne 0 -or @($result).Count -ne 1) { throw 'Could not read verification counts.' }
    return ($result | ConvertFrom-Json)
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
    if (-not (Test-Path -LiteralPath $mediaSource -PathType Container)) { throw 'Local media directory is missing; verify Media:Mode=Local and its storage path.' }
    $runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [guid]::NewGuid().ToString('N')
    $target = Join-Path $backupRoot $runId
    Assert-Inside $target $backupRoot
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $dump = Join-Path $target 'database.dump'
    & pg_dump --format=custom --no-owner --no-acl --file=$dump
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; incomplete backup must not be used.' }
    Copy-Item -LiteralPath $mediaSource -Destination (Join-Path $target 'media') -Recurse
    $counts = Get-Counts $env:PGDATABASE
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
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '_' + [guid]::NewGuid().ToString('N')
$database = 'sb_restore_' + $runId
$target = Join-Path $restoreRoot $runId
Assert-Inside $target $restoreRoot
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'media') -Destination (Join-Path $target 'media') -Recurse
& createdb --maintenance-db=postgres --template=template0 $database
if ($LASTEXITCODE -ne 0) { throw 'Could not create dedicated restore database.' }
& pg_restore --exit-on-error --no-owner --no-acl --dbname=$database (Join-Path $source 'database.dump')
if ($LASTEXITCODE -ne 0) { throw "Restore failed; inspect dedicated test database $database. No existing database was modified." }
$counts = Get-Counts $database
if (($manifest.counts | ConvertTo-Json -Compress) -ne ($counts | ConvertTo-Json -Compress)) { throw 'Restored row counts differ from backup manifest.' }
Write-Host "Restore drill passed. Test database: $database; private media copy: $target"
