#!/usr/bin/env python3
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import zipfile
from build_update import ROOT, NAME, ALLOWED


def verify():
    path = ROOT / "Downloads" / (NAME + ".zip")
    checks = []
    def check(ok, name):
        checks.append({"check": name, "passed": bool(ok)})
        if not ok: raise AssertionError(name)
    with zipfile.ZipFile(path) as archive:
        check(archive.testzip() is None, "ZIP CRC")
        entries = archive.namelist()
        check(len(entries) == len(set(entries)), "no duplicate archive entries")
        check(all(p.startswith(NAME + "/") and ".." not in PurePosixPath(p).parts for p in entries), "one safe extracted root")
        check(not any(PurePosixPath(p).suffix in {".meta", ".anim", ".fbx", ".unity", ".prefab", ".controller", ".unitypackage"} or "/PolyOne/" in p or "/Generated/" in p for p in entries), "no vendor assets, scenes, models or .meta")
        manifest = json.loads(archive.read(NAME + "/delivery-manifest.json"))
        installer_entry=NAME+"/Install_Bounds_Fix.command"
        installer=archive.read(installer_entry).decode()
        check(archive.getinfo(installer_entry).external_attr >> 16 & 0o111, "Mac installer executable permission")
        check("@WORKSHOP_SHA256@" not in installer and "@BOUNDS_SHA256@" not in installer, "installer hashes rendered")
        for rel in ["LocalPackages/com.silence.multiplayer/Editor/Visual/CharacterReworkWorkshop.cs", "Assets/_Silence/Scripts/Core/SkinBoundsModel.cs"]:
            check(hashlib.sha256(archive.read(NAME+"/"+rel)).hexdigest() in installer,"installer hash matches shipped source "+rel)
        workshop=archive.read(NAME+"/LocalPackages/com.silence.multiplayer/Editor/Visual/CharacterReworkWorkshop.cs").decode()
        check('Version="'+manifest["version"]+'"' in workshop and "Версия проверки: " in workshop,"compiled workshop exposes current version")
        check("Пустые bounds:" not in workshop,"obsolete bounds error removed from shipped workshop")
        for row in manifest["files"]:
            data = archive.read(NAME + "/" + row["path"])
            check(len(data) == row["bytes"] and hashlib.sha256(data).hexdigest() == row["sha256"], "manifest bytes " + row["path"])
            source = ROOT / row["path"]
            if source.is_file() and row["path"] != "README_RU.md": check(source.read_bytes() == data, "current complete source " + row["path"])
        for prerequisite in ["LocalPackages/com.silence.multiplayer/package.json", "Assets/_Silence/Scripts/Networking/IWorldAuthority.cs"]:
            check(NAME + "/" + prerequisite in entries, "old updater root prerequisite " + prerequisite)
        previous = subprocess.check_output(["git", "show", "9533a0e:Assets/_Silence/Editor/SilenceSourceUpdater.cs"], cwd=ROOT, text=True)
        match = re.search(r"string\[\] roots = \{(.*?)\};", previous, re.S)
        old_roots = re.findall(r'"([^"]+)"', match.group(1))
        sources = [r["path"] for r in manifest["files"] if r["path"].startswith(("Assets/", "LocalPackages/"))]
        check(all(any(p.startswith(root + "/") for root in old_roots) and PurePosixPath(p).suffix in ALLOWED for p in sources), "all Unity update files copied by existing 0.3.3 updater")
        changed = subprocess.check_output(["git", "diff", "--name-only", "9533a0e", "--", "Assets", "LocalPackages"], cwd=ROOT, text=True).splitlines()
        added = subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "Assets", "LocalPackages"], cwd=ROOT, text=True).splitlines()
        check(set(changed + added).issubset(sources), "all changed and new Unity source files included")
    result = {"kind": "delivery integrity/static updater compatibility; not a Unity test", "archive": str(path.relative_to(ROOT)),
              "bytes": path.stat().st_size, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
              "passed": len(checks), "failed": 0, "checks": checks}
    report = ROOT / "Docs/Validation/Animation/delivery.json"
    report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print("Delivery:", len(checks), "checks passed;", path.stat().st_size, "bytes")


if __name__ == "__main__":
    verify()
