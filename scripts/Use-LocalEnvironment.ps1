# Dot-source this file. It affects only the current shell, never machine-wide settings.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.nuget/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $projectRoot '.tools/playwright'

$localDotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $localDotnetPath) {
    $env:DOTNET_ROOT = Split-Path -Parent $localDotnetPath
    $dotnetExecutable = $localDotnetPath
} else {
    $dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
}

$localEnvPath = Join-Path $projectRoot '.env'
if (Test-Path -LiteralPath $localEnvPath) {
    $localConfig = @{}
    foreach ($line in [IO.File]::ReadAllLines($localEnvPath)) {
        if ($line -match '^\s*(POSTGRES_DB|POSTGRES_USER|POSTGRES_PASSWORD|POSTGRES_PORT|IDENTITY_OTP_PEPPER|IDENTITY_JWT_SIGNING_KEY|ASPNETCORE_ENVIRONMENT|DOTNET_ENVIRONMENT|ASPNETCORE_URLS|VITE_API_BASE_URL|VITE_MAPTILER_API_KEY|AdminSession__AllowedOrigin|BrowserSession__CustomerOrigin|BrowserSession__PartnerOrigin|Media__Mode|Media__S3Region|Media__S3Bucket|Booking__QuoteSeconds|Booking__MaxAdvanceDays|Outbox__AlertAttempts)=(.*)$') {
            $localConfig[$matches[1]] = $matches[2].Trim()
        }
    }
    foreach ($key in @('POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD', 'POSTGRES_PORT', 'IDENTITY_OTP_PEPPER', 'IDENTITY_JWT_SIGNING_KEY')) {
        if (-not $localConfig.ContainsKey($key) -or $localConfig[$key] -notmatch '^[A-Za-z0-9_-]+$') {
            throw "Invalid or missing $key in .env. Use unquoted letters/digits/underscore/hyphen for local config."
        }
    }
    if ($localConfig.POSTGRES_PASSWORD -eq 'replace-with-local-password') {
        throw 'Replace the example password in .env with a local secret before running.'
    }
    if ($localConfig.IDENTITY_OTP_PEPPER -eq 'replace-with-random-local-secret') { throw 'Replace the example IDENTITY_OTP_PEPPER in .env with a local secret before running.' }
    $databasePort = 0
    if (-not [int]::TryParse($localConfig.POSTGRES_PORT, [ref]$databasePort) -or $databasePort -lt 1 -or $databasePort -gt 65535) {
        throw 'POSTGRES_PORT must be between 1 and 65535.'
    }
    if (-not $env:ConnectionStrings__ShuttleBook) {
        $env:ConnectionStrings__ShuttleBook = "Host=localhost;Port=$databasePort;Database=$($localConfig.POSTGRES_DB);Username=$($localConfig.POSTGRES_USER);Password=$($localConfig.POSTGRES_PASSWORD);Timeout=3;Command Timeout=5"
    }
    if (-not $env:Identity__OtpPepper) { $env:Identity__OtpPepper = $localConfig.IDENTITY_OTP_PEPPER }
    if (-not $env:Identity__JwtSigningKey) { $env:Identity__JwtSigningKey = $localConfig.IDENTITY_JWT_SIGNING_KEY }
    foreach ($name in @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT', 'ASPNETCORE_URLS', 'VITE_API_BASE_URL', 'VITE_MAPTILER_API_KEY', 'AdminSession__AllowedOrigin', 'BrowserSession__CustomerOrigin', 'BrowserSession__PartnerOrigin', 'Media__Mode', 'Media__S3Region', 'Media__S3Bucket', 'Booking__QuoteSeconds', 'Booking__MaxAdvanceDays', 'Outbox__AlertAttempts')) {
        if ($localConfig.ContainsKey($name) -and -not [Environment]::GetEnvironmentVariable($name, 'Process')) {
            [Environment]::SetEnvironmentVariable($name, $localConfig[$name], 'Process')
        }
    }
}
