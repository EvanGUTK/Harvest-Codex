"""Build core libraries and import their generated assemblies into Unity."""

import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    projects = sorted((root / "src").glob("Harvest.*/*.csproj"))
    if not projects:
        parser.error("no core projects are present; integrate worker commits first")
    destination = root / "game/Assets/Harvest/Plugins/Generated"
    # Each build has fresh output; never import stale bin-directory assemblies.
    # Prepare and validate the entire set before changing Unity's destination.
    with tempfile.TemporaryDirectory(prefix="harvest-core-import-") as temporary:
        workspace = Path(temporary)
        assemblies = {}
        for project in projects:
            output = workspace / project.stem
            subprocess.run([
                "dotnet", "build", str(project), "--configuration", args.configuration,
                "--framework", "netstandard2.1", "--nologo", "--output", str(output),
                "-p:CopyLocalLockFileAssemblies=true",
            ], cwd=root, check=True)
            for assembly in output.glob("*.dll"):
                digest = hashlib.sha256(assembly.read_bytes()).hexdigest()
                existing = assemblies.get(assembly.name)
                if existing and existing[1] != digest:
                    raise RuntimeError(f"conflicting dependency versions: {assembly.name}")
                assemblies[assembly.name] = (assembly, digest)
        staging = workspace / "staging"
        staging.mkdir()
        for name, (source, _) in assemblies.items():
            shutil.copy2(source, staging / name)
        destination.mkdir(parents=True, exist_ok=True)
        for name in assemblies:
            shutil.copy2(staging / name, destination / name)
        for old in destination.glob("*.dll"):
            if old.name not in assemblies:
                old.unlink()
                metadata = old.with_name(old.name + ".meta")
                metadata.unlink(missing_ok=True)
    print(f"Imported {len(assemblies)} assemblies into {destination}")


if __name__ == "__main__":
    main()
