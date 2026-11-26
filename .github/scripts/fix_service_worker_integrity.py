#!/usr/bin/env python3
"""
Re-stamp the Blazor service worker's integrity hashes.

Publishing writes service-worker-assets.js with a SHA-256 for every file it lists. The
GitHub Pages deploy then has to edit two of those files after publish - index.html, to point
<base href> at the repository subpath, and appsettings.json, to point the app at the API.

That leaves the recorded hashes stale, the service worker fails its integrity check, and the
app silently stops being installable as a PWA while still appearing to work. Recomputing the
hashes here keeps the manifest honest.

It can also drop entries entirely. Cloudflare Pages treats _redirects and _headers as build
configuration and does not serve them, so a service worker that tries to cache them fails its
install and the app stops being installable - while still looking perfectly fine.

Usage:  python fix_service_worker_integrity.py <wwwroot> [--exclude a,b,c]
"""

import base64
import hashlib
import json
import pathlib
import re
import sys


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__.strip(), file=sys.stderr)
        return 2

    root = pathlib.Path(sys.argv[1])

    excluded = set()
    if "--exclude" in sys.argv:
        raw = sys.argv[sys.argv.index("--exclude") + 1]
        excluded = {name.strip() for name in raw.split(",") if name.strip()}

    manifest_path = root / "service-worker-assets.js"

    if not manifest_path.exists():
        print(f"{manifest_path} not found - nothing to do (not a PWA publish?)")
        return 0

    raw = manifest_path.read_text(encoding="utf-8-sig")

    # The file is JavaScript: "self.assetsManifest = { ... };"
    match = re.search(r"^(?P<prefix>[^{]*)(?P<json>\{.*\})(?P<suffix>;?\s*)$", raw, re.S)
    if not match:
        print("Could not parse service-worker-assets.js", file=sys.stderr)
        return 1

    manifest = json.loads(match.group("json"))

    updated = 0
    missing = 0
    dropped = 0

    kept = []

    for asset in manifest.get("assets", []):
        if asset["url"] in excluded:
            print(f"  dropped {asset['url']} (not served by the host)")
            dropped += 1
            continue
        kept.append(asset)

    for asset in kept:
        target = root / asset["url"]
        if not target.exists():
            missing += 1
            continue

        digest = base64.b64encode(hashlib.sha256(target.read_bytes()).digest()).decode()
        fresh = f"sha256-{digest}"

        if asset.get("hash") != fresh:
            print(f"  re-stamped {asset['url']}")
            asset["hash"] = fresh
            updated += 1

    manifest["assets"] = kept

    manifest_path.write_text(
        match.group("prefix") + json.dumps(manifest) + match.group("suffix"),
        encoding="utf-8",
    )

    print(
        f"service worker: {updated} hash(es) re-stamped, {dropped} entry(ies) dropped, "
        f"{missing} file(s) listed but absent"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
