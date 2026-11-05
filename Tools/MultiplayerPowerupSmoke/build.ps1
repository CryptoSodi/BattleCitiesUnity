param([Parameter(Mandatory=$true)][string]$OutputPath)
$env:BC_POWERUP_SMOKE_OUTPUT = $OutputPath
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
unity run C:/repos/Unity/BattleCitiesUnity --editor-version 6000.6.0f1 --timeout 1200 --no-tail --log-file (Join-Path $OutputPath 'unity-build.log') --non-interactive -- -executeMethod BattleCities.Editor.PowerupSmokeBuild.Windows
exit $LASTEXITCODE
