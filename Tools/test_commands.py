#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Exercise the real running Unity application's NDJSON endpoint."""
import argparse
import json
from pathlib import Path
import socket
import time

if __package__:
    from .send_command import send
else:
    from send_command import send


def raw(payload, port=8765):
    if isinstance(payload, str):
        payload = payload.encode("utf-8")
    with socket.create_connection(("127.0.0.1", port), timeout=6) as conn:
        conn.sendall(payload + b"\n")
        with conn.makefile("rb") as stream:
            return json.loads(stream.readline(8193))


def wait_until_ready(port, seconds=10):
    deadline = time.monotonic() + seconds
    while True:
        try:
            result = raw('{"action":"servo_state","side":"right"}', port)
            if result.get("accepted"):
                return
        except OSError:
            pass
        if time.monotonic() >= deadline:
            raise TimeoutError("Normal Unity command listener is not ready.")
        time.sleep(.1)


def validate(port=8765):
    tests = []

    def record(name, response, accepted):
        tests.append({"name": name, "passed": response.get("accepted") is accepted
                      and (accepted or bool(response.get("error"))), "response": response})

    wait_until_ready(port)
    for action in ("idle", "handshake", "wave", "double_thumbs_up"):
        record(action, send(action, 2.5 if action != "idle" else 0, port), True)
    for name, command in (
        ("unknown action", '{"action":"unknown"}'),
        ("short duration", '{"action":"wave","duration":0.1}'),
        ("negative duration", '{"action":"wave","duration":-2}'),
        ("long duration", '{"action":"wave","duration":60}'),
        ("missing action", '{}'),
        ("malformed JSON", 'not-json'),
    ):
        record(name, raw(command, port), False)
    send("idle", port=port)
    state = raw('{"action":"servo_state","side":"right"}', port)
    record("13-channel state", state, True)
    tests[-1]["passed"] &= len(state.get("targetValues", [])) == 13
    values = state["targetValues"]
    for side in ("right", "left"):
        record(side + " servo pose", raw(json.dumps({"action": "servo_pose", "side": side, "values": values}), port), True)
    for name, command in (
        ("wrong servo channel count", {"action": "servo_pose", "side": "right", "values": [90] * 12}),
        ("missing servo values", {"action": "servo_pose", "side": "right"}),
        ("invalid servo side", {"action": "servo_pose", "side": "other", "values": values}),
        ("out-of-range servo angle", {"action": "servo_pose", "side": "right", "values": [181] + values[1:]}),
        ("non-finite servo angle", {"action": "servo_pose", "side": "right", "values": values[:-1] + [float("nan")]}),
    ):
        record(name, raw(json.dumps(command), port), False)
    after = raw('{"action":"servo_state","side":"right"}', port)
    record("invalid packets preserve target", after, True)
    tests[-1]["passed"] &= after.get("targetValues") == values
    for name, command in (
        ("uppercase action", '{"action":"WAVE"}'),
        ("minimum duration", '{"action":"wave","duration":1.2}'),
        ("maximum duration", '{"action":"wave","duration":15}'),
    ):
        record(name, raw(command, port), True)
    record("invalid UTF-8 frame", raw(b'\xff', port), False)
    record("oversized frame", raw(b'x' * 8192, port), False)
    # The default server allows four in-flight clients. Hold them mid-frame.
    time.sleep(.1)
    held = []
    try:
        for _ in range(4):
            held.append(socket.create_connection(("127.0.0.1", port), timeout=6))
            held[-1].sendall(b'{')
        time.sleep(.1)
        record("bounded client admission", raw('{"action":"idle"}', port), False)
    finally:
        for connection in held:
            connection.close()
    wait_until_ready(port)
    send("idle", port=port)
    return {"mode": "Live standalone localhost TCP / newline-delimited JSON",
            "allPassed": all(t["passed"] for t in tests), "tests": tests}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parent.parent / "TestResults/command-validation.json")
    args = parser.parse_args()
    args.output.unlink(missing_ok=True)
    report = validate(args.port)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    raise SystemExit(0 if report["allPassed"] else 1)
