$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$settings=[IO.File]::ReadAllText("$root/ProjectSettings/ProjectSettings.asset")
if($settings -notmatch '(?m)^\s+Android: sol\.battle\.cities\s*$'){throw 'Android package is incorrect'}
if($settings -notmatch '(?m)^\s*bundleVersion: \d+\.\d+\.\d+\s*$' -or $settings -notmatch '(?m)^\s*AndroidBundleVersionCode: [1-9]\d*\s*$'){throw 'Android release version is invalid'}
$config=Get-Content "$root/Assets/BattleCities/Notifications/google-services.json" -Raw|ConvertFrom-Json
$client=@($config.client|Where-Object {$_.client_info.android_client_info.package_name -eq 'sol.battle.cities'})
if($config.project_info.project_id -ne 'battle-cities-84acd' -or $client.Count -ne 1 -or $client[0].client_info.mobilesdk_app_id -ne '1:295482350041:android:f745e7401010eea5fce1c8'){throw 'Firebase registration mismatch'}
[xml]$xml=Get-Content "$root/Assets/Plugins/Android/BattleCitiesNotifications.androidlib/src/main/res/values/firebase-config.xml" -Raw
$expected=@{google_app_id=$client[0].client_info.mobilesdk_app_id;project_id=$config.project_info.project_id;gcm_defaultSenderId=$config.project_info.project_number;google_api_key=$client[0].api_key[0].current_key;google_storage_bucket=$config.project_info.storage_bucket}
foreach($name in $expected.Keys){
 $entry=@($xml.resources.string|Where-Object name -eq $name)
 if($entry.Count -ne 1 -or $entry[0].InnerText -ne $expected[$name]){throw "Firebase XML mismatch: $name"}
}
foreach($file in Get-ChildItem "$root/Tools/Publishing" -Filter '*.ps1'){
 $errors=$null;$tokens=$null
 $null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
 if($errors){throw "PowerShell parsing failed: $($file.Name): $($errors.Message)"}
}
$workflow=[IO.File]::ReadAllText("$root/.github/workflows/publish-game.yml")
if(-not $workflow.Contains("startsWith(github.event.release.tag_name, 'v')")){throw 'Android-only releases must skip Pages deployment'}
Write-Host 'Android package, version, Firebase JSON/XML, publication isolation and script syntax verified.'
