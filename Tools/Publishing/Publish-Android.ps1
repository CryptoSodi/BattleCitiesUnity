[CmdletBinding(SupportsShouldProcess=$true)]
param(
 [Parameter(Mandatory=$true)][string]$ApkPath,
 [Parameter(Mandatory=$true)][string]$VerificationPath,
 [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$TargetCommit,
 [string]$Repository='CryptoSodi/BattleCitiesUnity',
 [switch]$SkipLegacyApkMirror
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Release-Functions.ps1')
function Invoke-GitHub([string[]]$Arguments){
 $output=& gh @Arguments
 if($LASTEXITCODE -ne 0){throw "GitHub operation failed: $($Arguments[0])"}
 return ($output -join [Environment]::NewLine)
}
$apk=(Resolve-Path -LiteralPath $ApkPath).Path
$receipt=Get-Content -LiteralPath $VerificationPath -Raw|ConvertFrom-Json
if($receipt.package -ne 'sol.battle.cities' -or -not $receipt.apksignerVerified -or -not $receipt.firebaseVerified -or -not $receipt.zipalignVerified -or -not $receipt.previousCertificateMatches -or -not $receipt.credentialsCleared){throw 'APK verification is incomplete'}
if($receipt.certificateSha256 -ne 'd3673cb75cdf37466d9ced1d1ffd23def10034ed8be2f08451204cf79f8f88a4'){throw 'Signing certificate mismatch'}
if((Get-ReleaseSha256 $apk) -ne $receipt.apkSha256){throw 'APK no longer matches its verification receipt'}
$tag="android-v$($receipt.versionName)-$($receipt.versionCode)"
$directory=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) "Builds/Releases/$tag/Publish"
[IO.Directory]::CreateDirectory($directory)|Out-Null
$staged=Join-Path $directory 'battlecities.apk'
$compat=Join-Path $directory 'BattleCities-0.1.1.apk'
Copy-Item -LiteralPath $apk -Destination $staged -Force
Copy-Item -LiteralPath $apk -Destination $compat -Force
$checksums=Join-Path $directory 'SHA256SUMS.txt'
[IO.File]::WriteAllLines($checksums,@("$($receipt.apkSha256)  battlecities.apk","$($receipt.apkSha256)  BattleCities-0.1.1.apk"))
# Publish a sanitized verification manifest, never local credential paths or local machine details.
$manifest=Join-Path $directory 'android-release.json'
$public=[ordered]@{package=$receipt.package;versionName=$receipt.versionName;versionCode=$receipt.versionCode;architecture=$receipt.architecture;developmentBuild=$false;debuggable=$false;certificateSha256=$receipt.certificateSha256;apkSha256=$receipt.apkSha256;firebaseProject=$receipt.firebaseProject;firebaseAndroidAppId=$receipt.firebaseAndroidAppId;apksignerVerified=$true;zipalignVerified=$true;previousCertificateMatches=$true;sourceCommit=$TargetCommit}
[IO.File]::WriteAllText($manifest,($public|ConvertTo-Json))
$notes=Join-Path $directory 'release-notes.md'
$text=@"
Battle Cities Android $($receipt.versionName) (code $($receipt.versionCode))

- Package: sol.battle.cities (new Android application).
- Production ARM64 release APK, signed with the existing Battle Cities certificate.
- Firebase project: battle-cities-84acd.
- Existing web game and API deployments are unchanged.
- Adds independent turret aiming through the Android touch FIRE control, alongside the current gameplay and results improvements.
- Device installation, wallet handoff and Firebase push delivery have not been tested on hardware.
- Download: https://github.com/$Repository/releases/latest/download/battlecities.apk

Certificate SHA-256: $($receipt.certificateSha256)
Source: $TargetCommit
"@
[IO.File]::WriteAllText($notes,$text)
if(-not $PSCmdlet.ShouldProcess($Repository,"Publish Android-only $tag and update APK downloads")){return}
$all=Invoke-GitHub @('api',"repos/$Repository/releases?per_page=100")|ConvertFrom-Json
$existing=@($all|Where-Object tag_name -eq $tag)
if($existing.Count -and -not $existing[0].draft){throw 'Release already published; use the next Android version code'}
$assets=@($staged,$compat,$checksums,$manifest)
if($existing.Count){
 $null=Invoke-GitHub (@('release','upload',$tag,'--repo',$Repository,'--clobber')+$assets)
 $null=Invoke-GitHub @('release','edit',$tag,'--repo',$Repository,'--target',$TargetCommit,'--notes-file',$notes)
}else{
 $null=Invoke-GitHub (@('release','create',$tag,'--repo',$Repository,'--target',$TargetCommit,'--draft','--title',"Battle Cities Android $($receipt.versionName)",'--notes-file',$notes)+$assets)
}
if(-not $SkipLegacyApkMirror){
 $null=Invoke-GitHub @('release','upload','v0.1.1',$compat,'--repo','CryptoSodi/BattleCitiesUnity-Releases','--clobber')
}
$null=Invoke-GitHub @('release','edit',$tag,'--repo',$Repository,'--draft=false','--latest')
Write-Host "Published: https://github.com/$Repository/releases/tag/$tag"
Write-Host "APK: https://github.com/$Repository/releases/latest/download/battlecities.apk"
