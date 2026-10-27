$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Launcher.Core.ps1')
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('battlecities-launcher-tests-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot)|Out-Null
$script:GameRoot=$testRoot
$script:Calls=[Collections.Generic.List[string]]::new()
$script:checks=0;$script:failOta=$false
function Check([bool]$Condition,[string]$Message){if(-not $Condition){throw "FAILED: $Message"};$script:checks++}
function Assert-LauncherPrerequisites($Plan,$Settings){$script:Calls.Add('preflight')}
function Enter-GameAutomation{return [IO.MemoryStream]::new()}
function Invoke-GameBuild($Target,$Version,[switch]$TestNetwork){$script:Calls.Add("build:$Target");return (Join-Path $testRoot 'web')}
function Publish-LauncherSource{$script:Calls.Add('source');return ('a'*40)}
function Publish-LauncherWeb($Web,$Plan,$Commit,$Job){$script:Calls.Add('play');return 'https://play.battlecities.com/'}
function Start-LauncherLocalhost($Web,$Settings,$Job){$script:Calls.Add('localhost');return 'http://localhost:8080/'}
function Get-LauncherAdb{return 'mock-adb'}
function Invoke-LauncherChild([string]$Script,[string[]]$Parameters=@()){
    $script:Calls.Add($Script)
    if($Script -like '*Build-SignedAndroid.ps1'){
        $root=Join-Path $testRoot "Builds/Android/$($script:testPlan.version)-v$($script:testPlan.androidCode)/mock"
        [IO.Directory]::CreateDirectory((Join-Path $root 'RemoteArt'))|Out-Null
        [IO.File]::WriteAllText((Join-Path $root 'battlecities.apk'),'fixture only; not a real APK')
        $hash=Get-ReleaseSha256 (Join-Path $root 'battlecities.apk')
        [IO.File]::WriteAllText((Join-Path $root "RemoteArt/$hash.bundle"),'fixture only; not a real APK')
        [IO.File]::WriteAllText((Join-Path $root 'RemoteArt/content-manifest.json'),(@{sha256=$hash;bytes=(Get-Item (Join-Path $root 'battlecities.apk')).Length;url="https://assets.battlecities.com/android/art/$hash.bundle"}|ConvertTo-Json))
        [IO.File]::WriteAllText((Join-Path $root 'release-verification.json'),'{}')
    }
    if($script:failOta -and $Script -like '*Upload-AndroidArt.ps1'){throw 'Injected OTA upload failure'}
}
function Invoke-GameTool($Name,[string[]]$Arguments,[switch]$AllowFailure,[switch]$Echo){
    if($Name -eq 'python'){
        if('verify-ota' -in $Arguments){$script:Calls.Add('verify-ota')}
        if('upload' -in $Arguments){
            $script:Calls.Add('psg1')
            $at=[array]::IndexOf($Arguments,'--output')
            [IO.File]::WriteAllText($Arguments[$at+1],'{"publicUrl":"https://example.test/mock.apk"}')
        }
    }
    if($Name -eq 'mock-adb'){
        if('install' -in $Arguments){$script:Calls.Add('install');return [pscustomobject]@{Output="Success`n";ExitCode=0;Errors=''}}
        if('dumpsys' -in $Arguments){return [pscustomobject]@{Output="versionCode=$($script:testPlan.androidCode) minSdk=26`nversionName=$($script:testPlan.version)`n";ExitCode=0;Errors=''}}
        $script:Calls.Add('launch')
    }
    return [pscustomobject]@{Output='';ExitCode=0;Errors=''}
}
$settings=[pscustomobject]@{sshHost='user@host';sshKey='not-read';remoteArtBaseUrl='https://assets.battlecities.com/android/art';assetBucket='battlecities-game-assets';psg1McpUrl='https://mcp.playsolana.com/mcp';psg1SubmissionId='draft';localhostPort=8080}
function Run-Case([string]$Name,[string]$Kind,[string[]]$Targets){
    $script:Calls.Clear()
    $script:testPlan=[pscustomobject]@{kind=$Kind;version='9.8.7';androidCode=42;serial='mock';targets=$Targets}
    $job=Join-Path $testRoot $Name
    return Invoke-LauncherPlan $script:testPlan $settings $job
}
try{
    $web=Run-Case 'web-all' 'web' @('play','test','magic','localhost')
    Check ($web.status -eq 'succeeded') 'web all succeeds'
    Check (@($script:Calls|Where-Object {$_ -eq 'build:WebGL'}).Count -eq 1) 'web builds exactly once'
    Check (@($script:Calls|Where-Object {$_ -eq 'source'}).Count -eq 1) 'one source snapshot for all websites'
    Check ('play' -in $script:Calls -and 'Tools/Publishing/Deploy-TestWeb.ps1' -in $script:Calls -and 'Tools/Publishing/Deploy-MagicWeb.ps1' -in $script:Calls) 'all selected web fronts invoked'
    Check (-not @($script:Calls|Where-Object {$_ -match 'Android|psg1|install'}).Count) 'web never builds/uploads an APK'
    $local=Run-Case 'web-local' 'web' @('localhost')
    Check ('source' -notin $script:Calls -and 'play' -notin $script:Calls) 'localhost does not publish/commit'
    Check ($local.links[0] -eq 'http://localhost:8080/') 'localhost URL returned'
    $only=Run-Case 'web-test' 'web' @('test')
    Check ('play' -notin $script:Calls -and 'Tools/Publishing/Deploy-MagicWeb.ps1' -notin $script:Calls) 'single-front publication isolates destinations'
    $script:Calls.Clear()
    Invoke-LauncherPlan ([pscustomobject]@{kind='android';version='9.8.7';androidCode=42;serial='mock';targets=@('install','github','psg1')}) $settings '' -DryRun
    Check ($script:Calls.Count -eq 0) 'dry run never calls build/publish adapters'
    foreach($bad in @(
        [pscustomobject]@{kind='web';version='9.8.7; bad';androidCode=0;targets=@()},
        [pscustomobject]@{kind='web';version='9.8.7';androidCode=0;targets=@('github')},
        [pscustomobject]@{kind='android';version='9.8.7';androidCode=0;targets=@()},
        [pscustomobject]@{kind='android';version='9.8.7';androidCode=2.5;targets=@()}
    )){
        $rejected=$false;try{Assert-LauncherPlan $bad}catch{$rejected=$true};Check $rejected 'invalid input rejected'
    }
    $android=Run-Case 'android-all' 'android' @('install','github','psg1')
    Check ($android.status -eq 'succeeded') 'all Android destinations complete'
    Check ($script:Calls.IndexOf('verify-ota') -lt $script:Calls.IndexOf('install')) 'verify OTA before device installation'
    Check ($script:Calls.IndexOf('verify-ota') -lt $script:Calls.IndexOf('Tools/Publishing/Publish-Android.ps1')) 'verify OTA before public APK release'
    Check ($script:Calls.IndexOf('verify-ota') -lt $script:Calls.IndexOf('psg1')) 'verify OTA before PSG1 upload'
    Check ('launch' -in $script:Calls) 'installed game is launched'
    Check ($android.links.Count -eq 3) 'local, GitHub and PSG1 links returned'
    $script:failOta=$true
    # Use a fresh version so the mock signed receipt is uniquely new.
    $script:testPlan=[pscustomobject]@{kind='android';version='9.8.8';androidCode=43;serial='mock';targets=@('install','github','psg1')}
    $script:Calls.Clear();$job=Join-Path $testRoot 'failed-ota';$failed=$false
    try{Invoke-LauncherPlan $script:testPlan $settings $job}catch{$failed=$true}
    Check $failed 'OTA failure stops the run'
    Check ('install' -notin $script:Calls -and 'psg1' -notin $script:Calls -and 'source' -notin $script:Calls) 'failed OTA cannot install/publish APK'
    $receipt=Get-Content (Join-Path $job 'result.json') -Raw|ConvertFrom-Json
    Check ($receipt.status -eq 'failed' -and $receipt.completed.Count -eq 1 -and $receipt.completed[0] -eq 'build') 'partial run records completed build accurately'
    Write-Host "PASSED: $script:checks launcher workflow checks. No real builds, publications or device changes." -ForegroundColor Green
}finally{
    # Exact, freshly generated test directory only; never delete project outputs.
    $resolved=[IO.Path]::GetFullPath($testRoot)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if($resolved.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved) -like 'battlecities-launcher-tests-*'){
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
