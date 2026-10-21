# 0.2.0 测试结果与验证范围

测试日期：**2026-10-21**。本机 macOS / Apple M5 Pro / Unity **6000.5.7f1** / URP 17.5.0。实际构建并运行 **ARM64 + Mono、Metal** 独立窗口。当前角色为 Rocketbox 物理蒙皮，使用 7 路手臂 + 6 路手掌模板。

## 结果总览

| 测试层 | 通过数 | 原始报告 |
|---|---:|---|
| Unity NUnit EditMode 契约 | 55/55 | `TestResults/editmode-results.xml` |
| Python 客户端 / 文档契约 | 9/9 | `TestResults/repository-validation.json` |
| Editor 显式 PhysX.Simulate | 26/26 | `TestResults/physics-validation.json` |
| Metal 独立窗口 / FixedUpdate / 接触 | 29/29 | `TestResults/Standalone/runtime-validation.json` |
| 正常独立应用的实际 TCP 请求 | 25/25 | `TestResults/command-validation.json` |

合计 **144 个软件检查项通过**，不是 144 个独立生物力学实验。另有文档链接、示例语法、MPL 声明和程序集边界静态检查。GitHub Actions 只执行 Python 与静态检查，不替代本机 Unity/PhysX/Metal 测试。

## 新增契约测试

- 动作名与大小写规范化、duration 边界、非法 JSON、错误侧别、缺失通道。
- 13 路顺序、所有通道正/反方向编码、非法配置和非有限输入、手指数据深拷贝、腕部两轴投影。
- 排队请求取消/过期后不能执行、一次性完成、关闭监听后释放端口。
- Python 用临时 loopback 端口检查真实帧收发、错误响应、NaN、超长包、缺失换行及原有 `send` API；不连接 8765 或控制正在运行的用户场景。

公开 XML 只将本机项目/用户路径替换为占位符，测试计数、状态和断言未改动。原始 XML 保留在本机忽略的 `Logs/`。

## 物理回归与重复性

- 41 个 Rigidbody、39 个限力矩电机；可见蒙皮直接绑定物理骨骼，无 Animator。
- 三种动作各重复 3 次，检查抬手/前伸、指节、返回 idle、重力下垂、阻挡/恢复、冲量与物体支撑。
- One H1 curls multiple physical index joints: proximal -70.8°, middle joint -82.3°; separate middle finger -0.3°.
- Max anchor separation 0.0004m (R_Thumb_0); max linear speed 3.83m/s.
- Maximum excursion beyond configured flexion/extension range 1.42 degrees (numerical tolerance 3 degrees).

重构回归曾发现逐段诊断复位的瞬时速度异常。修复为冻结整条动态链、统一复位后恢复动态与邻接碰撞设置；普通动作不使用冻结或传送。**没有放宽原有稳定性判据**。

修复后，重建后的首次运行及两个独立 Editor 进程均通过：`TestResults/Repeatability/editor-rebuild.json`、`editor-1.json`、`editor-2.json`。重复运行不再次累加到上表。

## 独立应用与可见蒙皮

- 实际 Metal 窗口、自动 FixedUpdate、真实接触回调及帧缓冲截图。
- UI Toolkit NavigationSubmitEvent 触发实际 Button；Slider ChangeEvent 测手指输入。覆盖动作、回位、暂停/恢复、阻挡板、复位、Debug、13 路输入释放。
- Right index -70.8°; right middle -0.3°; left index -4.1°. One input per finger, multiple simulated phalanges.
- 动作保持阶段采样，不是全轨迹误差上界：
  - Wrist (0.228,1.222,0.445) m; major mean error 2.84 degrees.
  - Wrist (0.384,1.653,0.007) m; major mean error 4.08 degrees.
  - Wrist (0.253,1.238,0.302) m; major mean error 3.93 degrees.
- 三个动作均将 CPU BakeMesh 顶点与物理骨骼矩阵加权结果对照，最大差异按 6 位小数显示为 0.000000m；容差 0.002m。
- Free z=0.450, blocked z=0.334 m.
- Recovered z=0.476 m.
- 350 g block displacement 0.2875 m; new external contacts 6.
- Maximum anchor error 0.0238 m; speed 5.55 m/s; 3266 physics steps.

截图位于 `TestResults/Standalone/`：Idle、Handshake、Wave、DoubleThumbsUp、ServoMapping、HandServoMapping、ObstacleContact、MovableObjectContact、PhysicsDebug。近景由真实相机取景。最终窗口报告副本保存在 `Repeatability/standalone-final.json`。

自测模式隔离原生动作输入、相机和 TCP 监听；合成 UI 事件由测试代码显式派发。正常启动不禁用这些功能，也不自动播放测试或退出。

## 实际接口检查

对正常启动的最终应用发送 TCP 请求：原四种动作、13 路查询与左右目标、非法输入原子拒绝、大小写规范化、duration 两端边界、非法 UTF-8、超长帧及默认 4 客户端并发上限均通过。

检查的是请求 ACK 与边界，不把目标接受当作到位。手动目标是输入目标，不是虚拟关节测量或实物编码器反馈。排队过期可取消，但已开始执行的命令不能因网络断开回滚。

## 本轮没有覆盖

- 原生鼠标端到端自动化、长时间压力测试、不同硬件/刷新率，以及 Windows/Intel/Universal 构建。
- 实物设备、轴序、端口、方向、零位、传动比、串口/总线、编码器和真实硬件闭环。
- 患者拟合、临床有效性、软组织、真实机械连杆闭环或精细抓握。
- 任意其他 FBX/GLB 的通用自动重定向。内置 Rocketbox 已运行，其他资产仍需手工校准和验证。

当前模型低多边形、脸部静态；同手自碰撞简化、固定躯干、有限求解误差等边界见 README、PHYSICS 和 HARDWARE_MAPPING。
