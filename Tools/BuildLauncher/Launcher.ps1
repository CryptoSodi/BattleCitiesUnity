[CmdletBinding()]
param([string]$PlanPath,[switch]$DryRun,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Launcher.Core.ps1')
Set-Location -LiteralPath $script:GameRoot

function Read-LauncherChoice([string]$Prompt,[string[]]$Allowed,[string]$Default) {
    while($true){
        $value=(Read-Host "$Prompt [$Default]").Trim().ToLowerInvariant()
        if(-not $value){$value=$Default}
        if($value -in $Allowed){return $value}
        Write-Host ('Choose '+($Allowed -join ', ')) -ForegroundColor Yellow
    }
}

function Read-LauncherVersion {
    $current=Get-LocalGameVersion;$v=[version]$current;$next="$($v.Major).$($v.Minor).$($v.Build+1)"
    Write-Host "`nCurrent game version: $current"
    $choice=Read-LauncherChoice "Version: 1 = next ($next), 2 = keep ($current), 3 = enter manually" @('1','2','3') '1'
    if($choice -eq '1'){return $next};if($choice -eq '2'){return $current}
    while($true){$entered=(Read-Host 'Game version (for example 1.0.19)').Trim();if($entered -match '^\d+\.\d+\.\d+$'){return $entered};Write-Host 'Enter three numbers separated by dots.'}
}

function Read-LauncherTargets([string]$Kind) {
    if($Kind -eq 'web'){
        Write-Host "`n1 Localhost (test API)    2 Play (mainnet)    3 Test (devnet)    4 Magic (devnet)    5 All 3 websites    0 Build only"
        $map=@{'1'='localhost';'2'='play';'3'='test';'4'='magic'}
    }else{
        Write-Host "`n1 Install on phone    2 Publish GitHub APK    3 Upload PSG1 draft    4 All three    0 Build only"
        $map=@{'1'='install';'2'='github';'3'='psg1'}
    }
    while($true){
        $text=(Read-Host 'Destinations (comma-separated; default 0)').Trim();if(-not $text -or $text -eq '0'){return @()}
        if($Kind -eq 'web' -and $text -eq '5'){return @('play','test','magic')}
        if($Kind -eq 'android' -and $text -eq '4'){return @('install','github','psg1')}
        $parts=@($text -split ','|ForEach-Object {$_.Trim()})
        if(@($parts|Where-Object {-not $map.ContainsKey($_)}).Count -eq 0){return @($parts|ForEach-Object {$map[$_]}|Select-Object -Unique)}
        Write-Host 'Use the numbers shown above; all/build-only must be selected on its own.' -ForegroundColor Yellow
    }
}

function Read-LauncherDevice($Settings) {
    $devices=@(Get-LauncherDevices)
    if(-not $devices.Count){throw 'No authorized Android device. Reconnect USB, unlock the phone and accept USB debugging.'}
    for($i=0;$i -lt $devices.Count;$i++){Write-Host ("{0} {1}" -f ($i+1),$devices[$i].description)}
    $default='1';for($i=0;$i -lt $devices.Count;$i++){if($devices[$i].serial -eq $Settings.androidSerial){$default=[string]($i+1)}}
    $chosen=Read-LauncherChoice 'Select phone' @(1..$devices.Count|ForEach-Object {[string]$_}) $default
    return $devices[[int]$chosen-1].serial
}

function Invoke-LauncherRun($Plan,$Settings,[switch]$Preview) {
    if($Preview){Invoke-LauncherPlan $Plan $Settings '' -DryRun;return}
    $runs=Join-Path $script:GameRoot 'Builds/Launcher';[IO.Directory]::CreateDirectory($runs)|Out-Null
    $job=Join-Path $runs ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
    [IO.Directory]::CreateDirectory($job)|Out-Null
    # This lock covers a complete launcher run, including the child publishing scripts.
    $lock=$null;$transcript=$false
    try{
        try{$lock=[IO.File]::Open((Join-Path $runs 'launcher.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Another Battle Cities launcher run is active. Wait for it to finish before starting another.'}
        Start-Transcript -Path (Join-Path $job 'launcher.log')|Out-Null;$transcript=$true
        $null=Invoke-LauncherPlan $Plan $Settings $job
    }finally{if($transcript){Stop-Transcript|Out-Null};if($lock){$lock.Dispose()}}
}

$settings=Get-LauncherSettings
if($CheckOnly){
    Write-Host "Project: $script:GameRoot"
    foreach($name in @('unity','git','gh','python','cf','ssh','scp')){
        try{Write-Host "$name : $(Get-GameProgram $name)"}catch{Write-Host "$name : missing" -ForegroundColor Yellow}
    }
    Write-Host "Game version: $(Get-LocalGameVersion); suggested Android code: $(Get-NextAndroidVersionCode)"
    Write-Host "ADB: $(Get-LauncherAdb)"
    Write-Host "SSH key available: $([bool]($settings.sshKey -and (Test-Path -LiteralPath $settings.sshKey)))"
    Write-Host 'No builds, network changes or publication performed.'
    exit 0
}
if($PlanPath){
    try{$plan=Get-Content -LiteralPath $PlanPath -Raw|ConvertFrom-Json;Invoke-LauncherRun $plan $settings -Preview:$DryRun;exit 0}catch{Write-Error $_ -ErrorAction Continue;exit 1}
}

while($true){
    Write-Host "`n=== BATTLE CITIES BUILDER ===" -ForegroundColor Cyan
    Write-Host "1 Web    2 Android APK    3 Open output folder    4 Settings    0 Exit"
    $choice=Read-LauncherChoice 'Choose' @('0','1','2','3','4') '1'
    if($choice -eq '0'){break}
    if($choice -eq '3'){
        $outputs=Join-Path $script:GameRoot 'Builds/Launcher';[IO.Directory]::CreateDirectory($outputs)|Out-Null
        Start-Process explorer.exe -ArgumentList (ConvertTo-WindowsArgument $outputs);continue
    }
    if($choice -eq '4'){
        $local=Join-Path $PSScriptRoot 'settings.local.json'
        if(-not(Test-Path -LiteralPath $local)){[IO.File]::WriteAllText($local,($settings|ConvertTo-Json))}
        $edit=Start-Process notepad.exe -ArgumentList (ConvertTo-WindowsArgument $local) -PassThru;$edit.WaitForExit()
        try{$settings=Get-LauncherSettings}catch{Write-Host $_.Exception.Message -ForegroundColor Red}
        continue
    }
    try{
        $kind=if($choice -eq '1'){'web'}else{'android'}
        $version=Read-LauncherVersion;$targets=@(Read-LauncherTargets $kind)
        $code=0;$serial=''
        if($kind -eq 'android'){
            $suggested=Get-NextAndroidVersionCode
            while($true){
                $value=(Read-Host "Android build/version code [$suggested]").Trim();if(-not $value){$value=[string]$suggested}
                $parsed=0;if([int]::TryParse($value,[ref]$parsed) -and $parsed -gt 0 -and $parsed -le 2100000000){$code=$parsed;break}
                Write-Host 'Enter a positive Android version code, maximum 2100000000.'
            }
            if('install' -in $targets){$serial=Read-LauncherDevice $settings}
        }
        $plan=[pscustomobject]@{kind=$kind;version=$version;androidCode=$code;serial=$serial;targets=$targets}
        Write-Host "`nVersion $version / Android code $code" -ForegroundColor Cyan
        Get-LauncherSteps $plan|ForEach-Object {Write-Host " - $_"}
        if(@($targets|Where-Object {$_ -in @('play','test','magic','github')}).Count){Write-Host 'Publishing commits and pushes all non-ignored project changes.'}
        $action=Read-LauncherChoice 'B = build/run, P = preview only, 0 = back' @('b','p','0') 'b'
        if($action -eq '0'){continue}
        Invoke-LauncherRun $plan $settings -Preview:($action -eq 'p' -or $DryRun)
    }catch{Write-Host $_.Exception.Message -ForegroundColor Red}
    $null=Read-Host 'Press Enter to return to the menu'
}
