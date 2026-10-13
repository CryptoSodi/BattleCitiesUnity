#!/usr/bin/python3
"""Add a separate static Magic site. API requests continue to use test.battlecities.com."""
import pathlib,subprocess,time
root=pathlib.Path('/opt/battlecities-magic');root.mkdir(exist_ok=True)
(root/'web').mkdir(exist_ok=True)
config=pathlib.Path('/etc/caddy/Caddyfile');original=config.read_text()
block="""
# BEGIN battlecities-magic
magic.battlecities.com {
    encode zstd gzip
    header X-Robots-Tag "noindex, nofollow"
    root * /opt/battlecities-magic/web/current
    @wasm path *.wasm
    header @wasm Content-Type application/wasm
    @fresh path / /index.html /release-info.json
    header @fresh Cache-Control "no-cache"
    file_server
}
# END battlecities-magic
"""
if '# BEGIN battlecities-magic' not in original:
    (root/('Caddyfile-backup-'+str(int(time.time())))).write_text(original)
    config.write_text(original+block)
    try:
        subprocess.run(['caddy','validate','--config',str(config),'--adapter','caddyfile'],check=True)
        subprocess.run(['systemctl','reload','caddy'],check=True)
    except Exception:
        config.write_text(original);subprocess.run(['systemctl','reload','caddy'],check=True);raise
print('Magic static site installed; test and production site definitions preserved.')
