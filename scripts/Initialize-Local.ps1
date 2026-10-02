$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localEnvPath = Join-Path $projectRoot '.env'
function New-LocalSecret {
    $randomBytes = New-Object byte[] 32
    $randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $randomGenerator.GetBytes($randomBytes) } finally { $randomGenerator.Dispose() }
    return ([BitConverter]::ToString($randomBytes)).Replace('-', '').ToLowerInvariant()
}

if (Test-Path -LiteralPath $localEnvPath) {
    $existing = [IO.File]::ReadAllLines($localEnvPath)
    if (@($existing | Where-Object { $_ -match '^IDENTITY_OTP_PEPPER=' }).Count -eq 0) {
        [IO.File]::AppendAllText($localEnvPath, "`nIDENTITY_OTP_PEPPER=$(New-LocalSecret)`n", (New-Object Text.UTF8Encoding($false)))
        Write-Host 'Added a random local OTP pepper to ignored .env. No secret printed.'
    } else { Write-Host '.env already has an OTP pepper.' }
    if (@($existing | Where-Object { $_ -match '^IDENTITY_JWT_SIGNING_KEY=' }).Count -eq 0) {
        [IO.File]::AppendAllText($localEnvPath, "IDENTITY_JWT_SIGNING_KEY=$(New-LocalSecret)`n", (New-Object Text.UTF8Encoding($false)))
        Write-Host 'Added a random local JWT signing key to ignored .env. No secret printed.'
    }
    foreach ($setting in @(
        'ASPNETCORE_ENVIRONMENT=Development',
        'DOTNET_ENVIRONMENT=Development',
        'ASPNETCORE_URLS=http://localhost:5080',
        'VITE_API_BASE_URL=http://localhost:5080'
    )) {
        $name = ($setting -split '=', 2)[0]
        if (@($existing | Where-Object { $_ -match "^$name=" }).Count -eq 0) {
            [IO.File]::AppendAllText($localEnvPath, "$setting`n", (New-Object Text.UTF8Encoding($false)))
            Write-Host "Added $name to ignored .env."
        }
    }
    exit 0
}

$envLines = @(
    '# Generated local development configuration. Never commit this file.'
    'POSTGRES_DB=shuttlebook'
    'POSTGRES_USER=shuttlebook'
    "POSTGRES_PASSWORD=$(New-LocalSecret)"
    'POSTGRES_PORT=54329'
    "IDENTITY_OTP_PEPPER=$(New-LocalSecret)"
    "IDENTITY_JWT_SIGNING_KEY=$(New-LocalSecret)"
    'ASPNETCORE_ENVIRONMENT=Development'
    'DOTNET_ENVIRONMENT=Development'
    'ASPNETCORE_URLS=http://localhost:5080'
    'VITE_API_BASE_URL=http://localhost:5080'
)
[IO.File]::WriteAllLines($localEnvPath, $envLines, (New-Object Text.UTF8Encoding($false)))
Write-Host 'Created ignored .env with random local secrets. No secret printed.'
