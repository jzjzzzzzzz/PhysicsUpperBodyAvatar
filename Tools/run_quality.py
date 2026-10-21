#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Run portable checks and save a machine-readable report; no Unity required."""
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
from Tools.check_documentation import validate as check_documents
from Tools.check_repository import validate as check_sources


def cases(suite):
    for item in suite:
        if isinstance(item, unittest.TestSuite):
            yield from cases(item)
        else:
            yield item


def main():
    suite = unittest.defaultTestLoader.discover(str(ROOT / "Tests/Python"))
    names = [case.id() for case in cases(suite)]
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    failed = {case.id() for case, _ in result.failures + result.errors + result.skipped}
    checks = []
    for name, run in (("documentation", check_documents), ("license-and-boundaries", check_sources)):
        try:
            run()
            checks.append({"name": name, "passed": True})
        except Exception as error:
            checks.append({"name": name, "passed": False, "error": str(error)})
    report = {
        "mode": "Python contract tests and static repository checks; not Unity physics",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "allPassed": result.wasSuccessful() and not result.skipped and all(c["passed"] for c in checks),
        "tests": [{"name": name, "passed": name not in failed} for name in names],
        "staticChecks": checks,
    }
    path = ROOT / "TestResults/repository-validation.json"
    path.parent.mkdir(exist_ok=True)
    path.write_text(json.dumps(report, indent=2) + "\n")
    return 0 if report["allPassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
