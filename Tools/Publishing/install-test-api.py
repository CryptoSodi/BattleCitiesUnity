#!/usr/bin/python3
"""Install only the isolated battlecities test service; production is untouched."""
import hashlib, json, os, pathlib, secrets, shutil, subprocess, sys, tarfile, time
root=pathlib.Path('/opt/battlecities-test')
archive=pathlib.Path(sys.argv[1]); checksum=sys.argv[2]
if not archive.name.startswith('battlecities-test-api-') or archive.parent!=pathlib.Path('/tmp'): raise RuntimeError('Unexpected archive')
if hashlib.file_digest(archive.open('rb'),'sha256').hexdigest()!=checksum: raise RuntimeError('API checksum mismatch')
def run(args, **kw): return subprocess.run(args,check=True,text=True,**kw)
def sql(query): return run(['runuser','-u','postgres','--','psql','-At','-v','ON_ERROR_STOP=1','-c',query],capture_output=True).stdout.strip()
root.mkdir(exist_ok=True)
if subprocess.run(['id','battlecities-test'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode:
    run(['useradd','--system','--home',str(root),'--shell','/usr/sbin/nologin','battlecities-test'])
release=root/'api'/'releases'/checksum
if not release.exists():
    release.mkdir(parents=True)
    with tarfile.open(archive) as tar: tar.extractall(release,filter='data')
    run(['npm','ci','--ignore-scripts','--no-audit','--no-fund'],cwd=release)
    run(['npm','run','build'],cwd=release)
config=root/'config'; config.mkdir(exist_ok=True)
envFile=config/'api.env'
databaseName='battlecities_devnet_test'; databaseRole='battlecities_devnet'
if not envFile.exists():
    if sql("SELECT 1 FROM pg_roles WHERE rolname='battlecities_devnet'") or sql("SELECT 1 FROM pg_database WHERE datname='battlecities_devnet_test'"):
        raise RuntimeError('A preexisting test database/role has no matching configuration; refusing to replace it.')
    password=secrets.token_hex(32)
    sql("CREATE ROLE battlecities_devnet LOGIN PASSWORD '"+password+"' NOSUPERUSER NOCREATEDB NOCREATEROLE")
    sql('CREATE DATABASE battlecities_devnet_test OWNER battlecities_devnet')
    envFile.write_text('\n'.join([
        'NODE_ENV=production','PORT=3003','BATTLECITY_API_HOST=127.0.0.1',
        'DATABASE_URL=postgresql://battlecities_devnet:'+password+'@127.0.0.1:5432/battlecities_devnet_test',
        'BATTLECITY_DATABASE_SSL=disable','BATTLECITY_ENVIRONMENT=devnet','BATTLECITY_SOLANA_NETWORK=devnet',
        'BATTLECITY_SOLANA_RPC_URL=https://api.devnet.solana.com',
        'BATTLECITY_SHOP_SOLANA_RPC_URL=https://api.devnet.solana.com',
        'BATTLECITY_SHOP_QUOTE_SECRET='+secrets.token_hex(32),
        'BATTLECITY_COMPETITIONS_WORKER_ENABLED=0', 'BATTLECITY_DROP_REWARDS_ENABLED=0',
    ])+'\n')
    envFile.chmod(0o640)
    run(['chown','root:battlecities-test',str(envFile)])
environment=os.environ.copy()
for line in envFile.read_text().splitlines():
    key,value=line.split('=',1); environment[key]=value
if environment['BATTLECITY_SOLANA_NETWORK']!='devnet' or '/battlecities_devnet_test' not in environment['DATABASE_URL']: raise RuntimeError('Wrong test configuration')
environment['NODE_ENV']='test'
# Query this specific test database, never the production database.
testTables=run(['runuser','-u','postgres','--','psql','-d',databaseName,'-At','-c',"SELECT count(*) FROM pg_tables WHERE schemaname='public'"],capture_output=True).stdout.strip()
if testTables=='0': run(['node','scripts/bootstrap-test-database.mjs'],cwd=release,env=environment)
run(['node','scripts/migrate.mjs'],cwd=release,env=environment)
data=root/'data'; data.mkdir(exist_ok=True)
run(['chown','-R','battlecities-test:battlecities-test',str(data)])
run(['chmod','-R','a+rX',str(release)])
current=root/'api'/'current'; temporary=root/'api'/'current-next'
if temporary.is_symlink(): temporary.unlink()
temporary.symlink_to(release); os.replace(temporary,current)
unit='''[Unit]
Description=BattleCities isolated Devnet test API
After=network-online.target postgresql.service
Wants=network-online.target
[Service]
User=battlecities-test
Group=battlecities-test
WorkingDirectory=/opt/battlecities-test/data
EnvironmentFile=/opt/battlecities-test/config/api.env
ExecStart=/usr/bin/node /opt/battlecities-test/api/current/dist/src/index.js
Restart=on-failure
RestartSec=3
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/opt/battlecities-test/data
[Install]
WantedBy=multi-user.target
'''
pathlib.Path('/etc/systemd/system/battlecities-test-api.service').write_text(unit)
run(['systemctl','daemon-reload']); run(['systemctl','enable','--now','battlecities-test-api.service']); run(['systemctl','restart','battlecities-test-api.service'])
import urllib.request
for attempt in range(30):
    try:
        with urllib.request.urlopen('http://127.0.0.1:3003/api/ready',timeout=5) as response:
            if response.status==200: break
    except Exception:
        if attempt==29: raise
        time.sleep(1)
with urllib.request.urlopen('http://127.0.0.1:3003/api/economy/catalog') as response:
    catalog=json.load(response)
    if catalog['currency']['sol']['network']!='devnet': raise RuntimeError('Test catalog is not Devnet')
web=root/'web'; web.mkdir(exist_ok=True)
block='''
# BEGIN battlecities-devnet-test
test.battlecities.com {
    encode zstd gzip
    header X-Robots-Tag "noindex, nofollow"
    @api path /api /api/*
    handle @api {
        reverse_proxy 127.0.0.1:3003
    }
    handle {
        root * /opt/battlecities-test/web/current
        @wasm path *.wasm
        header @wasm Content-Type application/wasm
        @fresh path / /index.html /release-info.json
        header @fresh Cache-Control "no-cache"
        file_server
    }
}
# END battlecities-devnet-test
'''
caddy=pathlib.Path('/etc/caddy/Caddyfile'); original=caddy.read_text()
if '# BEGIN battlecities-devnet-test' not in original:
    backup=root/'config'/('Caddyfile-before-test-'+str(int(time.time())))
    shutil.copy2(caddy,backup)
    caddy.write_text(original+block)
    try:
        run(['caddy','validate','--config',str(caddy),'--adapter','caddyfile'])
        run(['systemctl','reload','caddy'])
    except Exception:
        caddy.write_text(original); run(['systemctl','reload','caddy']); raise
print(json.dumps({'api':'devnet','database':databaseName,'listen':'127.0.0.1:3003','service':'battlecities-test-api','source':checksum}))
