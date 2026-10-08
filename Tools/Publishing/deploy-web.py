#!/usr/bin/python3
import hashlib, json, os, pathlib, re, subprocess, sys, zipfile
archive=pathlib.Path(sys.argv[1]); checksum=sys.argv[2]
if not re.fullmatch(r'[a-f0-9]{64}',checksum) or archive != pathlib.Path('/tmp/battlecities-test-web-'+checksum+'.zip'): raise RuntimeError('Unexpected test archive path')
if hashlib.file_digest(archive.open('rb'),'sha256').hexdigest()!=checksum: raise RuntimeError('Checksum mismatch')
root=pathlib.Path('/opt/battlecities-test/web'); root.mkdir(parents=True,exist_ok=True)
destination=root/checksum
with zipfile.ZipFile(archive) as z:
    info=json.loads(z.read('release-info.json'))
    if info.get('network')!='devnet' or info.get('api')!='https://test.battlecities.com': raise RuntimeError('Not a Devnet web build')
    if 'TEST BUILD' not in z.read('index.html').decode(): raise RuntimeError('Missing test label')
    for entry in z.infolist():
        target=(destination/entry.filename).resolve()
        if not target.is_relative_to(destination.resolve()) or '\\' in entry.filename: raise RuntimeError('Unsafe archive entry')
    if not destination.exists(): z.extractall(destination)
subprocess.run(['chmod','-R','a+rX',str(destination)],check=True)
temporary=root/'current-next'
if temporary.is_symlink(): temporary.unlink()
temporary.symlink_to(destination); os.replace(temporary,root/'current')
archive.unlink()
print(json.dumps({'site':'https://test.battlecities.com','version':info['version'],'network':'devnet','sourceCommit':info['sourceCommit']}))
