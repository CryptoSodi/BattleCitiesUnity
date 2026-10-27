[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ManifestPath,[Parameter(Mandatory=$true)][string]$BucketName)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../Publishing/Automation-Functions.ps1')
if($BucketName -ne 'battlecities-game-assets'){throw 'Unexpected artwork bucket'}
$manifest=Get-Content -LiteralPath $ManifestPath -Raw|ConvertFrom-Json
if($manifest.sha256 -notmatch '^[a-f0-9]{64}$' -or $manifest.url -ne "https://assets.battlecities.com/android/art/$($manifest.sha256).bundle"){throw 'Unexpected artwork manifest'}
$bundle=Join-Path (Split-Path -Parent $ManifestPath) ($manifest.sha256+'.bundle')
if((Get-ReleaseSha256 $bundle) -ne $manifest.sha256 -or (Get-Item -LiteralPath $bundle).Length -ne $manifest.bytes){throw 'Artwork checksum/size mismatch'}
# Run through PowerShell so an npm-installed cf.cmd works without cmd.exe string escaping.
$cf=Get-GameProgram cf
& $cf r2 objects put "android/art/$($manifest.sha256).bundle" --bucket-name $BucketName --content-type application/octet-stream --file $bundle
if($LASTEXITCODE -ne 0){throw 'Cloudflare artwork upload failed'}
