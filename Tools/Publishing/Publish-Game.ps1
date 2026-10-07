[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'Directory')]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^v?[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version,
    [Parameter(Mandatory = $true, ParameterSetName = 'Directory')][string]$WebBuildPath,
    [Parameter(Mandatory = $true, ParameterSetName = 'Archive')][string]$WebArchivePath,
    [Parameter(Mandatory = $true)][string]$ApkPath,
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository = 'CryptoSodi/BattleCitiesUnity',
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$LegacyRepository = 'CryptoSodi/BattleCitiesUnity-Releases',
    [string]$LegacyTag = 'v0.1.1',
    [ValidatePattern('^[a-fA-F0-9]{40}$')][string]$TargetCommit,
    [switch]$CarryForwardApk,
    [switch]$SkipLegacyApkMirror,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot 'Release-Functions.ps1')

function Invoke-GitHub([string[]]$Arguments) {
    $response = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: gh $($Arguments -join ' ')" }
    return ($response -join "`n")
}

$number = $Version -replace '^v', ''
$tag = "v$number"
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputDirectory = Join-Path $projectDirectory "Builds/Releases/$number/Publish"
$apkFile = (Resolve-Path -LiteralPath $ApkPath).Path
$apkName = 'battlecities.apk'
$legacyApkName = 'BattleCities-0.1.1.apk'
$zipName = "BattleCities-$number-Web.zip"

# Keep binaries in the ignored Builds folder; publish them as release assets.
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$webZip = Join-Path $outputDirectory $zipName
$stagedApk = Join-Path $outputDirectory $apkName
$compatibilityApk = Join-Path $outputDirectory $legacyApkName
if ($PSCmdlet.ParameterSetName -eq 'Archive') {
    $sourceZip = (Resolve-Path -LiteralPath $WebArchivePath).Path
    if ($sourceZip -ne $webZip) { Copy-Item -LiteralPath $sourceZip -Destination $webZip -Force }
} else {
    $webDirectory = (Resolve-Path -LiteralPath $WebBuildPath).Path
    if (-not (Test-Path -LiteralPath (Join-Path $webDirectory 'index.html'))) {
        throw 'WebBuildPath must be the Unity web build directory containing index.html.'
    }
    if ($outputDirectory.StartsWith($webDirectory.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Publishing output must be outside the web build input directory.'
    }
    if (Test-Path -LiteralPath $webZip) { Remove-Item -LiteralPath $webZip }
    # Windows PowerShell's .NET Framework can create backslash ZIP entries.
    # Use portable entry names so validation and Linux Pages extraction agree.
    $archive = [IO.Compression.ZipFile]::Open($webZip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $rootLength = $webDirectory.TrimEnd('\', '/').Length + 1
        foreach ($file in [IO.Directory]::EnumerateFiles($webDirectory, '*', [IO.SearchOption]::AllDirectories)) {
            $entryName = $file.Substring($rootLength).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file, $entryName,
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
}
if ($apkFile -ne $stagedApk) { Copy-Item -LiteralPath $apkFile -Destination $stagedApk -Force }
Copy-Item -LiteralPath $stagedApk -Destination $compatibilityApk -Force

foreach ($artifact in @($webZip, $stagedApk)) {
    $archive = [IO.Compression.ZipFile]::OpenRead($artifact)
    try {
        if ($artifact -eq $webZip) {
            if (-not ($archive.Entries | Where-Object { $_.FullName -eq 'index.html' }) -or
                -not ($archive.Entries | Where-Object { $_.FullName -like 'Build/*.loader.js' })) {
                throw 'Web archive must contain index.html and its Unity loader at the expected paths.'
            }
        } elseif (-not ($archive.Entries | Where-Object { $_.FullName -eq 'AndroidManifest.xml' })) {
            throw 'APK is missing AndroidManifest.xml.'
        }
    } finally { $archive.Dispose() }
}

$checksums = foreach ($artifact in @($webZip, $stagedApk, $compatibilityApk)) {
    '{0}  {1}' -f (Get-ReleaseSha256 $artifact),
        [IO.Path]::GetFileName($artifact)
}
$checksumFile = Join-Path $outputDirectory 'SHA256SUMS.txt'
[IO.File]::WriteAllLines($checksumFile, $checksums, [Text.UTF8Encoding]::new($false))
$notesFile = Join-Path $outputDirectory 'release-notes.md'
$notes = @"
Battle Cities $number

- Play: https://play.battlecities.com/
- APK: https://github.com/$Repository/releases/latest/download/$apkName
- Web archive and SHA256 checksums are attached to this release.
- The APK is named battlecities.apk. A compatibility copy preserves previously distributed download links.
"@
[IO.File]::WriteAllText($notesFile, $notes, [Text.UTF8Encoding]::new($false))
if ($CarryForwardApk) {
    [IO.File]::AppendAllText($notesFile, "`nThis release builds WebGL. The existing Android APK is carried forward unchanged.`n")
}
Write-Output "Prepared release assets: $outputDirectory"
if ($PrepareOnly) { return }
if (-not $PSCmdlet.ShouldProcess($Repository, "Publish $tag and update the existing public APK link")) { return }

$releasePages = Invoke-GitHub -Arguments @('api', "repos/$Repository/releases?per_page=100", '--paginate', '--slurp')
$existing = @(foreach ($releasePage in ($releasePages | ConvertFrom-Json)) {
    foreach ($release in $releasePage) {
        if ($release.tag_name -eq $tag) { $release }
    }
})
if ($existing.Count -gt 0 -and -not $existing[0].draft) {
    throw 'This release is already published. Use a new version to preserve historical release assets.'
}
$assets = @($webZip, $stagedApk, $compatibilityApk, $checksumFile)
if ($existing.Count -gt 0) {
    Invoke-GitHub -Arguments (@('release', 'upload', $tag, '--repo', $Repository, '--clobber') + $assets) | Out-Null
    Invoke-GitHub -Arguments @('release', 'edit', $tag, '--repo', $Repository, '--notes-file', $notesFile) | Out-Null
    if ($TargetCommit) { Invoke-GitHub -Arguments @('release', 'edit', $tag, '--repo', $Repository, '--target', $TargetCommit) | Out-Null }
} else {
    $target = if ($TargetCommit) { $TargetCommit } else { 'main' }
    Invoke-GitHub -Arguments (@('release', 'create', $tag, '--repo', $Repository, '--target', $target, '--draft',
        '--title', "Battle Cities $number", '--notes-file', $notesFile) + $assets) | Out-Null
}

if (-not $SkipLegacyApkMirror) {
    $legacyRelease = Invoke-GitHub -Arguments @('api', "repos/$LegacyRepository/releases/tags/$LegacyTag") |
        ConvertFrom-Json
    $legacyApk = @($legacyRelease.assets | Where-Object { $_.name -eq $legacyApkName })
    $legacyDigest = if ($legacyApk.Count -gt 0) { $legacyApk[0].digest } else { $null }
    $apkDigest = 'sha256:' + (Get-ReleaseSha256 $stagedApk)
    if ($legacyDigest -ne $apkDigest) {
        Invoke-GitHub -Arguments @('release', 'upload', $LegacyTag, $compatibilityApk, '--repo', $LegacyRepository, '--clobber') | Out-Null
    }
    Write-Output "Existing APK link preserved: https://github.com/$LegacyRepository/releases/download/$LegacyTag/$legacyApkName"
}

Invoke-GitHub -Arguments @('release', 'edit', $tag, '--repo', $Repository, '--draft=false', '--latest') | Out-Null
Write-Output "Published: https://github.com/$Repository/releases/tag/$tag"
Write-Output 'GitHub Actions will deploy the web archive to GitHub Pages.'
