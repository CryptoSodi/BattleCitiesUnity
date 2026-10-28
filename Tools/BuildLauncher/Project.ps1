# Resolve the Unity project independently of where the launcher is installed.
$script:LauncherHome=$PSScriptRoot
$launcherProjectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$launcherSettings=Join-Path $PSScriptRoot 'settings.local.json'
if(Test-Path -LiteralPath $launcherSettings){
    $launcherLocal=Get-Content -LiteralPath $launcherSettings -Raw|ConvertFrom-Json
    if($launcherLocal.PSObject.Properties['projectRoot'] -and $launcherLocal.projectRoot){
        $launcherProjectRoot=[IO.Path]::GetFullPath([string]$launcherLocal.projectRoot)
    }
}
$launcherAutomation=Join-Path $launcherProjectRoot 'Tools/Publishing/Automation-Functions.ps1'
if(-not(Test-Path -LiteralPath $launcherAutomation) -or -not(Test-Path -LiteralPath (Join-Path $launcherProjectRoot 'ProjectSettings/ProjectVersion.txt'))){
    throw 'Unity project not found. Set projectRoot in the launcher settings.local.json to the Battle Cities Unity repository.'
}
. $launcherAutomation
