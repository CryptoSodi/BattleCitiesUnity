[CmdletBinding()]
param([string]$Message, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Automation-Functions.ps1')
$lock = $null
try {
    $null = Assert-GameGitReady
    $null = Invoke-GameTool git @('status', '--short') -Echo
    if ($CheckOnly) { Write-Host 'Check completed. No files staged, committed, or pushed.'; exit 0 }
    if (-not $Message) { $Message = Read-Host 'Commit message (Enter for a timestamped update)' }
    if (-not $Message.Trim()) { $Message = 'Update Battle Cities ' + [DateTime]::Now.ToString('yyyy-MM-dd HH:mm') }
    $lock = Enter-GameAutomation
    $commit = Invoke-GameCommitPush $Message
    Write-Host "Everything committed and pushed: $commit"
    exit 0
} catch { Write-Error $_ -ErrorAction Continue; exit 1 }
finally { if ($lock) { $lock.Dispose() } }
