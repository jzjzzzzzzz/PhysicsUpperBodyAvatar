#!/usr/bin/env python3
"""Run while the app is open; exercises the actual localhost TCP interface."""
import json
from pathlib import Path
import socket
from send_command import send


def raw(payload):
    with socket.create_connection(("127.0.0.1", 8765), timeout=6) as conn:
        conn.sendall((payload + "\n").encode())
        with conn.makefile("rb") as stream:
            return json.loads(stream.readline(8193))


tests = []
for action in ("idle", "handshake", "wave", "double_thumbs_up"):
    response = send(action, 2.5 if action != "idle" else 0)
    tests.append({"name": action, "passed": response.get("accepted") is True, "response": response})
for name, command in (
    ("unknown action", '{"action":"unknown"}'),
    ("short duration", '{"action":"wave","duration":0.1}'),
    ("negative duration", '{"action":"wave","duration":-2}'),
    ("long duration", '{"action":"wave","duration":60}'),
    ("missing action", '{}'),
    ("malformed JSON", 'not-json'),
):
    response = raw(command)
    tests.append({"name": name, "passed": response.get("accepted") is False and bool(response.get("error")), "response": response})
send("idle")
state = raw('{"action":"servo_state","side":"right"}')
tests.append({"name": "13-channel state", "passed": state.get("accepted") is True and len(state.get("targetValues", [])) == 13, "response": state})
values = state["targetValues"]
for side in ("right", "left"):
    response = raw(json.dumps({"action": "servo_pose", "side": side, "values": values}))
    tests.append({"name": side + " servo pose", "passed": response.get("accepted") is True and len(response.get("targetValues", [])) == 13, "response": response})
for name, command in (
    ("wrong servo channel count", {"action": "servo_pose", "side": "right", "values": [90] * 12}),
    ("missing servo values", {"action": "servo_pose", "side": "right"}),
    ("invalid servo side", {"action": "servo_pose", "side": "other", "values": values}),
    ("out-of-range servo angle", {"action": "servo_pose", "side": "right", "values": [181] + values[1:]}),
    ("non-finite servo angle", {"action": "servo_pose", "side": "right", "values": values[:-1] + [float("nan")]}),
):
    response = raw(json.dumps(command))
    tests.append({"name": name, "passed": response.get("accepted") is False and bool(response.get("error")), "response": response})
after = raw('{"action":"servo_state","side":"right"}')
tests.append({"name": "invalid packets preserve target", "passed": after.get("targetValues") == values, "response": after})
send("idle")
report = {"mode": "Live standalone localhost TCP / newline-delimited JSON", "allPassed": all(t["passed"] for t in tests), "tests": tests}
path = Path(__file__).resolve().parent.parent / "TestResults" / "command-validation.json"
path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
print(json.dumps(report, ensure_ascii=False, indent=2))
raise SystemExit(0 if report["allPassed"] else 1)
