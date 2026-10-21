#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Stage and validate source/app packages, including offline user guides."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import plistlib
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Distribution"
REPORTS = (
    "physics-validation.json",
    "Standalone/runtime-validation.json",
    "command-validation.json",
    "repository-validation.json",
)
SOURCE_ITEMS = (
    "Assets", "Packages", "ProjectSettings", "Documentation", "Tools", "Tests", "LICENSES", ".github",
    "TestResults", "LICENSE", "VERSION", ".gitignore", ".gitattributes", ".editorconfig",
    *[p.name for p in sorted(ROOT.glob("*.md"))],
)



def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def files(item):
    path = ROOT / item
    if not path.exists():
        raise FileNotFoundError(path)
    for candidate in sorted(path.rglob("*")) if path.is_dir() else [path]:
        if candidate.is_file() and "__pycache__" not in candidate.parts and candidate.name != ".DS_Store" and candidate.suffix != ".pyc":
            yield candidate


def add_files(archive, item, prefix=Path()):
    for path in files(item):
        archive.write(path, prefix / path.relative_to(ROOT))


def read_reports():
    summary = []
    for name in REPORTS:
        path = ROOT / "TestResults" / name
        report = json.loads(path.read_text())
        tests = report.get("tests", [])
        if not report.get("allPassed") or not tests or not all(test.get("passed") for test in tests):
            raise ValueError(f"Missing or failing test results: {path}")
        summary.append({
            "path": "TestResults/" + name,
            "passed": len(tests),
            "total": len(tests),
            "sha256": sha256(path),
        })
    from xml.etree import ElementTree
    unit_path = ROOT / "TestResults/editmode-results.xml"
    unit = ElementTree.parse(unit_path).getroot()
    if unit.get("result") != "Passed" or int(unit.get("failed", "1")) != 0 or int(unit.get("total", "0")) == 0:
        raise ValueError("Missing or failing Unity EditMode tests.")
    summary.append({"path": "TestResults/editmode-results.xml", "passed": int(unit.get("passed")),
                    "total": int(unit.get("total")), "sha256": sha256(unit_path)})
    return summary


def main():
    OUT.mkdir(exist_ok=True)
    subprocess.run([sys.executable, str(ROOT / "Tools/check_documentation.py")], check=True)
    reports = read_reports()
    app = ROOT / "Builds/macOS/PhysicsUpperBodyAvatar.app"
    with (app / "Contents/Info.plist").open("rb") as handle:
        info = plistlib.load(handle)
    executable = app / "Contents/MacOS" / info["CFBundleExecutable"]
    if not executable.is_file():
        raise FileNotFoundError("Build the macOS application first.")
    subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)

    # Existing deliverables remain intact if validation or packaging fails.
    with tempfile.TemporaryDirectory(prefix=".packaging-", dir=OUT) as temporary:
        staging = Path(temporary)
        source_zip = staging / "PhysicsUpperBodyAvatar-UnityProject.zip"
        with zipfile.ZipFile(source_zip, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
            for item in SOURCE_ITEMS:
                add_files(archive, item, Path(ROOT.name))

        app_zip = staging / "PhysicsUpperBodyAvatar-macOS-ARM64.zip"
        # ditto preserves the bundle's executable bits and macOS metadata.
        subprocess.run(["ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", str(app), str(app_zip)], check=True)
        app_readme = """# ALS HDT 数字人应用

1. 本包适用于 Apple Silicon Mac。打开同目录的 `PhysicsUpperBodyAvatar.app`。
2. 初次使用先阅读 [快速使用指南](Documentation/GETTING_STARTED.md)。
3. 机械手对应关系见 [13 路映射与标定](Documentation/HARDWARE_MAPPING.md)。
4. Python/外部程序连接方式见 [本地命令接口](Documentation/COMMAND_API.md)。

应用内含真人风格角色、物理驱动上肢、三个预设动作和 13 路输入滑块。
当前是待实物标定的软件原型，不向实物舵机下发控制。

`Documentation/` 为完整文档，`TestResults/` 为现有软件测试报告及截图，
`Tools/` 为可选 Python 命令客户端。运行应用本身不需要 Python、Unity Editor 或网络。
需要修改 Unity 工程时，请另取 `PhysicsUpperBodyAvatar-UnityProject.zip`。
"""
        with zipfile.ZipFile(app_zip, "a", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
            archive.writestr("README.md", app_readme)
            for item in ("Documentation", "TestResults", "Tools/send_command.py", "Tools/send_servo.py", "Tools/hdt_client.py", "LICENSE", "NOTICE.md", "LICENSES"):
                add_files(archive, item)

        artifacts = []
        for path in (source_zip, app_zip):
            with zipfile.ZipFile(path) as archive:
                damaged = archive.testzip()
                if damaged:
                    raise ValueError(f"Corrupt archive member: {damaged}")
            artifacts.append({"name": path.name, "bytes": path.stat().st_size, "sha256": sha256(path)})
        try:
            revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, stderr=subprocess.DEVNULL, text=True).strip()
            dirty = bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip())
        except (OSError, subprocess.CalledProcessError):
            revision, dirty = None, None
        manifest = {
            "schema": "als-hdt-release-v1",
            "version": (ROOT / "VERSION").read_text().strip(),
            "gitRevision": revision,
            "workingTreeDirty": dirty,
            "license": "MPL-2.0 (original sources); third-party exceptions in NOTICE.md",
            "packagedAtUtc": datetime.now(timezone.utc).isoformat(),
            "platform": "macOS ARM64 / Mono",
            "unityVersion": json.loads((ROOT / "TestResults/physics-validation.json").read_text())["unityVersion"],
            "appExecutableSha256": sha256(executable),
            "packagingRunsUnityTests": False,
            "validationReports": reports,
            "artifacts": artifacts,
        }
        (staging / "RELEASE_MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
        (staging / "SHA256SUMS.txt").write_text("".join(f"{item['sha256']}  {item['name']}\n" for item in artifacts))
        delivery_readme = f"""# ALS HDT 数字人交付目录

## 选择需要的包

| 文件 | 用途 |
|---|---|
| [macOS 应用 ZIP](PhysicsUpperBodyAvatar-macOS-ARM64.zip) | Apple Silicon Mac 直接运行；附使用文档、映射说明、测试报告和截图 |
| [Unity 源码 ZIP](PhysicsUpperBodyAvatar-UnityProject.zip) | 修改、重新构建；包含源模型和全部配置，不包含 Unity 缓存 |
| [SHA256SUMS.txt](SHA256SUMS.txt) | 检查两个 ZIP 的 SHA256 |
| [RELEASE_MANIFEST.json](RELEASE_MANIFEST.json) | 构建信息、可执行文件摘要和测试报告摘要 |

解压后先读根目录 `README.md`，再看 `Documentation/GETTING_STARTED.md`。
应用 ZIP 内的工程路径说明用于定位源码；源码需使用单独的 Unity 源码 ZIP。

## 当前范围

- 真人风格上半身，PhysX 动态双臂和手指；握手、挥手、双手点赞。
- 一套设备 7 路手臂 + 6 路手掌的逻辑模板；左右侧均可选。
- 实物轴序、舵机 ID、行程与零位待确认；当前没有实物控制输出。
- 现有报告：Editor {reports[0]['passed']} 项、独立应用 {reports[1]['passed']} 项、TCP {reports[2]['passed']} 项、Python {reports[3]['passed']} 项、Unity EditMode {reports[4]['passed']} 项通过。
- 打包会检查现有报告和文档，不重新执行 Unity 测试；测试范围以 `Documentation/TESTING.md` 为准。

旧版人偶包位于 `Archive-Mannequin-v1/`，不属于当前默认角色。
"""
        (staging / "README.md").write_text(delivery_readme)
        for path in staging.iterdir():
            path.replace(OUT / path.name)
        for item in artifacts:
            print(f"{item['name']}: {item['bytes'] / 1024 / 1024:.1f} MiB")
        print("Delivery entry point: Distribution/README.md")


if __name__ == "__main__":
    main()
