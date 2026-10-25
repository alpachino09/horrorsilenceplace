#!/usr/bin/env python3
"""Build a code-only update accepted by the existing SILENCE menu 00.

Never bundles the user's licensed animation archive, vendor .anim, art, generated
worlds, .meta, credentials, or Unity settings. The original Unity package is
selected separately by its owner in menu 25.
"""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
VERSION = "0.4.3"
NAME = "SILENCE_HumanMotion_" + VERSION
DIRECTORIES = ["Assets/_Silence/Scripts", "Assets/_Silence/Editor", "Assets/_Silence/Tests",
               "Assets/_Silence/Settings", "Assets/_Silence/Graphics",
               "Assets/Characters/HoodedGirl/Editor", "Assets/Characters/HoodedGirl/Scripts",
               "Assets/Creatures/SilencePredator/Scripts", "LocalPackages/com.silence.multiplayer"]
ALLOWED = {".cs", ".asmdef", ".inputactions", ".json", ".md"}


def build():
    files = {}
    for directory in DIRECTORIES:
        for path in (ROOT / directory).rglob("*"):
            if path.is_file() and path.suffix in ALLOWED:
                files[path.relative_to(ROOT).as_posix()] = path.read_bytes()
    for path in (ROOT / "Docs/Animation").glob("*.md"):
        files[path.relative_to(ROOT).as_posix()] = path.read_bytes()
    files["README_RU.md"] = (f"SILENCE Human Motion {VERSION}\n\n"
        "Новое: V переключает первое/третье лицо, камера учитывает стены,\n"
        "настраивается внешний вид тела от первого лица и защита от перекрещённых за спиной рук.\n\n"
        "Останови Play. В Unity: SILENCE → 00 Update Sources From Folder.\n"
        "Выбери ЭТУ распакованную папку с Assets и LocalPackages внутри, не Unity-проект.\n"
        "После компиляции открой СВОЙ НЫНЕШНИЙ игровой мир с героиней, не старую Campaign и не просмотр 23.\n"
        "SILENCE → 26 Player Camera and Body → Создать копию текущего мира: камера и тело.\n"
        "Новая сцена ..._View откроется сама. Play → Создать сессию → Начать раунд. V — сменить вид.\n"
        "При уже установленной героине не нужно повторять импорт Basic Motions или шаг 25.\n"
        "Для первой установки героини используй 21, для её анимаций — 25.\n"
        "Для кооператива заново собери и Host, и Client из новой сцены через окно 26.\n"
        "Площадка проверки отдельная; кнопка Возврата в окне 25 снова открывает игровой мир.\n"
        "Камера и тело: Docs/Animation/CAMERA_BODY_RU.md. Общая инструкция: Docs/Animation/README_RU.md.\n"
        "Код и исходные данные проверены в облаке. Unity Play/ILPP/сеть/качество движений ещё требуют проверки.\n").encode()
    manifest = {"version": VERSION, "kind": "code-only update; import the original user-provided Unity animation package separately",
                "requires_existing_prepared_hooded_girl": True, "native_unity_play_tested": False,
                "camera_body_menu": "SILENCE/26 Player Camera and Body", "perspective_key": "V",
                "original_basic_motions_sha256": "20b52be48a73cd5a288d6c405b17d27e0a0541fdfa777a1fa36db7d3c51e5b7d",
                "files": [{"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()} for name, data in sorted(files.items())]}
    files["delivery-manifest.json"] = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode()
    destination = ROOT / "Downloads" / (NAME + ".zip")
    destination.parent.mkdir(exist_ok=True)
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path, data in sorted(files.items()):
            info = zipfile.ZipInfo(NAME + "/" + path, (2026, 10, 25, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data)
    print(destination.relative_to(ROOT), len(files), "files,", destination.stat().st_size, "bytes")
    return destination


if __name__ == "__main__":
    build()
