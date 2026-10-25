"""Exercise the exact shipped macOS bash installer on disposable projects.

Filesystem/copy/hash/rollback tests run in Linux. macOS folder picker and
native Unity compilation require the user's Mac and are not claimed here.
"""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile
from build_update import ROOT, NAME

FILES = ["LocalPackages/com.silence.multiplayer/Editor/Visual/CharacterReworkWorkshop.cs",
         "Assets/_Silence/Scripts/Core/SkinBoundsModel.cs"]


class BoundsInstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="SILENCE bounds test ")
        self.base = Path(self.temp.name)
        self.patch = self.base / "patch with spaces"
        self.project = self.base / "Unity project with spaces"
        self.originals = {}
        with zipfile.ZipFile(ROOT / "Downloads" / (NAME + ".zip")) as archive:
            for rel in ["Install_Bounds_Fix.command"] + FILES:
                target = self.patch / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.read(NAME + "/" + rel))
        for rel in FILES:
            target = self.project / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            self.originals[rel] = ("previous source " + rel + "\n").encode()
            target.write_bytes(self.originals[rel])
            Path(str(target) + ".meta").write_bytes(b"original meta guid\n")
        settings = self.project / "ProjectSettings/ProjectVersion.txt"
        settings.parent.mkdir(parents=True)
        settings.write_text("m_EditorVersion: 6000.6.7f1\n")
        (self.project / "Assets/MyCurrentWorld.unity").write_text("keep current world\n")
        self.before = self.snapshot()

    def tearDown(self):
        self.temp.cleanup()

    def snapshot(self):
        return {p.relative_to(self.project).as_posix(): p.read_bytes()
                for p in self.project.rglob("*") if p.is_file() and "Library" not in p.relative_to(self.project).parts}

    def run_installer(self, project=None, env=None):
        return subprocess.run(["bash", str(self.patch / "Install_Bounds_Fix.command"), str(project or self.project)],
                              text=True, capture_output=True, timeout=15, env=env)

    def test_replaces_only_two_sources_preserves_meta_world_and_backs_up_both(self):
        result = self.run_installer()
        self.assertEqual(result.returncode, 0, result.stderr)
        expected = dict(self.before)
        for rel in FILES:
            expected[rel] = (self.patch / rel).read_bytes()
        self.assertEqual(self.snapshot(), expected)
        backups = list((self.project / "Library/Silence/SourceBackups").iterdir())
        self.assertEqual(len(backups), 1)
        for rel in FILES:
            self.assertEqual((backups[0] / rel).read_bytes(), self.originals[rel])
        receipt = (self.project / "Library/Silence/BoundsFixInstalled.txt").read_text()
        self.assertIn("0.4.9", receipt)
        self.assertIn(str(self.project), receipt)
        self.assertIn("Версия проверки: 0.4.9", result.stdout)

    def test_wrong_folder_is_rejected_without_project_changes(self):
        wrong = self.base / "not a Unity project"
        wrong.mkdir()
        result = self.run_installer(wrong)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(list(wrong.iterdir()), [])
        self.assertEqual(self.snapshot(), self.before)

    def test_tampered_archive_source_is_rejected_before_any_write(self):
        (self.patch / FILES[1]).write_text("changed after extraction\n")
        result = self.run_installer()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.snapshot(), self.before)
        self.assertFalse((self.project / "Library").exists())

    def test_second_copy_failure_restores_both_original_sources(self):
        binary = self.base / "test tools"
        binary.mkdir()
        cp = binary / "cp"
        cp.write_text('#!/bin/bash\nif [ "$1" = "$SILENCE_TEST_PATCH_ROOT/Assets/_Silence/Scripts/Core/SkinBoundsModel.cs" ]; then exit 42; fi\nexec /bin/cp "$@"\n')
        cp.chmod(0o755)
        env = dict(os.environ, PATH=str(binary) + os.pathsep + os.environ["PATH"], SILENCE_TEST_PATCH_ROOT=str(self.patch))
        result = self.run_installer(env=env)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.snapshot(), self.before)
        self.assertIn("прежний код восстановлен", result.stderr)
        self.assertFalse((self.project / "Library/Silence/BoundsFixInstalled.txt").exists())

    def test_reinstall_keeps_correct_sources_and_separate_backups(self):
        first = self.run_installer()
        self.assertEqual(first.returncode, 0, first.stderr)
        snapshot = self.snapshot()
        second = self.run_installer()
        self.assertEqual(second.returncode, 0, second.stderr)
        self.assertEqual(self.snapshot(), snapshot)
        self.assertEqual(len(list((self.project / "Library/Silence/SourceBackups").iterdir())), 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
