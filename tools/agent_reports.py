"""Read agent-owned reports without Paneflow return messaging."""

import argparse
from pathlib import Path
import time

ROLES = ("simulation", "runtime", "experience", "persistence", "reviewer", "creative")


def collect(root: Path, seen: dict[str, str]) -> None:
    for role in ROLES:
        report = root / role / "AGENT_REPORT.md"
        if not report.is_file():
            if role not in seen:
                print(f"[{role}] Awaiting report", flush=True)
                seen[role] = ""
            continue
        try:
            content = report.read_text(encoding="utf-8")
        except (OSError, UnicodeError) as error:
            print(f"[{role}] Could not read report: {error}", flush=True)
            continue
        if content != seen.get(role):
            print(f"\n[{role}] {report}\n{content}", flush=True)
            seen[role] = content


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--watch", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    seen: dict[str, str] = {}
    try:
        while True:
            collect(root, seen)
            if not args.watch:
                return
            time.sleep(2)
    except KeyboardInterrupt:
        return


if __name__ == "__main__":
    main()
