"""Publish a prepared Cherry Unity site without touching the Play repository."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid

REPOSITORY = 'CryptoSodi/BattleCitiesCherry'
SOURCE_REPOSITORY = 'CryptoSodi/BattleCitiesUnity'
BRANCH = 'main'
DOMAIN = 'cherry.battlecities.com'
APP_ID = 'c0dcc3df-8b13-445b-afa1-573c8c567315'
MARKER = 'cherry-deployment.json'
FILE_LIMIT = 100 * 1024 * 1024
MANAGED = ('Build', 'StreamingAssets', 'TemplateData', '.well-known/cherry-app.json',
           '.nojekyll', 'CNAME', 'cherry.js', 'index.html', 'release-info.json',
           'seeker-connect-licenses.txt', 'seeker-connect.js', 'unity-build.provenance.json', MARKER)


def run(args, *, cwd=None, input_text=None):
    result = subprocess.run(args, cwd=cwd, input=input_text, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError('{} failed: {}'.format(args[0], result.stderr.strip() or result.stdout.strip()))
    return result.stdout.strip()


def git(*args, cwd=None):
    return run(['git', *args], cwd=cwd)


def gh_json(path):
    return json.loads(run(['gh', 'api', path]))


def check_remote():
    repo = gh_json('repos/' + REPOSITORY)
    pages = gh_json('repos/' + REPOSITORY + '/pages')
    source = pages.get('source') or {}
    if repo.get('default_branch') != BRANCH or pages.get('build_type') != 'legacy' or \
       pages.get('cname') != DOMAIN or source.get('branch') != BRANCH or source.get('path') != '/':
        raise ValueError('Cherry Pages configuration is unexpected; refusing to change it.')
    refs = git('ls-remote', '--heads', 'https://github.com/' + REPOSITORY + '.git', 'refs/heads/' + BRANCH)
    if not refs:
        raise ValueError('The existing Cherry Pages branch was not found.')
    return refs.split()[0]


def is_managed(relative):
    return any(relative == item or (item in ('Build', 'StreamingAssets', 'TemplateData') and
                                     relative.startswith(item + '/')) for item in MANAGED)


def validate_site(site, version):
    site = Path(site).resolve(strict=True)
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('Invalid game version.')
    for file in site.rglob('*'):
        relative = file.relative_to(site).as_posix()
        if file.is_symlink() or file.name.lower() in ('.git', '.gitattributes'):
            raise ValueError('Unsafe Cherry site entry: ' + relative)
        if file.is_file() and (not is_managed(relative) or file.stat().st_size >= FILE_LIMIT):
            raise ValueError('Unexpected or oversized Cherry site file: ' + relative)
    required = ['index.html', 'release-info.json', 'CNAME', '.nojekyll', 'cherry.js', '.well-known/cherry-app.json']
    if any(not (site / name).is_file() for name in required):
        raise ValueError('The prepared Cherry site is incomplete.')
    if not list((site / 'Build').glob('*.loader.js')):
        raise ValueError('The prepared Cherry site is missing its Unity loader.')
    if (site / 'CNAME').read_text(encoding='utf-8-sig').strip() != DOMAIN or \
       json.loads((site / '.well-known/cherry-app.json').read_text(encoding='utf-8-sig')).get('appId') != APP_ID:
        raise ValueError('The prepared Cherry domain or app ID is incorrect.')
    release = json.loads((site / 'release-info.json').read_text(encoding='utf-8-sig'))
    page = (site / 'index.html').read_text(encoding='utf-8-sig')
    script_version = hashlib.sha256((site / 'cherry.js').read_bytes()).hexdigest()[:12]
    if release.get('repository') != SOURCE_REPOSITORY or release.get('release') != 'v' + version or \
       ('cherry.js?v=' + script_version) not in page or ('productVersion: "' + version + '"') not in page:
        raise ValueError('The prepared Cherry site does not match this build/version.')
    return site


def prepare_commit(site, remote, work, version, source_commit, archive_hash, deployment_id):
    site = validate_site(site, version)
    if not re.fullmatch(r'[a-f0-9]{40}', source_commit) or not re.fullmatch(r'[a-f0-9]{64}', archive_hash):
        raise ValueError('Invalid source commit or archive checksum.')
    if not re.fullmatch(r'[a-f0-9]{32}', deployment_id):
        raise ValueError('Invalid deployment ID.')
    work = Path(work)
    git('init', '-q', str(work))
    git('remote', 'add', 'origin', remote, cwd=work)
    git('fetch', '-q', '--depth=1', 'origin', 'refs/heads/' + BRANCH, cwd=work)
    old = git('rev-parse', 'FETCH_HEAD', cwd=work)
    if git('show', old + ':CNAME', cwd=work).strip() != DOMAIN or \
       json.loads(git('show', old + ':.well-known/cherry-app.json', cwd=work)).get('appId') != APP_ID:
        raise ValueError('Existing Cherry branch has an unexpected domain or app ID.')
    tracked = git('ls-tree', '-r', '--name-only', old, cwd=work).splitlines()
    if any(Path(name).name.lower() == '.gitattributes' for name in tracked):
        raise ValueError('Cherry branch has Git attributes that could transform published files.')
    git('read-tree', old, cwd=work)
    # Only replace the known generated site paths in a temporary index; preserve
    # any unrelated files already in this repository and the entire history.
    git('rm', '-r', '-f', '--cached', '--ignore-unmatch', '--', *MANAGED, cwd=work)
    for item in site.iterdir():
        destination = work / item.name
        if item.name == '.well-known':
            destination.mkdir(exist_ok=True)
            shutil.copy2(item / 'cherry-app.json', destination / 'cherry-app.json')
        elif item.is_dir():
            shutil.copytree(item, destination)
        else:
            shutil.copy2(item, destination)
    marker = {'version': version, 'sourceCommit': source_commit, 'archiveSha256': archive_hash,
              'cherryScriptSha256': hashlib.sha256((site / 'cherry.js').read_bytes()).hexdigest(),
              'deploymentId': deployment_id, 'repository': SOURCE_REPOSITORY}
    (work / MARKER).write_text(json.dumps(marker, indent=2) + '\n', encoding='utf-8')
    git('config', 'core.autocrlf', 'false', cwd=work)
    attributes = 'NUL' if os.name == 'nt' else '/dev/null'
    existing = [p for p in MANAGED if (work / p).exists()]
    git('-c', 'core.attributesFile=' + attributes, 'add', '-f', '--', *existing, cwd=work)
    tree = git('write-tree', cwd=work)
    new = git('-c', 'user.name=BattleCities Builder', '-c', 'user.email=builder@battlecities.com',
              'commit-tree', tree, '-p', old, '-m', 'Publish Cherry web ' + version, cwd=work)
    return old, new, marker


def push_commit(work, new):
    # Normal fast-forward push: Git rejects concurrent deployments. Never force.
    git('push', 'origin', new + ':refs/heads/' + BRANCH, cwd=work)


def wait_for_live(marker, timeout=1200):
    deadline = time.monotonic() + timeout
    base = 'https://' + DOMAIN + '/'
    while time.monotonic() < deadline:
        try:
            nonce = str(time.time_ns())
            def load(path):
                separator = '&' if '?' in path else '?'
                req = urllib.request.Request(base + path + separator + 'check=' + nonce,
                                             headers={'User-Agent': 'Mozilla/5.0 BattleCities-Builder/1.0'})
                with urllib.request.urlopen(req, timeout=20) as response:
                    return response.read()
            live_marker = json.loads(load(MARKER))
            release = json.loads(load('release-info.json'))
            app = json.loads(load('.well-known/cherry-app.json'))
            page = load('').decode('utf-8-sig')
            script_version = marker['cherryScriptSha256'][:12]
            script = load('cherry.js?v=' + script_version)
            if live_marker == marker and release.get('release') == 'v' + marker['version'] and \
               app.get('appId') == APP_ID and ('cherry.js?v=' + script_version) in page and \
               hashlib.sha256(script).hexdigest() == marker['cherryScriptSha256'] and \
               ('productVersion: "' + marker['version'] + '"') in page:
                print('Live Cherry site verified: ' + base, flush=True)
                return
        except (OSError, ValueError, urllib.error.URLError):
            pass
        print('Waiting for the Cherry Pages deployment...', flush=True)
        time.sleep(15)
    raise TimeoutError('Cherry was pushed, but the live deployment could not be verified. Check the run record and GitHub Pages before retrying.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=('check-remote', 'publish'))
    parser.add_argument('--site')
    parser.add_argument('--version')
    parser.add_argument('--commit')
    parser.add_argument('--archive')
    args = parser.parse_args()
    check_remote()
    if args.action == 'check-remote':
        print('Cherry Pages preflight passed.')
        return
    if not all((args.site, args.version, args.commit, args.archive)):
        raise ValueError('The Cherry site, version, source commit and archive are required.')
    digest = hashlib.sha256()
    with open(args.archive, 'rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    with tempfile.TemporaryDirectory(prefix='battlecities-cherry-pages-') as temp:
        work = Path(temp) / 'site'
        old, new, marker = prepare_commit(args.site, 'https://github.com/' + REPOSITORY + '.git',
                                           work, args.version, args.commit, digest.hexdigest(), uuid.uuid4().hex)
        push_commit(work, new)
        print('Cherry Pages commit pushed: ' + new, flush=True)
    wait_for_live(marker)


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        print('Cherry publishing failed: ' + str(exc), file=sys.stderr)
        sys.exit(1)
