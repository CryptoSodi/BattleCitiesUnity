[CmdletBinding()]
param([string]$Version, [string]$CommitMessage, [string]$ApkPath, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Automation-Functions.ps1')
$lock = $null
try {
    $latest = Get-LatestGameRelease
    $Version = Read-GameVersion $Version (Get-SuggestedWebVersion $latest) -CheckOnly:$CheckOnly
    if ([version]$Version -le [version]($latest.tag_name -replace '^v', '')) {
        throw "Choose a version newer than $($latest.tag_name). Published releases are not overwritten."
    }
    $null = Assert-GameGitReady
    $null = Assert-GameBuildReady WebGL
    if ($ApkPath) { $ApkPath = (Resolve-Path -LiteralPath $ApkPath).Path }
    Write-Host "Web release: v$Version -> https://play.battlecities.com/"
    if ($CheckOnly) { Write-Host 'Check completed. No build, commit, push, or deployment started.'; exit 0 }
    $lock = Enter-GameAutomation
    $web = Invoke-GameBuild WebGL $Version
    $carried = -not $ApkPath
    if ($carried) { $ApkPath = Get-CarriedGameApk $latest $Version }
    $publish = Join-Path $PSScriptRoot 'Publish-Game.ps1'
    & $publish -Version $Version -WebBuildPath $web -ApkPath $ApkPath -PrepareOnly
    $staging = Join-Path $script:GameRoot "Builds/Releases/$Version/Publish"
    $archive = Join-Path $staging "BattleCities-$Version-Web.zip"
    $validation = Join-Path $script:GameRoot ('Builds/Automation/site-' + [guid]::NewGuid().ToString('N'))
    & (Join-Path $PSScriptRoot 'Prepare-WebSite.ps1') -ArchivePath $archive -Destination $validation `
        -ExpectedSha256 (Get-ReleaseSha256 $archive) -Repository $script:GameRepository -ReleaseTag "v$Version"
    if (-not $CommitMessage) { $CommitMessage = "Release Battle Cities web $Version" }
    $commit = Invoke-GameCommitPush $CommitMessage
    $publishedAfter = [DateTime]::UtcNow
    & $publish -Version $Version -WebArchivePath $archive -ApkPath (Join-Path $staging 'battlecities.apk') `
        -TargetCommit $commit -CarryForwardApk:$carried -SkipLegacyApkMirror:$carried
    Wait-GamePages $Version $commit $publishedAfter
    Write-Host "Done. Play: https://play.battlecities.com/"
    Write-Host "Release: https://github.com/$script:GameRepository/releases/tag/v$Version"
    exit 0
} catch { Write-Error $_ -ErrorAction Continue; exit 1 }
finally { if ($lock) { $lock.Dispose() } }
