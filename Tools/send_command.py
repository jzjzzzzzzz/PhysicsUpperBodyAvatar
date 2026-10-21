#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Standard-library localhost client: no pip dependencies or external service."""
import argparse
import json
if __package__:
    from .hdt_client import request
else:
    from hdt_client import request


def send(action: str, duration: float = 0, port: int = 8765) -> dict:
    command = {"action": action}
    if duration:
        command["duration"] = duration
    return request(command, port=port)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["handshake", "wave", "double_thumbs_up", "idle"])
    parser.add_argument("--duration", type=float, default=0, help="0 = default; otherwise 1.2–15 seconds")
    parser.add_argument("--port", type=int, default=8765)
    options = parser.parse_args()
    print(json.dumps(send(options.action, options.duration, options.port), indent=2))
