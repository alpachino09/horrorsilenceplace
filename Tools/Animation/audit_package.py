#!/usr/bin/env python3
"""Inspect the supplied Unity archive without executing assets or importing its demo.

Requires PyYAML. Extraction is opt-in, confined to Assets/PolyOne, preserves GUIDs,
rejects traversal/links/duplicate entries, and never overwrites a different file.
The licensed source files are deliberately excluded from Git and delivery ZIPs.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path, PurePosixPath
import re
import tarfile
import yaml


def digest(data):
    return hashlib.sha256(data).hexdigest()


def inspect(package, extract=None):
    report = {"package_sha256": digest(package.read_bytes()), "assets": [], "clips": [],
              "native_unity_import_tested": False, "quantum_source_files_present": False}
    with tarfile.open(package, "r:gz") as archive:
        members = archive.getmembers()
        if any(not (m.isfile() or m.isdir()) for m in members):
            raise ValueError("Links/special archive entries are not allowed")
        if len({m.name for m in members}) != len(members):
            raise ValueError("Duplicate archive entries")
        if sum(m.size for m in members) > 128 * 1024 * 1024:
            raise ValueError("Archive expands beyond 128 MiB")
        by_name = {m.name: m for m in members}
        paths = set()
        pending = []
        for member in members:
            if not member.name.endswith("/pathname"):
                continue
            guid = member.name.split("/")[0]
            if not re.fullmatch("[0-9a-f]{32}", guid):
                raise ValueError("Invalid GUID")
            path = archive.extractfile(member).read().decode("utf-8-sig").strip()
            parts = PurePosixPath(path).parts
            if "\\" in path or ".." in parts or parts[:2] != ("Assets", "PolyOne") or path in paths:
                raise ValueError("Unsafe or duplicate asset path: " + path)
            paths.add(path)
            if PurePosixPath(path).suffix not in {"", ".anim", ".controller", ".mat", ".prefab", ".unity", ".fbx", ".playable"}:
                raise ValueError("Unexpected executable/asset type: " + path)
            asset = by_name.get(guid + "/asset")
            meta = by_name.get(guid + "/asset.meta")
            data = archive.extractfile(asset).read() if asset else b""
            metadata = archive.extractfile(meta).read() if meta else b""
            row = {"path": path, "guid": guid, "bytes": len(data), "sha256": digest(data)}
            report["assets"].append(row)
            if path.endswith(".anim"):
                text = data.decode("utf-8")
                document = yaml.safe_load(re.sub(r"^--- !u!.*$", "---", text, flags=re.M))
                clip = document["AnimationClip"]
                curves = clip.get("m_FloatCurves", [])
                attributes = {c["attribute"] for c in curves}
                keys = [k for c in curves for k in c["curve"]["m_Curve"]]
                if not keys or any(not math.isfinite(float(k["value"])) for k in keys):
                    raise ValueError("Invalid animation keys: " + path)
                def movement(attribute):
                    curve = next((c["curve"]["m_Curve"] for c in curves if c["attribute"] == attribute), [])
                    return float(curve[-1]["value"]) - float(curve[0]["value"]) if curve else 0
                duration = float(clip["m_AnimationClipSettings"]["m_StopTime"])
                displacement = [movement("RootT." + axis) for axis in "xyz"]
                row = {**row, "name": clip["m_Name"], "duration": duration,
                       "sample_rate": clip["m_SampleRate"], "curve_count": len(curves),
                       "key_count": len(keys), "legacy": bool(clip["m_Legacy"]),
                       "root_displacement_human_units": displacement,
                       "root_speed_human_units": math.hypot(displacement[0], displacement[2]) / duration,
                       "humanoid_curve_count": sum(c.get("classID") == 95 for c in curves),
                       "muscle_curve_count": len([a for a in attributes if not re.match(r"(Root|Motion|LeftFoot|RightFoot|LeftHand|RightHand)[TQ]\.", a)]),
                       "events": len(clip.get("m_Events", [])),
                       "mirrored": bool(clip["m_AnimationClipSettings"].get("m_Mirror", 0)),
                       "foot_translation_curves": sorted(a for a in attributes if re.match(r"(Left|Right)FootT\.", a))}
                report["clips"].append(row)
            if extract:
                destination = (extract / path).resolve()
                if not destination.is_relative_to(extract.resolve()):
                    raise ValueError("Extraction escapes root")
                if asset:
                    pending.append((destination, data))
                if meta:
                    pending.append((Path(str(destination) + ".meta"), metadata))
        # Validate the entire extraction before the first mutation.
        for destination, data in pending:
            if destination.exists() and destination.read_bytes() != data:
                raise FileExistsError("Refusing to overwrite: " + str(destination))
        for destination, data in pending:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    report["clips"].sort(key=lambda c: c["name"])
    report["asset_count"] = len(report["assets"])
    report["clip_count"] = len(report["clips"])
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path)
    parser.add_argument("--extract", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    result = inspect(args.package, args.extract)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n")
    print("Assets:", result["asset_count"], "real animation clips:", result["clip_count"])
    for c in result["clips"]:
        print(f'{c["name"]}: {c["duration"]:.3f}s, {c["humanoid_curve_count"]} Humanoid curves, {c["key_count"]} keys')
