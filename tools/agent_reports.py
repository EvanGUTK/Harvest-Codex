"""Collect reports or wait for a report update; requires Python 3.9+."""

import argparse
import hashlib
from pathlib import Path
import time

ROLES = ("simulation", "runtime", "experience", "persistence", "reviewer", "creative")


def read_report(path):
    try:
        return ("present", path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return ("missing", "Awaiting report")
    except (OSError, UnicodeError) as error:
        return ("error", str(error))


def collect(root, seen):
    for role in ROLES:
        report = root / role / "AGENT_REPORT.md"
        state = read_report(report)
        if state != seen.get(role):
            print(f"\n[{role}] {state[0]}: {report}\n{state[1]}", flush=True)
            seen[role] = state


def wait_for_update(root, role, digest, timeout):
    deadline = time.monotonic() + timeout
    report = root / role / "AGENT_REPORT.md"
    while True:
        state, content = read_report(report)
        if state == "present":
            current = hashlib.sha256(content.encode("utf-8")).hexdigest()
            if current != digest:
                print(f"[{role}] Updated report ({current})\n{content}", flush=True)
                return 0
        if time.monotonic() >= deadline:
            print(f"[{role}] No report update within {timeout:g}s", flush=True)
            return 2
        time.sleep(min(1, max(0, deadline - time.monotonic())))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--watch", action="store_true")
    parser.add_argument("--wait-for", choices=ROLES)
    parser.add_argument("--after-sha256", default="")
    parser.add_argument("--timeout", type=float, default=45)
    args = parser.parse_args()
    if not 0 < args.timeout <= 60:
        parser.error("timeout must be greater than 0 and at most 60 seconds")
    if args.watch and args.wait_for:
        parser.error("choose --watch or --wait-for")
    root = Path(__file__).resolve().parents[2]
    if args.wait_for:
        return wait_for_update(root, args.wait_for, args.after_sha256, args.timeout)
    seen = {}
    try:
        while True:
            collect(root, seen)
            if not args.watch:
                return 0
            time.sleep(2)
    except KeyboardInterrupt:
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
