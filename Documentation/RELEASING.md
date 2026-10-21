# 本地构建与发布流程

## 1. 准备

使用固定 Unity 6000.5.7f1 及 macOS 构建模块。关闭打开本工程的 Editor，确认 `VERSION`、`CHANGELOG.md` 与文档一致。不要把 Library、日志、构建目录或 ZIP 加进 Git。

## 2. 完整验证

```bash
python3 Tools/run_quality.py
Tools/unity_task.sh test
Tools/unity_task.sh all
Tools/runtime_test.sh
```

最后正常启动生成的应用，再运行：

```bash
python3 Tools/test_commands.py
```

自测 player 不监听 TCP；不要把接口测试发给别的旧实例。保留最新 EditMode XML、Python/静态检查、物理、player 和 TCP 报告。公开 XML 仅将本机路径前缀替换为占位符，未经修改的原始 XML 留在忽略的 Logs 目录；计数、断言和结果不变。更新 `Documentation/TESTING.md` 中的实际结果，不把 hosted CI 当物理测试。

## 3. 打包

```bash
python3 Tools/package_project.py
```

工具检查当前报告、文档和签名，然后先暂存 ZIP，校验成功再替换交付文件。应用 ZIP 包含离线指南、报告、Python 客户端和 MPL / 第三方许可；源码 ZIP 包含测试、社区文件和配置，不包含 Git 或 Unity 缓存。

打包本身不重新运行 Unity；更改运行时代码后必须先执行第 2 步。应用为本机构建的 ARM64 + Mono，不自动变成 Universal、Windows 或 IL2CPP 版本。

## 4. GitHub 发布

提交经过验证的源码与报告。需要二进制发行版时，将 ZIP、SHA256SUMS 和 RELEASE_MANIFEST 作为 GitHub Release 附件，而不是放进 Git 历史。只有实际上传附件后才在 README 宣称该 release 可下载。

遵守 [NOTICE.md](../NOTICE.md)：0.2.0 新原创代码采用 MPL-2.0，历史 MIT 版本及 Microsoft 素材的许可保持不变。
