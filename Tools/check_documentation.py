#!/usr/bin/env python3
"""Check local documentation links, example syntax and the 13-channel worksheet."""
import ast
import csv
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parent.parent
IDS = [f"A{i}" for i in range(1, 8)] + [f"H{i}" for i in range(1, 7)]


def validate():
    documents = [ROOT / "README.md", *sorted((ROOT / "Documentation").rglob("*.md"))]
    counts = {"local_links": 0, "json_examples": 0, "python_examples": 0}
    for path in documents:
        source = path.read_text(encoding="utf-8")
        for match in re.finditer(r"!?\[[^\]]*\]\(([^\s)]+)\)", source):
            url = match.group(1)
            if urlsplit(url).scheme or url.startswith("#"):
                continue
            target = (path.parent / unquote(url.split("#", 1)[0])).resolve()
            if not target.exists():
                raise ValueError(f"Broken local link in {path.relative_to(ROOT)}: {url}")
            counts["local_links"] += 1
        for language, example in re.findall(r"```([\w+-]+)\n(.*?)\n```", source, re.DOTALL):
            if language == "json":
                try:
                    json.loads(example)
                except json.JSONDecodeError:
                    for line in example.splitlines():
                        if line.strip():
                            json.loads(line)
                counts["json_examples"] += 1
            elif language == "python":
                ast.parse(example, filename=str(path))
                counts["python_examples"] += 1
    for side in ("Left", "Right"):
        path = ROOT / f"Assets/StreamingAssets/Hardware/ServoRig{side}.json"
        profile = json.loads(path.read_text())
        if [c["id"] for c in profile["channels"]] != IDS:
            raise ValueError(f"Unexpected channel order: {path}")
        if [c["function"] for c in profile["channels"]] != list(range(13)):
            raise ValueError(f"Unexpected function order: {path}")
    with (ROOT / "Documentation/Hardware/servo-channel-map.csv").open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if [row["logical_id"] for row in rows] != IDS:
        raise ValueError("Calibration worksheet must preserve all 13 ordered logical channels.")
    if any("confirmed_function" not in row or "reference_file" not in row for row in rows):
        raise ValueError("Calibration worksheet is missing measurement/reference columns.")
    return {"documents": len(documents), **counts, "ordered_channels": len(IDS)}


if __name__ == "__main__":
    result = validate()
    print("Documentation checks passed: " + json.dumps(result, ensure_ascii=False))
