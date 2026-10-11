"""Importer regression checks use isolated files and a fake successful compiler."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("import_core", Path(__file__).parents[2] / "tools/import_core.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ImportChecks(unittest.TestCase):
    def exercise(self, conflict):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            destination = root / "game/Assets/Harvest/Plugins/Generated"
            destination.mkdir(parents=True)
            (destination / "previous.dll").write_bytes(b"old")
            (destination / "previous.dll.meta").write_text("old-guid")
            (destination / "shared.dll.meta").write_text("stable-guid")
            for name in ("Harvest.One", "Harvest.Two"):
                project = root / "src" / name
                project.mkdir(parents=True)
                (project / (name + ".csproj")).write_text("fixture")
                stale = project / "bin/Release/netstandard2.1"
                stale.mkdir(parents=True)
                (stale / "unrelated.dll").write_bytes(b"stale")

            def build(command, **kwargs):
                self.assertTrue(kwargs["check"])
                output = Path(command[command.index("--output") + 1])
                self.assertFalse(output.exists())
                output.mkdir()
                name = Path(command[2]).stem
                (output / (name + ".dll")).write_bytes(name.encode())
                (output / "shared.dll").write_bytes(name.encode() if conflict else b"same")

            with patch.object(MODULE, "__file__", str(root / "tools/import_core.py")), patch.object(MODULE.subprocess, "run", side_effect=build), patch("sys.argv", ["import_core"]):
                if conflict:
                    with self.assertRaisesRegex(RuntimeError, "conflicting dependency"):
                        MODULE.main()
                    self.assertEqual((destination / "previous.dll").read_bytes(), b"old")
                    self.assertFalse((destination / "shared.dll").exists())
                else:
                    MODULE.main()
                    self.assertFalse((destination / "unrelated.dll").exists())
                    self.assertFalse((destination / "previous.dll").exists())
                    self.assertFalse((destination / "previous.dll.meta").exists())
                    self.assertEqual((destination / "shared.dll").read_bytes(), b"same")
                self.assertEqual((destination / "shared.dll.meta").read_text(), "stable-guid")

    def test_clean_outputs_exclude_stale_dlls_and_preserve_metadata(self):
        self.exercise(False)

    def test_conflicting_dependencies_preserve_destination(self):
        self.exercise(True)


if __name__ == "__main__":
    unittest.main()
