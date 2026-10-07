[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$Destination,
    [ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSha256,
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository = 'CryptoSodi/BattleCitiesUnity',
    [string]$ReleaseTag = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot 'Release-Functions.ps1')

$archiveFile = (Resolve-Path -LiteralPath $ArchivePath).Path
$siteDirectory = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $siteDirectory) {
    throw 'Use a new, empty destination path for the Pages artifact.'
}
if ($ExpectedSha256 -and (Get-ReleaseSha256 $archiveFile) -ne $ExpectedSha256) {
    throw 'Web ZIP checksum does not match the release manifest.'
}

$archive = [IO.Compression.ZipFile]::OpenRead($archiveFile)
try {
    if (-not ($archive.Entries | Where-Object { $_.FullName -eq 'index.html' })) {
        throw 'The web ZIP must have index.html at its root.'
    }
    if (-not ($archive.Entries | Where-Object { $_.FullName -like 'Build/*.loader.js' })) {
        throw 'The web ZIP is missing its Unity loader.'
    }
    $sitePrefix = $siteDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($entry in $archive.Entries) {
        $entryPath = [IO.Path]::GetFullPath((Join-Path $siteDirectory $entry.FullName))
        if (-not $entryPath.StartsWith($sitePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Archive entry escapes the Pages directory: $($entry.FullName)"
        }
    }
} finally {
    $archive.Dispose()
}

[IO.Compression.ZipFile]::ExtractToDirectory($archiveFile, $siteDirectory)

# Apply the current viewport presentation to older, checksum-verified releases too.
# The compiled game and its original release archive remain unchanged.
$viewportSource = Join-Path $PSScriptRoot '../../Assets/WebGLTemplates/BattleCities/TemplateData/battle-cities-viewport.css'
if (-not (Test-Path -LiteralPath $viewportSource)) { throw 'The Battle Cities viewport stylesheet is missing.' }
$indexPath = Join-Path $siteDirectory 'index.html'
$index = [IO.File]::ReadAllText($indexPath)
if ($index -notmatch 'id="unity-canvas"' -or $index -notmatch '</head>') {
    throw 'Expected a Unity web page with a canvas and HTML head.'
}
$viewportName = 'battle-cities-viewport.' + (Get-ReleaseSha256 $viewportSource).Substring(0, 12) + '.css'
$templateDirectory = Join-Path $siteDirectory 'TemplateData'
[IO.Directory]::CreateDirectory($templateDirectory) | Out-Null
Copy-Item -LiteralPath $viewportSource -Destination (Join-Path $templateDirectory $viewportName)
$viewportLink = '<link id="battle-cities-viewport" rel="stylesheet" href="TemplateData/' + $viewportName + '">'
$index = [regex]::Replace($index, '<link id="battle-cities-viewport"[^>]*>\s*', '')
$index = $index.Replace('</head>', "    $viewportLink`n  </head>")
$index = [regex]::Replace($index, '<title>[^<]*</title>', '<title>Battle Cities</title>')
if ($index -notmatch '<meta[^>]+name="viewport"') {
    $index = $index.Replace('</head>', '    <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">' + "`n  </head>")
}
# Replace legacy Unity loading artwork with the current game's template assets.
# Validate the PNG signature so a Git LFS pointer cannot become a broken logo.
$brandDirectory = Join-Path $PSScriptRoot '../../Assets/WebGLTemplates/BattleCities/TemplateData'
$logoSource = Join-Path $brandDirectory 'battle-cities-logo.png'
$styleSource = Join-Path $brandDirectory 'style.css'
$logoBytes = [IO.File]::ReadAllBytes($logoSource)
if ($logoBytes.Length -lt 8 -or [BitConverter]::ToString($logoBytes, 0, 8) -ne '89-50-4E-47-0D-0A-1A-0A') {
    throw 'The game logo is not a PNG. Download Git LFS assets before preparing the site.'
}
$logoPattern = '<div id="unity-logo"\s*>\s*</div>|<img id="unity-logo"[^>]*>'
if ([regex]::Matches($index, $logoPattern).Count -ne 1 -or -not $index.Contains('href="TemplateData/style.css"')) {
    throw 'Expected exactly one Unity loading logo and the template stylesheet.'
}
$logoName = 'battle-cities-logo.' + (Get-ReleaseSha256 $logoSource).Substring(0, 12) + '.png'
$styleName = 'battle-cities-style.' + (Get-ReleaseSha256 $styleSource).Substring(0, 12) + '.css'
Copy-Item -LiteralPath $logoSource -Destination (Join-Path $templateDirectory $logoName)
Copy-Item -LiteralPath $styleSource -Destination (Join-Path $templateDirectory $styleName)
$logoUrl = 'TemplateData/' + $logoName
$logoElement = '<img id="unity-logo" src="' + $logoUrl + '" alt="Battle Cities" width="1254" height="1254" fetchpriority="high">'
$index = [regex]::Replace($index, $logoPattern, $logoElement)
$index = $index.Replace('href="TemplateData/style.css"', 'href="TemplateData/' + $styleName + '"')
$index = [regex]::Replace($index, '<link[^>]+rel="(?:shortcut )?icon"[^>]*>', '<link rel="icon" type="image/png" href="' + $logoUrl + '">')
$index = $index.Replace('</head>', '    <link rel="preload" as="image" href="' + $logoUrl + '">' + "`n  </head>")
[IO.File]::WriteAllText($indexPath, $index, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $siteDirectory '.nojekyll'), '')
$releaseInfo = [ordered]@{
    repository = $Repository
    release = $ReleaseTag
    apk = "https://github.com/$Repository/releases/latest/download/battlecities.apk"
}
[IO.File]::WriteAllText((Join-Path $siteDirectory 'release-info.json'), ($releaseInfo | ConvertTo-Json))
Write-Output "Pages artifact ready: $siteDirectory"
