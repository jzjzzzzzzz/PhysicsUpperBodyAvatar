# 写实人体角色升级

## 选择与来源

- 模型：[Microsoft Rocketbox — Male_Adult_04](https://github.com/microsoft/Microsoft-Rocketbox/tree/master/Assets/Avatars/Adults/Male_Adult_04)。
- 固定源 commit：`0943055db6ec570bcef9f2c8b41c9e5467c808f9`。
- [官方 MIT 许可](https://github.com/microsoft/Microsoft-Rocketbox/blob/master/LICENSE.md)，Copyright (c) 2020 Microsoft。完整许可保留在源资产目录及应用 StreamingAssets/ThirdPartyNotices 中。
- 下载的是人物 FBX、身体/头部颜色贴图与法线、头发透明贴图；没有下载或播放该库的动作动画。
- 原始来源清单：`Assets/ThirdParty/MicrosoftRocketbox/SOURCE.json`。

这是写实风格、预绑定的人体角色：真正的脸部、耳朵、鼻子、嘴、眼睛、头发、衣服、手掌与连续手指网格，而非几何球/胶囊拼出的人偶。它仍是实时低多边形角色，不等同于电影级扫描。

## 如何接到物理骨架

`RocketboxSkinBuilder.TryAttach` 在 **Editor 构建阶段**完成：

1. 读取原始静止姿态，按肩中心距确定统一尺度，校准颈与头的固定连接点。
2. 为上臂、前臂、手掌、各指节建立源解剖坐标系，基于源蒙皮权重，把 A-pose 静止几何转换到物理骨架的 canonical bind pose。
3. 合并脊柱/衣服及裁切线附近的髋部权重到躯干，面部/头发权重到头；每个手指仍对应自己的三个动态指节。
4. 在腰部下缘裁掉腿部及下半身三角面，不直接删除带大腿混合权重的夹克下摆；保留下垂到腰以下的手，避免错误地按全局高度裁掉手指。
5. 输出实际网格资产，重建 bind poses，SkinnedMeshRenderer.bones **直接指向物理 Rigidbody 的 Transform**。
6. 删除临时导入骨架。运行场景里没有源模型的 Animator，也没有每帧用 Transform 把皮肤追到目标骨架的脚本。

身体/头部使用 URP Lit 颜色 + 法线，头发/睫毛用无玻璃高光的软透明材质。摄像机默认距离由 2.65m 调整到 2.25m，提高脸部细节可见度，同时保留双手活动范围。Idle 的前臂轴转已调整为更自然的掌心向内。

物理架构仍然是 41 个 Rigidbody、39 个关节电机、动态双臂与指节、有限力矩、外部接触与恢复。没有用模型自带动画替代物理。

## 验证

- Editor 检查当前真正可见的 Renderer，而不再只检查隐藏的旧人偶。
- 检查实际场景无 Animator，三组材质均有纹理。
- 检查 **32/32 手掌与指节**都影响保留下来的可见网格，防止“物理手还在、显示手却被裁掉”。
- 独立应用在每个动作检查 CPU 蒙皮顶点与物理骨骼矩阵计算结果一致，并保存实际渲染截图。
- 姿态跟踪、碰撞阻挡、恢复和动态物体推动测试保留。

最新结果与范围见 `TESTING.md` 和 `TestResults/`，不要用旧版人偶报告代替新版模型测试。

## 边界

- 不是个体扫描模型，不具备人物身份拟合。
- 当前脸部表情静态；没有实现面部捕捉、唇形或 TTS。
- 皮肤是线性混合蒙皮，不是软组织/肌肉/布料物理；夹克褶皱主要由贴图与蒙皮表现。
- 原始 FBX 配方只针对这个固定资产自动运行；其他模型需重新校准映射和尺度。

手部坐标已按真实人体校正：掌心 +Z、右拇指在 +X，负 X 弯曲朝掌心；不是把指节向手背掰成握拳。拇指 CMC 起点移到掌内，避免拇指异常拉长。
