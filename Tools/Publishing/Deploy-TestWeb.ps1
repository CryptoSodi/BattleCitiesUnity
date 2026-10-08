[CmdletBinding()]
param([string]$Version, [string]$SshHost = 'ubuntu@129.153.16.84', [string]$SshKey = $env:BATTLECITIES_TEST_SSH_KEY, [switch]$BuildOnly, [string]$ExistingBuildPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Automation-Functions.ps1')
if (-not $Version) { $Version = Get-LocalGameVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part game version.' }
if (-not $BuildOnly -and -not (Test-Path -LiteralPath $SshKey)) { throw 'Pass -SshKey or set BATTLECITIES_TEST_SSH_KEY to your existing Oracle SSH key.' }
$editor = Assert-GameBuildReady WebGL
if (-not $editor) { throw 'Open this Unity project to build the test profile with settings restoration.' }
$lock = Enter-GameAutomation
$job = Join-Path $script:GameRoot ('Builds/TestWeb/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory($job) | Out-Null
$snapshot = Join-Path $job 'editor-settings.json'
$snapshotLiteral = $snapshot.Replace('\','\\')
$saveScript = Join-Path $job 'save-settings.cs'
[IO.File]::WriteAllText($saveScript, @"
var settings = new Newtonsoft.Json.Linq.JObject {
    {"version", UnityEditor.PlayerSettings.bundleVersion},
    {"defines", UnityEditor.PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL)}
};
System.IO.File.WriteAllText("$snapshotLiteral", settings.ToString()); return true;
"@)
$restoreScript = Join-Path $job 'restore-settings.cs'
[IO.File]::WriteAllText($restoreScript, @"
var settings = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("$snapshotLiteral"));
UnityEditor.PlayerSettings.bundleVersion = (string)settings["version"];
UnityEditor.PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL, (string)settings["defines"]);
UnityEditor.AssetDatabase.SaveAssets(); return true;
"@)
try {
    if ($ExistingBuildPath) {
        $web = (Resolve-Path -LiteralPath $ExistingBuildPath).Path
        $buildJob = Split-Path $web -Leaf
        $receiptRoot = Join-Path $script:GameRoot ("Builds/Automation/" + $buildJob)
        $request = Get-Content -LiteralPath (Join-Path $receiptRoot "build-request.json") -Raw | ConvertFrom-Json
        $report = Get-Content -LiteralPath (Join-Path $receiptRoot "build-report.json") -Raw | ConvertFrom-Json
        if (-not $request.testNetwork -or $request.networkSelection -ne "exact-test-host" -or $request.target -ne "WebGL" -or $request.version -ne $Version -or
            [IO.Path]::GetFullPath($request.outputPath) -ne $web -or $report.result -ne "Succeeded" -or $report.totalErrors -ne 0) {
            throw "Only a verified build with hostname-based network selection can be reused."
        }
    } else {
    $null = Invoke-UnityEditorCommand eval_file @('--file',$saveScript)
    try { $web = Invoke-GameBuild WebGL $Version -TestNetwork }
    finally {
        if (Test-Path -LiteralPath $snapshot) {
            Wait-GameEditorIdle
            $null = Invoke-UnityEditorCommand eval_file @('--file',$restoreScript)
            Wait-GameEditorIdle
        }
    }
    }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    function New-TestArchive([string]$Source, [string]$Target) {
        $archive = [IO.Compression.ZipFile]::Open($Target, [IO.Compression.ZipArchiveMode]::Create)
        try {
            $prefix = $Source.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
            foreach ($file in Get-ChildItem -LiteralPath $Source -File -Recurse) {
                $name = $file.FullName.Substring($prefix.Length).Replace('\','/')
                $entry = $archive.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
                $input = [IO.File]::OpenRead($file.FullName); $output = $entry.Open()
                try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
            }
        } finally { $archive.Dispose() }
    }
    $rawArchive = Join-Path $job 'raw-web.zip'
    New-TestArchive $web $rawArchive
    $site = Join-Path $job 'site'
    & (Join-Path $PSScriptRoot 'Prepare-WebSite.ps1') -ArchivePath $rawArchive -Destination $site -ExpectedSha256 (Get-ReleaseSha256 $rawArchive)
    $indexPath = Join-Path $site 'index.html'
    $index = [IO.File]::ReadAllText($indexPath).Replace('<title>Battle Cities</title>','<title>Battle Cities - Devnet Test</title>')
    $index = $index.Replace('</head>', '<meta name="robots" content="noindex,nofollow"><style>#test-network-badge{position:fixed;top:6px;left:50%;transform:translateX(-50%);z-index:1000;padding:4px 12px;border-radius:4px;background:#ffdb4a;color:#071d36;font:700 13px sans-serif;pointer-events:none;white-space:nowrap}</style></head>')
    $index = $index.Replace('<body>', '<body><div id="test-network-badge" hidden>TEST BUILD | SOLANA DEVNET</div><script>if(location.hostname.toLowerCase()==="test.battlecities.com"){document.getElementById("test-network-badge").hidden=false;}else{document.title="Battle Cities";}</script>')
    # The checkout helper is a StreamingAsset, so a verified player can be reused
    # for a wallet-only fix. Preload its content-hashed URL to avoid stale CDN copies.
    $walletSource = Join-Path $script:GameRoot 'Assets/StreamingAssets/BattleCitiesCheckout/wallet.js'
    $walletHash = Get-ReleaseSha256 $walletSource
    $walletDirectory = Join-Path $site 'StreamingAssets/BattleCitiesCheckout'
    [IO.Directory]::CreateDirectory($walletDirectory) | Out-Null
    Copy-Item -LiteralPath $walletSource -Destination (Join-Path $walletDirectory 'wallet.js')
    $walletName = 'wallet.' + $walletHash.Substring(0,12) + '.js'
    Copy-Item -LiteralPath $walletSource -Destination (Join-Path $walletDirectory $walletName)
    $index = $index.Replace('</head>', ('<script src="StreamingAssets/BattleCitiesCheckout/' + $walletName + '"></script></head>'))
    [IO.File]::WriteAllText($indexPath,$index,[Text.UTF8Encoding]::new($false))
    $commit = (Invoke-GameTool git @('rev-parse','HEAD')).Output.Trim()
    [IO.File]::WriteAllText((Join-Path $site 'release-info.json'),([ordered]@{version=$Version;network='devnet';api='https://test.battlecities.com';networkSelection='exact-test-host';walletBundleSha256=$walletHash;sourceCommit=$commit;builtAt=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    $archive = Join-Path $job 'battlecities-devnet-web.zip'
    New-TestArchive $site $archive
    $checksum = Get-ReleaseSha256 $archive
    Write-Host "Test web build: $site"
    Write-Host "Test archive: $archive"
    if (-not $BuildOnly) {
        $remote = '/tmp/battlecities-test-web-' + $checksum + '.zip'
        $null = Invoke-GameTool scp @('-i',$SshKey,'-o','BatchMode=yes','-o','IdentitiesOnly=yes','-o','StrictHostKeyChecking=yes',$archive,($SshHost + ':' + $remote)) -Echo
        $null = Invoke-GameTool ssh @('-T','-i',$SshKey,'-o','BatchMode=yes','-o','IdentitiesOnly=yes','-o','StrictHostKeyChecking=yes',$SshHost,('sudo -n /opt/battlecities-test/deploy-web.py ' + $remote + ' ' + $checksum)) -Echo
        $release = Invoke-RestMethod ('https://test.battlecities.com/release-info.json?check=' + [guid]::NewGuid().ToString('N'))
        if ($release.network -ne 'devnet' -or $release.sourceCommit -ne $commit) { throw 'The live test site does not match this build.' }
        Write-Host 'Test deployment verified: https://test.battlecities.com/'
    }
} finally { $lock.Dispose() }
