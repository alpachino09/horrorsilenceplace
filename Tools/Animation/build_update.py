#!/usr/bin/env python3
"""Build a code-only update accepted by the existing SILENCE menu 00.

Never bundles the user's licensed animation archive, vendor .anim, art, generated
worlds, .meta, credentials, or Unity settings. The original Unity package is
selected separately by its owner in menu 25.
"""
import hashlib
import json
import shlex
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
VERSION = "0.6.0"
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
    for path in (ROOT / "Docs/CharacterRework").glob("*.md"):
        files[path.relative_to(ROOT).as_posix()] = path.read_bytes()
    installer = (ROOT / "Tools/Animation/install_bounds_fix.command").read_text()
    for token, path in [("@WORKSHOP_SHA256@", "LocalPackages/com.silence.multiplayer/Editor/Visual/CharacterReworkWorkshop.cs"),
                        ("@BOUNDS_SHA256@", "Assets/_Silence/Scripts/Core/SkinBoundsModel.cs")]:
        installer = installer.replace(token, hashlib.sha256(files[path]).hexdigest())
    files["Install_Bounds_Fix.command"] = installer.encode()
    source_files=sorted(p for p in files if p.startswith(("Assets/","LocalPackages/")))
    full=(ROOT/"Tools/Animation/install_update.command").read_text().replace("@VERSION@",VERSION)
    full=full.replace("@FILE_LIST@","\n".join("  "+shlex.quote(p) for p in source_files))
    full=full.replace("@HASH_LIST@","\n".join("  "+shlex.quote(hashlib.sha256(files[p]).hexdigest()) for p in source_files))
    files["Install_Update.command"]=full.encode()
    files["README_RU.md"] = (f"SILENCE Human Motion {VERSION}\n\n"
        "Обновление персонажей V2: согласование IK-слоёв, непрозрачные материалы,\n"
        "устойчивое локальное FPS-отображение, пальцы, сглаживание и 15 проверок.\n\n"
        "Сохрани свой текущий игровой мир и закрой Unity. Распакуй архив в Downloads.\n"
        "В стандартном macOS Terminal выполни:\n"
        f"bash ~/Downloads/{NAME}/Install_Update.command\n"
        "Выбери папку Unity-проекта с Assets, Packages, ProjectSettings и LocalPackages.\n"
        "Установщик сохраняет backup кода, сверяет SHA-256 и оставляет .meta и ассеты.\n"
        "Открой проект, дождись компиляции и открой свой НЫНЕШНИЙ игровой мир.\n"
        "SILENCE → 29 Character Audit and Review: сначала проверка без изменений,\n"
        "затем один раз применить исправления к КОПИИ нынешнего мира.\n"
        "Play → создать сессию → начать раунд. V — камера снаружи.\n"
        "Кнопка 3 создаёт ОТДЕЛЬНУЮ площадку 15 проверок; после неё останови Play\n"
        "и нажми в 29 «Вернуться из площадки в игровой мир».\n"
        "Подробности: Docs/CharacterRework/REBUILD_V2_RU.md.\n\n"
        "Если отдельно подключаешь новые текстуры ObjectiveEnvironment_Assets и движения\n"
        "Kevin Iglesias: окно SILENCE → 28 Imported Textures and Human Motions.\n"
        "Это отдельная операция с копией мира: Docs/Animation/IMPORTED_ASSETS_RU.md.\n"
        "Сами лицензионные ассеты не входят в этот архив; используются уже импортированные на Mac.\n"
        "В собственных запасных исходных клипах найдены пересечения. Это не выдано\n"
        "за исправленную AAA-анимацию: реальные замены подбираются в 28.\n"
        "Native Unity Play, хват фактической модели фонарика и кооператив здесь\n"
        "не проверены. Отчёт: Docs/CharacterRework/REPORT_V2_RU.md.\n").encode()
    manifest = {"version": VERSION, "kind": "code-only update; import the original user-provided Unity animation package separately",
                "requires_existing_prepared_hooded_girl": True, "native_unity_play_tested": False,
                "camera_body_menu": "SILENCE/26 Player Camera and Body", "perspective_key": "V",
                "character_rework_menu": "SILENCE/27 Character Rework and Audit",
                "character_review_menu": "SILENCE/29 Character Audit and Review",
                "original_basic_motions_sha256": "20b52be48a73cd5a288d6c405b17d27e0a0541fdfa777a1fa36db7d3c51e5b7d",
                "files": [{"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()} for name, data in sorted(files.items())]}
    files["delivery-manifest.json"] = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode()
    destination = ROOT / "Downloads" / (NAME + ".zip")
    destination.parent.mkdir(exist_ok=True)
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path, data in sorted(files.items()):
            info = zipfile.ZipInfo(NAME + "/" + path, (2026, 10, 25, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = (0o100755 if path.endswith(".command") else 0o100644) << 16
            archive.writestr(info, data)
    print(destination.relative_to(ROOT), len(files), "files,", destination.stat().st_size, "bytes")
    return destination


if __name__ == "__main__":
    build()
