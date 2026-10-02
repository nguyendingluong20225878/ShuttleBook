# Install a verified Microsoft SDK locally; does not modify PATH or machine-wide SDKs.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkVersion = (Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'global.json') | ConvertFrom-Json).sdk.version
$sdkDirectory = Join-Path $projectRoot '.tools/dotnet'
$sdkExecutable = Join-Path $sdkDirectory 'dotnet.exe'
if (Test-Path -LiteralPath $sdkExecutable) {
    $installedSdks = & $sdkExecutable --list-sdks
    if ($installedSdks -match "^$([regex]::Escape($sdkVersion)) ") {
        Write-Host "SDK $sdkVersion is already installed locally."
        exit 0
    }
}
New-Item -ItemType Directory -Force (Join-Path $projectRoot '.tools') | Out-Null
$metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$sdk = $metadata.releases | ForEach-Object { $_.sdks } | Where-Object { $_.version -eq $sdkVersion } | Select-Object -First 1
$asset = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $asset -or ([uri]$asset.url).Host -ne 'builds.dotnet.microsoft.com' -or ([uri]$asset.url).Scheme -ne 'https') {
    throw 'Expected Microsoft win-x64 SDK asset was not found.'
}
$archivePath = Join-Path $projectRoot '.tools/dotnet-sdk.zip'
Invoke-WebRequest -UseBasicParsing -Uri $asset.url -OutFile $archivePath
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA512).Hash -ne $asset.hash) { throw 'SDK SHA-512 mismatch; installation stopped.' }
Expand-Archive -LiteralPath $archivePath -DestinationPath $sdkDirectory -Force
& $sdkExecutable --version
if ($LASTEXITCODE -ne 0) { throw 'Installed SDK failed to start.' }
