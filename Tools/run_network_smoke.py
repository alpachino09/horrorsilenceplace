"""Run real Unity development players; missing/current-run evidence is a failure."""
import argparse
import json
import math
import pathlib
import plistlib
import socket
import subprocess
import sys
import time
import uuid


def executable(build):
    path = pathlib.Path(build).expanduser().resolve()
    if path.suffix == ".app":
        with (path / "Contents/Info.plist").open("rb") as stream:
            name = plistlib.load(stream)["CFBundleExecutable"]
        path = path / "Contents/MacOS" / name
    if not path.is_file():
        raise ValueError(f"Unity player not found: {path}")
    return path


def validate_evidence(data, run_id, role, players):
    if not isinstance(data, dict):
        return ["Evidence must be a JSON object."]
    errors = []
    required = {
        "runId": run_id, "role": role, "expectedPlayers": players,
        "observedPlayers": players, "passed": True, "itemConsumed": True,
        "activeCameras": 1, "activeListeners": 1, "inputOwners": 1,
    }
    for key, value in required.items():
        if type(data.get(key)) is not type(value) or data.get(key) != value:
            errors.append(f"{key}: expected {value!r}, got {data.get(key)!r}")
    movement, ticks = data.get("movementMeters"), data.get("poseTicks")
    if (type(movement) not in (int, float) or not math.isfinite(movement) or movement <= 1 or
            type(ticks) is not int or not 30 < ticks <= 0xFFFFFFFF):
        errors.append("No sufficient movement/replication evidence.")
    if data.get("error"):
        errors.append(str(data["error"]))
    if role == "host" and data.get("authoritativeItems") != 1:
        errors.append("Contested item must exist in exactly one authoritative inventory.")
    if role == "host" and players > 1 and data.get("cleanupConfirmed") is not True:
        errors.append("Disconnect cleanup not confirmed.")
    if type(data.get("localItems")) is not int or data["localItems"] not in (0, 1):
        errors.append("Owner inventory evidence is invalid.")
    return errors


def validate_inventory_replication(reports):
    # Compare owners' actual snapshots, not just the server's authoritative count.
    return [] if sum(report["localItems"] for report in reports) == 1 else [
        "Owner inventory snapshots must contain the contested item exactly once across all processes."]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host-build", required=True, help="Host/SILENCE.app, .exe or Linux player")
    parser.add_argument("--client-build", help="Client/SILENCE.app, .exe or Linux player")
    parser.add_argument("--players", type=int, choices=(1, 2, 4), default=2)
    parser.add_argument("--output", help="New evidence directory; must not exist")
    args = parser.parse_args()
    run_id = str(uuid.uuid4())
    output = pathlib.Path(args.output or f"TestResults/Network-{run_id}").resolve()
    processes, errors, reports = [], [], []
    try:
        host = executable(args.host_build)
        if args.players > 1 and not args.client_build:
            raise ValueError("--client-build is required for multiple processes.")
        client = executable(args.client_build) if args.players > 1 else host
        output.mkdir(parents=True, exist_ok=False)
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.bind(("127.0.0.1", 0))
            port = probe.getsockname()[1]
        for index in range(args.players):
            role = "host" if index == 0 else "client"
            prefix = f"{role}-{index}"
            evidence = output / f"{prefix}.json"
            command = [str(host if index == 0 else client), "-batchmode", "-nographics",
                       "-logFile", str(output / f"{prefix}.log"),
                       "--silence-role", role, "--silence-run", run_id,
                       "--silence-players", str(args.players), "--silence-port", str(port),
                       "--silence-evidence", str(evidence)]
            stream = (output / f"{prefix}-process.log").open("w", encoding="utf-8")
            try:
                process = subprocess.Popen(command, stdout=stream, stderr=subprocess.STDOUT)
            finally:
                stream.close()
            processes.append((role, process, evidence))
            if index == 0 and args.players > 1:
                time.sleep(1)
        deadline = time.monotonic() + 75
        while any(p.poll() is None for _, p, _ in processes) and time.monotonic() < deadline:
            time.sleep(.2)
        for role, process, evidence in processes:
            if process.poll() is None:
                errors.append(f"{role}: player timed out.")
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
            if process.returncode != 0:
                errors.append(f"{role}: process exit {process.returncode}.")
            if not evidence.is_file():
                errors.append(f"{role}: no evidence file; no pass assumed.")
                continue
            try:
                data = json.loads(evidence.read_text(encoding="utf-8"))
                errors.extend(f"{evidence.name}: {error}" for error in validate_evidence(data, run_id, role, args.players))
                reports.append(data)
            except (ValueError, TypeError) as exception:
                errors.append(f"{role}: invalid evidence: {exception}")
        if not errors and len(reports) == args.players:
            errors.extend(validate_inventory_replication(reports))
    except (OSError, ValueError, KeyError) as exception:
        errors.append(str(exception))
    finally:
        for _, process, _ in processes:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
    summary = {"kind": "real Unity process test", "runId": run_id, "players": args.players,
               "passed": not errors and len(reports) == args.players, "errors": errors, "reports": reports}
    # Never write a result into an existing directory that was not created for this run.
    if output.is_dir() and processes:
        (output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
