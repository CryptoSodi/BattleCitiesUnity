[CmdletBinding()]
param([string]$Version='1.0.14',[int]$VersionCode=1)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Automation-Functions.ps1')
$lock=Enter-GameAutomation
$snapshot=@{}
try {
 if(Get-GameEditor){throw 'Close the game Unity Editor before building the isolated devnet profile.'}
 $job=Join-Path $script:GameRoot ('Builds/AndroidDevnet/'+$Version+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
 [IO.Directory]::CreateDirectory($job)|Out-Null
 foreach($relative in @('ProjectSettings/ProjectSettings.asset','ProjectSettings/UnityConnectSettings.asset','Assets/Settings/UniversalRenderPipelineGlobalSettings.asset','Assets/BattleCities/Resources/RuntimeShaders/TankDropperGlow-0.mat','Assets/Plugins/Android/BattleCitiesNotifications.androidlib/src/main/res/values/firebase-config.xml')){
  $path=Join-Path $script:GameRoot $relative
  $snapshot[$path]=[IO.File]::ReadAllBytes($path)
 }
 $requestPath=Join-Path $job 'build-request.json'
 $reportPath=Join-Path $job 'build-report.json'
 $apk=Join-Path $job 'battlecities-devnet.apk'
 $request=[ordered]@{version=$Version;target='Android';outputPath=$apk;reportPath=$reportPath;
  androidVersionCode=$VersionCode;androidApplicationId='sol.battle.cities.devnet';testNetwork=$true;
  useDebugSigning=$true;buildAppBundle=$false;exportAndroidProject=$false}
 [IO.File]::WriteAllText($requestPath,($request|ConvertTo-Json))
 $prior=$env:BATTLECITIES_BUILD_REQUEST
 try {
  $env:BATTLECITIES_BUILD_REQUEST=$requestPath
  $null=Invoke-GameTool unity @('build',$script:GameRoot,'--target','Android','--execute-method',
   'BattleCities.Editor.AutomationBuild.Build','--output-path',$apk,'--allow-dirty-build','--no-tail',
   '--timeout','3600','--log-file',(Join-Path $job 'unity-build.log')) -Echo
 } finally {$env:BATTLECITIES_BUILD_REQUEST=$prior}
 $report=Get-Content $reportPath -Raw|ConvertFrom-Json
 if($report.result -ne 'Succeeded' -or $report.totalErrors -ne 0){throw 'Devnet APK build failed'}
 $info=[ordered]@{apk=$apk;applicationId='sol.battle.cities.devnet';api='https://test.battlecities.com';
  chain='solana:devnet';allowGenericMwa=$true;allowMwaTesting=$true;version=$Version;versionCode=$VersionCode;
  sha256=(Get-ReleaseSha256 $apk);sourceCommit=(Invoke-GameTool git @('rev-parse','HEAD')).Output.Trim();sourceDirty=$true}
 [IO.File]::WriteAllText((Join-Path $job 'devnet-profile.json'),($info|ConvertTo-Json))
 [IO.File]::WriteAllText((Join-Path $script:GameRoot 'Builds/Automation/latest-devnet-apk.json'),($info|ConvertTo-Json))
 Write-Host "DEVNET APK READY: $apk"
} finally {
 foreach($entry in $snapshot.GetEnumerator()){[IO.File]::WriteAllBytes($entry.Key,$entry.Value)}
 $lock.Dispose()
}
