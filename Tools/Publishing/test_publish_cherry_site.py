import importlib.util
import hashlib
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('cherry', Path(__file__).with_name('publish_cherry_site.py'))
cherry = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cherry)


class CherryPublisherTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix='battlecities-cherry-test-')
        self.root = Path(self.tmp.name)
        self.remote = self.root / 'remote.git'
        subprocess.run(['git', 'init', '--bare', '-q', str(self.remote)], check=True)
        self.seed = self.root / 'seed'
        cherry.git('init', '-q', str(self.seed))
        cherry.git('remote', 'add', 'origin', str(self.remote), cwd=self.seed)
        for name, value in {'CNAME': cherry.DOMAIN, '.well-known/cherry-app.json': json.dumps({'appId': cherry.APP_ID}),
                            '.well-known/keep.txt': 'keep-well-known', 'keep.txt': 'keep-root',
                            'Build/old.loader.js': 'old', 'index.html': 'old site'}.items():
            dest = self.seed / name
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_text(value, encoding='utf-8')
        cherry.git('add', '--all', cwd=self.seed)
        tree = cherry.git('write-tree', cwd=self.seed)
        self.old = cherry.git('-c', 'user.name=Test', '-c', 'user.email=test@example.test',
                               'commit-tree', tree, '-m', 'existing Cherry site', cwd=self.seed)
        cherry.git('push', 'origin', self.old + ':refs/heads/main', cwd=self.seed)
        self.site = self.root / 'site'
        for name, value in {'CNAME': cherry.DOMAIN, '.well-known/cherry-app.json': json.dumps({'appId': cherry.APP_ID}),
                            '.nojekyll': '', 'cherry.js': '// cherry', 'Build/new.loader.js': '// unity',
                            'index.html': '<script src="cherry.js?v=' + hashlib.sha256(b'// cherry').hexdigest()[:12] + '"></script> productVersion: "1.2.3"',
                            'release-info.json': json.dumps({'repository': cherry.SOURCE_REPOSITORY, 'release': 'v1.2.3'})}.items():
            dest = self.site / name
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_text(value, encoding='utf-8')

    def tearDown(self):
        self.tmp.cleanup()

    def prepare(self, name):
        work = self.root / name
        old, new, marker = cherry.prepare_commit(self.site, str(self.remote), work,
                                                  '1.2.3', 'a' * 40, 'b' * 64,
                                                  hashlib.sha256(name.encode()).hexdigest()[:32])
        return work, old, new, marker

    def test_preserves_history_and_unrelated_files_while_replacing_generated_site(self):
        work, old, new, marker = self.prepare('first')
        self.assertEqual(old, self.old)
        self.assertEqual(cherry.git('rev-parse', new + '^', cwd=work), self.old)
        self.assertEqual(cherry.git('show', new + ':keep.txt', cwd=work), 'keep-root')
        self.assertEqual(cherry.git('show', new + ':.well-known/keep.txt', cwd=work), 'keep-well-known')
        self.assertEqual(cherry.git('show', new + ':Build/new.loader.js', cwd=work), '// unity')
        with self.assertRaises(RuntimeError):
            cherry.git('show', new + ':Build/old.loader.js', cwd=work)
        self.assertEqual(json.loads(cherry.git('show', new + ':' + cherry.MARKER, cwd=work)), marker)
        cherry.push_commit(work, new)
        self.assertEqual(cherry.git('ls-remote', '--heads', 'origin', 'refs/heads/main', cwd=work).split()[0], new)

    def test_concurrent_update_is_never_overwritten(self):
        one, _, new_one, _ = self.prepare('one')
        stale, _, new_stale, _ = self.prepare('stale')
        cherry.push_commit(one, new_one)
        with self.assertRaises(RuntimeError):
            cherry.push_commit(stale, new_stale)
        self.assertEqual(cherry.git('ls-remote', '--heads', 'origin', 'refs/heads/main', cwd=one).split()[0], new_one)

    def test_wrong_domain_and_unexpected_files_are_rejected(self):
        (self.site / 'CNAME').write_text('play.battlecities.com', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'domain or app ID'):
            cherry.validate_site(self.site, '1.2.3')
        (self.site / 'CNAME').write_text(cherry.DOMAIN, encoding='utf-8')
        (self.site / 'unexpected.txt').write_text('not a build asset', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Unexpected or oversized'):
            cherry.validate_site(self.site, '1.2.3')

    def test_script_reference_must_match_bundle_hash(self):
        (self.site / 'cherry.js').write_text('// changed after page preparation', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'does not match'):
            cherry.validate_site(self.site, '1.2.3')

    def test_remote_must_have_existing_cherry_pages_configuration(self):
        repo = {'default_branch': 'main'}
        pages = {'build_type': 'legacy', 'cname': cherry.DOMAIN, 'source': {'branch': 'main', 'path': '/'}}
        with patch.object(cherry, 'gh_json', side_effect=[repo, pages]), patch.object(cherry, 'git', return_value='a'*40+'\trefs/heads/main'):
            self.assertEqual(cherry.check_remote(), 'a'*40)
        pages['cname'] = 'play.battlecities.com'
        with patch.object(cherry, 'gh_json', side_effect=[repo, pages]), self.assertRaisesRegex(ValueError, 'unexpected'):
            cherry.check_remote()

    def test_live_verification_checks_the_exact_cherry_bundle(self):
        script = b'// cherry'
        digest = hashlib.sha256(script).hexdigest()
        marker = {'version': '1.2.3', 'sourceCommit': 'a' * 40, 'archiveSha256': 'b' * 64,
                  'cherryScriptSha256': digest, 'deploymentId': 'c' * 32,
                  'repository': cherry.SOURCE_REPOSITORY}
        def response(request, **_):
            url = request.full_url
            if '/' + cherry.MARKER + '?' in url:
                return io.BytesIO(json.dumps(marker).encode())
            if '/release-info.json?' in url:
                return io.BytesIO(b'{"release":"v1.2.3"}')
            if '/.well-known/cherry-app.json?' in url:
                return io.BytesIO(json.dumps({'appId': cherry.APP_ID}).encode())
            if '/cherry.js?v=' + digest[:12] in url:
                return io.BytesIO(script)
            return io.BytesIO(('<script src="cherry.js?v=' + digest[:12] + '"></script> productVersion: "1.2.3"').encode())
        with patch.object(cherry.urllib.request, 'urlopen', side_effect=response):
            cherry.wait_for_live(marker, timeout=1)


if __name__ == '__main__':
    unittest.main()
