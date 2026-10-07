[CmdletBinding()]
param(
    [string]$Version,
    [ValidateRange(0, 2100000000)][int]$VersionCode = 0,
    [ValidateSet('APK', 'AAB')][string]$Format,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Automation-Functions.ps1')
$lock = $null
try {
    if (-not $Format) {
        $Format = 'APK'
        if (-not $CheckOnly) {
            $answer = Read-Host 'Android format: 1 = APK, 2 = AAB [1]'
            switch ($answer.Trim().ToUpperInvariant()) {
                { $_ -in @('', '1', 'APK') } { $Format = 'APK'; break }
                { $_ -in @('2', 'AAB') } { $Format = 'AAB'; break }
                default { throw 'Choose 1/APK or 2/AAB.' }
            }
        }
    }
    $Format = $Format.ToUpperInvariant()
    $Version = Read-GameVersion $Version (Get-LocalGameVersion) -CheckOnly:$CheckOnly
    if ($VersionCode -eq 0) { $VersionCode = Get-NextAndroidVersionCode }
    Write-Host "Android ${Format}: version $Version; version code $VersionCode"
    $null = Assert-GameBuildReady Android
    if ($CheckOnly) { Write-Host 'Check completed. No settings changed and no build started.'; exit 0 }
    $lock = Enter-GameAutomation
    $artifact = Invoke-GameBuild Android $Version $VersionCode $Format
    Write-Host "${Format} ready: $artifact"
    if ($Format -eq 'APK') {
        Write-Host 'Use Deploy-Web -ApkPath to publish this APK with a web release.'
    } else {
        Write-Host 'Android App Bundle ready for your store upload.'
    }
    exit 0
} catch { Write-Error $_ -ErrorAction Continue; exit 1 }
finally { if ($lock) { $lock.Dispose() } }
