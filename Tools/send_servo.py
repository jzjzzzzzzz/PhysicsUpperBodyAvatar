#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Send only virtual-avatar targets over localhost TCP; no physical servo output."""
import argparse
import json
if __package__:
    from .hdt_client import request
else:
    from hdt_client import request


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
