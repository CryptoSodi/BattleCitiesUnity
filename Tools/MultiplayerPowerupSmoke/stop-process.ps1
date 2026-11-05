param([Parameter(Mandatory=$true)][int]$ProcessId,[Parameter(Mandatory=$true)][string]$Executable)
$process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
if($process -and $process.Path -eq (Resolve-Path -LiteralPath $Executable).Path){
    Stop-Process -Id $ProcessId
    Write-Output "Stopped isolated test process $ProcessId."
}
