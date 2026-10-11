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
    assemblies = {}
    for project in projects:
        subprocess.run([
            "dotnet", "build", str(project), "--configuration", args.configuration,
            "--framework", "netstandard2.1", "--nologo",
            "-p:CopyLocalLockFileAssemblies=true",
        ], cwd=root, check=True)
        output = project.parent / "bin" / args.configuration / "netstandard2.1"
        for assembly in output.glob("*.dll"):
            digest = hashlib.sha256(assembly.read_bytes()).hexdigest()
            existing = assemblies.get(assembly.name)
            if existing and existing[1] != digest:
                raise RuntimeError(f"conflicting dependency versions: {assembly.name}")
            assemblies[assembly.name] = (assembly, digest)
    destination = root / "game/Assets/Harvest/Plugins/Generated"
    destination.mkdir(parents=True, exist_ok=True)
    # Prepare all copies before replacing existing assemblies. Keep Unity metadata.
    with tempfile.TemporaryDirectory(prefix="harvest-core-import-") as temporary:
        staging = Path(temporary)
        for name, (source, _) in assemblies.items():
            shutil.copy2(source, staging / name)
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
