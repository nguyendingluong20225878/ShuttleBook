param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('customer', 'partner', 'admin')]
    [string]$Portal
)

. (Join-Path $PSScriptRoot 'Use-LocalEnvironment.ps1')
Push-Location $projectRoot
try {
    & npm.cmd run dev "--workspace=@shuttlebook/$Portal-web"
    $commandExit = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $commandExit
