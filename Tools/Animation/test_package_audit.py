import io
from pathlib import Path
import tarfile
import tempfile
import unittest
from audit_package import inspect


class ArchiveSafetyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.package = self.root / "input.unitypackage"
        self.output = self.root / "project"
        self.output.mkdir()

    def tearDown(self):
        self.temp.cleanup()

    def archive(self, rows, extra=None):
        with tarfile.open(self.package, "w:gz") as archive:
            for index, (path, data) in enumerate(rows):
                guid = f"{index + 1:032x}"
                for name, content in [("pathname", path.encode()), ("asset", data)]:
                    member = tarfile.TarInfo(guid + "/" + name)
                    member.size = len(content)
                    archive.addfile(member, io.BytesIO(content))
            if extra:
                archive.addfile(extra)

    def test_valid_nested_paths_and_spaces_are_preserved(self):
        path = "Assets/PolyOne/Basic Motions/Example.prefab"
        self.archive([(path, b"fixture")])
        report = inspect(self.package, self.output)
        self.assertEqual(report["asset_count"], 1)
        self.assertEqual((self.output / path).read_bytes(), b"fixture")
        self.assertFalse(report["native_unity_import_tested"])

    def test_relative_traversal_is_rejected_without_writes(self):
        self.archive([("Assets/PolyOne/../../escape.prefab", b"bad")])
        with self.assertRaises(ValueError): inspect(self.package, self.output)
        self.assertEqual(list(self.output.iterdir()), [])

    def test_absolute_paths_are_rejected(self):
        self.archive([("/Assets/PolyOne/x.prefab", b"bad")])
        with self.assertRaises(ValueError): inspect(self.package, self.output)

    def test_backslash_traversal_is_rejected(self):
        self.archive([("Assets/PolyOne/..\\x.prefab", b"bad")])
        with self.assertRaises(ValueError): inspect(self.package, self.output)

    def test_scripts_and_binaries_cannot_be_extracted(self):
        for suffix in [".cs", ".dll", ".dylib", ".so"]:
            self.archive([("Assets/PolyOne/code" + suffix, b"bad")])
            with self.assertRaises(ValueError): inspect(self.package, self.output)

    def test_existing_different_file_aborts_all_writes(self):
        first = "Assets/PolyOne/a.prefab"
        second = "Assets/PolyOne/b.prefab"
        existing = self.output / second
        existing.parent.mkdir(parents=True)
        existing.write_bytes(b"user art")
        self.archive([(first, b"new"), (second, b"different")])
        with self.assertRaises(FileExistsError): inspect(self.package, self.output)
        self.assertEqual(existing.read_bytes(), b"user art")
        self.assertFalse((self.output / first).exists())

    def test_duplicate_paths_are_rejected(self):
        self.archive([("Assets/PolyOne/x.prefab", b"1"), ("Assets/PolyOne/x.prefab", b"2")])
        with self.assertRaises(ValueError): inspect(self.package, self.output)
        self.assertEqual(list(self.output.iterdir()), [])

    def test_archive_links_are_rejected(self):
        link = tarfile.TarInfo("link")
        link.type = tarfile.SYMTYPE
        link.linkname = "/tmp"
        self.archive([], link)
        with self.assertRaises(ValueError): inspect(self.package, self.output)

    def test_existing_symlink_cannot_escape_extraction_root(self):
        outside = self.root / "outside"
        outside.mkdir()
        (self.output / "Assets").symlink_to(outside, target_is_directory=True)
        self.archive([("Assets/PolyOne/x.prefab", b"bad")])
        with self.assertRaises(ValueError): inspect(self.package, self.output)
        self.assertEqual(list(outside.iterdir()), [])


if __name__ == "__main__":
    unittest.main()
