param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5080',
    [switch]$Database
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-HttpStatus([string]$path) {
    try {
        $response = Invoke-WebRequest -Uri ($ApiBaseUrl.TrimEnd('/') + $path) -TimeoutSec 5 -UseBasicParsing
        return [int]$response.StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}
$live = Get-HttpStatus '/health/live'
$ready = Get-HttpStatus '/health/ready'
$result = [ordered]@{ liveHttp = $live; readyHttp = $ready; workerVerified = $false }
$failed = $live -ne 200 -or $ready -ne 200

if ($Database) {
    foreach ($name in @('PGHOST', 'PGPORT', 'PGUSER', 'PGDATABASE', 'PGPASSWORD')) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) { throw "Missing process environment variable $name." }
    }
    if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw 'Required PostgreSQL CLI is unavailable: psql' }
    $sql = "SELECT json_build_object('outboxDue',(SELECT count(*) FROM outbox_messages WHERE processed_at IS NULL AND next_attempt_at <= now()),'outboxRetryThreshold',(SELECT count(*) FROM outbox_messages WHERE processed_at IS NULL AND attempts >= 8),'ownerOverdue',(SELECT count(*) FROM payments WHERE status IN ('TRANSFER_REPORTED','NEEDS_REVIEW') AND first_reported_at <= now() - interval '30 minutes'),'expiredQuoteHolds',(SELECT count(*) FROM quote_reservations WHERE released_at IS NULL AND consumed_at IS NULL AND expires_at <= now()))"
    $counts = & psql --no-psqlrc --quiet --tuples-only --no-align --command=$sql
    if ($LASTEXITCODE -ne 0 -or @($counts).Count -ne 1) { throw 'Could not read operational counts.' }
    $result.database = $counts | ConvertFrom-Json
}
$result | ConvertTo-Json -Depth 4
if ($failed) { exit 1 }
