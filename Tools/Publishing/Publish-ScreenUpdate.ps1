[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][ValidateRange(1,2147483647)][int]$Sequence,
 [string]$DocumentPath,
 [string]$MinimumAppVersion='1.0.4',
 [string]$MaximumAppVersion='2.0.0'
)
$ErrorActionPreference='Stop'
if([Version]::Parse($MinimumAppVersion) -ge [Version]::Parse($MaximumAppVersion)){throw 'Maximum app version must be greater than minimum app version.'}
$gameRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $DocumentPath){$DocumentPath=Join-Path $gameRoot 'Content/ScreenUpdates/android-v1.json'}
$DocumentPath=[IO.Path]::GetFullPath($DocumentPath)
$baseUrl='https://assets.battlecities.com/android/screens/v1/'
$bucket='battlecities-game-assets'
$output=Join-Path $gameRoot "Builds/ScreenUpdates/$Sequence"
[IO.Directory]::CreateDirectory($output)|Out-Null
$utf8=[Text.UTF8Encoding]::new($false)
function Hash([byte[]]$bytes){$sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}}
function Upload([string]$key,[string]$path,[string]$type){
 & rtk proxy cf r2 objects put $key --bucket-name $bucket --content-type $type --file $path
 if($LASTEXITCODE -ne 0){throw "R2 upload failed: $key"}
}
function GetPublic([string]$url){
 $client=New-Object Net.WebClient
 try{$client.Headers['Cache-Control']='no-cache';return ,$client.DownloadData($url)}finally{$client.Dispose()}
}
function EnsureObject([string]$hash,[string]$extension,[string]$path,[string]$type){
 $url=$baseUrl+$hash+$extension
 $exists=$false
 try{$bytes=GetPublic $url;if((Hash $bytes) -ne $hash){throw 'Existing content-hashed object does not match its hash.'};$exists=$true}
 catch [Net.WebException]{if($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -ne 404){throw}}
 if(-not $exists){Upload "android/screens/v1/$hash$extension" $path $type}
 $actual=GetPublic $url
 if((Hash $actual) -ne $hash){throw "Public download verification failed: $url"}
}
Add-Type -AssemblyName System.Security
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$signingPath=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'BattleCities/ContentSigning/production-screens-v1.dpapi'
if(-not(Test-Path -LiteralPath $signingPath)){throw 'Run Initialize-ScreenSigning.ps1 first.'}
$plain=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($signingPath),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{
 $rsa.FromXmlString([Text.Encoding]::UTF8.GetString($plain))
 $config=Get-Content -LiteralPath (Join-Path $gameRoot 'Assets/BattleCities/Resources/ScreenUpdates/config.json') -Raw|ConvertFrom-Json
 $publicKey=$rsa.ExportParameters($false)
 if($config.modulus -ne [Convert]::ToBase64String($publicKey.Modulus) -or $config.exponent -ne [Convert]::ToBase64String($publicKey.Exponent)){throw 'Publishing key does not match the APK public key.'}
 # Guard against accidentally reusing a sequence or overwriting a newer production release.
 try{
  $existing=($utf8.GetString((GetPublic ($baseUrl+'latest.json?check='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()))))|ConvertFrom-Json
  $payload=[Convert]::FromBase64String($existing.payload)
  if(-not $rsa.VerifyData($payload,'SHA256',[Convert]::FromBase64String($existing.signature))){throw 'Existing release signature does not match this publishing key.'}
  $previous=$utf8.GetString($payload)|ConvertFrom-Json
  if($previous.sequence -ge $Sequence){throw "Sequence must be greater than current release $($previous.sequence)."}
 }catch [Net.WebException]{if($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -ne 404){throw}}
 $document=Get-Content -LiteralPath $DocumentPath -Raw -Encoding UTF8|ConvertFrom-Json
 $normalized=$false
 foreach($image in $document.images){
  if($image.PSObject.Properties.Name -contains 'source'){
   $source=[IO.Path]::GetFullPath((Join-Path $gameRoot $image.source))
   $allowed=[IO.Path]::GetFullPath($gameRoot).TrimEnd('\')+'\'
   if(-not $source.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Screen image source must be inside the game repository.'}
   $bytes=[IO.File]::ReadAllBytes($source)
   if($bytes.Length -lt 24 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71){throw 'Screen artwork must be PNG.'}
   $width=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,16));$height=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,20))
   if($width -le 0 -or $height -le 0 -or $width -gt 4096 -or $height -gt 4096 -or $bytes.Length -gt 8388608){throw 'Screen artwork exceeds the supported size.'}
   $hash=Hash $bytes;$image.PSObject.Properties.Remove('source')
   $image|Add-Member -Force NoteProperty width $width;$image|Add-Member -Force NoteProperty height $height
   $image|Add-Member -Force NoteProperty file ([ordered]@{url=$baseUrl+$hash+'.png';sha256=$hash;bytes=$bytes.Length})
   $imagePath=Join-Path $output "$hash.png";[IO.File]::WriteAllBytes($imagePath,$bytes)
   $normalized=$true
  }
 }
 $documentBytes=if($normalized){$utf8.GetBytes(($document|ConvertTo-Json -Depth 40 -Compress))}else{[IO.File]::ReadAllBytes($DocumentPath)}
 $documentHash=Hash $documentBytes;$publishedDocument=Join-Path $output "$documentHash.json"
 [IO.File]::WriteAllBytes($publishedDocument,$documentBytes)
 # A receipt is valid only for these exact bytes and the current validator sources.
 $validationPath=Join-Path $gameRoot "Builds/ScreenUpdateChecks/$documentHash-validation.json"
 $validatorBytes=New-Object 'System.Collections.Generic.List[byte]'
 foreach($name in @('Assets/BattleCities/UI/Runtime/ScreenUpdates.cs','Assets/BattleCities/UI/Runtime/ScreenContent.cs','Assets/BattleCities/UI/Runtime/AuthoredScreenElement.cs', 'Assets/BattleCities/UI/Runtime/RemoteScreenView.cs','Assets/BattleCities/Editor/ScreenUpdateChecks.cs')){$validatorBytes.AddRange([IO.File]::ReadAllBytes((Join-Path $gameRoot $name)))}
 $validatorHash=Hash $validatorBytes.ToArray();$validated=$false
 if(Test-Path -LiteralPath $validationPath){$receipt=Get-Content -LiteralPath $validationPath -Raw|ConvertFrom-Json;$validated=$receipt.sha256 -eq $documentHash -and $receipt.validatorHash -eq $validatorHash}
 if(-not $validated){
  $prior=$env:BATTLECITIES_SCREEN_DOCUMENT
  try{$env:BATTLECITIES_SCREEN_DOCUMENT=$publishedDocument
   & rtk proxy unity run $gameRoot --timeout 600 -- -executeMethod BattleCities.Editor.ScreenUpdateChecks.ValidateSource -logFile (Join-Path $output 'validation.log')
   if($LASTEXITCODE -ne 0 -or -not(Test-Path -LiteralPath $validationPath)){throw 'Unity rejected the screen definition.'}
  }finally{$env:BATTLECITIES_SCREEN_DOCUMENT=$prior}
 }
 foreach($image in $document.images){
  $imagePath=Join-Path $output ($image.file.sha256+'.png')
  if(Test-Path -LiteralPath $imagePath){EnsureObject $image.file.sha256 '.png' $imagePath 'image/png'}
  else{$bytes=GetPublic $image.file.url;if((Hash $bytes) -ne $image.file.sha256){throw 'Referenced image is not hosted correctly.'}}
 }
 EnsureObject $documentHash '.json' $publishedDocument 'application/json'
 $engine=([regex]::Match([IO.File]::ReadAllText((Join-Path $gameRoot 'ProjectSettings/ProjectVersion.txt')),'m_EditorVersion:\s*(\S+)')).Groups[1].Value
 $release=[ordered]@{schemaVersion=1;sequence=$Sequence;releaseId="screens-$Sequence";channel='android-ui-v1';runtimeId='battlecities-screens-v1';unityVersion=$engine;minimumAppVersion=$MinimumAppVersion;maximumAppVersion=$MaximumAppVersion;document=[ordered]@{url=$baseUrl+$documentHash+'.json';sha256=$documentHash;bytes=$documentBytes.Length}}
 $payload=$utf8.GetBytes(($release|ConvertTo-Json -Depth 20 -Compress))
 $envelope=[ordered]@{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($rsa.SignData($payload,'SHA256'))}
 $envelopePath=Join-Path $output 'release.json';[IO.File]::WriteAllText($envelopePath,($envelope|ConvertTo-Json -Compress),$utf8)
 # Publish the pointer only after immutable content is available and verified.
 Upload "android/screens/v1/releases/$Sequence.json" $envelopePath 'application/json'
 Upload 'android/screens/v1/latest.json' $envelopePath 'application/json'
 $public=$utf8.GetString((GetPublic ($baseUrl+'latest.json?verify='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds())))|ConvertFrom-Json
 if($public.payload -ne $envelope.payload -or $public.signature -ne $envelope.signature){throw 'Public release pointer does not match the signed release.'}
 Write-Host "Published screens-$Sequence. Document: $($documentBytes.Length) bytes. APK rebuild not required for installed compatible players."
}finally{$rsa.Dispose();if($plain){[Array]::Clear($plain,0,$plain.Length)}}
