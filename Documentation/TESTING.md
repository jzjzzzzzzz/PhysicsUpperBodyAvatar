# 测试记录与验证范围

日期：2026-10-20。macOS / Apple M5 Pro / Unity **6000.5.7f1** / URP **17.5.0**。本轮实际构建 **Mono + ARM64 非 Development macOS 应用**，运行 Metal 窗口，没有用无图形模式替代渲染测试。

当前版本包括 **Rocketbox 真人风格蒙皮**和**每侧手臂 7 路 + 手掌 6 路映射**。

## 结果

| 测试组 | 通过 | 原始报告 |
|---|---:|---|
| Editor 显式 PhysX.Simulate 物理回归 | **26/26** | `TestResults/physics-validation.json` |
| 有窗口独立应用、FixedUpdate、接触回调、渲染与 UI 事件 | **29/29** | `TestResults/Standalone/runtime-validation.json` |
| 对正常运行应用的 localhost TCP 请求 | **19/19** | `TestResults/command-validation.json` |

合计 **74 个检查项**；并非同等数量的独立生物力学实验。没有连接或标定实物机械手。

Editor 物理回归在修复复位插值同步后连续通过；两次额外运行报告保存在 `TestResults/Repeatability/editor-1.json`、`editor-2.json`。最终构建的窗口报告副本为 `standalone-final.json`。这些重复运行不重复计入上表。

## Editor 物理回归

- 41 个 Rigidbody、39 个限力矩关节电机；双臂、手掌与指节为动态刚体。
- 实际可见蒙皮直接引用物理骨骼，无 Animator；32/32 个手/指节骨骼影响保留的可见网格。
- 左右各 13 路逻辑通道，腕 Y 锁定；所有通道验证正/反方向编码解码。端口号未绑定，软件转换通过不代表完成实物标定。
- 独立食指输入：One H1 curls multiple physical index joints: proximal -71.4°, middle joint -83.1°; separate middle finger -0.3°.
- 验证整包非法输入不改变目标、手动控制释放后平滑回 idle。
- 每种动作重复 3 次：前伸/抬手、握拳、拇指方向、自动回 idle；另测关闭电机后重力下垂、障碍阻挡/移除恢复、外力冲量、物体支撑。
- Max anchor separation 0.0004m (R_Thumb_0); max linear speed 3.63m/s.
- Maximum excursion beyond configured flexion/extension range 0.00 degrees (numerical tolerance 3 degrees).

手动 Editor 模拟不覆盖窗口输入或正常 Play-mode 接触事件；下面的独立应用覆盖这些渲染与接触行为。

## 独立应用 / 真实窗口

- 实际运行 `.app`，自动 FixedUpdate，Metal 帧缓冲截图。
- 真实 UI Toolkit Button 的 **NavigationSubmitEvent** 测试三种动作、Return to Idle、Pause/Resume、阻挡板、Reset 和 Debug；暂停时物理步数停止。
- 真实 Slider 的合成 ChangeEvent 测 H2；直接输入测 H1 独立指联动；通过 Release servo control 按钮回 idle。
- Right index -71.4°; right middle -0.3°; left index -5.7°. One input per finger, multiple simulated phalanges.
- 动作保持阶段采样（不是全轨迹上界）：
  - Wrist (0.228,1.218,0.443) m; major mean error 3.75 degrees.
  - Wrist (0.380,1.652,0.007) m; major mean error 3.03 degrees.
  - Wrist (0.253,1.238,0.302) m; major mean error 5.24 degrees.
- 三个动作均对 CPU BakeMesh 顶点与物理骨骼矩阵的加权结果做数值对照，最大差异按 6 位小数显示为 **0.000000m**；容差 0.002m。
- Free z=0.451, blocked z=0.330 m.
- Recovered z=0.476 m.
- 350 g block displacement 0.2679 m; new external contacts 7.
- Maximum anchor error 0.0238 m; speed 5.55 m/s; 3260 physics steps.
- 物理配置步长 1/90 秒；UI 显示实测步频，不保证所有硬件/负载始终 90Hz。

`-hdtSelfTest` 模式专门阻止原生动作按钮/滑块输入干扰、关闭鼠标相机控制和 TCP 监听；合成 UI 事件由测试代码显式派发。普通启动不禁用这些交互，也不自动播放测试或退出。

截图位于 `TestResults/Standalone/`：Idle、Handshake、Wave、DoubleThumbsUp、ServoMapping、HandServoMapping、ObstacleContact、MovableObjectContact、PhysicsDebug。手部近景通过真实相机取景，不是修改照片。

## 正常应用的本地接口

单独正常启动最终 `.app`，连接 **127.0.0.1:8765** 测试：

- 原四种动作接受；未知动作、短/负/过长持续时间、缺少 action、非法 JSON 拒绝。
- `servo_state` 返回 13 路目标；`servo_pose` 分别接受左右侧输入。
- 通道数量不对、缺少 values、错误 side、越界角度、NaN 均拒绝，随后核对原目标没有被部分修改。

测试的是实际 socket ACK 和路由/验证边界，不把“已接受目标”当作“已经完成动作”。`targetValues` 是目标，不是实物编码器反馈。监听地址是 loopback，不是所有网卡。

## 本轮修复及边界

1. 将 Microsoft Rocketbox 静止蒙皮在 Editor 一次性重定向并裁切，运行时直接由物理骨骼变形；修复混合腰部权重被误裁造成的夹克缺口。
2. 校准手掌 +Z、手指 −Y、左右镜像的指骨框架及拇指基点；四指不再错误地向手背弯曲。
3. 限制硬件模板为 7+6，增加独立指联动、完整数据包校验、输入限速和从手动姿态平滑切换。
4. 修复诊断复位时的插值旧姿态同步：复位清空插值历史并同步完整姿态；无渲染周期的 Editor 手动模拟不使用 Rigidbody 插值。未通过关闭重力、全部碰撞或提高稳定性容差来掩盖发散。
5. 增加全窗口背景清屏，消除主视口之外 UI 字形残留；关节与接触物理不依赖该渲染修复。

仍需人工/实物复核：

- **原生鼠标端到端**操作未自动化确认；上述按钮/滑块是明确标识的合成 UI 事件测试，不伪称真实鼠标测试。
- 目标电脑上实际点三个动作、Pause/Resume、Reset、阻挡板，拖动/滚轮和 13 路滑块；本轮不覆盖 10–30 分钟压力测试、Intel 或 Windows 构建。
- 实物舵机 ID、方向、行程、零位、拇指两路功能、手臂轴链和连杆比均待用户提供；没有实物通信、下发或硬件闭环测试。
- 手指是共享目标的物理多指节联动，不是真实机械连杆闭环；同手内部碰撞简化、有限力矩、固定躯干等见 README 和 HARDWARE_MAPPING.md。
- 真人资产为低多边形实时角色，脸部静态、头发透明卡片；极端弯曲仍可能有蒙皮瑕疵。内置 Rocketbox FBX 已集成并运行，通用 FBX/GLB 工具对其他模型仍需人工对齐与验证。
