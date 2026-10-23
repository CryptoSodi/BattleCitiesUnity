[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ArtManifestPath,
    [switch]$Publish
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Release-Functions.ps1')
$source=Join-Path $PSScriptRoot 'AssetHealth'
$manifestFile=(Resolve-Path -LiteralPath $ArtManifestPath).Path
$manifest=[IO.File]::ReadAllText($manifestFile)|ConvertFrom-Json
if($manifest.sha256 -notmatch '^[a-f0-9]{64}$' -or
   $manifest.url -ne ('https://assets.battlecities.com/android/art/'+$manifest.sha256+'.bundle') -or
   $manifest.bytes -le 0 -or $manifest.bytes -gt 1073741824){throw 'Invalid production artwork manifest.'}
$bundle=Join-Path (Split-Path $manifestFile) ($manifest.sha256+'.bundle')
if((Get-Item -LiteralPath $bundle).Length -ne $manifest.bytes -or (Get-ReleaseSha256 $bundle) -ne $manifest.sha256){throw 'Local bundle does not match the APK manifest.'}
$receiptPath=Join-Path (Split-Path (Split-Path $manifestFile)) 'release-verification.json'
$receipt=[IO.File]::ReadAllText($receiptPath)|ConvertFrom-Json
$sampleBytes=[int][Math]::Min(65536,$manifest.bytes)
$buffer=New-Object byte[] $sampleBytes
$stream=[IO.File]::OpenRead($bundle)
try{
 $read=0
 while($read -lt $sampleBytes){$count=$stream.Read($buffer,$read,$sampleBytes-$read);if($count -eq 0){throw 'Bundle sample is incomplete.'};$read+=$count}
}finally{$stream.Dispose()}
$sha=[Security.Cryptography.SHA256]::Create()
try{$sampleHash=[BitConverter]::ToString($sha.ComputeHash($buffer)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
$encoding=[Text.UTF8Encoding]::new($false)
$probePath=Join-Path $source 'ping.json'
[IO.File]::WriteAllText($probePath,'{"service":"battlecities-assets","schema":1,"purpose":"connectivity-check"}'+[char]10,$encoding)
$config=[ordered]@{
 schema=1;publishedAt=[DateTime]::UtcNow.ToString('o')
 artifact=[ordered]@{url=$manifest.url;bytes=[long]$manifest.bytes;sha256=$manifest.sha256;unityVersion=$manifest.unityVersion;versionName=$receipt.versionName;versionCode=$receipt.versionCode}
 sample=[ordered]@{bytes=$sampleBytes;sha256=$sampleHash}
 probeSha256=(Get-ReleaseSha256 $probePath)
}
[IO.File]::WriteAllText((Join-Path $source 'config.json'),($config|ConvertTo-Json -Depth 5),$encoding)
$job=Join-Path $PSScriptRoot ('../../Builds/AssetHealth/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory($job)|Out-Null
$page=[IO.File]::ReadAllText((Join-Path $source 'index.html'))
$page=$page.Replace('<link rel="stylesheet" href="/health/health.css?v=1">',('<style>'+[IO.File]::ReadAllText((Join-Path $source 'health.css'))+'</style>'))
$page=$page.Replace('<script type="module" src="/health/health.mjs?v=1"></script>',('<script type="module">'+[IO.File]::ReadAllText((Join-Path $source 'health.mjs'))+'</script>'))
$pagePath=Join-Path $job 'health.html'
[IO.File]::WriteAllText($pagePath,$page,$encoding)
Write-Host ("Prepared health page for Android {0} ({1}); sample SHA256 {2}" -f $receipt.versionName,$receipt.versionCode,$sampleHash)
Write-Host ("Standalone page: "+[IO.Path]::GetFullPath($pagePath))
if(-not $Publish){Write-Host 'Prepared locally. Pass -Publish to upload the standalone health page.';return}
$font=Join-Path $PSScriptRoot '../../Assets/BattleCities/UI/Art/shared/fonts/BarlowCondensed-Bold.ttf'
$objects=@(
 @('health/ping.json','application/json',$probePath),
 @('health/config.json','application/json',(Join-Path $source 'config.json')),
 @('health/barlow-condensed-bold.ttf','font/ttf',$font),
 @('health.html','text/html; charset=utf-8',$pagePath)
)
foreach($object in $objects){
 & rtk proxy cf r2 objects put $object[0] --bucket-name battlecities-game-assets --content-type $object[1] --content-length ([string](Get-Item -LiteralPath $object[2]).Length) --file $object[2]
 if($LASTEXITCODE -ne 0){throw "Upload failed: $($object[0])"}
}
Write-Host 'Published: https://assets.battlecities.com/health.html'
