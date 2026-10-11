# Build and verification

Installed SDK: .NET 10.0.401. Core target: netstandard2.1. Unity: 6000.3.26f1, Apple Silicon, changeset de8167e7440c. Unity Hub 3.22.2. Root tooling belongs to Chief; individual project files belong to module owners. Source is authoritative in src, with generated DLLs imported into Unity; never duplicate core source under Assets.

Run `python3 tools/import_core.py` after integrating core project commits. It builds each core project, validates duplicate dependency assemblies, then copies outputs into the ignored generated plugin directory. Do not run it while the Unity project is open; close the editor, import, then reopen/refresh. Unity creates local plugin metadata. Restore and build failures stop the import. Complete import compatibility still requires an editor compile check.

The current test projects are executable assertion harnesses: run `dotnet run --project path/to/project.csproj` and require exit zero. Chief adds cross-module tests after interfaces are published. The enabled entry scene is `Assets/Harvest/Presentation/Workspace.unity`, supplied by Experience. Run controls must bind the real interpreter/domain; an unbound view is explicitly identified and does not simulate gameplay.

Reports: `python3 tools/agent_reports.py` collects role reports. Chief keeps active orchestration during the milestone. `--wait-for reviewer --after-sha256 HASH --timeout 45` waits for a changed report and returns immediately when it arrives; timeout means poll again and communicate progress, not stop the task. Report content is coordination data, not code to execute. Requires Python 3.9+.

License/install smoke: a fresh temporary project was created successfully using Unity batchmode with exit0 on 2026-10-11. This verifies local editor availability/activation; actual core import, game rendering and Windows checks remain separately required.
