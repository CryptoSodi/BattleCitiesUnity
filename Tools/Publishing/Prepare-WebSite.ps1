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
[IO.File]::WriteAllText((Join-Path $siteDirectory '.nojekyll'), '')
$releaseInfo = [ordered]@{
    repository = $Repository
    release = $ReleaseTag
    apk = "https://github.com/$Repository/releases/latest/download/BattleCities-0.1.1.apk"
}
[IO.File]::WriteAllText((Join-Path $siteDirectory 'release-info.json'), ($releaseInfo | ConvertTo-Json))
Write-Output "Pages artifact ready: $siteDirectory"
