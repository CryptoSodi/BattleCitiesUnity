"""Portable Web archives and full OTA integrity verification; standard library only."""
import argparse,hashlib,json,urllib.request,zipfile
from pathlib import Path
import network

def web_zip(source,output):
    root=Path(source).resolve();out=Path(output).resolve()
    if out.is_relative_to(root): raise ValueError('Archive must be outside its source')
    if not (root/'index.html').is_file() or not list((root/'Build').glob('*.loader.js')):
        raise ValueError('Not a complete Unity WebGL build')
    if out.exists(): raise ValueError('Refusing to overwrite an existing archive')
    with zipfile.ZipFile(out,'w',compression=zipfile.ZIP_DEFLATED) as z:
        for file in root.rglob('*'):
            if file.is_file():
                resolved=file.resolve()
                if not resolved.is_relative_to(root): raise ValueError('Archive source contains an external symlink')
                z.write(file,file.relative_to(root).as_posix())
    print(f'Web archive: {out} ({out.stat().st_size} bytes)')

def verify_ota(manifest):
    m=json.loads(Path(manifest).read_text(encoding='utf-8-sig'))
    if m['url']!='https://assets.battlecities.com/android/art/'+m['sha256']+'.bundle':
        raise ValueError('Unexpected OTA URL')
    h=hashlib.sha256();count=0
    with network.urlopen(m['url'],timeout=180) as r:
        if r.status!=200 or r.url!=m['url']: raise ValueError('Unexpected OTA response/redirect')
        while chunk:=r.read(1024*1024):
            count+=len(chunk)
            if count>m['bytes']: raise ValueError('OTA download exceeds expected size')
            h.update(chunk)
    if count!=m['bytes'] or h.hexdigest()!=m['sha256']: raise ValueError('Public OTA size/checksum mismatch')
    result={'verified':True,'url':m['url'],'bytes':count,'sha256':h.hexdigest()}
    Path(manifest).with_name('hosting-verification.json').write_text(json.dumps(result,indent=2))
    print('Public OTA bundle downloaded and verified: '+m['sha256'])

def main():
    p=argparse.ArgumentParser();subs=p.add_subparsers(dest='action',required=True)
    w=subs.add_parser('web-zip');w.add_argument('--source',required=True);w.add_argument('--output',required=True)
    o=subs.add_parser('verify-ota');o.add_argument('--manifest',required=True)
    a=p.parse_args()
    if a.action=='web-zip': web_zip(a.source,a.output)
    else: verify_ota(a.manifest)

if __name__=='__main__': main()
