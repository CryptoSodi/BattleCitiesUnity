param([Parameter(Mandatory=$true)][string]$DataPath,[switch]$HoldRedeem)
$env:BC_SMOKE_API_ROOT = 'C:/repos/BattleCities-API'
if($HoldRedeem){$env:BC_SMOKE_HOLD_REDEEM = '1'}
node (Join-Path $PSScriptRoot 'server.cjs') $DataPath
exit $LASTEXITCODE
