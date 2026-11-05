import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("pages", Path(__file__).with_name("publish_pages_branch.py"))
pages = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pages)


class PagesBranchTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="battlecities-pages-test-")
        self.root = Path(self.temp.name)
        self.remote = self.root / "remote.git"
        subprocess.run(["git", "init", "--bare", "-q", str(self.remote)], check=True)
        self.site = self.root / "site"
        self.site.mkdir()
        (self.site / "index.html").write_text("test", encoding="utf-8")
        (self.site / "release-info.json").write_text('{"release":"v1.2.3"}', encoding="utf-8")
        (self.site / "game.unityweb").write_bytes(b"actual web content")

    def tearDown(self):
        self.temp.cleanup()

    def prepare(self, name):
        path = self.root / name
        old, new = pages.prepare_branch(self.site, str(self.remote), path, "1.2.3", "a" * 40, "b" * 64)
        return path, old, new

    def test_push_replaces_only_managed_branch_and_checks_lease(self):
        first, old, commit = self.prepare("first")
        self.assertEqual(old, "")
        pages.push_branch(first, old, commit)
        self.assertEqual(pages.git("show", commit + ":game.unityweb", cwd=first), "actual web content")
        self.assertEqual(pages.git("show", commit + ":CNAME", cwd=first), pages.DOMAIN)
        self.assertEqual(json.loads(pages.git("show", commit + ":" + pages.MARKER, cwd=first))["managedBy"], pages.OWNER)
        second, old_second, new_second = self.prepare("second")
        stale, old_stale, new_stale = self.prepare("stale")
        self.assertEqual(old_second, commit)
        (self.site / "index.html").write_text("updated", encoding="utf-8")
        third, old_third, new_third = self.prepare("third")
        pages.push_branch(third, old_third, new_third)
        with self.assertRaises(RuntimeError):
            pages.push_branch(stale, old_stale, new_stale)
        self.assertEqual(pages.git("rev-list", "--count", new_third, cwd=third), "1")
        self.assertEqual(pages.git("ls-remote", "--heads", "origin", cwd=third).split()[0], new_third)

    def test_unmanaged_branch_is_never_replaced(self):
        work = self.root / "unmanaged"
        pages.initialize(work, str(self.remote))
        (work / "other.txt").write_text("keep", encoding="utf-8")
        pages.git("add", "other.txt", cwd=work)
        tree = pages.git("write-tree", cwd=work)
        commit = pages.git("-c", "user.name=Test", "-c", "user.email=test@example.test",
                           "commit-tree", tree, "-m", "unmanaged", cwd=work)
        pages.git("push", "origin", commit + ":refs/heads/" + pages.BRANCH, cwd=work)
        with self.assertRaisesRegex(ValueError, "not managed"):
            self.prepare("blocked")
        self.assertEqual(pages.git("ls-remote", "--heads", "origin", cwd=work).split()[0], commit)

    def test_site_rejects_git_attributes(self):
        (self.site / ".gitattributes").write_text("*.unityweb filter=lfs", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unsafe site entry"):
            pages.validate_site(self.site)


if __name__ == "__main__":
    unittest.main()
