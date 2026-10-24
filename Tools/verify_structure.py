"""Static import-pack checks; this is not a Unity compilation or scene test."""
import hashlib
import json
import pathlib
import re
import sys
import uuid

root = pathlib.Path(__file__).resolve().parents[1]
assets = root / "Assets/_Silence"
network_package = root / "LocalPackages/com.silence.multiplayer"
source_roots = [assets, root / "Assets/Creatures/SilencePredator", root / "Assets/Characters/HoodedGirl", network_package]
checks = []


def check(condition, label):
    checks.append({"check": label, "passed": bool(condition)})


definitions = {}
for path in (path for source in source_roots for path in source.rglob("*.asmdef")):
    data = json.loads(path.read_text())
    check(data["name"] not in definitions, "unique assembly " + data["name"])
    definitions[data["name"]] = data

for name, data in definitions.items():
    check(all(r in definitions or r in {"Unity.InputSystem", "Unity.Netcode.Runtime", "Unity.Collections", "Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime"}
              for r in data.get("references", [])),
          "assembly reference names " + name)


def acyclic(name, stack):
    if name in stack:
        return False
    return all(acyclic(r, stack | {name}) for r in definitions[name].get("references", []) if r in definitions)


check(all(acyclic(n, set()) for n in definitions), "acyclic internal assembly graph")
check(definitions["Silence.Core"].get("noEngineReferences"), "pure core has no engine references")
check(definitions["Silence.Editor"].get("includePlatforms") == ["Editor"], "editor code excluded from players")
check(definitions["Silence.Multiplayer.Editor"].get("includePlatforms") == ["Editor"], "multiplayer editor excluded from players")
package = json.loads((network_package / "package.json").read_text())
check(package["dependencies"] == {"com.unity.netcode.gameobjects": "2.13.3", "com.unity.transport": "2.6.0"},
      "official networking dependencies pinned to inspected source versions")
check(not (root / "Packages/manifest.json").exists(), "source pack does not replace owner project manifest")

asset = json.loads((assets / "Settings/SilenceControls.inputactions").read_text())
ids = []
expected = {"Move", "Look", "LookStick", "Sprint", "Crouch", "Jump", "Interact", "Flashlight", "Inventory", "Pause", "Capture", "Use1", "Use2", "Use3", "Use4", "Use5", "Drop", "Throw", "DoorControl"}
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
for path in (network_package / "Runtime").glob("*.cs"):
    check("UnityEditor" not in path.read_text(), "no editor API in network runtime " + path.name)
for path in (assets / "Scripts/Core").glob("*.cs"):
    check("UnityEngine" not in path.read_text(), "no Unity API in pure core " + path.name)

# Unity serializes these through MonoScript assets; a matching filename is essential.
for source in source_roots:
    for path in source.rglob("*.cs"):
        text = path.read_text()
        for name, base in re.findall(r"\bclass\s+(\w+)\s*:\s*(MonoBehaviour|ScriptableObject|NetworkBehaviour|Interactable)\b", text):
            check(path.stem == name, "Unity script filename " + str(path.relative_to(root)))

for path in (assets / "Graphics/Runtime").glob("*.cs"):
    check("UnityEditor" not in path.read_text(), "no editor API in graphics runtime " + path.name)
check(definitions["Silence.Visual.Editor"].get("includePlatforms") == ["Editor"], "visual tools excluded from players")

brief = root / "Docs/ProjectBrief"
manifest = json.loads((brief / "SHA256SUMS.json").read_text())
check(all(hashlib.sha256((brief / name).read_bytes()).hexdigest() == digest for name, digest in manifest.items()),
      "six original documents preserved")
answers = (brief / "ORIGINAL_ANSWERS_RU.txt").read_text()
check([int(x) for x in re.findall(r"^(\d+)\. ", answers, re.M)] == list(range(1, 127)), "all 126 questionnaire entries")

report = {"kind": "static structure checks only", "checks": checks,
          "passed": sum(c["passed"] for c in checks), "failed": sum(not c["passed"] for c in checks),
          "csharp_assets": sum(len(list(source.rglob("*.cs"))) for source in source_roots)}
print(json.dumps(report, ensure_ascii=False, indent=2))
sys.exit(1 if report["failed"] else 0)
