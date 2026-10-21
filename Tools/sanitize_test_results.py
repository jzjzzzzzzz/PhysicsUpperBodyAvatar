#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Redact local path prefixes from public reports without changing test results."""
from pathlib import Path
import shutil
import sys
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parent.parent


def sanitize(path):
    path = Path(path)
    raw = path.read_text()
    ElementTree.fromstring(raw)
    archive = ROOT / "Logs" / (path.stem + "-raw.xml")
    archive.parent.mkdir(exist_ok=True)
    shutil.copy2(path, archive)
    cleaned = raw.replace(str(ROOT), "${PROJECT_ROOT}").replace(str(Path.home()), "${HOME}")
    ElementTree.fromstring(cleaned)
    path.write_text(cleaned)
    print("Public report paths redacted; unchanged raw report retained in ignored Logs/.")


if __name__ == "__main__":
    sanitize(sys.argv[1])
