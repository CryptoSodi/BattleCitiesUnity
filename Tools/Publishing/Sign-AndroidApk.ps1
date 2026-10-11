[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BuildDirectory,[string]$Version="1.0.0",[int]$VersionCode=1)
$ErrorActionPreference='Stop'
Import-Module "$env:WINDIR/System32/WindowsPowerShell/v1.0/Modules/Microsoft.PowerShell.Security/Microsoft.PowerShell.Security.psd1"
$job=[IO.Path]::GetFullPath($BuildDirectory)
$android='C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/PlaybackEngines/AndroidPlayer'
$tools="$android/SDK/build-tools/36.0.0"
$java="$android/OpenJDK/bin/java.exe"
$signer="$tools/lib/apksigner.jar"
$unsigned="$job/GradleProject/launcher/build/outputs/apk/release/launcher-release-unsigned.apk"
$aligned="$job/release-aligned-unsigned.apk"
$final="$job/battlecities.apk"
$expectedFingerprint='d3673cb75cdf37466d9ced1d1ffd23def10034ed8be2f08451204cf79f8f88a4'
$previous='C:/Users/tassa/Downloads/bkHwuVfH8QU0eaefor3z5xTgrmjMH4dvSLg8fXQjm1U.apk'
if(-not (Test-Path -LiteralPath $unsigned)){throw 'Unsigned release APK is missing'}
if(Test-Path -LiteralPath $final){throw 'Refusing to overwrite an existing signed APK'}
& "$tools/zipalign.exe" -f -P 16 4 $unsigned $aligned
if($LASTEXITCODE -ne 0){throw 'zipalign failed'}
$credential=$null;$keySecret=$null;$storePointer=[IntPtr]::Zero;$keyPointer=[IntPtr]::Zero;$process=$null
try{
 $credential=Import-Clixml -LiteralPath 'D:/keys/battle-cities-signing.xml'
 $keySecret=ConvertTo-SecureString ([IO.File]::ReadAllText('D:/keys/battle-cities-signing.txt').Trim())
 if($credential.UserName -ne 'battle-cities'){throw 'Original signing alias mismatch'}
 $start=[Diagnostics.ProcessStartInfo]::new()
 $start.FileName=$java
 $start.Arguments='-jar "'+$signer+'" sign --ks "D:\keys\battle-cities-dappstore.keystore" --ks-key-alias battle-cities --ks-pass stdin --key-pass stdin --v1-signing-enabled true --v2-signing-enabled true --v3-signing-enabled true --v4-signing-enabled false --out "'+$final+'" "'+$aligned+'"'
 $start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $process=[Diagnostics.Process]::new();$process.StartInfo=$start
 if(-not $process.Start()){throw 'APK signer could not start'}
 $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 $storePointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($credential.Password)
 $keyPointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($keySecret)
 $storePassword=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($storePointer)
 $keyPassword=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
 $process.StandardInput.WriteLine($storePassword);$process.StandardInput.WriteLine($keyPassword)
 $process.StandardInput.Close()
 $storePassword=$null;$keyPassword=$null
 [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($storePointer);$storePointer=[IntPtr]::Zero
 [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer);$keyPointer=[IntPtr]::Zero
 $process.WaitForExit()
 $capturedOutput=$stdout.GetAwaiter().GetResult();$capturedError=$stderr.GetAwaiter().GetResult()
 $capturedOutput=$null;$capturedError=$null
 if($process.ExitCode -ne 0){throw 'APK signing failed with the original keystore/alias; signer output was withheld to protect credentials.'}
}finally{
 $storePassword=$null;$keyPassword=$null;$capturedOutput=$null;$capturedError=$null
 if($storePointer -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($storePointer)}
 if($keyPointer -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)}
 if($credential){$credential.Password.Dispose();$credential=$null}
 if($keySecret){$keySecret.Dispose();$keySecret=$null}
 if($process){$process.Dispose();$process=$null}
 [GC]::Collect()
}
$verify=(& $java -jar $signer verify --verbose --print-certs $final) -join [Environment]::NewLine
if($LASTEXITCODE -ne 0){throw 'apksigner verification failed'}
[IO.File]::WriteAllText("$job/signature-verification.txt",$verify)
if($verify -notmatch 'certificate SHA-256 digest: ([a-fA-F0-9]+)'){throw 'No signer fingerprint found'}
$fingerprint=$Matches[1].ToLowerInvariant()
if($fingerprint -ne $expectedFingerprint){throw 'Unexpected APK signing certificate'}
$badging=(& "$tools/aapt.exe" dump badging $final) -join [Environment]::NewLine
if($LASTEXITCODE -ne 0){throw 'APK metadata inspection failed'}
[IO.File]::WriteAllText("$job/apk-metadata.txt",$badging)
if($badging -notmatch ("package: name='sol\.battle\.cities' versionCode='{0}' versionName='{1}'" -f $VersionCode,[regex]::Escape($Version))){throw 'APK identity/version mismatch'}
if($badging -match 'application-debuggable'){throw 'APK is debuggable'}
if($badging -notmatch "native-code: 'arm64-v8a'\s*(\r?\n|$)"){throw 'APK is not ARM64 only'}
$resources=(& "$tools/aapt.exe" dump --values resources $final) -join [Environment]::NewLine
if($LASTEXITCODE -ne 0){throw 'Compiled resource inspection failed'}
[IO.File]::WriteAllText("$job/apk-resources.txt",$resources)
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$config=Get-Content -LiteralPath "$root/Assets/BattleCities/Notifications/google-services.json" -Raw | ConvertFrom-Json
$client=@($config.client | Where-Object { $_.client_info.android_client_info.package_name -eq 'sol.battle.cities' })
if($config.project_info.project_id -ne 'battle-cities-84acd' -or $client.Count -ne 1 -or $client[0].client_info.mobilesdk_app_id -ne '1:295482350041:android:f745e7401010eea5fce1c8'){throw 'Firebase JSON mismatch'}
[xml]$firebaseXml=Get-Content -LiteralPath "$root/Assets/Plugins/Android/BattleCitiesNotifications.androidlib/src/main/res/values/firebase-config.xml" -Raw
$expectedResources=@{google_app_id=$client[0].client_info.mobilesdk_app_id;project_id=$config.project_info.project_id;gcm_defaultSenderId=$config.project_info.project_number;google_api_key=$client[0].api_key[0].current_key;google_storage_bucket=$config.project_info.storage_bucket}
foreach($name in $expectedResources.Keys){
 $entry=@($firebaseXml.resources.string | Where-Object name -eq $name)
 if($entry.Count -ne 1 -or $entry[0].InnerText -ne $expectedResources[$name]){throw "Firebase resource mismatch: $name"}
 if(-not $resources.Contains([string]$expectedResources[$name])){throw "Compiled Firebase resource mismatch: $name"}
}
$oldVerify=(& $java -jar $signer verify --print-certs $previous) -join [Environment]::NewLine
if($LASTEXITCODE -ne 0){throw 'Previous original release APK signature verification failed'}
if($oldVerify -notmatch 'certificate SHA-256 digest: ([a-fA-F0-9]+)'){throw 'Previous release signer not found'}
$matchesPrevious=($Matches[1].ToLowerInvariant() -eq $fingerprint)
if(-not $matchesPrevious){throw 'The previous original release certificate does not match'}
& "$tools/zipalign.exe" -c -P 16 4 $final
if($LASTEXITCODE -ne 0){throw 'Signed APK alignment verification failed'}
$hashAlgorithm=[Security.Cryptography.SHA256]::Create(); $hashStream=[IO.File]::OpenRead($final); try{$hash=[BitConverter]::ToString($hashAlgorithm.ComputeHash($hashStream)).Replace("-","").ToLowerInvariant()}finally{$hashStream.Dispose();$hashAlgorithm.Dispose()}
$result=[ordered]@{apk=[IO.Path]::GetFullPath($final);package='sol.battle.cities';versionCode=$VersionCode;versionName=$Version;publishedVersionCodeBaseline=0;publishedBaselineSource='User confirmed new package has never been published';architecture='arm64-v8a';developmentBuild=$false;debuggable=$false;signerAlias='battle-cities';certificateSha256=$fingerprint;apkSha256=$hash;apksignerVerified=$true;zipalignVerified=$true;previousApk=$previous;previousCertificateMatches=$matchesPrevious;passwordsSuppliedVia='Redirected stdin only';credentialsCleared=$true;firebaseVerified=$true;firebaseProject='battle-cities-84acd';firebaseAndroidAppId='1:295482350041:android:f745e7401010eea5fce1c8'}
[IO.File]::WriteAllText("$job/release-verification.json",($result|ConvertTo-Json -Depth 5))
$result|ConvertTo-Json -Depth 5