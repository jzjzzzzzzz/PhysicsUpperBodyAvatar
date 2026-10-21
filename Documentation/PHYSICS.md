# 物理参数、关节约定与诊断

配置资产：`Assets/Configuration/AvatarPhysics.asset`。Unity 长度单位按米，质量按 kg，角度按度。范围为相对解剖静止姿态的原型工程值。

## 默认参数

| 部位 / 轴 | 解剖范围 ° | 刚度 N·m/rad | 阻尼 N·m·s/rad | 电机力矩上限 N·m |
|---|---|---:|---:|---:|
| 肩 | X −105…65；Y ±100；Z ±112 | 145 | 14 | 34 / drive channel |
| 肘屈伸 | X −145…0；Y/Z 锁定 | 85 | 7 | 18 / torque pair |
| 前臂轴转 | 纵轴 Y ±90；另外两轴锁定 | 22 | 2 | 5 |
| 腕 | X ±45；Y ±15；Z ±28 | 28 | 2.8 | 4 / drive channel |
| 手指指节 | X −100…5；Y/Z 锁定 | 0.30 | 0.025 | 0.09 |
| 拇指根部 | X −70…12；Y ±20；Z ±40 | 0.35 | 0.03 | 0.12 / drive channel |
| 头/颈 | X ±22；Y ±35；Z ±22 | 55 | 6 | 12 / drive channel |

多轴关节的 `angularXDrive`、`angularYZDrive` 各自具有 `maximumForce` 上限；该值不是所有轴合力矩的欧氏范数上限。约束反力矩也不受电机 effort cap 限制。有限迭代、接触及冲量下存在数值误差，锁定轴不是解析意义上的绝对零。

| 物理段 | kg |
|---|---:|
| Torso（kinematic） | 16 |
| Neck（kinematic） | 1 |
| Head | 4 |
| 每侧 UpperArm | 1.85 |
| 每侧 Elbow 解耦载体 | 0.12 |
| 每侧 Forearm | 1.05 |
| 每侧 Palm | 0.32 |
| 每个 Phalanx | 0.008 |
| 每只手总质量，含 15 指节 | 0.44 |
| 测试方块 | 0.35 |

惯量与质心由 Collider 和质量自动计算。上臂长 0.30m，前臂长 0.26m，掌长 0.115m，肩中心距 0.44m。肘解耦载体是数值建模简化，不是额外的解剖骨块。

## 积分、接触与稳定性

- PhysX **Temporal Gauss Seidel (TGS)**，启用 Enhanced Determinism；
- 固定步长 **1/90 秒**；每段 position solver 40、velocity solver 20 次。
- 重力 `(0,-9.81,0)`；线性阻尼 0.05，角阻尼 0.15，最大角速度 9 rad/s。
- 动态骨骼 ContinuousDynamic CCD + Interpolate；kinematic 基座 Discrete。
- Collider contact offset **0.003m**；胶囊上臂/前臂/指节，盒形手掌，球形头。
- 不使用 JointProjection，也不靠位置投影对抗障碍物。
- layer 8 物理人物，layer 10 测试物体。不是全局关闭人物碰撞：只忽略直接连接邻居、共享肘关节的重叠胶囊、同一只手的掌/指节。
- 左右手之间、非邻居躯干以及所有外部物体接触保持有效。

## 关节空间与电机

旋转相对 connectedBody 计算，不相对平铺的 Transform.parent 计算。basis 由 axis 与 secondaryAxis 构建：

```
qJointTarget = inverse(basis) * inverse(desiredRelative) * bindRelative * basis
```

解剖配置使用 child-relative-to-parent 角度；ConfigurableJoint X twist limit 的符号相反，代码将 `[lowX,highX]` 映射成 `[-highX,-lowX]`。普通角驱动是力驱动（`useAcceleration=false`）。

### 肘部 torque pair

小型肘载体与前臂 pronation 分开，避免把屈伸后的轴转误当成腕部扭转。但小球惯量远低于整个远端链，单独在球上用 implicit joint drive 会导致串联约束跟踪很弱。因此保留铰链连接/限位，按远端刚体惯量计算稳定 PD，再向真实前臂/上臂施加等大反向力矩：

```
I = sum(I_rot_about_axis + m * perpendicular_distance_to_elbow²)
invEffectiveI = 1/I_distal + 1/I_proximal
u = (Kp * error - Kd * relativeOmega) /
    (1 + (Kd*dt + Kp*dt²) * invEffectiveI)
torque = clamp(u, -torqueLimit, torqueLimit)
forearm.AddTorque(axis * torque)
upperArm.AddTorque(-axis * torque)
```

不是直接改旋转、MoveRotation、IK 覆盖或位置吸引力。简化惯量估计没有完整求解关节链惯量矩阵。

## Debug 数值

- **F / T measured**：`Joint.currentForce/currentTorque`，PhysX 测得的**总关节约束反应**，包括连接、限位、接触传递等，不能称为纯电机转矩。
- **PD est**：基于误差/相对角速度估计并截断的控制 effort，不是 PhysX 电机冲量 readback。肘显示提交给 AddTorque 的指令，也不是测量值。
- **Joint error**：实际相对旋转到目标旋转的四元数夹角。UI 均值不含 30 个指节，以免掩盖大关节误差。
- **Angles XYZ**：ZYX 相对静止姿态分解，匹配轨迹 Compose；存在欧拉角奇异性，不代表独立的临床肌骨角。
- **External contacts**：手/指节最近 0.12 秒的外部接触点快照，Reporter 最近一组接触求和，不是全场景严格去重集合。
- **Physics steps Hz**：真实 FixedUpdate 步数 / 未缩放墙钟时间，不是 GPU 渲染帧率；暂停时降为 0。

## 真人模型的手部坐标约定

掌心为局部 **+Z**，手指纵轴沿 **−Y**。右手拇指/食指侧在局部 **+X**，左手镜像；负 X 指节屈曲必须朝掌心，而不是朝手背。Idle 的前臂轴转为右 −85°、左 +85°，使放松时掌心朝内。拇指 CMC 基点位于手掌内部，横向偏移 0.025m、腕向下偏移 0.020m，保存在 Physics Profile 中；不把整条拇指从掌外缘开始拼接而造成异常加长。

这组约定与新版 Rocketbox 皮肤绑定配方一致。新人物的颈/头连接点在 Editor 中按源模型尺度校准；运行时不再用源模型骨架覆盖 PhysX。

## 13 路硬件映射模式

默认场景已启用 `ServoAvatarBridge.enforceSevenArmSixHand`：肩 XYZ、肘 X、前臂 Y、腕 XZ 共 7 个可指令轴。腕 Y 直接设为 ConfigurableJoint Locked，原配置中的 wrist.limitY 在此模式下不生效。每根四指的一路 curl 联动三个关节，拇指 curl + spread 两路，共 6 路；联动比例由 ServoRigLeft/Right 配置保存。物理电机数量仍是 39，不等于独立硬件指令通道数。此处模拟目标联动，不包含照片中真实机械连杆的闭环约束。

显式 Reset Physics 先把整条动态链临时冻结，统一清空插值历史、写入初始姿态与速度，再恢复原动态段并重设邻接碰撞规则，避免逐段传送后沿用不一致的约束状态。此操作仅用于诊断复位，同一调用结束后双臂与指节全部恢复动态；普通手势和 servo 输入从不使用冻结/传送路径。手动 Editor 模拟关闭插值，正常 FixedUpdate 使用插值。详见 HARDWARE_MAPPING.md。
