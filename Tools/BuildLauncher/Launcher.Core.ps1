Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Project.ps1')

function Get-LauncherSettings {
    $settings=Get-Content (Join-Path $script:LauncherHome 'settings.example.json') -Raw|ConvertFrom-Json
    $local=Join-Path $script:LauncherHome 'settings.local.json'
    if(Test-Path -LiteralPath $local){
        $overrides=Get-Content -LiteralPath $local -Raw|ConvertFrom-Json
        foreach($property in $overrides.PSObject.Properties){
            if(-not $settings.PSObject.Properties[$property.Name]){throw "Unknown launcher setting: $($property.Name)"}
            $settings.($property.Name)=$property.Value
        }
    }
    if(-not $settings.sshKey){
        $candidate=Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads/ssh-key-2026-07-29.key'
        $settings.sshKey=if($env:BATTLECITIES_TEST_SSH_KEY){$env:BATTLECITIES_TEST_SSH_KEY}elseif(Test-Path -LiteralPath $candidate){$candidate}else{''}
    }
    return $settings
}

function Assert-LauncherPlan($Plan) {
    if($Plan.kind -notin @('web','android')){throw 'Choose web or android.'}
    if($Plan.version -notmatch '^\d+\.\d+\.\d+$'){throw 'Version must have three numbers, for example 1.0.19.'}
    $allowed=if($Plan.kind -eq 'web'){@('localhost','play','test','magic')}else{@('install','github','psg1')}
    foreach($target in @($Plan.targets)){if($target -notin $allowed){throw "Invalid destination: $target"}}
    $parsedCode=0
    if($Plan.kind -eq 'android' -and (-not [int]::TryParse([string]$Plan.androidCode,[ref]$parsedCode) -or $parsedCode -lt 1 -or $parsedCode -gt 2100000000)){throw 'Android version code must be an integer from 1 to 2100000000.'}
}

function Get-LauncherSteps($Plan) {
    Assert-LauncherPlan $Plan
    if($Plan.kind -eq 'web'){
        'Build WebGL once (runtime hostname selects mainnet/devnet)'
        if(@($Plan.targets|Where-Object {$_ -ne 'localhost'}).Count){'Commit and push current project source'}
        if('play' -in $Plan.targets){'Package web-only release; publish GitHub Pages; verify production'}
        if('test' -in $Plan.targets){'Deploy and verify Test / devnet'}
        if('magic' -in $Plan.targets){'Deploy and verify MagicBlock / devnet'}
        if('localhost' -in $Plan.targets){'Start localhost / test API and open browser'}
    }else{
        'Build signed mainnet ARM64 APK using existing signing identity'
        'Upload immutable OTA artwork and verify its complete checksum'
        if('github' -in $Plan.targets){'Commit and push source; publish verified Android GitHub release'}
        if('install' -in $Plan.targets){'Install on selected connected phone and launch'}
        if('psg1' -in $Plan.targets){'Upload APK to PSG1 draft; verify automatic analysis (no review submission)'}
        'Show local APK link and any published URLs'
    }
}

function Get-LauncherAdb {
    $version=([IO.File]::ReadAllText((Join-Path $script:GameRoot 'ProjectSettings/ProjectVersion.txt')) -split '\r?\n'|Where-Object {$_ -match '^m_EditorVersion: '}) -replace '^m_EditorVersion: ',''
    $adb="C:/Program Files/Unity/Hub/Editor/$version/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
    if(-not(Test-Path -LiteralPath $adb)){return Get-GameProgram adb}
    return $adb
}

function Get-LauncherDevices {
    $reply=Invoke-GameTool (Get-LauncherAdb) @('devices','-l')
    @($reply.Output -split '\r?\n'|ForEach-Object {
        if($_ -match '^(\S+)\s+device(?:\s|$)'){[pscustomobject]@{serial=$Matches[1];description=$_.Trim()}}
    })
}

function Save-LauncherResult($Result,[string]$Directory) {
    [IO.File]::WriteAllText((Join-Path $Directory 'result.json'),($Result|ConvertTo-Json -Depth 12))
}

function Publish-LauncherSource {
    return Invoke-GameCommitPush 'Update Battle Cities game and build launcher'
}

function Invoke-LauncherChild([string]$Script,[string[]]$Parameters=@()) {
    $path=if([IO.Path]::IsPathRooted($Script)){$Script}else{Join-Path $script:GameRoot $Script}
    $null=Invoke-GameTool powershell.exe (@('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',$path)+$Parameters) -Echo
}

function Assert-LauncherPrerequisites($Plan,$Settings) {
    Assert-LauncherPlan $Plan
    Assert-GameBuildDiskSpace $(if($Plan.kind -eq 'android'){'Android'}else{'WebGL'})
    foreach($name in @('unity','git','python')){$null=Get-GameProgram $name}
    $publish=@($Plan.targets|Where-Object {$_ -in @('play','test','magic','github')}).Count -gt 0
    if($publish){$null=Assert-GameGitReady}
    if('play' -in $Plan.targets -or 'github' -in $Plan.targets){
        $null=Invoke-GameTool gh @('auth','status')
        if('github' -in $Plan.targets){
            $tag="android-v$($Plan.version)-$($Plan.androidCode)"
            $existing=Invoke-GameTool gh @('release','view',$tag,'--repo',$script:GameRepository) -AllowFailure
            if($existing.ExitCode -eq 0){throw "Android release $tag already exists. Increase the version code before building."}
        }
        if('play' -in $Plan.targets){
            $latest=Get-LatestGameRelease
            if([version]$Plan.version -le [version]($latest.tag_name -replace '^v','')){throw "Production Web requires a version newer than $($latest.tag_name)."}
        }
    }
    if('test' -in $Plan.targets -or 'magic' -in $Plan.targets){
        if(-not $Settings.sshKey -or -not(Test-Path -LiteralPath $Settings.sshKey)){throw 'Set sshKey in settings.local.json or BATTLECITIES_TEST_SSH_KEY.'}
        if($Settings.sshHost -notmatch '^[A-Za-z0-9._-]+@[A-Za-z0-9.-]+$'){throw 'sshHost must be user@host, without shell arguments.'}
        foreach($name in @('ssh','scp')){$null=Get-GameProgram $name}
    }
    if($Plan.kind -eq 'android'){
        $null=Get-GameProgram cf
        foreach($path in @('D:/keys/battle-cities-dappstore.keystore','D:/keys/battle-cities-signing.xml','D:/keys/battle-cities-signing.txt')){
            if(-not(Test-Path -LiteralPath $path)){throw "Existing signing file missing: $path"}
        }
        if($Settings.remoteArtBaseUrl -ne 'https://assets.battlecities.com/android/art' -or $Settings.assetBucket -ne 'battlecities-game-assets'){throw 'Unexpected OTA artwork host/bucket. Configure a reviewed publishing adapter before changing it.'}
        if('install' -in $Plan.targets){
            $devices=@(Get-LauncherDevices)
            if(-not $Plan.serial -or $Plan.serial -notin @($devices|ForEach-Object serial)){throw 'The selected Android device is not connected/authorized.'}
            $installed=Invoke-GameTool (Get-LauncherAdb) @('-s',$Plan.serial,'shell','dumpsys','package','sol.battle.cities')
            if($installed.Output -match 'versionCode=(\d+)\s' -and [int]$Plan.androidCode -lt [int]$Matches[1]){throw "The phone has Android code $($Matches[1]); choose that code or higher."}
        }
        if('psg1' -in $Plan.targets){
            $null=Invoke-GameTool python @((Join-Path $script:LauncherHome 'playsolana.py'),'check','--endpoint',$Settings.psg1McpUrl,'--submission',$Settings.psg1SubmissionId) -Echo
        }
    }
}

function Publish-LauncherWeb([string]$Web,$Plan,[string]$Commit,[string]$Job) {
    $archive=Join-Path $Job ("BattleCities-$($Plan.version)-Web.zip")
    $null=Invoke-GameTool python @((Join-Path $script:LauncherHome 'artifacts.py'),'web-zip','--source',$Web,'--output',$archive) -Echo
    Invoke-LauncherChild 'Tools/Publishing/Prepare-WebSite.ps1' @('-ArchivePath',$archive,'-Destination',(Join-Path $Job 'validated-web'),'-ExpectedSha256',(Get-ReleaseSha256 $archive),'-Repository',$script:GameRepository,'-ReleaseTag',"v$($Plan.version)")
    $checksum=Join-Path $Job 'SHA256SUMS.txt'
    [IO.File]::WriteAllText($checksum,((Get-ReleaseSha256 $archive)+'  '+[IO.Path]::GetFileName($archive)+"`n"))
    $notes=Join-Path $Job 'web-notes.md'
    [IO.File]::WriteAllText($notes,"Battle Cities $($Plan.version)`n`nWeb-only release from $Commit. No APK is uploaded by this release.`n`nhttps://play.battlecities.com/`n")
    # Refuse all existing tags, including drafts: never silently replace somebody's release.
    $existing=Invoke-GameTool gh @('release','view',"v$($Plan.version)",'--repo',$script:GameRepository) -AllowFailure
    if($existing.ExitCode -eq 0){throw 'This release tag already exists. Choose a new version; existing releases are never overwritten.'}
    $null=Invoke-GameTool gh @('release','create',"v$($Plan.version)",$archive,$checksum,'--repo',$script:GameRepository,'--target',$Commit,'--title',"Battle Cities $($Plan.version)",'--notes-file',$notes,'--draft') -Echo
    $publishedAfter=[DateTime]::UtcNow
    $null=Invoke-GameTool gh @('release','edit',"v$($Plan.version)",'--repo',$script:GameRepository,'--draft=false','--latest=false') -Echo
    Wait-GamePages $Plan.version $Commit $publishedAfter
    return 'https://play.battlecities.com/'
}

function Stop-LauncherPreviousLocalhost([int]$Port) {
    try{$info=Invoke-RestMethod "http://127.0.0.1:$Port/__battlecities_build__" -TimeoutSec 2}catch{throw "Port $Port belongs to an unrecognized server; it has been left running."}
    $identity=[string]$info.identity
    if($identity -notmatch '^[a-zA-Z0-9-]+$'){throw "Port $Port has no valid launcher identity; it has been left running."}
    $receiptPath=Join-Path $script:GameRoot "Builds/Launcher/$identity/localhost-process.json"
    if(-not(Test-Path -LiteralPath $receiptPath)){throw "Port $Port has no saved launcher receipt; it has been left running."}
    $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json
    if($receipt.identity -ne $identity -or [int]$receipt.port -ne $Port){throw 'Previous localhost receipt does not match the running server.'}
    $serverPid=[int]$receipt.pid
    # Some Windows CIM providers return no listener even while this loopback server responds.
    # Verify the HTTP identity, saved PID and exact server command before using that fallback.
    $listeners=@(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    if($listeners.Count -gt 0 -and -not($listeners|Where-Object {$_.OwningProcess -eq $serverPid})){throw 'Previous localhost PID does not own this port; it has been left running.'}
    $running=Get-CimInstance Win32_Process -Filter "ProcessId = $serverPid"
    $command=([string]$running.CommandLine).Replace('\','/').ToLowerInvariant()
    $servers=@((Join-Path $script:LauncherHome 'serve.py'),(Join-Path $script:GameRoot 'Tools/BuildLauncher/serve.py'),(Join-Path $env:USERPROFILE 'Desktop/utils/BattleCities Builder/serve.py'))
    $recognized=@($servers|Where-Object {$command.Contains($_.Replace('\','/').ToLowerInvariant())}).Count -gt 0
    $portPattern='(?:^|\s)"?--port"?\s+"?'+$Port+'"?(?=\s|$)'
    if(-not $recognized -or -not $command.Contains($identity.ToLowerInvariant()) -or -not $command.Contains(([string]$receipt.root).Replace('\','/').ToLowerInvariant()) -or $command -notmatch $portPattern){throw 'Previous localhost process does not match the launcher server; it has been left running.'}
    $process=Get-Process -Id $serverPid -ErrorAction Stop
    Write-Host "Stopping previous Battle Cities localhost server ($identity)."
    Stop-Process -Id $serverPid -ErrorAction Stop
    if(-not $process.WaitForExit(5000)){throw 'Previous localhost server did not stop in time.'}
}

function Start-LauncherLocalhost([string]$Web,$Settings,[string]$Job) {
    $port=[int]$Settings.localhostPort
    if($port -lt 1024 -or $port -gt 65535){throw 'localhostPort must be 1024 to 65535.'}
    $probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
    $occupied=$false
    try{$probe.Start()}catch{$occupied=$true}finally{$probe.Stop()}
    if($occupied){
        Stop-LauncherPreviousLocalhost $port
        try{$probe.Start()}catch{throw "Port $port is still occupied after stopping the previous launcher server."}finally{$probe.Stop()}
    }
    $server=Join-Path $script:LauncherHome 'serve.py'
    $args=@($server,'--root',$Web,'--port',[string]$port,'--identity',([IO.Path]::GetFileName($Job)))
    $start=@{FilePath=(Get-GameProgram python);ArgumentList=(($args|ForEach-Object {ConvertTo-WindowsArgument $_}) -join ' ');WorkingDirectory=$script:GameRoot;WindowStyle='Hidden';PassThru=$true;RedirectStandardOutput=(Join-Path $Job 'localhost.log');RedirectStandardError=(Join-Path $Job 'localhost-errors.log')}
    $process=Start-Process @start
    try{
        $deadline=[DateTime]::UtcNow.AddSeconds(15)
        do{
            Start-Sleep -Milliseconds 300
            if($process.HasExited){throw 'Local server exited. See localhost-errors.log.'}
            try{$info=Invoke-RestMethod "http://127.0.0.1:$port/__battlecities_build__" -TimeoutSec 2}catch{$info=$null}
        }while((-not $info) -and [DateTime]::UtcNow -lt $deadline)
        if(-not $info -or $info.identity -ne [IO.Path]::GetFileName($Job)){throw 'Could not verify the new local server.'}
    }catch{if(-not $process.HasExited){$process.Kill()};throw}
    [IO.File]::WriteAllText((Join-Path $Job 'localhost-process.json'),([ordered]@{pid=$process.Id;port=$port;root=$Web;identity=$info.identity}|ConvertTo-Json))
    $url="http://localhost:$port/"
    Start-Process $url
    return $url
}

function Invoke-LauncherPlan($Plan,$Settings,[string]$Job,[switch]$DryRun) {
    Assert-LauncherPlan $Plan
    if($DryRun){Get-LauncherSteps $Plan|ForEach-Object {Write-Host "[PLAN] $_"};return}
    [IO.Directory]::CreateDirectory($Job)|Out-Null
    [IO.File]::WriteAllText((Join-Path $Job 'plan.json'),($Plan|ConvertTo-Json -Depth 5))
    $result=[ordered]@{status='running';kind=$Plan.kind;version=$Plan.version;completed=@();links=@();error=$null;buildPath=$null;sourceCommit=$null}
    Save-LauncherResult $result $Job
    try{
        Assert-LauncherPrerequisites $Plan $Settings
        if($Plan.kind -eq 'web'){
            $lock=Enter-GameAutomation
            try{$web=Invoke-GameBuild WebGL $Plan.version -TestNetwork}finally{$lock.Dispose()}
            $result.buildPath=$web;$result.completed+=@('build');Save-LauncherResult $result $Job
            if(@($Plan.targets|Where-Object {$_ -ne 'localhost'}).Count){$result.sourceCommit=Publish-LauncherSource;Save-LauncherResult $result $Job}
            if('play' -in $Plan.targets){$result.links+=@(Publish-LauncherWeb $web $Plan $result.sourceCommit $Job);$result.completed+=@('play');Save-LauncherResult $result $Job}
            foreach($target in @('test','magic')){
                if($target -in $Plan.targets){
                    $deploy=if($target -eq 'test'){'Deploy-TestWeb.ps1'}else{'Deploy-MagicWeb.ps1'}
                    Invoke-LauncherChild ("Tools/Publishing/$deploy") @('-Version',$Plan.version,'-ExistingBuildPath',$web,'-SshHost',$Settings.sshHost,'-SshKey',$Settings.sshKey)
                    $result.links+=@("https://$target.battlecities.com/");$result.completed+=@($target);Save-LauncherResult $result $Job
                }
            }
            if('localhost' -in $Plan.targets){$result.links+=@(Start-LauncherLocalhost $web $Settings $Job);$result.completed+=@('localhost')}
        }else{
            $before=@(Get-ChildItem (Join-Path $script:GameRoot 'Builds/Android') -Filter release-verification.json -Recurse -File -ErrorAction SilentlyContinue|ForEach-Object FullName)
            Invoke-LauncherChild 'Tools/Publishing/Build-SignedAndroid.ps1' @('-Version',$Plan.version,'-VersionCode',[string]$Plan.androidCode,'-RemoteArtBaseUrl',$Settings.remoteArtBaseUrl)
            $new=@(Get-ChildItem (Join-Path $script:GameRoot "Builds/Android/$($Plan.version)-v$($Plan.androidCode)") -Filter release-verification.json -Recurse -File|Where-Object {$_.FullName -notin $before})
            if($new.Count -ne 1){throw 'Could not uniquely identify this APK verification receipt.'}
            $build=$new[0].DirectoryName;$apk=Join-Path $build 'battlecities.apk'
            Write-Host "APK created: $apk" -ForegroundColor Green
            Write-Host ([uri]$apk).AbsoluteUri
            $result.buildPath=$apk;$result.completed+=@('build');$result.links+=@($apk);Save-LauncherResult $result $Job
            $manifestPath=Join-Path $build 'RemoteArt/content-manifest.json';$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
            $bundle=Join-Path $build ("RemoteArt/$($manifest.sha256).bundle")
            if((Get-ReleaseSha256 $bundle) -ne $manifest.sha256 -or (Get-Item $bundle).Length -ne $manifest.bytes){throw 'Local OTA bundle checksum/size mismatch.'}
            Invoke-LauncherChild (Join-Path $script:LauncherHome 'Upload-AndroidArt.ps1') @('-ManifestPath',$manifestPath,'-BucketName',$Settings.assetBucket)
            $null=Invoke-GameTool python @((Join-Path $script:LauncherHome 'artifacts.py'),'verify-ota','--manifest',$manifestPath) -Echo
            $result.completed+=@('ota');Save-LauncherResult $result $Job
            if('github' -in $Plan.targets){
                $result.sourceCommit=Publish-LauncherSource
                Invoke-LauncherChild 'Tools/Publishing/Publish-Android.ps1' @('-ApkPath',$apk,'-VerificationPath',$new[0].FullName,'-TargetCommit',$result.sourceCommit)
                $result.links+=@("https://github.com/$script:GameRepository/releases/download/android-v$($Plan.version)-$($Plan.androidCode)/battlecities.apk")
                $result.completed+=@('github');Save-LauncherResult $result $Job
            }
            if('install' -in $Plan.targets){
                $adb=Get-LauncherAdb
                $install=Invoke-GameTool $adb @('-s',$Plan.serial,'install','-r',$apk) -Echo
                if($install.Output -notmatch '(?m)^Success\s*$'){throw 'ADB did not confirm successful installation.'}
                $package=Invoke-GameTool $adb @('-s',$Plan.serial,'shell','dumpsys','package','sol.battle.cities')
                if($package.Output -notmatch ('versionCode='+$Plan.androidCode+'\s') -or $package.Output -notmatch ('versionName='+[regex]::Escape($Plan.version)+'(?:\s|$)')){throw 'Installed version did not match this APK.'}
                $null=Invoke-GameTool $adb @('-s',$Plan.serial,'shell','monkey','-p','sol.battle.cities','-c','android.intent.category.LAUNCHER','1')
                $result.completed+=@('install');Save-LauncherResult $result $Job
            }
            if('psg1' -in $Plan.targets){
                $uploadResult=Join-Path $Job 'psg1-upload.json'
                $null=Invoke-GameTool python @((Join-Path $script:LauncherHome 'playsolana.py'),'upload','--endpoint',$Settings.psg1McpUrl,'--submission',$Settings.psg1SubmissionId,'--apk',$apk,'--receipt',$new[0].FullName,'--output',$uploadResult) -Echo
                $uploaded=Get-Content $uploadResult -Raw|ConvertFrom-Json
                $result.links+=@($uploaded.publicUrl);$result.completed+=@('psg1');Save-LauncherResult $result $Job
            }
        }
        $result.status='succeeded';Save-LauncherResult $result $Job
        Write-Host "`nCompleted. Build: $($result.buildPath)" -ForegroundColor Green
        foreach($link in $result.links){Write-Host $link}
        Write-Host "Run record: $Job"
        return [pscustomobject]$result
    }catch{
        $result.status='failed';$result.error=$_.Exception.Message;Save-LauncherResult $result $Job
        Write-Host "Stopped. Completed steps: $($result.completed -join ', '). Run record: $Job" -ForegroundColor Yellow
        throw
    }
}
