$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Project.ps1')
$script:fakeFreeBytes=27MB
function Get-GameBuildFreeBytes{return $script:fakeFreeBytes}
$checks=0
foreach($target in @('Android','WebGL')){
    $blocked=$false
    try{Assert-GameBuildDiskSpace $target}catch{
        $blocked=$true
        if($_.Exception.Message -notlike '*Not enough disk space*'){throw}
    }
    if(-not $blocked){throw "Full drive was not blocked: $target"};$checks++
}
$script:fakeFreeBytes=10GB
Assert-GameBuildDiskSpace WebGL;$checks++
$blocked=$false;try{Assert-GameBuildDiskSpace Android}catch{$blocked=$true}
if(-not $blocked){throw 'Android reserve was not enforced'};$checks++
$script:fakeFreeBytes=20GB
Assert-GameBuildDiskSpace Android;$checks++
Write-Host "PASSED: $checks disk-budget checks. No builds or cleanup performed."
