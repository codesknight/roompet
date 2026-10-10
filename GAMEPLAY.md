# 森林奔跑 · 玩法与实现说明

动物主题跑酷：一只方块小狐狸在森林小径上奔跑，跳过横倒的木头、绕开岩石树桩、
收集水果。跑在 `UnityMCPProject` 的 `Assets/Scenes/Main.unity` 里。
打开场景按 Play 即可，主菜单按 `Enter` 进无尽模式、`L` 进关卡模式。

## 操作

| 按键 | 动作 |
|---|---|
| `A` / `←` | 向左换道 |
| `D` / `→` | 向右换道 |
| `W` / `↑` / `空格` | 跳跃（长按跳更高，松开加速下落） |
| `S` / `↓` | 滑铲（约 0.6 秒） |
| `P` / `Esc` | 暂停 |
| `R` | 重开（暂停/结算界面） |

三条道，宽度 2.4 米。跳跃顶点约 2.5 米、滞空 0.62 秒。

## 角色：程序化方块动物

主角是代码拼出来的小狐狸（`AnimalAvatar`），没有用骨骼模型——因为拿不到可靠的
CC0 带骨骼动物（见下面的「素材来源」）。层级结构是这样的：

```
Visual
└ Rig（单位缩放）
    ├ Body / Chest                      网格叶子节点
    ├ HeadBone   → 头、吻、鼻、眼、双耳
    ├ TailBone   → TailMidBone → TailEndBone（三段渐细，奶白尾尖）
    └ LegFLBone / FR / BL / BR → 腿 + 脚掌
```

跑步动画是程序化的：前后腿对角成对摆动（小跑步态）、身体按两倍步频起伏、
尾巴左右摆、腾空时耳朵竖起、落地时由 `PlayerController` 做挤压拉伸。

**这里有个必须遵守的结构约束**：所有网格立方体都挂在**缩放为 1 的骨骼**下。
如果把一个立方体挂到另一个**被缩放的**立方体下，它会继承那个非等比缩放——
整个角色会被压扁进躯干内部（这个 bug 真实发生过，是靠包围盒检查发现的：
当时动物包围盒只有 `0.52×0.41×0.72`，就是躯干本身的尺寸）。

## 手感（可调）

`Cube → PlayerController` 上的字段控制视觉动作：

| 字段 | 默认 | 作用 |
|---|---:|---|
| `RollDegreesPerMetre` | **0** | 每米前进的翻滚角度。0 = 角色始终保持直立 |
| `SpeedLeanDegrees` | 9 | 达到关卡最高速时的前倾角度（"跑起来"的感觉） |
| `TiltDegrees` | 26 | 换道时的侧倾角度 |

**翻滚默认关闭**。之前是 62 度/米，在 12 m/s 下约每秒自转两圈，而且当时的实现把同一个
累加值同时喂给了 X 轴和 Y 轴（`Quaternion.Euler(roll * 0.35, roll, tilt)`），
等于在翻滚之上又叠了一个偏航自转，非常晕。现在即使打开翻滚也**只走 X 轴单轴前滚**。

想找回滚动感：把 `RollDegreesPerMetre` 设成 10~25（轻微的滚），
或者 90（方块面翻面的物理翻滚）。

## 障碍与判定

| 类型 | 外形 | 通过方式 |
|---|---|---|
| `Block` | 岩石 / 石柱 / 树桩，2.1 米高 | **只能换道躲**，永远不会堵满三条道 |
| `JumpBarrier` | 横倒在路上的木头，0.62 米高 | **必须跳过**，可横跨三条道 |
| `SlideBarrier` | 悬空架起的木头，底部 1.6 米 | **必须滑铲**，可横跨三条道 |

判定是**逻辑判定**而不是碰撞体形状判定：所有障碍都是宽松的触发体，命中时由
`PlayerController.ResolveObstacle` 判断是否真的通过（跳过低栏看脚底高度，滑铲看状态）。
这样跳跃/滑铲的手感调参集中在一处，也不依赖碰撞体缩放。

## 素材来源与授权

环境道具和收集品是直接下载的 **CC0** 模型，来自 [Kenney](https://kenney.nl) 的
[**Nature Kit**](https://kenney.nl/assets/nature-kit) 与
[**Food Kit**](https://kenney.nl/assets/food-kit)（Kenney 全部素材为 CC0 公有领域，
无需署名，此处署名出于礼貌）。共 40 个 FBX、1.35 MB：

| 用途 | 模型 |
|---|---|
| 遮挡障碍 | `rock_tallA` / `stone_tallA` / `stump_square` / `rock_largeA` |
| 跳过 / 滑铲障碍 | `log` |
| 风景树 | `tree_default` / `tree_pineTallA` / `tree_oak` / `tree_simple` / `tree_thin` / `tree_fat` / `tree_blocks` / `tree_pineRoundA` |
| 灌木花草 | `grass` / `plant_bush*` / `flower_*` / `mushroom_*` / `rock_small*` |
| 栅栏 | `fence_simple` / `fence_planks` / `fence_simpleHigh` |
| 水果收集品 | `apple` / `carrot` / `cherries` / `strawberry` |
| 道具 | `corn` / `watermelon` |

下载原始文件留在 `..\.assets\kenney\`，导入结果在 `Assets/Resources/Runner/Nature` 与
`Assets/Resources/Runner/Items`。

**导入尺寸不可信**：`import_model_file` 给的 `target_size` 实际会偏大 10 倍
（导入器 `globalScale` 与 `useFileScale=False` 组合的结果——apple 要 0.55 米，实际 5.5 米）。
所以 `PropLibrary` 不信导入设置，而是在实例化时**实测包围盒再按目标尺寸缩放**。

Unity 内置的三条素材渠道（Sketchfab 市场、Tripo/Meshy/fal 模型生成、图片生成）
在这个环境里**都没有配 API key**，需要在 Unity 的 `Window → MCP for Unity → Advanced`
里填。这也是主角用程序化方块动物而不是骨骼模型的原因。

## 天空盒

场景用的是 `Runner/ForestSky`（蓝天 + 柔和云层 + 暖色地平线），由 `DSH/Dreamy Skybox`
shader 换配色得到。之前的彩虹梦幻天空盒材质 `Assets/Materials/DreamySky.mat` **保留在工程里**，
想换回去直接把它拖到 `RenderSettings.skybox` 即可。

## 积分（距离越远，单位距离越值钱）

```
每米得分 = 1 + floor(距离 / 150) * 0.3        ← 每 150 米升一档，每档 +0.3
金币     = 10 分 × 当前倍率
最终得分 = Σ(每米得分 × 道具倍率) + Σ(金币分)
```

| 距离 | 每米得分 |
|---:|---:|
| 0 m | 1.00 |
| 150 m | 1.30 |
| 300 m | 1.60 |
| 1500 m | 4.00 |
| 3000 m | 7.00 |

累计是按**中点**积分的，所以一帧跨过档位边界不会丢分或重复计分。

## 难度曲线

```
速度     = min(关卡最高速, 起始速度 + 距离 × 加速率)
难度进度 = 距离 / 关卡目标距离（无尽模式按 2000 米归一）
障碍概率 = 关卡密度 × (0.55 + 0.85 × 难度进度)
同时封锁道数 = 进度 <25% 时 1 条，之后 2 条（永远不到 3 条）
```

**关键的公平性约束**：障碍行之间的间距不是固定值，而是按反应时间预算算出来的：

```
间距 = clamp(当前速度 × 0.6 秒, 9 米, 30 米)
```

所以速度越快，障碍越稀疏——玩家永远有 0.6 秒以上的反应时间。这条规则有单元测试覆盖。

## 关卡

8 个关卡配置在 `LevelLibrary.Levels`（纯代码，改起来直观）：

| # | 名称 | 目标 | 速度 | 障碍密度 |
|---:|---|---:|---:|---:|
| 0 | 无尽模式 | — | 11→34 | 0.28 |
| 1 | 第 1 关 · 起步 | 500 m | 10→16 | 0.20 |
| 2 | 第 2 关 · 加速 | 700 m | 12→19 | 0.28 |
| 3 | 第 3 关 · 低栏 | 850 m | 13→22 | 0.34 |
| 4 | 第 4 关 · 窄缝 | 1000 m | 15→25 | 0.42 |
| 5 | 第 5 关 · 组合 | 1200 m | 16→28 | 0.50 |
| 6 | 第 6 关 · 疾走 | 1400 m | 18→31 | 0.58 |
| 7 | 第 7 关 · 极限 | 1800 m | 20→36 | 0.68 |

通关即解锁下一关并记录该关最佳分。进度存在 PlayerPrefs，`Tools/DSH Runner/Reset Saved Progress` 可清空。

## 道具

| 道具 | 效果 | 时长 |
|---|---|---:|
| 护盾 | 免疫一次撞击（撞击后消耗） | 14 s |
| 磁铁 | 6.5 米内金币自动飞来 | 10 s |
| 双倍分 | 得分倍率 ×2 | 12 s |
| 减速 | 速度 ×0.55，给反应时间 | 6 s |

磁铁是**解析式**吸附（金币飞向玩家，到达即收取），不依赖两个碰撞体同时移动时的触发事件，
所以不会出现"金币从身边飞过却没吃到"的情况。

## 代码结构

```
Assets/Scripts/
  DshRunner.asmdef          独立程序集，便于单元测试引用
  Core/RunnerData.cs        GameConfig / Difficulty / ScoreRules / ScoreKeeper /
                            LevelDefinition / LevelLibrary / ProgressStore（全是纯逻辑）
  Core/GameManager.cs       状态机、计分、难度推进、关卡流程
  Core/RunnerMaterials.cs   Resources 材质加载 + 回退
  Core/RunnerSceneBuilder.cs 场景装配（编辑器菜单与运行时共用）
  Player/PlayerController.cs 换道/跳跃/滑铲/碰撞判定/前倾与形变
  Player/AnimalAvatar.cs    ★ 程序化方块动物 + 跑步动画
  Track/TrackPlanner.cs     ★ 关卡生成的纯函数（可测）
  Track/TrackManager.cs     分段池化、回收、按 plan 摆放模型
  Track/PropLibrary.cs      Kenney 模型加载与实测归一化 + 池化标记
  Track/ObstacleMarker.cs   障碍标记与命中/通过反馈
  Track/Pickup.cs           水果/道具拾取与磁铁
  Items/PowerUpSystem.cs    道具计时与效果
  Camera/FollowCamera.cs    跟随、预瞄、速度感 FOV、轻微抖动
  UI/HudController.cs       HUD 与菜单（IMGUI）
Assets/Editor/RunnerSceneMenu.cs       Tools/DSH Runner/* 菜单
Assets/Tests/Editor/RunnerLogicTests.cs 15 个纯逻辑测试
Assets/Shaders/Neon.shader             地面/道具/角色用的发光材质 shader
Assets/Shaders/DreamySkybox.shader     天空盒 shader（森林天空与梦幻天空共用）
Assets/Resources/Runner/*.mat          运行时加载的材质
Assets/Resources/Runner/Nature/*.fbx   Kenney Nature Kit 模型（CC0）
Assets/Resources/Runner/Items/*.fbx    Kenney Food Kit 模型（CC0）
```

**设计要点**：关卡生成被抽成了 `TrackPlanner` 纯函数（不碰任何 Unity 对象），
所以"墙不会堵满三条道""反应时间够""金币不会生在障碍里""同种子布局一致"这些
**可玩性不变量**都能用单元测试守住，不需要跑起来看。

## 验证情况

- **单元测试 15/15 通过**（`DshRunner.Tests`，EditMode）：计分曲线单调性、
  难度封顶、关卡表递增一致、生成器确定性、公平性不变量、间距反应时间、布局多样性。
- **实机跑过**：无尽模式自动跑动、金币拾取、撞障碍判定与结算、关卡通关解锁下一关。
- **逐帧轨迹验证**操作：跳跃 y 0.5→1.02、换道 x 0→-2.4 缓动、滑铲形变 scaleY→0.55。
- **几何验证**角色：包围盒 `0.52×1.21×1.34`，脚掌世界 Y 正好 `0.000`，20 个渲染器
  （重复叠加过一次，靠渲染器计数才发现）。
- **素材引用验证**：33 个道具路径全部可解析，0 缺失。
- **截图色彩分析**：天空蓝 19.5%、植被绿 24.6%、土路棕 41.0%；角色近景毛色
  RGB≈(208,104,52) 与材质定义一致（`docs/evidence/forest.png`、`docs/evidence/animal_avatar.png`）。

## 已知取舍

- **UI 已从 IMGUI 迁到 uGUI**（第 41 轮完成）。此前全是 `OnGUI`；现在全部由 `com.unity.ugui` +
  `DshMobile.Ugui`（运行时 Canvas / 控件工厂）+ `UguiFont`（运行时中文字体，仍零字体资产）搭建，各 HUD、
  小游戏、跑酷、宠物房间（聊天/宠物卡/地图/设置/记事本/收集/商城/拼图/记忆/移动端控制等）全部 uGUI。
  唯一保留在 uGUI 之外的是**移动端多指触控的输入**（`MobileTouch` 直接读 `Input.touches`，因为 uGUI 旧输入
  只有单指），它只做输入、绘制已 uGUI 化。旧的 `Draw*` 方法与 `UiSkin`/`MobileWidgets` 已成死代码待清理。
- **玩家移动用 transform 驱动**（kinematic Rigidbody 只为触发事件）。
  注意 `Rigidbody.interpolation` **必须保持 None**：kinematic 体一开插值，
  Unity 会用物理状态回写 transform，吃掉一部分位移，导致计分与实际位移脱节
  （这个 bug 在开发中真实出现过，距离计数器一度是实际位移的 2 倍）。
- **角色是程序化拼的**，不是骨骼动画：小跑步态 + 身体起伏 + 尾巴摆动，够用但没有
  真正的四足动画。要升级得先解决素材渠道（配 API key，或自备带骨骼的 FBX）。
- **`AnimalAvatar` 会优先复用场景里已有的骨架**。如果无条件重建，场景里序列化的那套
  加上运行时 `Awake` 建的那套会叠成两份（40 个渲染器）——同样真实发生过。
- 目前没有音效、没有对象池上限保护、没有移动端触屏输入。
