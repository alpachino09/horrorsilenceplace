"""Static import-pack checks; this is not a Unity compilation or scene test."""
import hashlib
import json
import pathlib
import re
import sys
import uuid

root = pathlib.Path(__file__).resolve().parents[1]
assets = root / "Assets/_Silence"
checks = []


def check(condition, label):
    checks.append({"check": label, "passed": bool(condition)})


definitions = {}
for path in assets.rglob("*.asmdef"):
    data = json.loads(path.read_text())
    check(data["name"] not in definitions, "unique assembly " + data["name"])
    definitions[data["name"]] = data

for name, data in definitions.items():
    check(all(r in definitions or r == "Unity.InputSystem" for r in data.get("references", [])),
          "assembly reference names " + name)


def acyclic(name, stack):
    if name in stack:
        return False
    return all(acyclic(r, stack | {name}) for r in definitions[name].get("references", []) if r in definitions)


check(all(acyclic(n, set()) for n in definitions), "acyclic internal assembly graph")
check(definitions["Silence.Core"].get("noEngineReferences"), "pure core has no engine references")
check(definitions["Silence.Editor"].get("includePlatforms") == ["Editor"], "editor code excluded from players")

asset = json.loads((assets / "Settings/SilenceControls.inputactions").read_text())
ids = []
expected = {"Move", "Look", "LookStick", "Sprint", "Crouch", "Jump", "Interact", "Flashlight", "Inventory", "Pause", "Capture"}
for action_map in asset["maps"]:
    ids.append(action_map["id"])
    names = {action["name"] for action in action_map["actions"]}
    check(names == expected, "all input actions present")
    ids.extend(action["id"] for action in action_map["actions"])
    ids.extend(binding["id"] for binding in action_map["bindings"])
    check(all(binding["action"] in names for binding in action_map["bindings"]), "bindings reference existing actions")
    check(not any(b["action"] == "Capture" and b["path"].startswith("<Mouse>")
                  for b in action_map["bindings"]), "settings mouse does not capture the cursor")
check(len(ids) == len(set(ids)), "unique input UUIDs")
check(all(str(uuid.UUID(value)) == value for value in ids), "valid input UUIDs")

for path in (assets / "Scripts").rglob("*.cs"):
    check("UnityEditor" not in path.read_text(), "no editor API in runtime " + path.name)
for path in (assets / "Scripts/Core").glob("*.cs"):
    check("UnityEngine" not in path.read_text(), "no Unity API in pure core " + path.name)

brief = root / "Docs/ProjectBrief"
manifest = json.loads((brief / "SHA256SUMS.json").read_text())
check(all(hashlib.sha256((brief / name).read_bytes()).hexdigest() == digest for name, digest in manifest.items()),
      "six original documents preserved")
answers = (brief / "ORIGINAL_ANSWERS_RU.txt").read_text()
check([int(x) for x in re.findall(r"^(\d+)\. ", answers, re.M)] == list(range(1, 127)), "all 126 questionnaire entries")

report = {"kind": "static structure checks only", "checks": checks,
          "passed": sum(c["passed"] for c in checks), "failed": sum(not c["passed"] for c in checks),
          "csharp_assets": len(list(assets.rglob("*.cs")))}
print(json.dumps(report, ensure_ascii=False, indent=2))
sys.exit(1 if report["failed"] else 0)
