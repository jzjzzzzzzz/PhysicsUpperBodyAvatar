# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Small, strict NDJSON client for the local Unity command endpoint."""
import json
import socket

HOST = "127.0.0.1"
DEFAULT_PORT = 8765
MAX_FRAME_BYTES = 8192


def request(command, port=DEFAULT_PORT, timeout=6):
    """Return a protocol reply; accepted=false is an application-level rejection."""
    payload = (json.dumps(command, allow_nan=False) + "\n").encode("utf-8")
    if len(payload) - 1 >= MAX_FRAME_BYTES:
        raise ValueError("Command exceeds the protocol frame limit.")
    with socket.create_connection((HOST, port), timeout=timeout) as connection:
        connection.sendall(payload)
        with connection.makefile("rb") as stream:
            response = stream.readline(MAX_FRAME_BYTES + 1)
    if not response:
        raise RuntimeError("No response. Confirm that the Unity application is running.")
    if not response.endswith(b"\n") or len(response) > MAX_FRAME_BYTES:
        raise ValueError("Incomplete or oversized command response.")
    reply = json.loads(response)
    if not isinstance(reply, dict) or not isinstance(reply.get("accepted"), bool):
        raise ValueError("Response is not an avatar command reply.")
    return reply
