$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work = Join-Path ([IO.Path]::GetTempPath()) ('battlecities-cherry-proof-' + [guid]::NewGuid())
$source = Join-Path $work 'source'
$site = Join-Path $work 'site'
New-Item -ItemType Directory -Path (Join-Path $source 'Build') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $source 'index.html'), '<html><head><title>Test</title><link rel="stylesheet" href="TemplateData/style.css"></head><body><canvas id="unity-canvas"></canvas><div id="unity-logo"></div></body></html>')
[IO.File]::WriteAllText((Join-Path $source 'Build/game.loader.js'), '')
$archive = Join-Path $work 'web.zip'
Compress-Archive -Path (Join-Path $source '*') -DestinationPath $archive
& (Join-Path $root 'Tools/Publishing/Prepare-CherryWebSite.ps1') -ArchivePath $archive -Destination $site
$proof = Get-Content -Raw -LiteralPath (Join-Path $site '.well-known/cherry-app.json') | ConvertFrom-Json
if ($proof.appId -ne 'c0dcc3df-8b13-445b-afa1-573c8c567315') { throw 'Cherry domain proof has the wrong app ID.' }
$domain = (Get-Content -Raw -LiteralPath (Join-Path $site 'CNAME')).Trim()
if ($domain -ne 'cherry.battlecities.com') { throw 'Wrong Cherry custom domain.' }
$index = Get-Content -Raw -LiteralPath (Join-Path $site 'index.html')
if (-not $index.Contains('<script src="cherry.js"></script>') -or -not (Test-Path -LiteralPath (Join-Path $site 'cherry.js'))) { throw 'Cherry bootstrap is missing.' }
$normalSite = Join-Path $work 'normal-site'
& (Join-Path $root 'Tools/Publishing/Prepare-WebSite.ps1') -ArchivePath $archive -Destination $normalSite
if (Test-Path -LiteralPath (Join-Path $normalSite '.well-known/cherry-app.json')) { throw 'Cherry proof leaked into the normal artifact.' }
if (Test-Path -LiteralPath (Join-Path $normalSite 'cherry.js')) { throw 'Cherry bootstrap leaked into the normal artifact.' }
Write-Output 'Cherry proof and custom domain are isolated to the Cherry site artifact.'
