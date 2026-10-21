#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Check licensing scope and source assembly boundaries without Unity."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def validate():
    license_text = (ROOT / "LICENSE").read_text()
    assert "Mozilla Public License Version 2.0" in license_text
    assert "MIT License" in (ROOT / "LICENSES/Legacy-MIT.txt").read_text()
    third_party = (ROOT / "Assets/ThirdParty/MicrosoftRocketbox/LICENSE.md").read_text()
    assert "MIT License" in third_party and "2020 Microsoft" in third_party
    sources = []
    for directory in ("Assets/Scripts", "Assets/Editor", "Assets/Tests", "Tools", "Tests"):
        sources.extend(p for p in (ROOT / directory).rglob("*") if p.suffix in {".cs", ".py", ".sh"})
    for path in sources:
        text = path.read_text()
        assert "SPDX-License-Identifier: MPL-2.0" in text[:500], path
        if path.is_relative_to(ROOT / "Assets/Scripts"):
            assert "using UnityEditor" not in text, path
    assert "UnityEngine" not in (ROOT / "Assets/Scripts/Networking/LocalCommandTransport.cs").read_text()
    for path in (ROOT / "Assets").rglob("*.asmdef"):
        data = json.loads(path.read_text())
        if data["name"].endswith(".Editor"):
            assert data.get("includePlatforms") == ["Editor"], path
    assert re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", (ROOT / "VERSION").read_text().strip())
    for path in (ROOT / "TestResults").rglob("*.xml"):
        text = path.read_text()
        assert str(ROOT) not in text and str(Path.home()) not in text, path
    print(f"Repository checks passed: {len(sources)} original source files, MPL notices and assembly boundaries.")


if __name__ == "__main__":
    validate()
