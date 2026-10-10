# 牛来 Blender 首版骨骼动作

骨骼模型已接入游戏的 NiuLaiPlayer 预制体；在开局角色选择中选择牛来即可使用。

- `NiuLai_Rigged.blend`：可编辑源文件，包含打包颜色/法线贴图、15 骨骼 Generic 绑定和四组动作。默认打开 Idle 动作。
- `preview/NiuLai-actions.gif`：待机、跑步、顶撞并排动画预览，来源为 Blender 实际渲染。
- 骨骼 FBX：`Assets/Game/Characters/NiuLai/Rigged/NiuLai_Rigged.fbx`，Unity Generic 导入，Idle/Run 循环，Attack 单次播放，禁用 Root Motion。

在 Blender 中选中 `NiuLaiRig`，将任一编辑区域切换成 Dope Sheet → Action Editor，可选择 Idle、Run、Attack。帧率 30：Idle 为 1–60 帧，Run 为 1–24 帧，Attack 为 1–18 帧，Flight 为 1–90 帧。播放其他动作时将时间线结束帧改成对应范围。

首版保留抱臂姿势，手臂随胸部运动，未制作自由摆臂。绑定采用按身体区域平滑混合的确定性权重；所有 44555 个网格顶点均有权重。已查看动作渲染与变形范围，尚未进行逐顶点权重精修。游戏移动切换 Run，停止切换 Idle，普攻触发 Attack。

复现骨骼及动作：`blender --background --python docs/rig-niulai.py`。
复现 Blender 渲染：`blender --background --python docs/preview-niulai-rig.py`。

技能指示：按住 A 显示普攻范围；E 显示 5.5 米飞行范围与落点；R 显示 7 米施法范围与 2.8 米砸地半径。蓝色可施放，红色无效，取消、死亡、撤退和结算隐藏预览。

Unity 菜单 Arknights Frontline/Prepare NiuLai and Character Selection 可重新生成；骨骼控制器由 NiuLaiRigSetup 创建。

E 已接入原抱臂模型完整 Flight 动作，包含起飞、水平前扑、下降及落地缓冲；全程 1.5 秒，骨骼进度与位移同步。R 降落预警延长至 1.8 秒，落地单次伤害，伤害、范围和冷却保持原值。

完整 E 导出：`blender --background --python docs/export-niulai-full-flight.py`。动画中的整体高度由游戏控制，导出保留姿势和脚底补偿，避免重复抬升。
