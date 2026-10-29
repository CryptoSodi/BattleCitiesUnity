. (Join-Path $PSScriptRoot 'Launcher.Core.ps1')
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('battlecities-restart-'+[guid]::NewGuid().ToString('N'))
$script:GameRoot=$testRoot
$identity='20261029-test-server'
$job=Join-Path $testRoot "Builds/Launcher/$identity"
$null=New-Item -ItemType Directory -Force -Path $job
$web=Join-Path $testRoot 'web'
@{pid=12345;port=18080;root=$web;identity=$identity}|ConvertTo-Json|Set-Content (Join-Path $job 'localhost-process.json')
$script:case='valid';$script:stopped=0
function Invoke-RestMethod {param($Uri,$TimeoutSec) if($script:case -eq 'foreign'){return @{identity='unknown'}};return @{identity=$identity}}
function Get-NetTCPConnection {param($LocalPort,$State,$ErrorAction) if($script:case -eq 'missing-listener'){return};return [pscustomobject]@{OwningProcess=$(if($script:case -eq 'pid'){54321}else{12345})}}
function Get-CimInstance {param($ClassName,$Filter) return [pscustomobject]@{CommandLine=$(if($script:case -eq 'command'){'unrelated.exe'}else{"python `"$(Join-Path $script:LauncherHome 'serve.py')`" --root `"$web`" --identity $identity --port $(if($script:case -eq 'port'){9999}else{18080})"})}}
function Get-Process {param($Id,$ErrorAction) $p=[pscustomobject]@{};$p|Add-Member ScriptMethod WaitForExit {param($timeout) return $true};return $p}
function Stop-Process {param($Id,$ErrorAction) $script:stopped++}
foreach($scenario in @('foreign','pid','command','port')){
    $script:case=$scenario;$rejected=$false
    try{Stop-LauncherPreviousLocalhost 18080}catch{$rejected=$true}
    if(-not $rejected -or $script:stopped -ne 0){throw "Restart safety check failed: $scenario"}
}
$script:case='valid';Stop-LauncherPreviousLocalhost 18080
if($script:stopped -ne 1){throw 'Verified launcher server was not stopped exactly once.'}
$script:case='missing-listener';Stop-LauncherPreviousLocalhost 18080
if($script:stopped -ne 2){throw 'Verified receipt fallback failed when CIM returned no listener.'}
function Get-CimInstance {param($ClassName,$Filter) return [pscustomobject]@{CommandLine="python `"$(Join-Path $script:LauncherHome 'serve.py')`" --root `"$web`" --identity $identity `"--port`" `"18080`""}}
Stop-LauncherPreviousLocalhost 18080
if($script:stopped -ne 3){throw 'Quoted Windows port arguments were not recognized.'}
Write-Host 'PASSED: foreign server, PID mismatch and command mismatch preserved; verified previous launcher server replaced. No real processes stopped.'
