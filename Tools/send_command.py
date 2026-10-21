#!/usr/bin/env python3
"""Standard-library localhost client: no pip dependencies or external service."""
import argparse
import json
import socket


def send(action: str, duration: float = 0, port: int = 8765) -> dict:
    command = {"action": action}
    if duration:
        command["duration"] = duration
    with socket.create_connection(("127.0.0.1", port), timeout=6) as connection:
        connection.sendall((json.dumps(command) + "\n").encode("utf-8"))
        with connection.makefile("rb") as stream:
            response = stream.readline(8193)
    if not response:
        raise RuntimeError("No response. Confirm that the Unity application is running.")
    return json.loads(response)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["handshake", "wave", "double_thumbs_up", "idle"])
    parser.add_argument("--duration", type=float, default=0, help="0 = default; otherwise 1.2–15 seconds")
    parser.add_argument("--port", type=int, default=8765)
    options = parser.parse_args()
    print(json.dumps(send(options.action, options.duration, options.port), indent=2))
