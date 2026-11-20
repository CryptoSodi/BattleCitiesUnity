[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$Destination,
    [AllowEmptyString()][ValidatePattern('^$|^[a-fA-F0-9]{64}$')][string]$ExpectedSha256,
    [string]$ReleaseTag = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# A separate artifact: this never alters the normal play site artifact.
$arguments = @{ ArchivePath = $ArchivePath; Destination = $Destination; ReleaseTag = $ReleaseTag }
if ($ExpectedSha256) { $arguments.ExpectedSha256 = $ExpectedSha256 }
& (Join-Path $PSScriptRoot 'Prepare-WebSite.ps1') @arguments
$site = [IO.Path]::GetFullPath($Destination)
$wellKnown = Join-Path $site '.well-known'
[IO.Directory]::CreateDirectory($wellKnown) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Cherry/cherry-app.json') -Destination (Join-Path $wellKnown 'cherry-app.json') -Force
$cherryScript = Join-Path $PSScriptRoot 'Cherry/cherry.js'
if (-not (Test-Path -LiteralPath $cherryScript)) { throw 'Cherry SDK bundle is missing. Run npm run build in Tools/Wallet.' }
Copy-Item -LiteralPath $cherryScript -Destination (Join-Path $site 'cherry.js') -Force
$indexPath = Join-Path $site 'index.html'
$index = [IO.File]::ReadAllText($indexPath)
if (-not $index.Contains('</head>')) { throw 'The web page has no head element.' }
$index = $index.Replace('</head>', '<script src="cherry.js"></script>' + "`n</head>")
[IO.File]::WriteAllText($indexPath, $index, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $site 'CNAME'), "cherry.battlecities.com`n", [Text.UTF8Encoding]::new($false))
Write-Output "Cherry GitHub Pages artifact ready: $site"
