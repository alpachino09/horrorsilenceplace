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
VERSION = "0.4.8"
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
    files["README_RU.md"] = (f"SILENCE Human Motion {VERSION}\n\n"
        "0.4.8 исправляет ложные ошибки Пустые bounds для Teeth_Lower/Teeth_Upper/Tongue и их LOD.\n"
        "Размер localBounds зависит от масштаба FBX: маленький меш не является пустым.\n"
        "Обнови код через 00 из этой распакованной папки, дождись компиляции.\n"
        "В уже созданном ..._Character мире нажми 27 → Проверить ссылки камеры и персонажа.\n"
        "Для этого исправления не нужно создавать ещё одну копию мира или повторять 25.\n"
        "Подробности: Docs/CharacterRework/SMALL_BOUNDS_FIX_RU.md.\n\n"
        "0.4.7: измерение skin по костям и bind poses вместо преобразования результата BakeMesh.\n"
        "Это обновление для ошибки Basic_Idle / Body_Clothed / размер 130 метров после 0.4.6.\n"
        "Останови Play, обнови код через 00 из этой распакованной папки, дождись компиляции.\n"
        "Оставь открытым свой текущий игровой ..._Motion мир и снова создай копию через 27.\n"
        "25, импорт модели и ручная смена протокола для этого обновления не нужны.\n"
        "Реальная большая деформация не уменьшается автоматически: при отказе полная диагностика\n"
        "появится в Console как SILENCE CharacterRework diagnostics и сохранится в Library/Silence.\n"
        "Подробности: Docs/CharacterRework/SKINNING_SCALE_FIX_RU.md.\n"
        "Математика проверена также на настоящих деформированных вершинах Blender-модели;\n"
        "нативный Humanoid-сэмплинг в Unity 6000.6.7f1 в облаке не запускался.\n\n"
        "0.4.6 исправляет проверку bounds в пункте 27: отдельные позы, полная матрица масштаба, изолированный AnimatorController.\n"
        "Если 27 остановился на Body_Clothed: обнови через 00, дождись компиляции и снова создай копию через 27\n"
        "из своего ТЕКУЩЕГО ..._Motion мира. Повторять импорт пакета и генерацию анимаций через 25 ради старого сообщения не нужно.\n"
        "Протокол вручную не меняй: он настроится при успешной установке копии.\n"
        "При повторном отказе детали сохраняются в Library/Silence/CharacterReworkFailure.json.\n\n"
        "0.4.5 исправляет два жёлтых CS0618 в BasicMotionsCatalog: новый API импорта для Unity 6.6+.\n"
        "Если 0.4.4 уже установлена: обнови код через 00, дождись компиляции и нажми Console → Clear.\n"
        "Ради этих предупреждений не нужно повторять импорт пакета или создание мира через 27.\n\n"
        "Ниже — шаги первой установки технической переработки персонажа.\n"
        "Новое: общая защита рук от проникновения в тело/ноги, исправление порядка IK,\n"
        "непрозрачные материалы, проверка рига/весов и bounds на настоящих клипах. V меняет вид.\n\n"
        "Останови Play. В Unity: SILENCE → 00 Update Sources From Folder.\n"
        "Выбери ЭТУ распакованную папку с Assets и LocalPackages внутри, не Unity-проект.\n"
        "После компиляции открой СВОЙ НЫНЕШНИЙ игровой мир с героиней, не старую Campaign и не просмотр 23.\n"
        "SILENCE → 27 Character Rework and Audit → Создать копию мира: исправить и проверить персонажей.\n"
        "Новая сцена ..._Character откроется сама. Play → Создать сессию → Начать раунд. V — сменить вид.\n"
        "При уже установленной героине не нужно повторять импорт Basic Motions или шаг 25.\n"
        "Для первой установки героини используй 21, для её анимаций — 25.\n"
        "Для кооператива заново собери и Host, и Client из новой сцены через окно 27.\n"
        "Площадка проверки отдельная; кнопка Возврата в окне 25 снова открывает игровой мир.\n"
        "Переработка персонажей: Docs/CharacterRework/README_RU.md и REPORT_RU.md.\n"
        "Код и исходные данные проверены в облаке. Unity Play/ILPP/сеть/качество движений ещё требуют проверки.\n").encode()
    manifest = {"version": VERSION, "kind": "code-only update; import the original user-provided Unity animation package separately",
                "requires_existing_prepared_hooded_girl": True, "native_unity_play_tested": False,
                "camera_body_menu": "SILENCE/26 Player Camera and Body", "perspective_key": "V",
                "character_rework_menu": "SILENCE/27 Character Rework and Audit",
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
