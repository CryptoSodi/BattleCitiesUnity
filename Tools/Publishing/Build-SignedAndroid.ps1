[CmdletBinding()]
param([string]$Version='1.0.0',[ValidateRange(1,2100000000)][int]$VersionCode=1,[string]$ExistingBuildDirectory,[string]$RemoteArtBaseUrl,[switch]$SelfContained)
$ErrorActionPreference='Stop'
if($SelfContained -and $RemoteArtBaseUrl){throw 'Choose either OTA artwork or an explicit self-contained APK.'}
if(-not $SelfContained -and -not $ExistingBuildDirectory -and -not $RemoteArtBaseUrl){$RemoteArtBaseUrl='https://assets.battlecities.com/android/art'}
. (Join-Path $PSScriptRoot 'Automation-Functions.ps1')
Assert-GameBuildDiskSpace Android
$lock=Enter-GameAutomation
$editor=$null
try {
 if(-not $ExistingBuildDirectory){$editor=Assert-GameBuildReady Android}
 if($RemoteArtBaseUrl -and ($editor -or $ExistingBuildDirectory)){throw 'Remote artwork builds require Unity closed and a fresh build directory.'}
 $job=Join-Path $script:GameRoot ("Builds/Android/$Version-v$VersionCode/"+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
  if($ExistingBuildDirectory){
  $job=[IO.Path]::GetFullPath($ExistingBuildDirectory)
  $allowed=[IO.Path]::GetFullPath((Join-Path $script:GameRoot 'Builds/Android')).TrimEnd('\')+'\'
  if(-not $job.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Existing export must be under Builds/Android'}
 }
 [IO.Directory]::CreateDirectory($job)|Out-Null
 $export=Join-Path $job 'GradleProject'
 $requestPath=Join-Path $job 'build-request.json'
 $reportPath=Join-Path $job 'unity-build-report.json'
  if($ExistingBuildDirectory){
  $saved=Get-Content -LiteralPath $requestPath -Raw|ConvertFrom-Json
  if($saved.version -ne $Version -or $saved.androidVersionCode -ne $VersionCode -or $saved.androidApplicationId -ne 'sol.battle.cities' -or -not $saved.exportAndroidProject){throw 'Existing export identity/version mismatch'}
  $report=Get-Content -LiteralPath $reportPath -Raw|ConvertFrom-Json
 }else{
 $request=[ordered]@{version=$Version;target='Android';outputPath=$export;reportPath=$reportPath;androidVersionCode=$VersionCode;buildAppBundle=$false;testNetwork=$false;exportAndroidProject=$true;androidApplicationId='sol.battle.cities';remoteArtBaseUrl=$RemoteArtBaseUrl}
 [IO.File]::WriteAllText($requestPath,($request|ConvertTo-Json))
 Write-Host "Signed Android $Version ($VersionCode). Output: $job"
 if($editor){
  $null=Invoke-UnityEditorCommand editor_stop
  Wait-GameEditorIdle
  $settings=Invoke-UnityEditorCommand get_build_settings
  if($settings.activeBuildTarget -ne 'Android'){
   $null=Invoke-UnityEditorCommand switch_build_target @('--target','Android','--confirm','true')
   Wait-GameEditorIdle
  }
  $configure=Join-Path $job 'configure.cs'
  [IO.File]::WriteAllText($configure,'BattleCities.Editor.AutomationBuild.ConfigureFromFile("'+$requestPath.Replace('\','\\')+'"); return true;')
  $null=Invoke-UnityEditorCommand eval_file @('--file',$configure)
  Wait-GameEditorIdle
  $queued=Invoke-UnityEditorCommand build @('--target','Android','--outputPath',$export,'--confirm','true')
  $deadline=[DateTime]::UtcNow.AddHours(1)
  do{
   Start-Sleep -Seconds 15
   $report=Invoke-UnityEditorCommand build_status
   if($report.buildId -ne $queued.buildId){throw 'Another operation replaced the build'}
   Write-Host "Unity export: $($report.status)"
   if([DateTime]::UtcNow -gt $deadline){throw 'Unity export timed out'}
  }while($report.status -ne 'completed')
  [IO.File]::WriteAllText($reportPath,([ordered]@{result=$report.result;totalErrors=$report.totalErrors;totalWarnings=$report.totalWarnings;buildId=$report.buildId}|ConvertTo-Json))
 }else{
  $prior=$env:BATTLECITIES_BUILD_REQUEST
  try{
   $env:BATTLECITIES_BUILD_REQUEST=$requestPath
   $null=Invoke-GameTool unity @('build',$script:GameRoot,'--target','Android','--execute-method','BattleCities.Editor.AutomationBuild.Build','--output-path',$export,'--log-file',(Join-Path $job 'unity-build.log'),'--allow-dirty-build','--no-tail','--timeout','3600') -Echo
  }finally{$env:BATTLECITIES_BUILD_REQUEST=$prior}
  $report=Get-Content -LiteralPath $reportPath -Raw|ConvertFrom-Json
 }
 }
 if($report.result -ne 'Succeeded' -or $report.totalErrors -ne 0){throw "Unity export failed: $reportPath"}
 $launcher=Join-Path $export 'launcher/build.gradle'
 $gradle=[IO.File]::ReadAllText($launcher)
 $gradle=[regex]::Replace($gradle,'(?s)\s*signingConfigs\s*\{.*?\n    \}','')
 $gradle=[regex]::Replace($gradle,'(?m)^\s*signingConfig signingConfigs\.\w+\s*$','')
 if($gradle -match 'storePassword|keyPassword|signingConfigs'){throw 'Unexpected generated signing configuration'}
 [IO.File]::WriteAllText($launcher,$gradle)
 $android='C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/PlaybackEngines/AndroidPlayer'
 $priorRoot=$script:GameRoot
 try{
  $script:GameRoot=$export
  $gradleResult=Invoke-GameTool "$android/OpenJDK/bin/java.exe" @('-classpath',"$android/Tools/gradle/lib/gradle-launcher-9.1.0.jar",'org.gradle.launcher.GradleMain','--no-daemon','--console=plain',':launcher:assembleRelease') -AllowFailure
  [IO.File]::WriteAllText((Join-Path $job 'gradle-build.log'),$gradleResult.Output)
  [IO.File]::WriteAllText((Join-Path $job 'gradle-errors.log'),$gradleResult.Errors)
  if($gradleResult.ExitCode -ne 0){throw "Gradle failed: $job/gradle-build.log"}
 }finally{$script:GameRoot=$priorRoot}
 & (Join-Path $PSScriptRoot 'Sign-AndroidApk.ps1') -BuildDirectory $job -Version $Version -VersionCode $VersionCode
 if(-not(Test-Path -LiteralPath (Join-Path $job 'release-verification.json'))){throw 'Verification receipt missing'}
 Write-Host "APK ready: $job/battlecities.apk"
}finally{
 try{
  if($editor -and $job){
   $cleanup=Join-Path $job 'clear-signing.cs'
   [IO.File]::WriteAllText($cleanup,'UnityEditor.PlayerSettings.Android.keystorePass=""; UnityEditor.PlayerSettings.Android.keyaliasPass=""; UnityEditor.EditorUserBuildSettings.exportAsGoogleAndroidProject=false; UnityEditor.AssetDatabase.SaveAssets(); return new { credentialsEmpty=string.IsNullOrEmpty(UnityEditor.PlayerSettings.Android.keystorePass) && string.IsNullOrEmpty(UnityEditor.PlayerSettings.Android.keyaliasPass) };')
   $null=Invoke-UnityEditorCommand eval_file @('--file',$cleanup)
  }
 }finally{if($lock){$lock.Dispose()}}
}
