"""Run the delivered full source installer against disposable Unity projects."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile
from build_update import ROOT, NAME, VERSION


class FullInstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix="SILENCE full update ")
        self.base=Path(self.temp.name);self.patch=self.base/"patch with spaces";self.project=self.base/"Unity with spaces"
        self.sources={}
        with zipfile.ZipFile(ROOT/"Downloads"/(NAME+".zip")) as archive:
            for item in archive.namelist():
                rel=item[len(NAME)+1:]
                if rel.startswith(("Assets/","LocalPackages/")) or rel=="Install_Update.command":
                    target=self.patch/rel;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(archive.read(item))
                    if rel!="Install_Update.command":self.sources[rel]=target.read_bytes()
        existing={"ProjectSettings/ProjectVersion.txt":b"m_EditorVersion: 6000.6.7f1\n",
            "LocalPackages/com.silence.multiplayer/package.json":b"previous package\n",
            "Assets/_Silence/Scripts/Core/SkinBoundsModel.cs":b"previous source\n",
            "Assets/_Silence/Scripts/Core/SkinBoundsModel.cs.meta":b"keep guid\n",
            "Assets/MyWorld.unity":b"keep world\n",
            "Assets/Kevin Iglesias/keep.anim":b"vendor fixture marker\n",
            "Assets/ObjectiveEnvironment_Assets/keep.png":b"vendor fixture marker\n"}
        for rel,data in existing.items():
            target=self.project/rel;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
        self.before=self.snapshot()
    def tearDown(self):self.temp.cleanup()
    def snapshot(self):return {p.relative_to(self.project).as_posix():p.read_bytes() for p in self.project.rglob('*') if p.is_file() and 'Library' not in p.relative_to(self.project).parts}
    def run_update(self,project=None,env=None):return subprocess.run(['bash',str(self.patch/'Install_Update.command'),str(project or self.project)],capture_output=True,text=True,env=env,timeout=90)
    def test_full_update_adds_new_code_preserves_vendor_art_world_and_meta(self):
        result=self.run_update();self.assertEqual(result.returncode,0,result.stderr)
        self.assertEqual(self.snapshot(),dict(self.before,**self.sources))
        receipt=(self.project/'Library/Silence/UpdateInstalled.txt').read_text();self.assertIn(VERSION,receipt)
        backups=list((self.project/'Library/Silence/SourceBackups').iterdir());self.assertEqual(len(backups),1)
        self.assertEqual((backups[0]/'Assets/_Silence/Scripts/Core/SkinBoundsModel.cs').read_bytes(),b'previous source\n')
        self.assertIn('28 Imported Textures and Human Motions',result.stdout)
    def test_wrong_project_is_rejected_before_writing(self):
        wrong=self.base/'wrong';wrong.mkdir();result=self.run_update(wrong)
        self.assertNotEqual(result.returncode,0);self.assertEqual(list(wrong.iterdir()),[]);self.assertEqual(self.snapshot(),self.before)
    def test_tampered_source_is_rejected_before_writing(self):
        (self.patch/'LocalPackages/com.silence.multiplayer/Editor/Visual/ImportedAssetsWorkshop.cs').write_text('tampered')
        result=self.run_update();self.assertNotEqual(result.returncode,0);self.assertEqual(self.snapshot(),self.before);self.assertFalse((self.project/'Library').exists())
    def test_copy_failure_restores_old_code_and_removes_new_files(self):
        tools=self.base/'test tools';tools.mkdir();cp=tools/'cp'
        cp.write_text('#!/bin/bash\nif [ "$1" = "$SILENCE_TEST_PATCH_ROOT/LocalPackages/com.silence.multiplayer/Editor/Visual/ImportedAssetsWorkshop.cs" ]; then exit 42; fi\nexec /bin/cp "$@"\n');cp.chmod(0o755)
        env=dict(os.environ,PATH=str(tools)+os.pathsep+os.environ['PATH'],SILENCE_TEST_PATCH_ROOT=str(self.patch))
        result=self.run_update(env=env);self.assertNotEqual(result.returncode,0);self.assertEqual(self.snapshot(),self.before)
        self.assertFalse((self.project/'Library/Silence/UpdateInstalled.txt').exists())
    def test_reinstall_preserves_sources_and_separate_backups(self):
        first=self.run_update();self.assertEqual(first.returncode,0,first.stderr);expected=self.snapshot()
        second=self.run_update();self.assertEqual(second.returncode,0,second.stderr);self.assertEqual(self.snapshot(),expected)
        self.assertEqual(len(list((self.project/'Library/Silence/SourceBackups').iterdir())),2)


if __name__=='__main__':unittest.main(verbosity=2)
