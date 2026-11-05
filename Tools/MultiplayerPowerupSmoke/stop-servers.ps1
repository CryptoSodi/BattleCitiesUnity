param([Parameter(Mandatory=$true)][string]$DataPath)
$servers=Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -match 'MultiplayerPowerupSmoke[\\/]server\.cjs' }
foreach($server in $servers){
    if($server.CommandLine.Contains($DataPath)){Stop-Process -Id $server.ProcessId;Write-Output "Stopped isolated test server $($server.ProcessId)."}
}
