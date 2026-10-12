"""Extract selected Synty .unitypackage files straight into the project.

A .unitypackage is a gzip tar: <guid>/asset, <guid>/asset.meta, <guid>/pathname (+ preview.png).
We stream it and write each asset to Assets/_Project/Art/Synty/<path under Assets/Synty/>, keeping
the .meta files so GUID references between assets survive. Files that already exist are skipped, so
the shared PolygonGeneric base is written once (from the first, newest package in the list).

Usage: python tools/synty_extract.py [--dry] <package.unitypackage> [...]
Run from the project root.
"""
import io
import os
import sys
import tarfile

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Synty")
SRC_PREFIX = "Assets/Synty/"

SKIP_SUBSTRINGS = (
    "/SyntyPackageHelper/",   # editor nag window, not needed
    "_BIRP.unity",            # built-in pipeline demo scenes
    "Demo_BIRP",
)


def target_path(pathname):
    """Map a pathname inside the package to a destination path. None = skip."""
    if not pathname.startswith("Assets/"):
        return None
    for s in SKIP_SUBSTRINGS:
        if s in pathname:
            return None
    if pathname.startswith(SRC_PREFIX):
        rel = pathname[len(SRC_PREFIX):]
    else:
        rel = "_Other/" + pathname[len("Assets/"):]
    return os.path.join(DEST_ROOT, *rel.split("/"))


def extract(pkg, dry=False):
    print("==", os.path.basename(pkg), flush=True)
    written = skipped = existed = 0
    # Members of one GUID come in any order, so buffer them per GUID.
    pending = {}

    def flush(guid):
        nonlocal written, skipped, existed
        entry = pending.pop(guid, None)
        if entry is None or "pathname" not in entry:
            return
        pathname = entry["pathname"].decode("utf-8").split("\n")[0].strip()
        dest = target_path(pathname)
        if dest is None:
            skipped += 1
            return
        meta = entry.get("asset.meta")
        asset = entry.get("asset")
        if asset is None:
            # A folder.
            if not dry:
                os.makedirs(dest, exist_ok=True)
                if meta is not None and not os.path.exists(dest + ".meta"):
                    with open(dest + ".meta", "wb") as f:
                        f.write(meta)
            return
        if os.path.exists(dest):
            existed += 1
            return
        if not dry:
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, "wb") as f:
                f.write(asset)
            if meta is not None:
                with open(dest + ".meta", "wb") as f:
                    f.write(meta)
        written += 1

    with tarfile.open(pkg, "r:gz") as tar:
        for member in tar:
            parts = member.name.strip("/").split("/")
            if len(parts) != 2:
                continue
            guid, kind = parts
            if kind == "preview.png" or not member.isfile():
                continue
            data = tar.extractfile(member).read()
            pending.setdefault(guid, {})[kind] = data
            entry = pending[guid]
            # asset is optional (folders), so flush once pathname + meta are in and either asset arrived
            # or the next guid starts. Simplest: flush when we have all three, else at the end.
            if "pathname" in entry and "asset.meta" in entry and "asset" in entry:
                flush(guid)
        for guid in list(pending.keys()):
            flush(guid)
    print("   written %d, already present %d, skipped %d" % (written, existed, skipped), flush=True)


def main(argv):
    dry = "--dry" in argv
    pkgs = [a for a in argv if not a.startswith("--")]
    if not pkgs:
        print(__doc__)
        return 1
    for p in pkgs:
        extract(p, dry)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
