# 架构与模块边界

0.2.0 保留原有物理骨架与场景，重构的是模块边界、输入验证、传输生命周期和开发流程。重构不把动作替换成 Animator，也不通过关闭接触来维持跟踪。

## 程序集

| 程序集 | 目录 | 依赖与用途 |
|---|---|---|
| `ALS.HDT.Runtime` | `Assets/Scripts/` | 运行时逻辑；不能依赖 UnityEditor |
| `ALS.HDT.Editor` | `Assets/Editor/` | Runtime、URP；生成场景、导入模型、验证和构建 |
| `ALS.HDT.Tests.Editor` | `Assets/Tests/Editor/` | NUnit、Runtime、Editor；不进入正常 player |

原有 MonoBehaviour / ScriptableObject 文件及 `.meta` 保持路径与 GUID。新增纯数据类不改变场景组件的引用。编辑器重建会保存新的程序集标识。

## 输入与线程

```text
TCP worker: 读有限长度 UTF-8 帧 → 有界待处理队列 → 等待响应
                                              ↓
Unity Update: 取未过期请求 → Parse → Dispatcher → Avatar / ServoBridge → 回复
                                              ↓
FixedUpdate: 手势或限速输入 → 目标骨架 → 有限关节电机 → PhysX
```

- `AvatarCommandProtocol` 负责 JSON、动作名、持续时间、侧别、通道数量和有限数值。
- `AvatarCommandDispatcher` 只在主线程执行场景动作及配置范围校验。
- `LocalCommandTransport` 不调用 Unity API；限制活动连接、队列、帧长度和读取时间。
- `PendingAvatarCommand` 区分排队、执行与结束；排队时取消/过期的命令不能之后偷偷执行。
- `LocalCommandServer` 只是 MonoBehaviour 生命周期适配器，禁用时关闭监听与客户端，重新启用可重建监听。

请求已开始执行后，网络断开不能回滚场景副作用。因此 ACK、动作完成和连接状态不能混为一谈。localhost 无身份认证；同机程序均可连接。

## 13 路领域模型

`ServoRigSchema` 是 A1…A7、H1…H6 顺序和原型目标范围的唯一 C# 定义。`ServoChannel` 处理线性正/反向编码；`ServoRigProfile` 保存序列化标定并校验，不再临时创建另一份 ScriptableObject 来获得默认范围。

`ServoAvatarBridge` 管理左右覆盖、目标限速和手势切换。四指各一路输入，指节目标共享一组联动比例；拇指使用独立联动比例及根部另一轴。只有目标被写入，实际骨骼由物理电机驱动。

硬件端口目前只是元数据，没有通信适配器。13 路模板不等于已经复刻实物的串联轴、减速器或连杆闭环。

## 物理与显示

目标骨架无渲染/碰撞/刚体；物理骨架的 Transform 由 PhysX 拥有；SkinnedMeshRenderer 直接绑定物理 Transform。诊断复位是唯一主动传送刚体的操作：先冻结整个动态链、统一复位，再恢复动态和邻接碰撞设置；普通动作不改变 kinematic 状态。

关节参数继续来自 `AvatarPhysics.asset`，动作来自 `Gestures.asset`，不放进网络协议或 UI 回调。测量约束反力与估计控制 effort 仍分别显示，见 [PHYSICS.md](PHYSICS.md)。

## 测试边界

NUnit 检查协议、限值、左右侧、输入隔离、编码方向、过期与端口释放；Python 用临时 loopback 服务检查客户端帧与错误处理。两者都不能替代真实 PhysX 或 Metal 验证。

Editor 物理回归、独立 player 和实机 TCP 测试继续独立运行；原始报告及范围见 [TESTING.md](TESTING.md)。
