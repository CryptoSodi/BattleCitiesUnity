"""Publish a validated WebGL site through a dedicated GitHub Pages branch."""
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

BRANCH = "battlecities-pages"
DOMAIN = "play.battlecities.com"
MARKER = "battlecities-pages-manifest.json"
OWNER = "BattleCities Builder Pages deployment v1"
FILE_LIMIT = 100 * 1024 * 1024


def run(args, *, cwd=None, input_text=None):
    result = subprocess.run(args, cwd=cwd, input=input_text, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError("{} failed: {}".format(args[0], result.stderr.strip() or result.stdout.strip()))
    return result.stdout.strip()


def gh_json(*args, payload=None):
    output = run(["gh", "api", *args], input_text=json.dumps(payload) if payload is not None else None)
    return json.loads(output) if output else None


def validate_site(site):
    site = Path(site).resolve(strict=True)
    if not (site / "index.html").is_file() or not (site / "release-info.json").is_file():
        raise ValueError("The validated site must contain index.html and release-info.json.")
    for path in site.rglob("*"):
        if path.is_symlink() or path.name.lower() in (".git", ".gitattributes"):
            raise ValueError("Unsafe site entry: " + str(path))
        if path.is_file() and path.stat().st_size >= FILE_LIMIT:
            raise ValueError("GitHub rejects files of 100 MiB or more: " + str(path))
    return site


def get_configuration(repository):
    repo = gh_json("repos/" + repository)
    pages = gh_json("repos/" + repository + "/pages")
    if repo["default_branch"] == BRANCH:
        raise ValueError("The Pages branch must not be the repository's default branch.")
    if pages.get("cname") != DOMAIN:
        raise ValueError("Unexpected Pages custom domain; refusing to change its configuration.")
    source = pages.get("source") or {}
    if pages.get("build_type") == "legacy" and source.get("branch") != BRANCH:
        raise ValueError("Pages already uses a different branch; refusing to replace that site.")
    return pages


def git(*args, cwd=None, input_text=None):
    return run(["git", *args], cwd=cwd, input_text=input_text)


def inspect_branch(work):
    refs = git("ls-remote", "--heads", "origin", "refs/heads/" + BRANCH, cwd=work)
    old = refs.split()[0] if refs else ""
    if old:
        git("fetch", "-q", "origin", old, cwd=work)
        try:
            previous = json.loads(git("show", old + ":" + MARKER, cwd=work))
        except (RuntimeError, ValueError) as exc:
            raise ValueError("Existing Pages branch is not managed by BattleCities Builder.") from exc
        if previous.get("managedBy") != OWNER:
            raise ValueError("Existing Pages branch is not managed by BattleCities Builder.")
    return old


def initialize(work, remote):
    git("init", "-q", str(work))
    git("remote", "add", "origin", remote, cwd=work)


def prepare_branch(site, remote, work, version, commit, archive_hash):
    """Create an isolated commit, preserving unrelated branches and rejecting unmanaged content."""
    site = validate_site(site)
    work = Path(work)
    initialize(work, remote)
    old = inspect_branch(work)
    for item in site.iterdir():
        dest = work / item.name
        if item.is_dir():
            shutil.copytree(item, dest)
        else:
            shutil.copy2(item, dest)
    (work / "CNAME").write_text(DOMAIN + "\n", encoding="utf-8")
    (work / ".nojekyll").touch()
    manifest = {"managedBy": OWNER, "version": version, "sourceCommit": commit,
                "archiveSha256": archive_hash}
    (work / MARKER).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    git("config", "core.autocrlf", "false", cwd=work)
    # Disable global attributes (especially Git LFS), which GitHub Pages cannot serve.
    attributes = "NUL" if os.name == "nt" else "/dev/null"
    git("-c", "core.attributesFile=" + attributes, "add", "-f", "--all", cwd=work)
    tree = git("write-tree", cwd=work)
    new = git("-c", "user.name=BattleCities Builder",
              "-c", "user.email=builder@battlecities.com",
              "commit-tree", tree, "-m", "Publish Battle Cities " + version, cwd=work)
    return old, new


def push_branch(work, old, new):
    # A single-parentless commit limits repository growth. The lease prevents
    # overwriting a concurrent deployment, including when the branch is new.
    git("push", "origin", new + ":refs/heads/" + BRANCH,
        "--force-with-lease=refs/heads/" + BRANCH + ":" + old, cwd=work)


def configure_pages(repository, pages):
    source = pages.get("source") or {}
    if pages.get("build_type") == "legacy" and source.get("branch") == BRANCH and source.get("path") == "/":
        return
    payload = {"build_type": "legacy", "source": {"branch": BRANCH, "path": "/"},
               "cname": pages["cname"]}
    if pages.get("https_enforced") is not None:
        payload["https_enforced"] = pages["https_enforced"]
    gh_json("--method", "PUT", "repos/" + repository + "/pages", "--input", "-", payload=payload)


def wait_for_live(version, commit, archive_hash):
    deadline = time.monotonic() + 1200
    while time.monotonic() < deadline:
        try:
            nonce = str(time.time_ns())
            base = "https://" + DOMAIN + "/"
            with urllib.request.urlopen(base + MARKER + "?check=" + nonce, timeout=20) as response:
                marker = json.load(response)
            with urllib.request.urlopen(base + "release-info.json?check=" + nonce, timeout=20) as response:
                release = json.load(response)
            with urllib.request.urlopen(base + "?check=" + nonce, timeout=20) as response:
                page = response.read().decode("utf-8")
            if (marker.get("version") == version and marker.get("sourceCommit") == commit
                    and marker.get("archiveSha256") == archive_hash
                    and release.get("release") == "v" + version
                    and ('productVersion: "' + version + '"') in page):
                print("Live production site verified: " + base, flush=True)
                return
        except (OSError, ValueError, urllib.error.URLError):
            pass
        print("Waiting for the Pages branch to appear at " + DOMAIN + "...", flush=True)
        time.sleep(15)
    raise TimeoutError("The Pages branch was pushed, but the new site could not be verified. Check GitHub Pages deployment and retry; the existing release was not replaced.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("check", "publish"))
    parser.add_argument("--site", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--version")
    parser.add_argument("--commit")
    parser.add_argument("--archive")
    args = parser.parse_args()
    if args.repository != "CryptoSodi/BattleCitiesUnity":
        raise ValueError("Unexpected repository.")
    site = validate_site(args.site)
    pages = get_configuration(args.repository)
    remote = "https://github.com/" + args.repository + ".git"
    if args.action == "check":
        with tempfile.TemporaryDirectory(prefix="battlecities-pages-check-") as temp:
            work = Path(temp) / "site"
            initialize(work, remote)
            inspect_branch(work)
        print("Pages branch preflight passed.")
        return
    if not args.version or not re.fullmatch(r"\d+\.\d+\.\d+", args.version):
        raise ValueError("Invalid version.")
    if not args.commit or not re.fullmatch(r"[0-9a-f]{40}", args.commit):
        raise ValueError("Invalid source commit.")
    if not args.archive:
        raise ValueError("Missing web archive.")
    if json.loads((site / "release-info.json").read_text(encoding="utf-8-sig")).get("release") != "v" + args.version:
        raise ValueError("Validated site version does not match the release.")
    digest = hashlib.sha256()
    with open(args.archive, "rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    archive_hash = digest.hexdigest()
    with tempfile.TemporaryDirectory(prefix="battlecities-pages-") as temp:
        work = Path(temp) / "site"
        old, new = prepare_branch(site, remote, work, args.version, args.commit, archive_hash)
        push_branch(work, old, new)
    configure_pages(args.repository, pages)
    wait_for_live(args.version, args.commit, archive_hash)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("Pages publishing failed: " + str(exc), file=sys.stderr)
        sys.exit(1)
