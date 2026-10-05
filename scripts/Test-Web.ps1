param([Parameter(ValueFromRemainingArguments = $true)][string[]]$PlaywrightArguments)
. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
$playwright = Join-Path $projectRoot 'node_modules/.bin/playwright.cmd'
if (-not (Test-Path -LiteralPath $playwright)) {
    throw 'Playwright is not installed. Run npm.cmd ci first.'
}
$portals = @('customer', 'partner', 'admin')
$previewProcesses = @()
$commandExit = 1
Push-Location $projectRoot
try {
    $nodeExecutable = (Get-Command node -ErrorAction Stop).Source
    for ($i = 0; $i -lt $portals.Count; $i++) {
        $port = 5173 + $i
        $socket = [Net.Sockets.TcpClient]::new()
        try {
            $attempt = $socket.BeginConnect('127.0.0.1', $port, $null, $null)
            if ($attempt.AsyncWaitHandle.WaitOne(250)) {
                try { $socket.EndConnect($attempt); throw "Port $port is already in use; stop its owner before running browser tests." }
                catch [Net.Sockets.SocketException] { }
            }
        } finally { $socket.Dispose() }
    }
    $env:SHUTTLEBOOK_EXTERNAL_WEB = '1'
    foreach ($portal in $portals) {
        $port = 5173 + [array]::IndexOf($portals, $portal)
        $preview = Start-Process -FilePath $nodeExecutable -ArgumentList @(
            'node_modules/vite/bin/vite.js', 'preview', "apps/$portal-web", '--host', 'localhost',
            '--port', "$port", '--strictPort') -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
        $previewProcesses += $preview
    }
    foreach ($port in 5173..5175) {
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            try {
                $response = Invoke-WebRequest -Uri "http://localhost:$port/" -TimeoutSec 1 -UseBasicParsing
                if ($response.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $ready) { throw "Preview server on port $port did not become ready." }
    }
    & $playwright test @PlaywrightArguments
    $commandExit = $LASTEXITCODE
} finally {
    foreach ($preview in $previewProcesses) {
        if (-not $preview.HasExited) { Stop-Process -Id $preview.Id -Force -ErrorAction SilentlyContinue }
    }
    Pop-Location
}
exit $commandExit
