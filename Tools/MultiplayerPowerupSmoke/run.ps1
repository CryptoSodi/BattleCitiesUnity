param([Parameter(Mandatory=$true)][string]$BuildPath,[Parameter(Mandatory=$true)][string]$DataPath,[switch]$Migration,[switch]$Inflight,[switch]$Abandon)
$exe = Join-Path $BuildPath 'BattleCitiesPowerupSmoke.exe'
$hostArgs = @('-batchmode','-nographics','-logFile',(Join-Path $DataPath 'host.log'),'--bc-powerup-smoke','host',$DataPath)
$clientArgs = @('-batchmode','-nographics','-logFile',(Join-Path $DataPath 'client.log'),'--bc-powerup-smoke','client',$DataPath)
if($Migration){$hostArgs += '--bc-powerup-migrate';$clientArgs += '--bc-powerup-migrate'}
if($Inflight){$hostArgs += '--bc-powerup-inflight';$clientArgs += '--bc-powerup-inflight'}
if($Abandon){$hostArgs += '--bc-powerup-abandon';$clientArgs += '--bc-powerup-abandon'}
$hostProcess = Start-Process -FilePath $exe -ArgumentList $hostArgs -PassThru -WindowStyle Hidden
$clientProcess = Start-Process -FilePath $exe -ArgumentList $clientArgs -PassThru -WindowStyle Hidden
@{ hostPid=$hostProcess.Id; clientPid=$clientProcess.Id } | ConvertTo-Json | Set-Content (Join-Path $DataPath 'processes.json')
Write-Output "Started host $($hostProcess.Id) and client $($clientProcess.Id)."
