#!/usr/bin/env python3
"""Send only virtual-avatar targets over localhost TCP; no physical servo output."""
import argparse
import json
import socket


def request(command):
    with socket.create_connection(("127.0.0.1", 8765), timeout=6) as connection:
        connection.sendall((json.dumps(command, allow_nan=False) + "\n").encode())
        with connection.makefile("rb") as stream:
            return json.loads(stream.readline(8193))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--side", choices=["left", "right"], required=True)
    parser.add_argument("--values", type=float, nargs=13, metavar="ANGLE")
    args = parser.parse_args()
    command = {"action": "servo_pose" if args.values is not None else "servo_state", "side": args.side}
    if args.values is not None:
        command["values"] = args.values
    response = request(command)
    print(json.dumps(response, ensure_ascii=False, indent=2))
    raise SystemExit(0 if response.get("accepted") else 1)
