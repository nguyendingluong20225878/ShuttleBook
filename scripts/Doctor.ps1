$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$missing = @()

foreach ($tool in @('git', 'node', 'npm.cmd')) {
    $command = Get-Command $tool -ErrorAction SilentlyContinue
    if ($command) {
        $version = & $command.Source --version
        if ($LASTEXITCODE -eq 0) { Write-Host "[OK] $tool : $version" }
        else { $missing += $tool; Write-Host "[MISSING] $tool could not run" }
    } else { $missing += $tool; Write-Host "[MISSING] $tool" }
}
if (Get-Command node -ErrorAction SilentlyContinue) {
    & node -e 'const [major, minor] = process.versions.node.split(String.fromCharCode(46)).map(Number); process.exit((major === 22 && minor >= 12) || major === 24 ? 0 : 1)'
    if ($LASTEXITCODE -ne 0) { $missing += 'supported Node version'; Write-Host '[MISSING] Node 22.12+ (22.x) or 24.x required' }
}

$dotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) {
    $systemDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($systemDotnet) { $dotnetPath = $systemDotnet.Source }
}
if (Test-Path -LiteralPath $dotnetPath) {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local/dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    Push-Location $projectRoot
    try {
        $ErrorActionPreference = 'Continue'
        $sdk = & $dotnetPath --version 2>$null
        $sdkExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = 'Stop'; Pop-Location }
    if ($sdkExit -eq 0) { Write-Host "[OK] .NET SDK resolved by global.json: $sdk" }
    else { $missing += '.NET SDK'; Write-Host '[MISSING] SDK compatible with global.json' }
} else { $missing += '.NET SDK'; Write-Host '[MISSING] .NET SDK' }

$docker = Get-Command docker -ErrorAction SilentlyContinue
if ($docker) {
    try {
        $ErrorActionPreference = 'Continue'
        $dockerVersion = & docker info --format '{{.ServerVersion}}' 2>$null
        $dockerExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = 'Stop' }
    if ($dockerExit -eq 0) { Write-Host "[OK] Docker Engine $dockerVersion" }
    else { $missing += 'Docker Engine'; Write-Host '[MISSING] Docker Engine is not running' }
    try {
        $ErrorActionPreference = 'Continue'
        $composeVersion = & docker compose version --short 2>$null
        $composeExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = 'Stop' }
    if ($composeExit -eq 0) { Write-Host "[OK] Docker Compose $composeVersion" }
    else { $missing += 'Docker Compose v2'; Write-Host '[MISSING] Docker Compose v2' }
} else { $missing += 'Docker'; Write-Host '[MISSING] Docker Desktop / Engine + Compose v2' }

if (Test-Path -LiteralPath (Join-Path $projectRoot '.env')) { Write-Host '[OK] .env exists (content not shown)' }
else { $missing += '.env'; Write-Host '[MISSING] Run scripts/Initialize-Local.ps1' }

if ($missing.Count) { Write-Host "Setup incomplete: $($missing -join ', ')"; exit 1 }
Write-Host 'Toolchain ready. Run migration/build/tests to verify the application.'
