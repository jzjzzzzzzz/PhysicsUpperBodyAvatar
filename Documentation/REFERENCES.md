# 参考实现与设计取舍

研究日期：2026-10-20。只采用架构思想与关节空间约定；工程代码与 mannequin 几何独立实现，未引入参考库的角色或动作资产。

1. [ashleve/ActiveRagdoll](https://github.com/ashleve/ActiveRagdoll)，commit `749cd80dbccd73dffb7bb1d3827ad0e719b84f1c`，MIT。
   - 学习：目标与物理身体分离、角驱动追踪参考姿态。
   - 取舍：不采用位置吸引力，不让动画覆盖碰撞。
2. [sergioabreu-g/active-ragdolls](https://github.com/sergioabreu-g/active-ragdolls)，commit `459cbab9489c7481e65bf16ac93fcfff98e6a594`，Apache-2.0。
   - 学习：模块分离、ConfigurableJoint 目标空间转换。
   - 取舍：不实现腿部平衡，动作目标由参数化轨迹产生。
3. [EggyStudio/Unity.Humanoid.ActiveRagdoll](https://github.com/EggyStudio/Unity.Humanoid.ActiveRagdoll)，commit `ab3c423998b30cae4eb680ec39a0a5bf66eeb503`，MPL-2.0。
   - 学习：物理反应、可配置肌力和恢复。
   - 取舍：固定躯干，仅做有限力矩恢复，不集成整个插件。
4. [Unity 6 ConfigurableJoint](https://docs.unity3d.com/6000.0/Documentation/Manual/class-ConfigurableJoint.html)：轴、限位、angular drives、projection、力矩限制。
5. [Unity glTFast](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/index.html)：本地 GLB/glTF 导入。
6. [Unity 官方构建架构实现](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/BuildPipeline/DesktopStandaloneBuildWindowExtension.cs)：通过 EditorUserBuildSettings 的 Architecture 选项指定桌面目标架构。

各依赖许可位于 Unity Package Manager 包内的 LICENSE。工程不分发研究时下载的第三方源代码副本。

## 新增人物资产

[Microsoft Rocketbox](https://github.com/microsoft/Microsoft-Rocketbox)，固定 commit `0943055db6ec570bcef9f2c8b41c9e5467c808f9`，Male_Adult_04 人物，MIT / Copyright (c) 2020 Microsoft。人物 FBX、纹理和完整许可随工程分发。仅使用角色，不使用动作动画；源骨架只在 Editor 生成静止蒙皮绑定时临时读取。
