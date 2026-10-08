Set-StrictMode -Version Latest
$script:GameRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script:GameRepository = 'CryptoSodi/BattleCitiesUnity'
. (Join-Path $PSScriptRoot 'Release-Functions.ps1')

function Get-GameProgram([string]$Name) {
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $command) { throw "Install $Name and open a new terminal so it is on PATH. See Docs/Publishing.md." }
    return $command.Source
}

function ConvertTo-WindowsArgument([AllowEmptyString()][string]$Value) {
    # Escape argv directly, without a shell. This also handles spaces and embedded quotes on PowerShell 5.1.
    '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

function Invoke-GameTool([string]$Name, [string[]]$Arguments, [switch]$AllowFailure, [switch]$Echo) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = Get-GameProgram $Name
    $start.Arguments = ($Arguments | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' '
    $start.WorkingDirectory = $script:GameRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw "Could not start $Name." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $nextHeartbeat = 60
        while (-not $process.WaitForExit(1000)) {
            if ($timer.Elapsed.TotalSeconds -ge $nextHeartbeat) {
                Write-Host ("{0} is still running ({1:0} minutes)..." -f $Name, $timer.Elapsed.TotalMinutes)
                $nextHeartbeat += 60
            }
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errors = $stderr.GetAwaiter().GetResult()
        $code = $process.ExitCode
        if ($Echo) { if ($output.Trim()) { Write-Host $output.TrimEnd() }; if ($errors.Trim()) { Write-Host $errors.TrimEnd() } }
        if ($code -ne 0 -and -not $AllowFailure) { throw "$Name failed (exit $code).`n$output`n$errors" }
        return [pscustomobject]@{ ExitCode = $code; Output = $output; Errors = $errors }
    } finally { $process.Dispose() }
}

function Invoke-GameJson([string]$Name, [string[]]$Arguments) {
    $reply = Invoke-GameTool $Name $Arguments
    try { return ($reply.Output | ConvertFrom-Json) }
    catch { throw "$Name did not return valid JSON.`n$($reply.Output)`n$($reply.Errors)" }
}

function Invoke-UnityEditorCommand([string]$Name, [string[]]$Parameters = @()) {
    $envelope = Invoke-GameJson unity (@('command', $Name, '--project-path', $script:GameRoot,
        '--timeout', '90', '--format', 'json', '--no-pager', '--no-banner') + $Parameters)
    if (-not $envelope.success) { throw ($envelope.errors | ConvertTo-Json -Depth 6) }
    $result = $envelope.data.result
    if ($result -is [string] -and $result.TrimStart() -match '^[\[{]') { $result = $result | ConvertFrom-Json }
    if ($result -and $result.PSObject.Properties['success'] -and -not $result.success) {
        throw ($result | ConvertTo-Json -Depth 8)
    }
    return $result
}

function Get-LocalGameVersion {
    $settings = [IO.File]::ReadAllText((Join-Path $script:GameRoot 'ProjectSettings/ProjectSettings.asset'))
    if ($settings -notmatch '(?m)^\s*bundleVersion:\s*(\d+\.\d+\.\d+)\s*$') { throw 'Cannot read the project version.' }
    return $Matches[1]
}

function Get-NextAndroidVersionCode {
    $settings = [IO.File]::ReadAllText((Join-Path $script:GameRoot 'ProjectSettings/ProjectSettings.asset'))
    if ($settings -notmatch '(?m)^\s*AndroidBundleVersionCode:\s*(\d+)\s*$') { throw 'Cannot read Android version code.' }
    return ([int]$Matches[1] + 1)
}

function Get-LatestGameRelease {
    $null = Get-GameProgram gh
    $null = Invoke-GameTool gh @('auth', 'status')
    return (Invoke-GameJson gh @('api', "repos/$script:GameRepository/releases/latest"))
}

function Get-SuggestedWebVersion($LatestRelease) {
    $local = [version](Get-LocalGameVersion)
    if ($LatestRelease.tag_name -notmatch '^v?(\d+\.\d+\.\d+)$') { throw 'Latest release does not have a stable version tag.' }
    $released = [version]$Matches[1]
    if ($local -gt $released) { return $local.ToString() }
    return ('{0}.{1}.{2}' -f $released.Major, $released.Minor, ($released.Build + 1))
}

function Read-GameVersion([string]$Version, [string]$Default, [switch]$CheckOnly) {
    if (-not $Version) {
        $Version = $Default
        if (-not $CheckOnly) { $answer = Read-Host "Version [$Default]"; if ($answer.Trim()) { $Version = $answer.Trim() } }
    }
    $Version = $Version -replace '^v', ''
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a version like 0.1.10.' }
    return $Version
}

function Get-GameEditor {
    $null = Get-GameProgram unity
    $discovery = Invoke-GameJson unity @('pipeline', 'list', '--format', 'json', '--no-banner', '--no-pager')
    if (-not $discovery.success) { throw 'Unity editor discovery failed.' }
    $instances = @($discovery.data.instances | Where-Object {
        [IO.Path]::GetFullPath($_.projectPath).TrimEnd('\', '/') -eq $script:GameRoot.TrimEnd('\', '/') -and $_.isRunning
    })
    if ($instances.Count -gt 1) { throw 'More than one editor is running this project. Close the duplicate editor.' }
    if ($instances.Count -eq 1) {
        if (-not $instances[0].pipelineServer -or -not $instances[0].pipelineServer.isReachable) {
            throw 'Unity is open but its Pipeline server is unavailable. Fix compilation errors or close Unity before running this script.'
        }
        return $instances[0]
    }
    # Never start a second Editor against an already-open project just because Pipeline was unavailable.
    $running = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('\', '/').IndexOf($script:GameRoot.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($running.Count) { throw 'Unity is open without a reachable Pipeline connection. Enable Pipeline or close Unity and rerun.' }
    return $null
}

function Assert-GameBuildReady([string]$Target) {
    $editor = Get-GameEditor
    if ($editor) {
        $state = Invoke-UnityEditorCommand editor_status
        if ($state.compiling) { throw 'Unity is compiling. Wait for compilation to finish, then rerun.' }
        $catalog = Invoke-GameJson unity @('command', '--project-path', $script:GameRoot, '--detail', 'compact', '--format', 'json')
        $names = @($catalog.data.commands | ForEach-Object { $_.name })
        foreach ($name in @('eval_file', 'build', 'build_status', 'switch_build_target', 'get_build_settings', 'editor_stop')) {
            if ($names -notcontains $name) { throw "This Pipeline version is missing $name. Close Unity to use the headless build path." }
        }
        $build = Invoke-UnityEditorCommand build_status
        if ($build.status -in @('queued', 'building')) { throw 'Another Unity build is already running. Wait for it to finish.' }
        Write-Host "Ready: connected Unity Editor, target $Target."
    } else { Write-Host "Ready: Unity is closed; the project's installed Editor will build $Target in batch mode." }
    return $editor
}

function Enter-GameAutomation {
    $folder = Join-Path $script:GameRoot 'Builds/Automation'
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    try { return [IO.File]::Open((Join-Path $folder 'automation.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch { throw 'Another build, deploy, or push script is running for this project. Wait for it to finish.' }
}

function Wait-GameEditorIdle {
    $deadline = [DateTime]::UtcNow.AddMinutes(5)
    $lastError = $null
    $reportedReconnect = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $state = Invoke-UnityEditorCommand editor_status
            if (-not $state.compiling -and $state.playMode -eq 'stopped' -and
                (-not $state.PSObject.Properties['domainReloadInProgress'] -or -not $state.domainReloadInProgress)) { return }
        } catch {
            # Stopping Play Mode and switching platforms may restart Pipeline during an assembly reload.
            # Retry this read only; never repeat a queued build or other mutation after a network error.
            $lastError = $_.Exception.Message
            if (-not $reportedReconnect) { Write-Host 'Waiting for Unity to reconnect after assembly reload...'; $reportedReconnect = $true }
        }
        Start-Sleep -Seconds 3
    }
    throw "Unity did not reconnect, leave Play Mode, or finish compiling in five minutes. $lastError"
}

function Invoke-GameBuild([string]$Target, [string]$Version, [int]$AndroidVersionCode = 0,
    [ValidateSet('APK', 'AAB')][string]$AndroidFormat = 'APK', [switch]$TestNetwork) {
    $editor = Assert-GameBuildReady $Target
    $job = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $jobDirectory = Join-Path $script:GameRoot "Builds/Automation/$job"
    [IO.Directory]::CreateDirectory($jobDirectory) | Out-Null
    $outputDirectory = Join-Path $script:GameRoot "Builds/Releases/$Version/$Target/$job"
    $outputPath = if ($Target -eq 'Android') {
        Join-Path $outputDirectory ('battlecities.' + $AndroidFormat.ToLowerInvariant())
    } else { $outputDirectory }
    $reportPath = Join-Path $jobDirectory 'build-report.json'
    $requestPath = Join-Path $jobDirectory 'build-request.json'
    $request = [ordered]@{ version = $Version; target = $Target; outputPath = $outputPath; reportPath = $reportPath;
        androidVersionCode = $AndroidVersionCode; buildAppBundle = ($Target -eq 'Android' -and $AndroidFormat -eq 'AAB'); testNetwork = [bool]$TestNetwork; networkSelection = 'exact-test-host' }
    [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Host "Building $Target $Version..."
    Write-Host "Output: $outputPath"
    if ($editor) {
        $null = Invoke-UnityEditorCommand editor_stop
        Wait-GameEditorIdle
        $settings = Invoke-UnityEditorCommand get_build_settings
        if ($settings.activeBuildTarget -ne $Target) {
            $null = Invoke-UnityEditorCommand switch_build_target @('--target', $Target, '--confirm', 'true')
            $deadline = [DateTime]::UtcNow.AddMinutes(15)
            do {
                Start-Sleep -Seconds 5
                if ([DateTime]::UtcNow -gt $deadline) { throw 'Unity target switch timed out. Check the Editor.' }
                try { $settings = Invoke-UnityEditorCommand get_build_settings }
                catch { Write-Host 'Waiting for Unity platform switch and reconnection...'; continue }
            } while ($settings.activeBuildTarget -ne $Target)
            Wait-GameEditorIdle
        }
        $evalPath = Join-Path $jobDirectory 'configure-build.cs'
        $literalPath = $requestPath.Replace('\', '\\').Replace('"', '\"')
        [IO.File]::WriteAllText($evalPath, "BattleCities.Editor.AutomationBuild.ConfigureFromFile(`"$literalPath`"); return true;")
        $null = Invoke-UnityEditorCommand eval_file @('--file', $evalPath)
        Wait-GameEditorIdle
        $queued = Invoke-UnityEditorCommand build @('--target', $Target, '--outputPath', $outputPath, '--confirm', 'true')
        $deadline = [DateTime]::UtcNow.AddHours(1)
        do {
            Start-Sleep -Seconds 15
            $report = Invoke-UnityEditorCommand build_status
            if ($report.buildId -ne $queued.buildId) { throw 'Another operation replaced this Unity build. Check the Editor before retrying.' }
            Write-Host ("Build: {0} ({1})" -f $report.status, [DateTime]::Now.ToString('HH:mm:ss'))
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Build timed out after one hour. Check Unity before starting another build.' }
        } while ($report.status -ne 'completed')
        [IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 40))
    } else {
        $previous = $env:BATTLECITIES_BUILD_REQUEST
        try {
            $env:BATTLECITIES_BUILD_REQUEST = $requestPath
            $null = Invoke-GameTool unity @('build', $script:GameRoot, '--target', $Target,
                '--execute-method', 'BattleCities.Editor.AutomationBuild.Build', '--output-path', $outputPath,
                '--log-file', (Join-Path $jobDirectory 'unity-build.log'), '--allow-dirty-build', '--no-tail', '--timeout', '3600', '--format', 'human') -Echo
        } finally { $env:BATTLECITIES_BUILD_REQUEST = $previous }
        if (-not (Test-Path -LiteralPath $reportPath)) { throw "Unity did not write a build report. See $jobDirectory/unity-build.log." }
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    }
    if ($report.result -ne 'Succeeded' -or $report.totalErrors -ne 0) { throw "Build failed. Report: $reportPath" }
    if ($Target -eq 'WebGL') {
        $index = Join-Path $outputPath 'index.html'
        if (-not (Test-Path -LiteralPath $index)) { throw 'The successful build is missing index.html.' }
        if (-not [IO.File]::ReadAllText($index).Contains('productVersion: "' + $Version + '"')) { throw 'The web build has the wrong version.' }
    } else {
        if (-not (Test-Path -LiteralPath $outputPath)) { throw "The successful build is missing its $AndroidFormat." }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $androidArchive = [IO.Compression.ZipFile]::OpenRead($outputPath)
        try {
            $manifest = if ($AndroidFormat -eq 'AAB') { 'base/manifest/AndroidManifest.xml' } else { 'AndroidManifest.xml' }
            if (-not $androidArchive.GetEntry($manifest) -or
                ($AndroidFormat -eq 'AAB' -and -not $androidArchive.GetEntry('BundleConfig.pb'))) {
                throw "The Android output is not a valid $AndroidFormat archive."
            }
        } finally { $androidArchive.Dispose() }
    }
    Write-Host "Build succeeded. Report: $reportPath"
    return $outputPath
}

function Assert-GameGitReady {
    $null = Get-GameProgram git
    $branch = (Invoke-GameTool git @('symbolic-ref', '--quiet', '--short', 'HEAD')).Output.Trim()
    $remote = (Invoke-GameTool git @('remote', 'get-url', 'origin')).Output.Trim()
    if ($remote -notmatch 'github\.com[:/]CryptoSodi/BattleCitiesUnity(?:\.git)?$') { throw 'Origin must point to CryptoSodi/BattleCitiesUnity.' }
    Write-Host "Repository: $script:GameRepository; branch: $branch"
    return $branch
}

function Invoke-GameCommitPush([string]$Message) {
    $branch = Assert-GameGitReady
    $null = Invoke-GameTool git @('fetch', 'origin') -Echo
    $tracking = "refs/remotes/origin/$branch"
    $exists = Invoke-GameTool git @('rev-parse', '--verify', '--quiet', $tracking) -AllowFailure
    if ($exists.ExitCode -eq 0) {
        $ancestor = Invoke-GameTool git @('merge-base', '--is-ancestor', $tracking, 'HEAD') -AllowFailure
        if ($ancestor.ExitCode -ne 0) { throw 'GitHub has newer or diverged commits. Reconcile the branch and rerun; this script never force-pushes.' }
    }
    $null = Invoke-GameTool git @('add', '--all') -Echo
    $changed = Invoke-GameTool git @('diff', '--cached', '--quiet') -AllowFailure
    if ($changed.ExitCode -eq 1) {
        $messagePath = Join-Path $script:GameRoot ('Builds/Automation/commit-' + [guid]::NewGuid().ToString('N') + '.txt')
        [IO.File]::WriteAllText($messagePath, $Message, [Text.UTF8Encoding]::new($false))
        $null = Invoke-GameTool git @('commit', '--file', $messagePath) -Echo
    } elseif ($changed.ExitCode -ne 0) { throw 'Cannot inspect the staged Git changes.' }
    else { Write-Host 'No new changes to commit.' }
    $null = Invoke-GameTool git @('push', '--set-upstream', 'origin', $branch) -Echo
    $dirty = (Invoke-GameTool git @('status', '--porcelain')).Output.Trim()
    if ($dirty) { throw "Files changed while committing. Rerun before publishing.`n$dirty" }
    return (Invoke-GameTool git @('rev-parse', 'HEAD')).Output.Trim()
}

function Get-CarriedGameApk($LatestRelease, [string]$Version) {
    $asset = @($LatestRelease.assets | Where-Object { $_.name -eq 'battlecities.apk' })
    if ($asset.Count -eq 0) { $asset = @($LatestRelease.assets | Where-Object { $_.name -eq 'BattleCities-0.1.1.apk' }) }
    if ($asset.Count -ne 1) { throw 'The previous release has no stable APK asset. Supply -ApkPath explicitly.' }
    $folder = Join-Path $script:GameRoot "Builds/Releases/$Version/CarriedApk"
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $path = Join-Path $folder 'battlecities.apk'
    if (Test-Path -LiteralPath $path) {
        if ($asset[0].digest -eq ('sha256:' + (Get-ReleaseSha256 $path))) { return $path }
        Remove-Item -LiteralPath $path
    }
    $null = Invoke-GameTool gh @('release', 'download', $LatestRelease.tag_name, '--repo', $script:GameRepository,
        '--pattern', $asset[0].name, '--dir', $folder, '--clobber') -Echo
    $downloaded = Join-Path $folder $asset[0].name
    if ($downloaded -ne $path) { Copy-Item -LiteralPath $downloaded -Destination $path -Force }
    if ($asset[0].digest -ne ('sha256:' + (Get-ReleaseSha256 $path))) { throw 'Downloaded APK checksum does not match the previous release.' }
    return $path
}

function Wait-GamePages([string]$Version, [string]$Commit, [DateTime]$PublishedAfter) {
    $deadline = [DateTime]::UtcNow.AddMinutes(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        $runs = @(Invoke-GameJson gh @('run', 'list', '--repo', $script:GameRepository, '--workflow', 'publish-game.yml',
            '--event', 'release', '--limit', '20', '--json', 'databaseId,displayTitle,headSha,createdAt,status,conclusion,url'))
        $run = $runs | Where-Object {
            $_.headSha -eq $Commit -and $_.displayTitle -eq "Battle Cities $Version" -and [DateTime]$_.createdAt -ge $PublishedAfter.AddSeconds(-10)
        } | Select-Object -First 1
        if ($run) {
            Write-Host "Pages deployment: $($run.status)"
            if ($run.status -eq 'completed') {
                if ($run.conclusion -ne 'success') { throw "Pages deployment failed: $($run.url)" }
                break
            }
        }
        Start-Sleep -Seconds 15
    }
    if (-not $run -or $run.status -ne 'completed') { throw 'Timed out waiting for GitHub Pages. Check the repository Actions tab.' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $deadline = [DateTime]::UtcNow.AddMinutes(5)
    do {
        try {
            $nonce = [DateTime]::UtcNow.Ticks
            $info = Invoke-RestMethod -Uri "https://play.battlecities.com/release-info.json?check=$nonce" -TimeoutSec 30
            $page = Invoke-WebRequest -UseBasicParsing -Uri "https://play.battlecities.com/?check=$nonce" -TimeoutSec 30
            if ($info.release -eq "v$Version" -and $page.Content.Contains('productVersion: "' + $Version + '"')) {
                Write-Host "Live version verified: https://play.battlecities.com/ ($Version)"
                return
            }
        } catch { Write-Host 'Waiting for the new site to become available...' }
        Start-Sleep -Seconds 15
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Pages deployed, but the public domain did not show the new version within five minutes.'
}
