[CmdletBinding()]
param([string]$Bucket='battlecities-game-assets')
$ErrorActionPreference='Stop'
# Preserve unrelated existing rules; add only read access for the game's Web hosts.
$prior=@(& rtk proxy cf r2 buckets cors get $Bucket 2>&1)
if($LASTEXITCODE -ne 0){
 if(($prior -join "`n") -notmatch '10059|CORS configuration does not exist'){throw 'Could not inspect R2 CORS configuration.'}
 $policy=[pscustomobject]@{rules=@()}
}else{$policy=($prior -join "`n")|ConvertFrom-Json}
$rule=[ordered]@{id='battlecities-web-screen-read';allowed=[ordered]@{
 origins=@('https://play.battlecities.com','https://test.battlecities.com','https://magic.battlecities.com','https://battlecities.com','https://www.battlecities.com','http://localhost:8080','http://127.0.0.1:8080')
 methods=@('GET','HEAD');headers=@('Cache-Control','Content-Type','Accept')
};exposeHeaders=@('Content-Length','Content-Type','ETag');maxAgeSeconds=3600}
$rules=@($policy.rules|Where-Object {$_.id -ne $rule.id})+@($rule)
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('battlecities-screen-cors-'+[Guid]::NewGuid().ToString('N')+'.json')
try{
 [IO.File]::WriteAllText($temporary,(ConvertTo-Json -InputObject $rules -Depth 12),[Text.UTF8Encoding]::new($false))
 & rtk proxy cf r2 buckets cors update $Bucket --rules ('@'+$temporary) --force
 if($LASTEXITCODE -ne 0){throw 'Could not configure Web screen asset read access.'}
}finally{Remove-Item -LiteralPath $temporary -ErrorAction SilentlyContinue}
