[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$gameRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$signingDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'BattleCities/ContentSigning'
$keyPath=Join-Path $signingDirectory 'production-screens-v1.dpapi'
[IO.Directory]::CreateDirectory($signingDirectory)|Out-Null
Add-Type -AssemblyName System.Security
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider(3072)
$rsa.PersistKeyInCsp=$false
try {
 if(Test-Path -LiteralPath $keyPath){
  $encrypted=[IO.File]::ReadAllBytes($keyPath)
  $plain=[Security.Cryptography.ProtectedData]::Unprotect($encrypted,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
  $rsa.FromXmlString([Text.Encoding]::UTF8.GetString($plain))
 }else{
  $plain=[Text.Encoding]::UTF8.GetBytes($rsa.ToXmlString($true))
  $encrypted=[Security.Cryptography.ProtectedData]::Protect($plain,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
  [IO.File]::WriteAllBytes($keyPath,$encrypted)
 }
 $public=$rsa.ExportParameters($false)
 $config=[ordered]@{manifestUrl='https://assets.battlecities.com/android/screens/v1/latest.json';channel='android-ui-v1';modulus=[Convert]::ToBase64String($public.Modulus);exponent=[Convert]::ToBase64String($public.Exponent)}
 $path=Join-Path $gameRoot 'Assets/BattleCities/Resources/ScreenUpdates/config.json'
 [IO.Directory]::CreateDirectory((Split-Path $path -Parent))|Out-Null
 [IO.File]::WriteAllText($path,($config|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
 Write-Host 'Screen signing initialized. Private key is protected by Windows DPAPI outside the repository.'
 Write-Host "Public configuration: $path"
}finally{$rsa.Dispose();if($plain){[Array]::Clear($plain,0,$plain.Length)}}
