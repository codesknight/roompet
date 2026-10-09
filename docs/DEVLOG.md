# 开发日志与交接文档

> **这份文档是"新对话接入"的第一份读物。** 目标：读完它就能继续改这个项目，不需要翻历史对话。
>
> 配套文档：
> - `docs/MCP_SETUP.md` —— MCP 部署与复用（工具怎么用、环境怎么修）
> - `docs/REQUIREMENTS.md` —— 需求与实现对照（每轮要什么、怎么实现的、验收证据）
> - `PET_DESIGN.md` —— 虚拟宠物各轮设计的详细记录（含设计理由）
> - `GAMEPLAY.md` / `README.md` —— 跑酷玩法与部署说明

---

## 一、当前状态快照

| 项 | 状态 |
|---|---|
| 工程 | `D:\projects\dsh-unity\UnityMCPProject`，Unity **2022.3.62f3c1**（中国版），Built-in RP，**Gamma 色彩空间** |
| 场景 | `Assets/Scenes/Main.unity`（跑酷）、`Assets/Pet/Scenes/PetRoom.unity`（虚拟宠物）、`Assets/MiniGames/Scenes/FlyBird.unity`（小鸟飞行）、`Assets/MiniGames/Scenes/JumpQuest.unity`（跳一跳）、`Assets/MiniGames/Scenes/CatchFruit.unity`（接果子），五个都已在 Build Settings |
| 构建目标 | 已切到 **Android**（装了 Android Build Support：OpenJDK/SDK/NDK）；桌面端仍可随时切回 |
| 测试 | **248/248 通过**（虚拟宠物 152 + 跑酷 15 + 手机端 43 + 小游戏 38），EditMode |
| 编译 | 无 error、无 warning |
| 大模型 | 在线。本机从环境变量读到内网网关 `http://<内网网关>/v1` + `<内网模型>`（免鉴权） |
| 存档 | PlayerPrefs + `%USERPROFILE%\AppData\LocalLow\DefaultCompany\UnityMCPProject\dshpet-journal-*.json` |
| 安卓包 | `Tools/DSH Mobile/Build APK` 默认写 `UnityMCPProject/Builds/Android/RoomPet.apk`（约 16 MB，开发版；两个 `Builds/` 都被 git 忽略）。第 14 轮的包是直接调 MCP 构建、指定输出到仓库根的 `Builds/RoomPet.apk`，发布在 GitHub release `v0.2.0-mobile.1` |
| 回归证据图 | `docs/evidence/*.png`（随文档一起提交，便于复盘） |

**两个场景一句话说明**

- **跑酷（Main）**：键盘控制的小动物跑酷。左右换道 + 跳跃，无限循环赛道随机生成障碍，
  距离越远难度越高分数越高，有关卡模式与道具系统。
- **虚拟宠物（PetRoom）**：文字陪伴 + 环境互动的虚拟宠物。房间是手工程序化搭建的，
  宠物有 4 个物种可换、需求/情绪系统、主动与被动的行为表、日程表式持久记忆（日历 UI）、
  程序化音效、扔捡球、以及接 DeepSeek 兼容接口的"大脑"。玩家角色可在房间里自由走动。

---

## 二、代码地图

### 跑酷（`Assets/Scripts/`）

| 文件 | 职责 |
|---|---|
| `Core/RunnerData.cs` | 配置、难度曲线、计分规则、关卡表、进度存档 |
| `Core/GameManager.cs` | 一局流程：开始/结束/重开、关卡选择 |
| `Core/RunnerMaterials.cs` | 主题材质（跑道/边缘/障碍/金币/道具/地面） |
| `Core/RunnerSceneBuilder.cs` | 从代码搭场景 |
| `Player/PlayerController.cs` | 三车道移动 + 跳跃 |
| `Player/AnimalAvatar.cs` | 程序化小动物外观（各物种不同） |
| `Track/TrackPlanner.cs` | 赛段规划（保证可通过：**同一排不会填满所有车道**） |
| `Track/TrackManager.cs` | 无限赛道的生成/回收 |
| `Track/PropLibrary.cs` | Kenney CC0 自然素材的实例化与缩放修正 |
| `Track/ObstacleMarker.cs` / `Track/Pickup.cs` | 障碍与拾取物标记 |
| `Items/PowerUpSystem.cs` | 道具效果（磁铁/护盾/加速等） |
| `Camera/FollowCamera.cs` | 跟随相机 |
| `UI/HudController.cs` | IMGUI 抬头显示 |
| `Editor/RunnerSceneMenu.cs` | 菜单：`Tools/DSH Runner/{Build Scene, Validate Wiring, Reset Saved Progress}` |

### 虚拟宠物（`Assets/Pet/Scripts/`）

| 文件 | 职责 |
|---|---|
| `Core/PetGameManager.cs` | 总装配与中枢：需求/记忆/日记/大脑/行为调度的最小接口都在这里 |
| `Core/PetTypes.cs` | 枚举与数据结构（`PetAction`/`PetMood`/`InteractableKind`/`PetContext`…）+ `PetUtil` |
| `Core/PetNeeds.cs` | 需求衰减与情绪推导 |
| `Core/PetSpecies.cs` | 4 个物种的外观与人格（**改动时注意 `Copy()` 约定**，见坑 9） |
| `Core/PetMemory.cs` | 短期对话 + 长期事实（PlayerPrefs） |
| `Memory/PetJournal.cs` | 日程表式持久记忆（日历的数据源，带"钉住"语义） |
| `Brain/PetBrainConfig.cs` | 大脑配置（baseUrl/model/key/温度/额外提示词）+ 环境变量解析 |
| `Brain/PetPrompting.cs` | 提示词组装 + 回复解析（`<action>` / `<remember>` / JSON） |
| `Brain/DeepSeekPetBrain.cs` / `Brain/OpenAiClient.cs` / `Brain/LocalPetBrain.cs` | 网络大脑 / HTTP 客户端 / 离线兜底 |
| `Behavior/PetBehavior.cs` | 行为行定义 + 打分（纯函数，可单测） |
| `Behavior/PetBehaviorLibrary.cs` | 行为表（8 条主动 + 9 条被动 + fetch） |
| `Behavior/PetBehaviorScheduler.cs` | 主动/被动两条计时器，决定"下一步做什么" |
| `World/PetRoom.cs` | 房间程序化搭建、可交互物收集、**墙体娃娃屋剔除**、道具摆放与着色 |
| `World/PetController.cs` | 行为执行：Idle/Wander/Approach/React/Sleep/**Fetch** |
| `World/PetAvatar.cs` | 宠物外观与动作 |
| `World/PetBall.cs` | 球的物理与状态机（手持/飞行/叼着/静止） |
| `World/PetBubbles.cs` | 洗澡时冒的程序化肥皂泡（自建自毁，不留残渣） |
| `UI/UiSkin.cs` | **在 `Assets/Mobile/`**：运行时生成的圆角面板 + 投影（九宫格），两个游戏共用 |
| `Core/PetPersonality.cs` | 四维性格 + 原型名，按物种稳定生成并存档 |
| `World/PlayerRoomController.cs` | 玩家角色移动、E 交互、扔球 |
| `World/PlayerAvatar.cs` | 玩家外观 |
| `World/Interactable.cs` | 可点击物件（碗/床/球/梳子/门） |
| `World/RoomCameraRig.cs` | **三种视角**（全景/自由/跟随主角）+ 屏幕取景闭环 |
| `World/PropTint.cs` | 给素材道具自己的颜色（材质属性块不能序列化，所以做成组件） |
| `UI/PetHud.cs` | 全部 IMGUI 界面（状态面板/聊天/换物种/视角/浮层面板/日历），含手机布局与设计像素缩放 |
| `Audio/ProceduralAudio.cs` | 波形合成工具（正弦/噪声/包络/混音/琶音） |
| `Audio/PetAudioDirector.cs` | 16 个音效 + 按心情变调的"说话" + 环境音 |
| `Audio/PetVoice.cs` | 音色：物种定音区 + 性格弯曲 + 心情染色；也把音色换算成 TTS 的 pitch/rate |
| `Core/PetWorldMap.cs` | 地图：三个地方（小屋/花园/夜晚露台）的配色、道具、需求修正与解锁价格；解锁与当前所在地的存档 |
| `Core/PetChatter.cs` | 宠物之间的交流：6 种交换，按两只的性格挑，本地文案表（零 token） |
| `MiniGame/MiniGameLibrary.cs` | 小游戏注册表（门 → 出去玩的场景往返） |
| `Editor/PetSceneMenu.cs` | 菜单：`Tools/DSH Pet/{Build Pet Scene, Validate Wiring, Fix Script Encodings, Add Scenes To Build Settings, Clear Pet Save}` |
| `Tests/Editor/*.cs` | 120 条宠物测试（纯逻辑为主，不依赖场景） |

### 手机端（`Assets/Mobile/`，程序集 `DshMobile`）

**这一层不引用宠物或跑酷**（两者各自引用它），所以谁都能用、也不会造成循环依赖。

| 文件 | 职责 |
|---|---|
| `Scripts/MobileUi.cs` | 平台判断（`IsMobile` / `UseTouchControls` / 编辑器预览开关 `ForceTouchControls`）、HUD 缩放 `UiScale`/`ScaleFor`、安全区换算、`MinTouchTarget=44` |
| `Scripts/MobileTouch.cs` | **多点触控核心**：每帧读 `Input.touches`，自己做命中与手指归属（`btn:<id>`/`stick`/`gesture`/`ui`）；自动安装驱动器（`[DefaultExecutionOrder(-200)]`）；编辑器里用鼠标合成一根手指 |
| `Scripts/TouchGesture.cs` | 点击/滑动识别（阈值按屏幕短边，`ReferenceSize` 可注入所以能测） |
| `Scripts/VirtualJoystick.cs` | 浮动摇杆（按下的地方就是圆心，带死区与重映射） |
| `Scripts/MobileWidgets.cs` | 触控控件绘制 + **设计像素↔屏幕像素**换算（`BeginFrame`/`ToScreen`/`ToDesign`） |
| `Scripts/MobileButtonIds.cs` | 按钮 id 常量（宠物 5 个 + 跑酷 5 个） |
| `Scripts/MobileBootstrap.cs` | 移动端运行时设置：60fps、关垂直同步、常亮、阴影距离 ≤26、AA=2 |
| `Editor/MobileBuildMenu.cs` | 菜单：`Tools/DSH Mobile/{Report Mobile Status, Toggle Touch Preview, Configure Android Player Settings, Build APK}` |
| `Editor/MobileAndroidPackaging.cs` | 改生成的 Gradle 工程：安卓桌面图标名 + 开发版的 `usesCleartextTraffic` + `VIBRATE` 权限（见坑 27、30） |
| `Editor/MobilePreviewSizes.cs` | 反射改 Game 视图尺寸（真机竖屏/横屏/平板），用于照着真机比例看布局（见坑 31） |
| `Editor/IconPainter.cs` | 画图标用的软件光栅器（渐变/圆/圆角矩形/三角/拱门/径向辉光，4 倍超采样，归一化坐标） |
| `Editor/MobileIconBuilder.cs` | 画应用图标（小屋 + 门口的猫 + 月亮）并按 Android 三类图标写进玩家设置 |
| `Art/icon_*.png` | 生成出来的四个图标层（**要提交**：PlayerSettings 按 GUID 引用它们，缺了图标就没了） |
| `Scripts/MobileHaptics.cs` | 振动反馈：`HapticGate`（限流 + 开关，可测）+ 安卓 `Vibrator`/`VibrationEffect` |
| `Scripts/MobileTts.cs` | 语音输出：安卓 `TextToSpeech` + `AndroidJavaProxy`；`Speech()` 去掉括号动作与 emoji（纯函数，可测）；引擎初始化失败每 6 秒重试；`StatusText` 把引擎状态翻成人话；非安卓平台全部是无害空操作（见坑 58–61） |
| `Tests/Editor/MobileInputTests.cs` | 23 条测试（手势 / 摇杆 / 缩放 / 手机布局 / 坐标变换） |

### 小游戏（`Assets/MiniGames/`，程序集 `DshMiniGames` + `DshMiniGames.Editor`）

**这一层不引用宠物或跑酷**（和 `DshMobile` 一样），所以新玩法不需要动已有的两套代码。
回房间走 `SceneManager.LoadScene("PetRoom")` + `dshpet.away` 这个 PlayerPrefs 标记，
不建立程序集之间的引用。

| 文件 | 职责 |
|---|---|
| `Scripts/FlyBird/FlyBirdRules.cs` | 小鸟飞行的纯逻辑：一步积分（含长帧钳位）、速度/间隙的难度曲线与封顶、间隙中心的可达范围、碰撞判定（算鸟的宽度）、金币与评价 |
| `Scripts/FlyBird/FlyBirdGame.cs` | 玩法与场景内容（鸟/管道/地面都是 primitive 生成）、按屏幕形状取景（`FitCamera`）、管道生成与回收、点击输入、死亡与结算 |
| `Scripts/FlyBird/FlyBirdHud.cs` | IMGUI：分数、最高分、宠物币、开始提示、结算面板（再来一次 / 回到宠物小屋）、`PointerOverPanel`（防"点按钮同时扇翅膀"） |
| `Editor/FlyBirdSceneMenu.cs` | 菜单：`Tools/DSH Mini/{Build FlyBird Scene, Add Mini Game Scenes To Build Settings, Report Mini Games}` |
| `Scripts/JumpQuest/JumpQuestRules.cs` | 横版平台玩法的纯逻辑：跳跃弧线（含按住跳更高）、最小平移量碰撞解算、踩怪/侧撞判定、关卡生成与**可通过性校验**、金币与评价 |
| `Scripts/JumpQuest/JumpQuestGame.cs` | 玩法与场景内容（关卡由数据生成 primitive）、相机跟随、敌人巡逻、金币与终点、三条命 |
| `Scripts/JumpQuest/JumpQuestHud.cs` | IMGUI：金币/命数、开始与结算面板、**按住式**触屏方向键（借 `MobileTouch`，可同时按走与跳） |
| `Tests/Editor/FlyBirdRulesTests.cs` | 12 条测试（抬升与下落、帧率无关、长帧钳位、难度封顶、间隙永远可达、碰撞算宽度、金币单调） |
| `Tests/Editor/JumpQuestRulesTests.cs` | 12 条测试（蓄力距离与封顶、充能条与距离一致、完美/落地/落空的判定、方块永远跳得到且留有余量、难度递增、金币单调、飞行弧线与判定一致、偏移不超出一维判定） |

在屋里玩的**拼图**没有独立场景：`DshPet` 里的 `PetPuzzle`（纯状态机）+ `PuzzleArt`（程序化画图）+ `PetHud.DrawPuzzle`（面板）。
三个玩法的形状是一样的：**一个纯逻辑规则类（全部可单测）+ 一个只负责画矩形/面板的场景或面板 + 一个 IMGUI HUD**。
加第四个玩法的成本因此是可预测的。

### Shader（`Assets/Shaders/`）

`ShellFur`（几何着色器分层毛发）、`RainbowFur`（毛发 + 流动七彩）、`DreamySkybox`（三段渐变 + 极光 + 星空）、`Neon`（房间/跑道用的平涂光照材质）。

---

## 三、本机环境事实（会影响判断的）

1. **Unity 2022.3.62f3c1，Gamma 色彩空间**（`m_ActiveColorSpace: 0`）。没有 linear→sRGB 转换，
   按线性空间估算的颜色会偏亮，实际会偏暗——调色时以截图为准。
2. **区域设置是中文**，因此：源码必须带 UTF-8 BOM（否则中文字面量编译成乱码，见坑 1）；
   控制台/文件里可能出现 GBK 与 UTF-8 混用。
3. Windows 沙箱相关：本次会话是 `danger-full-access`；`git` 需要 `-c http.sslBackend=openssl`；
   受限模式下带管道的子进程会被拒。
4. MCP 服务端与 checkout 是**机器级共享**的；Unity 工程只是加一行包引用即可复用（详见 `docs/MCP_SETUP.md`）。
5. PlayerPrefs 键（存档迁移/清理时用）：

   | 键 | 内容 |
   |---|---|
   | `dshpet.species` | 当前物种 id |
   | `dshpet.petname.<speciesId>` | 每个物种各自的名字 |
   | `dshpet.needs.<speciesId>` | 需求（饱食/精力/开心/清洁） |
   | `dshpet.affection` | 亲密度 |
   | `dshpet.memory.recent.<speciesId>` / `.facts.<speciesId>` | 短期对话 / 长期事实 |
   | `dshpet.brain.config` | 大脑配置 JSON |
   | `dshpet.audio.muted` / `dshpet.audio.volume` | 音频设置 |
   | `dshpet.view` | 视角模式（0 全景 / 1 自由 / 2 跟随主角） |
   | `dshpet.away` | "出去玩了"标记（小游戏往返用） |
   | `dshpet.voice` | 宠物叫声开关（默认开） |
   | `dshpet.tts` / `dshpet.tts.hinted` | 朗读开关（**第 14 轮起默认开**，和叫声一致）/ "已经提示过设置里能开朗读" |
   | `dshpet.stt` | 语音输入开关（默认开，关掉就不显示麦克风按钮） |
   | `dshpet.memory.level` | 记忆配对的难度（`easy`/`normal`/`hard`） |
   | `dshpet.species` | **小游戏也读它**：跳一跳的主角就是这个物种（`DshMobile.MiniAnimal`）|
   | `dshpet.coins` | 宠物币（`DshMobile.PetWallet`） |
   | `dshpet.collection` | 商城/仓库/背包的 JSON（`PetCollection`） |
   | `dshpet.world.unlocked` / `dshpet.world.current` | 地图解锁列表 / 当前所在地 |
   | `dshpet.traits.<speciesId>` | 每个物种的四维性格 |

6. 日记存档是**独立 JSON 文件**，不在 PlayerPrefs 里：
   `%USERPROFILE%\AppData\LocalLow\DefaultCompany\UnityMCPProject\dshpet-journal-<speciesId>.json`。
   `Tools/DSH Pet/Clear Pet Save` 会**同时**清 PlayerPrefs 和这些文件。

---

## 四、这个项目里已经做过的关键决定（改之前先读）

- **UI 全部用 IMGUI（OnGUI）**，不用 Canvas/UI Toolkit：不需要字体资源和额外包，
  任何工程状态下都能跑。代价是布局要自己算——所以布局常量集中在 `PetHud.ComputeLayout()`，
  并且有测试钉住。
- **房间/角色/毛发都是程序化生成的**（除 Kenney CC0 素材外没有外部资产），
  所以整个工程可以只靠代码复现。
- **音效是程序化合成的**（无资源、无 key、无版权问题）。
- **宠物大脑可替换**：网络大脑失败会自动落到离线大脑，并且把错误显示在聊天区，
  不会让玩家对着空白发呆。
- **行为表是数据驱动的**：加一个活动 = 在 `PetBehaviorLibrary` 加一行（+ 可选新 `InteractableKind`），
  这是留给"虚拟小镇"的扩展点。

---

## 五、验证工作流（改完东西必须走一遍）

用 MCP 工具按顺序：

```
1) 改代码
2) mcp__unity__refresh_unity(compile: request, wait_for_ready: true)
3) mcp__unity__read_console(types: ["error"])        ← 必须为空
4) mcp__unity__run_tests(mode: EditMode) → get_test_job(wait_timeout: 120-180)
5) 需要看运行时行为 → mcp__unity__manage_editor(play) + mcp__unity__execute_code 做定量测量
6) 需要看画面 → mcp__unity__manage_camera(screenshot) 然后**用 read_image 真的看图**
7) 退出 Play → 保存场景（execute_code: EditorSceneManager.SaveOpenScenes）
8) 清理 Assets/Screenshots（要留的证据图先拷到 docs/evidence/）
```

工程自带的两个检查入口（比手写脚本快）：

- `Tools/DSH Pet/Validate Wiring` —— 输出房间/角色/相机/小游戏/行为表/球/HUD 几何/大脑状态的完整报告。
  排查"某处没接上"时先跑它。
- `Tools/DSH Runner/Validate Wiring` —— 跑酷侧的同类报告。

**经验**：UI 问题优先用 `execute_code` 量坐标，别靠肉眼；但**一定要真看图**——
本项目有 5 个 bug（面板重叠、浮层半透明、玩家被聊天面板挡住、道具飘在半空、中文乱码）
是"数值上没错、看图才发现"的。

---

## 六、坑清单（每一条都真实踩过）

### 编译 / 编辑器

1. **中文源码有两个独立的编码陷阱**（都真实踩过，且症状相似）：

   **1a. 缺 UTF-8 BOM → 编译期乱码。** 中文区域设置的 Windows 上，Unity 编译器按 GBK 解释
   **没有 BOM** 的 `.cs`，于是该文件里**所有**中文字面量变成乱码（实测：「全景」在 HUD 里
   显示成 `锟斤拷`）。修法：`Tools/DSH Pet/Fix Script Encodings` 一键补 BOM。
   **注意：用编辑工具改过含中文的脚本后 BOM 会掉，改完要再补一次。**

   **1b. 用 PowerShell 的 `Get-Content`/`Set-Content` 编辑这些文件 → 静默损坏。**
   这个更阴险：往返一次会把多字节字符替换成 `U+FFFD`（还常常连带吞掉后面的引号），
   **BOM 补不回来**，只能重新打字。实测一次往返损坏了 41 个字符（`PetLogicTests.cs` 39 个 +
   `RoomCameraRig.cs` 2 个），表现为 `error CS1010: Newline in constant`。
   **规矩：改这些文件用文件工具，或 `[System.IO.File]::ReadAllText/WriteAllText` 显式指定 UTF-8；
   绝不用 `Get-Content | Set-Content` 往返。**
   `Fix Script Encodings` 现在也会**报告**哪些文件含 `U+FFFD`（它修不了，但能让你立刻知道
   该去哪儿重打）。
2. **脚本域可能卡住**：DLL 更新了但运行时还是旧程序集（改代码不生效、不改代码却变）。
   `refresh_unity`/`RequestScriptReload`/进出 Play 都可能无效 → **重启 Unity 编辑器**
   （重启前先保存场景）。
3. **Bee 增量编译偶尔复用某文件的旧产物**（实测过 `PetSpecies.cs`）→ 改一下该文件 mtime 强制重编。
4. **场景里序列化的值会盖住脚本默认值**。改了脚本里的默认值但场景实例没跟着变
   （实测：相机 `RoomLimit`、跑酷的 `RollDegreesPerMetre`）。**改完记得把值写进场景并保存**。
5. **运行时的 List/Dictionary 不会被序列化**。场景里搭好的房间不会跑 `BuildShell`，
   于是"运行时才有"的列表是空的（实测：墙体剔除列表为空 → 近处的墙永不消失）。
   处理办法：按名字惰性重建，或改成组件字段。
6. **`Awake` 在 EditMode（编辑器/测试）里不一定跑**。只在 `Awake` 里缓存的东西会是 null，
   而且**失败是静默的**（实测：`_camera` 为 null → 整个取景逻辑不跑，只是画面差 8px）。
   处理办法：用惰性属性取。
7. **编辑器非播放模式下 `_Time` 只在重绘时推进**，静态截图里动画可能"看起来没动"。
8. **`Application.CanStreamedLevelBeLoaded` 在编辑器里恒 false**（即使场景已注册）→
   用 `SceneUtility.GetBuildIndexByScenePath(path) >= 0` 判断。

### IMGUI

9. **`GUILayout` 区域不裁剪绘制，但超出区域的控件收不到点击**。
   `GUILayout.TextArea(..., ExpandHeight(true))` 会把后面的按钮挤出面板 → **按钮变成点不到**
   （这个坑犯了两次：设置面板、提示词面板）。
   **规矩：内容可滚动、按钮钉在页脚（用 `inner.yMax - footerHeight`），不依赖滚动位置。**
10. **不要用"铺满面板的隐形 Button"挡点击**。看着像个好办法，实际上它会**抢走面板内所有控件的点击**
    （实测：设置面板的输入框因此完全没法输入）。正确做法是浮层打开时给下层面板
    `GUI.enabled = false`——与绘制顺序无关。
11. **物理事件不受 IMGUI 阻挡**：`OnMouseDown` 是物理消息，浮层盖着它照样触发。
    要用 `PetHud.ModalOpen` 之类的开关显式屏蔽世界交互。
12. **`GUI.skin.box` 是半透明的**。用它画浮层，整个场景会透过来（看起来像"面板没打开"）。
    需要遮罩 + 不透明填充。
13. **固定尺寸的居中浮层在小窗口会跑到屏幕外**（实测：720×560 的日历在矮窗口里标题栏渲染到 y<0）。
    统一用 `OverlayRect()` 夹进视口。

### 相机 / 世界

14. **相机被夹在房间里 ⇒ 玩家能走到镜头底下**，直接滑出屏幕下沿并躲到聊天面板后面。
    现在改成"从房间外往里看 + 娃娃屋剔除（只画镜头正对的那面墙）"。
    规则本体是 `PetRoom.ShouldShowFace`（有单测）。
15. **取景要靠闭环测量**：解析式地算俯角在不同间距/不同 HUD 高度下都会失败。
    现在是"每帧测量目标在屏幕上的实际位置，再修俯角"，并且**距离随目标分散程度拉远**。
16. **取景点的高度要一致**：角色取胸口、房间角取地面，混用会有半个身位的误差
    （实测：正是"地板被聊天面板切掉一条"的量）。
17. **`Renderer.bounds` 在刚实例化的帧里可能是旧的**：道具摆放因此算错，
    实测树和灌木飘在半空 1.3~1.8m。改成用 `MeshFilter.sharedMesh.bounds` + 变换矩阵自己算。

### 资源 / 序列化

18. **共享材质会被跨场景污染**：宠物的 Kenney 植物用的是跑酷的草地材质，被染成青色。
    修法是 `PropTint` 组件 + 材质属性块（**属性块不能序列化**，所以必须做成组件才会存进场景）。
19. **静态表元素不能直接赋给序列化字段**：Unity 反序列化会**就地覆写**那个共享对象，
    于是场景里存的数据把静态表改了（实测：`PetSpecies.All[0]` 运行时从狐狸变成兔子，
    换动物器出现两只兔子）。规矩：**存进字段一律用 `Copy()`**。
20. **`Save()`/`Load()` 字段要成对维护**：`PetBrainConfig` 存 JSON 但逐字段读回，
    新字段漏在 Load 里就会"保存成功但下次启动没了"。有测试逐字段盯着。

### 生命周期

21. **静态单例要在 `OnDestroy` 里清空**。否则场景卸载后静态引用还指着已销毁对象，
    下次调用在已销毁的 `AudioSource` 上抛异常（实测：出去玩一圈回来触发）。
    （这条是测试抓到的。）
22. **协程里抛异常会静默结束整个协程**，状态会卡住（`IsThinking` 卡住）→ 加了看门狗 + 兜底。

### 安卓 / 触控（第 6 轮新增）

23. **Android 模块必须在编辑器启动之前装好。** 编辑器跑着的时候装模块不会报错，但
    `BuildPipeline.BuildPlayer` 会直接抛 `Build target 'Android' not supported`——
    因为构建扩展程序集是启动时加载的，运行中不会补。更坑的是
    `BuildPipeline.IsBuildTargetSupported(Android)` 此刻**返回 True**，看它会被骗。
    **规矩：装完模块重启编辑器**（重启后同样的调用立刻成功）。
24. **IMGUI 只有一个指针。** `GUI.Button` 拿不到第二根手指，所以"左手推摇杆、右手点跳"
    在 IMGUI 里天然做不到。于是有了 `MobileTouch`：**自己读 `Input.touches` + 自己做矩形命中**，
    再把控件画成不接收输入的图案（`MobileWidgets`）。凡是要"同时按"的控件，都必须走这一层。
25. **设计像素和屏幕像素只能换算一次**（本轮最贵的一个 bug）。HUD 用
    `GUI.matrix` 整体缩放绘制，于是"绘制坐标"是设计像素，而"触控命中"是真实屏幕像素。
    一开始把 `ToScreen(designRect)` 的结果交给**在矩阵里绘制**的控件，等于缩放了两次：
    编辑器 0.9 倍下只偏 10%（肉眼像"差不多对"），真机 1.8 倍下是 3.24 倍，控件直接飞出屏幕。
    **现在的规矩：控件接口一律收设计矩形，内部绘制用原值、注册命中区时才 `ToScreen`；
    反方向（摇杆的触点）用 `ToDesign`。有 4 条单测钉住这个变换。**
26. **`GUILayout.BeginArea` 里的 `GUI.*` 坐标是相对这个区域的。** 收起的聊天条把绝对矩形
    塞进 `BeginArea` 里画，于是整条被又偏移了一次、画到屏幕外，看着就是"底部只有一条黑边、
    里面什么都没有"。**要么全用绝对坐标，要么全用 GUILayout，别混。**
27. **安卓 9+ 默认拦明文 http，而 Unity 的 `InsecureHttpOption.DevelopmentOnly` 只管
    `UnityWebRequest`。** 实测生成的 Gradle 工程里 `AndroidManifest.xml` **完全没有**
    `usesCleartextTraffic`。连局域网 http 网关要用 `Assets/Mobile/Editor/MobileAndroidManifest.cs`
    在开发版里补上（发布版保持安卓的安全默认）。
28. **编辑器 Game 视图的分辨率改不动。** `Screen.SetResolution` 在编辑器里无效，
    `GameViewSizes`/`selectedSizeIndex` 反射也拿不到（`instance` 为 null）。
    验证手机布局的办法是：`Tools/DSH Mobile/Toggle Touch Preview` 强制手机布局 + 单测按
    真实手机尺寸算几何 + 真机/出包实测。**别为了改分辨率在编辑器里折腾。**
29. **边玩边重编译会留下"半死"的播放状态**：编辑器重启脚本域时会保留场景对象但不会重跑
    `Awake`，于是 `GameManager.Instance` 是 null 而 `GameRoot` 还在。
    判据：`Application.isPlaying == true` 但单例为 null → **退出 Play 重新进**。
30. **改 `PlayerSettings` 的公司名/产品名 = 换存档目录（桌面端也一样）。**
    `Application.persistentDataPath` 和 Windows 上的 PlayerPrefs 都挂在
    `LocalLow\<公司>\<产品>` / `HKCU\Software\<公司>\<产品>` 上。
    本轮为了让手机桌面图标叫「RoomPet」改了一次这两个名字，结果日记文件当场"搬家"
    （`dsh-default` 下 24KB 的狐狸日记在新目录里变成空白）——**宠物会把主人忘了**。
    正确做法：**名字不动，只把安卓的 `app_name` 打进生成的 Gradle 资源**（见
    `MobileAndroidPackaging`）。改名前先问自己"这会不会搬走存档"。
31. **编辑器里改 Game 视图尺寸只能靠反射，而且 `GameViewSizes.instance` 在基类上。**
    `Screen.SetResolution` 在编辑器里是空操作；`GameViewSizes` 派生自
    `ScriptableSingleton<T>`，`instance` 属性声明在那个基类上，所以
    `sizesType.GetProperty("instance")` 返回 **null**（要加 `FlattenHierarchy`）——
    失败是静默的，代码不会抛异常，只是什么都没发生。
    另外 `GameViewSizeGroup.IndexOf(size)` 不能用来拿刚加进去的索引（它给的是 0 = Free Aspect），
    要加完之后**重新按宽高扫一遍**。实现见 `MobilePreviewSizes`（坑 28 那个"改不动分辨率"的结论到此作废）。
32. **`PlayerSettings` 改了没生效，先怀疑"改完没编译就点了菜单"。**
    我改完 `allowedAutorotateToPortrait = true` 立刻点 `Configure Android Player Settings`，
    Unity 还没重编译，跑的是旧程序集——日志里那句 "landscape" 就是旧代码打的。
    出包后清单里仍是 `userLandscape` 才发现。**改完脚本先 refresh 再点菜单**，
    并且**出包后一定要回读清单**，别信日志。
33. **增量打包会让 APK 虚胖。** 同一次改动的产物：增量出包 **26.0 MB**，
    加 `clean_build` 重出 **15.3 MB**——ZIP 条目之间留下了一堆空档
    （压过的内容两边都是 15.98 MB，用 `CompressedLength` 求和一对比就看出来了）。
    **要发给别人的包，用干净构建。**
34. **相机是按垂直 FOV 定义的，屏幕一窄水平视野就塌。**
    16:9 上刚好装下 3 条车道的相机，到竖屏（0.45）只剩三分之一的水平覆盖，
    两边车道直接切掉；14m 的房间同理。修法不是拍脑袋乘一个系数，而是
    **量出来**：`MobileUi.RequiredDistanceForWidth` 给出"要多远才装得下"，
    房间那边更进一步——把"要么退到够远"做成和俯仰同一个闭环（`FitSubjectsToWidth`），
    因为近处的角比房间中心张角更大，解析值会差几个百分点。
35. **缩放要按屏幕"短边"算，不是高度。** 竖屏 1080x2400 按高度算是 3.33 → 夹到 1.8，
    只剩 600px 宽的设计空间，而宠物界面左右两个面板本身就要 458px。
    按短边算，横竖屏在同一个手机上得到同一个缩放，字和按钮转屏时不会忽大忽小。
36. **"用真机像素居中、画在设计像素空间"= 位置被放大一个缩放系数**（真机反馈的 bug）。
    浮层一直用 `Screen.width/height` 算居中，而它画在 `GUI.matrix` 里：
    竖屏 1.63 倍下整个弹窗偏右下，底部按钮被推出屏幕——**桌面上看不出来**（缩放是 1），
    编辑器预览也几乎看不出来（1.35，只偏一点点），真机上就明显了。
    **规矩：进了 `GUI.matrix` 之后，任何 `Screen.width/height`、`WorldToScreenPoint` 都必须先换算**
    （`PetHud.DesignWidth/DesignHeight` / `ScreenToDesign` / `WorldLabelRect`）。
    `Event.current.mousePosition` 是例外——它本来就在当前 GUI 空间里。
37. **"预览用了对的像素尺寸"不等于"预览等于真机"：还有 dpi。**
    编辑器报 96dpi、手机报 400-500，而缩放里带 dpi 修正，所以同一个 1080x2400
    在编辑器里是 1.35 倍（设计空间 800 宽）、在手机上是 1.63 倍（665 宽）——
    差了 20%，"在编辑器里验过的布局"根本不是真机跑的那套（坑 36 就是这么漏掉的）。
    现在预览菜单会连密度一起模拟，**并且记进 `EditorPrefs`**：静态字段进 Play 会被域重载清掉，
    不持久化的话按了 Play 就悄悄退回 96dpi（`[InitializeOnLoadMethod]` 负责重放）。
38. **安卓图标不是一个槽，是三类六档，而且自适应图标的前景会被裁。**
    老 API（`SetIconsForTargetGroup` + `IconKind`）在这一版里根本没有 `Adaptive` 这个值，
    要用新的 `PlayerSettings.GetPlatformIcons/SetPlatformIcons` + `PlatformIconKind`：
    **Adaptive（API 26，两层）/ Round（API 25，一层）/ Legacy（一层）**，各 6 档密度。
    两层的那个必须先 `layerCount = 2` 再 `SetTexture(背景, 0)`、`SetTexture(前景, 1)`，
    顺序反了就是背景盖住前景。另外**前景里的东西必须留在中间 66% 内**——
    启动器会把外面三分之一裁掉并套圆形遮罩，屋顶被切掉一角看起来就像 bug 而不是设计。
39. **图标要在 36~48px 下看，不是在设计尺寸下看。** 出包前先跑
    `Tools/DSH Mobile/Icons/Render Size Preview Sheet` 把 192/96/72/48/36px 并排出图再看：
    第一版把猫画成了"圆头 + 圆身"的雪人，1:1 看没问题，缩到 48px 就只剩一坨黑；
    改成"三角形身体 + 圆头 + 耳朵 + 尾巴"的坐姿剪影之后才认得出是猫。
40. **"锚在哪个矩形上"本身就是一个设计决定。** 触控控件原本挂在聊天面板上（底部布局里
    聊天面板就等于屏幕下半部）。第 7 轮把对话搬到侧边栏之后，那套锚点把**摇杆放到了右手边**——
    面板还在，但它已经不在拇指待的地方了。规矩：**拇指控件锚视口，不锚面板**
    （`HudLayout.Viewport`），面板只用来决定"别压住什么"。
41. **响应式布局一改，"X 永远在 Y 下面"这类断言就全废了。** 把对话搬到侧边栏之后，
    四条老测试同时挂掉，因为它们的断言是 `status.yMax <= chat.y`。正确的不变量是
    **"这两个矩形不相交"**（`!a.Overlaps(b)`）——它同时能表达上下排布与左右排布，
    而且比原来的断言更接近真实约束（重叠会吞掉点击）。**别为了让测试过而放宽，
    要把断言换成真正想守的那条。**
42. **IMGUI 的 `GUI.skin.box` 是半透明的**（同一个坑第 2 次）：跑酷菜单用它画，
    森林直接透上来，标题和背景树线打架。凡是要当"面板"用的地方，一律走
    `DshMobile.UiSkin.Panel`（运行时生成的圆角不透明面板 + 投影）。
43. **同一份状态不要在两处各自演算。** 抛球落点预览如果自己写一套抛物线，
    迟早会和 `PetBall` 的积分器漂开；这里直接把 `Gravity / MinThrowSpeed /
    MaxThrowSpeed / ThrowElevationDegrees` 拿来跑**同一个积分步进**，预览才敢叫"预测"。
44. **`ComputeLayout` 这类"静态查询"要小心它和绘制用的是不是同一份输入。**
    第 7 轮给布局加了"对话是否展开"的参数，绘制路径传了新参数，
    但 `ComputeLayout()`（无参、给相机用）读的是另一个静态字段——
    结果**相机以为边栏没了、HUD 还在画边栏**，画面里房间缩着、右边一片空。
    凡是"同一个几何被两处查询"，就把它收敛成一个入口。
45. **GUI 区域的"超出部分"是静默的**（第 8 轮由新测试抓到）。
    状态面板的四个桌面按钮需要 282px，而 640x400 下面板只有 272px：
    最后一个按钮被排在页脚矩形之外——**画得出来、点不到**，看不出任何异常。
    修法不是把面板加宽，而是**先量再排**：`PetHud.EstimatedLabelWidth`（CJK 算一个全角、
    拉丁约 0.56 em）+ 贪婪装箱 `PackRows`；测试同时断言"每行装得下"和"一个按钮都没丢"。
    **凡是手写 IMGUI 里成排的控件，都要有这样一个"放不下就换行"的规则。**
46. **竖屏不是"把横屏缩小"，构图要另想。** 方形房间在浅俯角下投射成一条又宽又扁的带子：
    实测只占竖屏高度的 28%，四分之三的屏幕是空的天空和地板，而改成俯视之后变成 33%
    且形状变成方块。更根本的是，0.45 宽高比下"整个房间都在画面里"与"宠物看得清"没法同时成立——
    所以竖屏首次进入默认给了「跟随主角」，全景留给想看房间的人。
47. **调数值要有观测方式，不然就是拍脑袋。** "多久出一次事才不烦人"这种问题，
    能回答它的不是推理而是**把宠物放着跑一段**：先前的数值实测下来是每分钟左右一地，
    读起来像 bug；现在加了 200 秒冷却 + 按距离算的宽限。这些数字仍需要真机上长时间跑一次才算验过。

48. **角色模型上默认没有碰撞体。** `PetAvatar` 会给每个 primitive 剥掉 collider（免得宠物
    把房间的点击挡掉），所以"点宠物"根本收不到 `OnMouseDown` —— 修法是给宠物**根节点**加一个
    胶囊体 + `PetClickTarget`。规则：**Unity 只把 OnMouseDown 派给 collider 自己所在的
    GameObject**，所以可点的那个对象上必须真的有 collider。
49. **`PlayerPrefs` 里那份存档会盖掉测试里的"替换成干净状态"。** 第一版
    `ReplaceForTests(data)` 只替换内存、没落盘，随后的 `Load()` 又把 PlayerPrefs 里的旧数据
    读回来，于是"新档应该只有 1 只宠物"的测试拿到 5 只。修法：替换时**一并落盘并补种初始宠物**，
    让它和真实加载走同一条路——测试里的状态必须是游戏真会产生的状态。
50. **跨玩法程序集共享的东西要放在共享层。** 跑酷发币、宠物房花币，而 `DshRunner` 与
    `DshPet` 互不引用，所以 `PetWallet` 放在 `DshMobile` 里。想放在任意一边都会编译不过——
    这不是麻烦，这是设计在提醒你"这属于两者之外"。
51. **语音输入/输出是本项目最需要真机的一环。** 安卓的 `SpeechRecognizer` / `TextToSpeech`
    都要 `AndroidJavaObject` 调用、都需要运行时权限，而且**只能靠耳朵验证延迟与发音**；
    第 10 轮把 TTS 做完了（R10.6），STT 仍然推迟并写进了 [BOARD.md](BOARD.md)。
52. **场景文件是快照，存档才是事实。** 搬去花园之后回到小屋，界面看起来像"地图没生效"：
    `PetGameManager.Start` 的判断是"房间里已经有 `Room` 子物体就复用"，
    而 `.unity` 里的房间是**上一轮建的那一间**，于是 `Room.Theme` 被设成花园、
    几何体还是小屋。修法：`PetRoom.BuiltFor(theme)` 把"这间房是为哪个地方建的"
    用 `[SerializeField]` 存下来（`Theme` 本身是 `NonSerialized`，加载时永远是默认值），
    对不上就重建。**凡是"生成出来的东西 + 存下来的状态"，都要能回答"这份生成物是什么时候的"。**
53. **`Destroy` 是延迟的，所以同一帧里数子物体是数不准的。** 换场景后立刻用
    `GetComponentsInChildren<Transform>` 统计道具，会同时数到**还没被销毁的旧房间**：
    实测搬到夜晚露台后"还有 2 块花园的石头"，下一帧才归 0。验证时要么等一帧，
    要么别用"数对象"当判据。顺带一提，这也意味着换场景会有一帧同时存在两间房——
    一帧的重复绘制，肉眼看不到，但别在这中间做查重逻辑。
54. **折行解决了"一排装不下"，不等于解决了"一列装不下"。** 第 8 轮把底栏按钮改成按宽度折行，
    但底栏高度还是写死的 62px：七个按钮在窄面板上折成三行需要 90px，
    最后一行（"重置"）被排在页脚矩形之外——**和坑 45 一模一样的失效方式，只是换了一个轴**。
    修法：底栏高度按**实际行数**算（`FooterHeight(rows)`），并且折行时留 24px 余量，
    因为宽度是估算的（不测字体，CJK 按一个全角估）。**修一个轴上的"装不下"时，
    顺手问一句另一个轴。**
55. **IMGUI 里 emoji 是看不见的，但宽度估算会当真。** `📖 本子` 实测渲染成" 本子"
    （Unity 内建字体没有 emoji），而 `EstimatedLabelWidth` 把代理对（`📖` = 两个 UTF-16）
    按**两个 em** 计——于是这个看不见的装饰**让底栏多折了一行**，正好把"重置"顶出去。
    结论：**结构性的按钮标签上不要放 emoji**（真机字体也许能渲染，但那意味着
    编辑器里验证过的排版在设备上不成立，比看不见更糟）。标题、道具行里留着无所谓，它们不决定布局。
56. **TTS 只该念"说的话"。** 宠物的话里全是"（开心地晃了晃）"，第一版直接把整句丢给合成音，
    结果是机器念括号里的动作说明——可爱立刻变尴尬。`MobileTts.Speech()` 把
    括号段（中英文括号都要）与 emoji 去掉，**整句都是动作就一个字都不念**：
    所以戳一下宠物是"啾"一声 + 一行字，而不是朗读旁白。
    另外安卓 TTS 的初始化是**异步**的（`OnInitListener` 几百毫秒后才回调），
    所以开关一打开就 `WarmUp()`，否则玩家听到的第一句会被初始化吞掉。
57. **多个开关时，"默认值"就是产品决策。** "宠物叫声"默认开（它是角色的一部分），
    "朗读宠物的话"默认**关**——手机突然开始说话是会被静音的那种功能。
    两个开关独立：想安静的人要能一次关掉两样，想听句子的人多半也想听叫声。
    `OnDestroy` 里要 `Stop()`：房间里最后那句话不该跟着玩家念到跑酷的加载画面上。
58. **Android 11 起，应用看不见别的包——包括语音引擎。** "宠物在真机上一句话都不说"
    的根因：清单里少了
    `<queries><intent><action android:name="android.intent.action.TTS_SERVICE"/></intent></queries>`。
    没有它，`TextToSpeech` 构造出来的对象**永远不会回调 `onInit`**，每一次 `speak()`
    都被静默丢弃：真机上毫无反应、日志里什么都没有、编辑器里一切正常。
    **凡是调用系统服务（TTS、识别、分享、相机 Intent），先去查这个 API 级别要不要 `queries`。**
59. **失败不要记成永久状态。** 同一个功能的第二个 bug：第一版把"引擎初始化失败"写进一个
    布尔，于是设备启动瞬间没有引擎、或者慢了一拍，**这一整局就再也不说话了**。
    现在失败只表示"这次没成"，每 6 秒重试一次。**"试过了"和"不行"是两件事。**
60. **玩家听不到的时候，要给一个能当场排查的按钮。** 光有一个开关和一个状态文案不够：
    加「▶ 测试朗读」——它**无视开关**（开关是偏好，测试是诊断），并且把引擎的真实回答
    （初始化状态码、`setLanguage` 的结果、`speak()` 的返回值）翻译成人话显示出来。
    另一个坑：手机没有中文语音包时 `setLanguage` 返回负数，引擎**照样接受**文本然后放静音。
61. **一个没人找到的开关等于没有功能。** 朗读默认关闭是对的（突然说话会被静音），
    但只在设置面板里写一行字，等于只有打开设置的人知道它存在。第一次收到回复时
    在对话里提示一次，用一个新消息类型（`system`）——它居中灰字显示，并且
    **不进模型的提示词**：宠物不该开始回答 UI 自己的建议。
62. **每加一只宠物，就多一处"哪个是真的"的歧义。** 房间里三只动物、面板只描述一只，
    而且没有办法知道是哪只——这不是"多宠物模式"缺一个界面，而是状态面板从第一天起
    就写死给了主宠。修法是让**宠物本身成为页签**，并且"点它"就是"看它"。
    顺带发现：同伴的 `PetNeeds` **从来没有被 tick 过**（只有主宠每帧推进），
    以前没人看见，因为它们是装饰品。
63. **多出来的那张卡该删就删。** 「换一只」卡片和「宠物」面板的背包页做的是同一件事，
    而且它那个按钮会**静默换掉你正在照顾的那只**（连记忆一起换），看起来却像页签。
    **"两个入口做同一件事"不是方便，其中一个迟早会做错。**
64. **相机取景必须同时看两个方向。** 小鸟飞行第一版按"飞行走廊的高度"取景，
    竖屏（0.45 宽高比）下可视宽度只有 4.9 个世界单位，而鸟站在 x=-4.2 ——
    **鸟本身在屏幕外**，画面上只有管道在飘。修法：取"按高度"和"按最小宽度"两者的大者，
    并且转屏时重算。**竖屏不是"横屏裁掉两边"，是另一套取景。**
65. **不要用 PowerShell 读写含中文的源码。** `Get-Content -Raw`（默认按 ANSI 代码页）
    加 `Set-Content -Encoding UTF8` 会把整个文件按 GBK 解码再按 UTF-8 写出：
    多字节字符的字节被"?"替换，注释与字符串全毁，而且**能编译的部分照样编译**
    （错的是文本，不是语法）。这次是靠"文件在 git 里有上一版 + 逐行比对"才救回来的。
    规则：**改 .cs 一律用编辑工具或 `[IO.File]::ReadAllText/WriteAllText` + 显式 UTF-8**，
    绝对不要 `Get-Content`/`Set-Content` 直接改源码。恢复顺序：先在文件里数"损坏行"
    （U+FFFD / 私有区字符 / 代理对残留），再对每一行去 git 的上一版里找原文，
    最后只剩"本轮新写的行"需要人工重写。
---

## 七、下一步候选（按我建议的优先级）

1. **超级玛丽式横版玩法**：三个例子里唯一还没做的。它需要一整套"能站的地面 + 跳跃手感 +
   敌人 + 关卡"，值得单独一轮；骨架已经被拼图和小鸟飞行验证过两次
   （一个场景 + 一个纯逻辑规则类 + 一个 IMGUI HUD）。
2. **真机验收 TTS**：这一轮修掉了两个"真机上完全不响"的原因（见坑 58、59），
   剩下的只有耳朵能判断：发音是否自然、延迟多少、切后台会不会继续念。
3. **更多小游戏**：`MiniGameLibrary` 加一行 + 一个场景即可。接果子、记忆配对都还没做。
4. **宠物社区（联网）**：需要服务端（账号 / 在线状态 / 好友 / 跨用户互动），
   本仓库是纯客户端，属于另一个工程而不是下一轮。
5. **第 4 个场景**：一个场景 = 一组配色 + 几个道具 + 一组需求修正（也许"雨天阳台"）。
6. **语音输入（STT）**：等 TTS 在真机上验过之后再评估，两者共用同一套"宠物在说话"的时序。
7. **扔球的地面落点指示**：现在有力蓄条和准星，但没有"球会落在哪"的地面标记。
8. **云存档**：日记与记忆现在是本地文件 + PlayerPrefs。

66. **"先竖直再水平"的碰撞解算会把撞墙的玩家顶到墙顶上。** 横版玩法的第一版按这个顺序解算：
    先看竖直重叠，再看水平重叠。一个跑向墙的玩家在竖直方向上也"重叠"着墙（墙有 4 个单位高），
    于是竖直那一步很贴心地把他放到了墙顶——**他就这样走过了关卡里的每一堵墙**。
    正确做法是**最小平移量**：算出四个方向的推出距离，沿最小的那个方向推出去，
    并且跑两遍（角落需要第一次推完才露出来）。这条是被单测抓到的，不是被眼睛。
67. **生成的关卡必须被"验算"，不能只是被生成。** 平台跳跃的第一条不变量是
    "这一关能不能过"：每个缺口都要短于一次跳跃、每级台阶都要低于一次跳跃、必须有终点。
    所以 `JumpQuestRules.IsPassable` 是生成器的一部分——不通过就重新生成
    （单测还会拿 60 个种子全跑一遍，外加三种"坏关卡"必须被拒）。
    **"过不去的关卡"不是难度，是 bug。**
68. **三个玩法共用一个形状，这件事值得写下来。** 拼图、小鸟飞行、跳跃冒险都是
    "纯逻辑规则类（吃到全部单测）+ 只负责画矩形/面板的视图 + IMGUI HUD"，
    场景里几乎没有逻辑。这个形状让"再加一个小游戏"变成一件可以估工的工作。
    另外两条从这两个游戏里学到的：**按住式的按钮不能用 `GUI.Button`**
    （它在松手时才触发，对"一直往左走"没有用），要用共享触控层注册矩形；
    **相机取景要看两个方向**（竖屏按高度取景会把主角放到屏幕外，见坑 64）。

69. **`AndroidJavaObject.Call` 是按参数去找方法的——返回值不同就是"没有这个方法"。**
    真机上报回来的错误是
    `NoSuchMethodError: no non-static method with name='setPitch' signature='(F)V'`。
    安卓的 `TextToSpeech.setPitch(float)` / `setSpeechRate(float)` **返回 int**，所以 JNI 签名是
    `(F)I`，而 `Call("setPitch", 1f)` 推导出来的是 `(F)V` —— 于是它去找一个不存在的方法。
    改成 `Call<int>` 就好了。**同一个对象的每个方法都要确认返回类型**，因为这类错误只在设备上
    出现（编辑器里根本没有这个方法），而且日志里只有一句 Java 异常。
70. **"这一下点在 UI 上吗"这个标志，必须由指针位置算出来。** 小鸟飞行的 HUD 在画
    "回到宠物小屋"按钮时顺手写了 `PointerOverPanel = true`（无条件），而游戏用它决定要不要处理
    点击——于是**每一次点击都被当成点在 UI 上**，屏幕怎么点都不飞。这类 bug 的可怕之处是：
    按钮全都正常，只是游戏永远收不到输入。
71. **分数和下标是两回事。** 跳一跳里"完美落地 +2 分"但只前进 1 个方块，而方块下标当时是拿
    分数当的——第一次跳中心就会去找一个还不存在的方块（脚本一跑就崩）。**凡是一个数字同时
    表示"成绩"和"位置"，就是下一次改玩法时会崩的地方。**
72. **"第一个就是主要的"这种假设，会在功能改变的那一刻变成 bug。** 同伴生成器以前跳过背包
    第一格（"那是主宠"）。当"主要照顾"可以通过点击切换之后，主宠不再必然是第一格——
    于是交接之后原来的主宠**根本没有被实例化**，房间里凭空少了一只。改成按记录 id 判断。
73. **按物种存档在"一个物种只有一只"时是对的。** 名字、需求、记忆、日记全都挂在
    `dshpet.*.<speciesId>` 上，两只猫于是共用一个名字、一个饭碗和一本日记。宠物成为"个体"
    （集合里的记录）之后，key 必须换成记录的 id——旧的物种 key 保留为兜底读取，
    这样老存档仍然能读出来。
74. **平台服务要三样东西齐了才会工作**：清单里的声明（Android 11 起还要 `<queries>`）、
    运行时的权限（麦克风）、以及**接口上的每一个回调**（`RecognitionListener` 有十个，
    少一个就在 Java 线程里抛异常，表现为"结果永远不来"）。三样都齐了才谈得上"功能坏没坏"。

75. **控件画在面板外面，比画错更糟：它根本不存在。** 麦克风按钮第一版放在
    `field.x - 46`，也就是输入框左边——在手机上那是面板外的屏幕边缘，**按钮从来没出现在屏幕上**，
    而设置里还在理直气壮地说「语音输入就绪」。**任何按钮的位置都必须从布局里扣出来**
    （先减掉它的宽度，再放输入框），不能「画在旁边」。
76. **重叠的触控按钮必须有一个确定的赢家。** `MobileTouch.FindButtonAt` 原来遍历字典返回第一个
    命中的——字典的顺序不是契约，于是「大按钮盖住小按钮」时谁赢是随机的。现在两条规则：
    **面积小的赢**（贴在上面的小按钮优先），面积相同则**后注册的赢**（像 IMGUI 的绘制顺序）。
    手机端「按钮点了没反应」十有八九是这一类。
77. **emoji 又一次骗了我们。** 圆形动作按钮第一版是「图标 + 小字」（💬🧺✋），结果每个按钮都是
    一个**空的彩色圆盘**——Unity 内建字体没有 emoji，这已经是这一路上第三次（宠物页签、底栏标签、
    现在按钮）；接果子的生命用 ♥ 也渲染成了小三角。**能用汉字就别用符号**：
    「聊天 / 蓄力 / 互动」比三个看不见的图标好得多。
78. **提示文字不能压到别的控件的命中区。** 摇杆的「这里拖动移动」虚影原来挂在摇杆区**下边缘之外**
    30px，正好压在聊天栏的「和它说说话」按钮上——玩家看到两层东西叠在一起，点下去时触控层又把
    手指判给了摇杆，于是「按钮不好使」。修法：虚影严格放在自己的区域**内部**，并且在玩家第一次
    拖动之后淡出（一个永久的提示到最后只剩噪音）。
79. **一个区域被定义两次，就一定会分叉。** 坑 78 只修了"画在哪"，没修"抓在哪"：摇杆的抓取区
    （`MobileTouch.StickZone`）仍然是**凭空写的一个矩形**——屏幕左侧 46% × 下方 60%，
    把整个聊天栏都盖住了。于是「和它说说话」依然点不动：点在按钮上，手指被判给了摇杆。
    现在抓取区**就是**绘制用的那个矩形（`ComputeMobileControls(layout).StickZone`）。
    **"画"和"点"必须是同一个数**；两份定义里错的那一份，永远不会在截图上显形。
80. **emoji 第四次：卡片正面。** 记忆配对第一版用 🍎🐟🦴 之类当牌面，结果每张牌都是空白——
    Unity 内建字体没有 emoji（前三次：宠物页签、底栏标签、圆按钮）。这次不再换成汉字了事：
    牌面改成**程序化画的小动物**（`PetAvatarArt`，96×96 逐像素：头 + 耳朵 + 眼睛高光 + 口鼻）。
    顺带一条经验：**程序化画的东西要能被单测"看"**——两条测试盯着"八张脸互不相同"
    和"覆盖率 18%–92%、颜色数 > 3"，因为"一片空白"和"一坨纯色"在编辑器截图里都不明显。
81. **配对成功后牌该消失。** 第一版配对成功只是盖着牌加个绿边，盘面永远是满的——玩家读到的是
    "已完成"，而不是"我赢了"。现在绿闪一下 → 动物缩小淡出 → 那一格**真的空掉**（0.45 秒）。
    实现上模型不用变：界面自己比较 `IsTaken` 的变化记下"刚配对"的时刻。
    **胜利最好看得见，不然它只是一个计数器。**
82. **难度不加钱就等于没有难度。** 三档难度只把盘面变大而奖励一样，玩家没有理由去点困难。
    现在简单/中等/困难 = 4/6/8 对，奖励最多 20/30/45（步数扣减、保底三成），并且**会记住**档位。
83. **拖动是"距离"，而 IMGUI 一帧会跑两遍。** 接果子改成滑屏之后，最危险的一行是
    "在 OnGUI 里读这一帧移动了多少"：Layout 和 Repaint 都会跑 OnGUI，同一帧的位移被应用两次，
    篮子走双倍距离——而**编辑器里静止的画面完全看不出来**。修法：拖动在 `Update`（一帧一次）
    里读，读完 `ConsumeFrameDelta()` 清掉。同一条道理反过来也成立：`FrameDelta` 是唯一一个
    "不是标志位"的输入，任何读它的地方都必须自己负责消费掉它。
84. **默认值就是产品决策（第二遍，反着来）。** 坑 57 说"朗读默认关"，理由是"手机突然说话会被
    静音"；真机反馈是"宠物还是不会说话"——玩家按了「▶ 测试朗读」听得见，宠物却一直闭嘴，
    因为那是同一个偏好值，而它默认是关的。**一个功能要靠翻设置才生效，就不是默认开不开的问题，
    而是没人知道它存在。** 现在默认开，并且**按下测试按钮就等于打开它**：玩家的动作已经表达
    了意图，不该再让他去找勾选框。
85. **同一个按钮点不动，通常有三个原因，要一次修完。** 「麦克风不好使」最后是：①尺寸太小
    （52 → 64 设计像素，栏高 68 → 80）②没权限时提示"再点一次"（一句没人读的话）——现在**记住
    意图，拿到权限自动开始听**；③国产 ROM 的识别服务要 `EXTRA_CALLING_PACKAGE`，否则回
    `ERROR_CLIENT` 就像"听过了"。**"点不动"是一个体验描述，不是一个 bug 描述**：
    尺寸、时序、平台差异都要各查一遍。
86. **emoji 第五次，而且这次是"看不见的按钮"。** 桌面聊天底栏的静音按钮写着 🔊/🔇——
    内建字体里没有这两个字形，所以它一直是**一个空白按钮**（前面四次：宠物页签、底栏标签、
    圆按钮、记忆配对牌面）。结论再升级一次：**凡是玩家要点的东西，标签只用汉字**；
    emoji 只允许出现在"纯装饰、且不参与布局"的地方。
87. **形状就是信息。** 接果子的果实以前是五种颜色的 `Sphere`——玩家看到的是"小球"（原话：
    "怎么是小球"）。换成用基本体搭的苹果/橘子/梨/香蕉/胡萝卜/番茄之后，**一眼就知道要接的是食物**。
    顺带一条：带梗带叶的果子必须**更大**才看得出细节（0.28 → 0.34 单位），
    因为细节是长在轮廓外面的。
88. **两个程序集之间，PlayerPrefs key 比程序集引用更松。** 跳一跳的主角要变成"宠物本身"，
    但小游戏（`DshMiniGames`）**故意**只引用共享层 `DshMobile`，够不到 `DshPet` 的物种表。
    与其为了一个造型把依赖接上去，不如**读同一个 key**（`dshpet.species`）——
    这和它们回房间用的 `dshpet.away` 是同一种做法：**数据耦合比代码耦合更难变成一团乱麻**。
    映射函数（`FromSpeciesId`）是纯的，所以"八个物种 id 各对应哪只动物、垃圾输入落到谁"全都能单测。
89. **`Destroy` 是延迟的，所以"我删掉了"在当帧是假的。** 新写的"小动物"构件里，
    每个部件都用 `Destroy(collider)` 去掉碰撞体——游戏里没问题（下一帧就没了），
    但测试里断言"没有碰撞体"时看到的是 **21 个**。编辑器里正确做法是 `DestroyImmediate`。
    **一条自己抓到自己 bug 的测试，比十条通过的测试更值钱**：它证明这条断言真的在测东西。
90. **"按钮没反应"要先量一遍"点击送到哪了"，再谈功能。** 用户报"点蓝色的说没反应"，
    第一嫌疑是两轮前刚修过的触控命中（大聊天栏吞掉小按钮）。于是给触控层加了
    `MobileTouch.HitTest(屏幕坐标)`——问的就是它自己用来分发的那段逻辑——答案是 `pet.mic` 赢了，
    命中没问题。**于是问题被推进到下一层**：真的在听，只是失败得无声无息
    （错误信息只画在设置面板里，而玩家在聊天面板）。这两件事必须分开测：
    **输入有没有送到**是触控层的事，**送到之后做了什么**是功能的事。
91. **失败必须留在屏幕上的历史里。** 识别失败以前写进一个会被每帧重画的标签，
    展开面板才看得见、切走就没了。现在同时：①写成对话里的**系统提示**（留在记录里、可回读、可截图）；
    ②底栏一行实时状态；③按钮自己变成「等」（权限弹窗还开着）。**"没反应"往往就是"反馈放错了地方"。**
92. **第一次失败以后要换一种问法，而不是再问一遍。** 安卓识别器有两种常见形状：
    带 `EXTRA_LANGUAGE=zh-CN`（能保证中文，但部分 ROM 直接回 `ERROR_CLIENT`）和不带（交给系统）。
    与其猜这台手机认哪种，不如**失败一次就换另一种再试一次**，并且**先重建识别器**——
    已经报过错的识别器对象可以一直错下去。只重试一次：重试两次失败说明这台手机就是不行，
    再打下去只会把一条清楚的错误信息变成一个谜。
93. **"太随机"通常是一个能算的数。** 小鸟飞行的管子以前每个洞独立在整片天空里随机
    （跨度 13.9 个单位），而小鸟在两个管子之间最多只能移动 3~5 个单位——**相邻落差平均 4.91、
    最大 14.78**。也就是说很多组合**在生成的那一刻就已经不可能通过**，玩家感受到的是"随机"，
    而实际上它是"不公平"。修法是把随机**限制在可达窗口里**：从上一个洞出发，算出这段时间里
    物理上能移动多远，洞心只能落在窗口内（`NextGapCentre`），再与"别掉出屏幕"取交集。
    随机性没有消失——它只是学会了尊重物理。
94. **算"能移动多远"时，上行和下行不是对称的。** 同一个 bug 的第二层：第一版两边都用
    "速度 × 时间"，但**扇翅膀是瞬时改变速度**（想上升立刻就能上升），而**下坠要被重力加速**——
    12 单位/秒的终端速度需要 0.55 秒才能达到，而两个管子只隔 0.5 秒。所以小鸟在管子之间
    实际只能掉 **2.7** 个单位，不是 6 个。`FallDistance`（自由落体 + 终端速度封顶）才是对的下行公式。
    **任何"速度 × 时间"都值得问一句：这个速度是从一开始就有的吗？**
95. **`CreatePrimitive(Cube)` 做阴影，就是一块黑方块。** 两个角色的接触阴影都是把 Cube 压成薄板
    涂成近黑色——玩家看到的是"脚下拖着一块黑方块"（尤其是走动时，因为那时眼睛在看脚）。
    形状本身也是信息：阴影必须**圆**且**边缘渐隐**。现在用平躺的四边形 + 程序化径向渐变贴图 +
    透明无光照 shader（`Sprites/Default`，在 Unity 默认 always-included 列表里，所以 `Shader.Find`
    在真机上也找得到）。**验收要用像素**：同一个机位渲染两次（开/关阴影），脚下的亮度差应当
    从中心平滑衰减到边缘；方块会表现为"中间一样暗、边缘一刀切"。
96. **难度上限要按"人的上限"定，不是按代码的上限。** 速度 2.2× 意味着每 0.5 秒来一个管子，
    数值上完全合法、也"理论上能过"，但实测超过拇指能处理的节奏；同理，一次扇翅膀上升 1.24 个单位
    而最小洞只有 2.3，等于**不到两个翅膀高**，玩家没有瞄准空间。现在上限 1.9×、最小洞 3.4。
    **"能通过"是必要条件，不是充分条件**：还要留出人的反应时间与瞄准空间。
97. **节奏必须长于角色自己的周期。** 同一个游戏的第三层：管子间距是固定的 4.6 单位，
    速度涨到 2.2× 时**每 0.5 秒**来一个管子，而一次扇翅膀起落（`2 * FlapSpeed / Gravity`）
    要 **0.64 秒**——玩家永远在接一个"半周期中的鸟"，那不是难度，是抽奖。
    现在间距随速度增长（`SpacingFor`），最短间隔锁在 0.85 秒。
    **凡是有"周期"的操作（一个跳、一次扇翅膀），障碍的间隔必须长于它**，这条比"可达"更早生效。
98. **不诚实的测试会替 bug 打掩护。** 「自动驾驶飞完 150 管 × 20 种子」这条测试第一版**通过了**，
    但它只在管子经过小鸟的**那一帧**检查高度——于是小鸟可以"擦着管子进去"却被记为干净通过。
    改成**逐帧**对照真实游戏（管子按间距生成、向左移动、每帧判定碰撞）之后，**它当场失败**：
    种子 1 只飞了 12 个管子。**测试的忠实度就是它的全部价值**：一个比游戏宽松的模型，
    只会证明那个模型自己没问题。修完之后这条测试带着我走完 12 → 55 → 108 → 150。
99. **要证明"能过"，就得让一个会玩的东西真的去玩。** 纯几何断言（"落差在窗口内"）能证明
    **理论可达**，但证明不了**可玩**：窗口是按"从静止开始、用尽全部位移"算的，而真实的鸟
    带着速度、玩家的手指会晚一帧。这一轮的所有调参都是被那条自动驾驶测试逼出来的，
    而不是被"看起来能过"逼出来的。最后又在**真实场景**里用同一个控制器飞了 462 个管子收尾。

---

## 八、一分钟速查

```powershell
# 工程与场景
D:\projects\dsh-unity\UnityMCPProject
Assets\Scenes\Main.unity            # 跑酷
Assets\Pet\Scenes\PetRoom.unity     # 虚拟宠物
Assets\MiniGames\Scenes\FlyBird.unity     # 小鸟飞行
Assets\MiniGames\Scenes\JumpQuest.unity   # 跳一跳
Assets\MiniGames\Scenes\CatchFruit.unity  # 接果子

# 仓库
git remote -v                       # origin = github.com/codesknight/roompet
# 提交里不含 unity-mcp/ 与 .venv/，也不含本机的包路径：
# Packages/manifest.json 与 packages-lock.json 的本机改动被 skip-worktree 隐藏了。
# 这两行要是被 reset 冲掉，一条命令装回来：
powershell -File scripts/enable-mcp-package.ps1
# GitHub 侧的标签/里程碑/issue/release 也能一键重建（幂等）：
powershell -File scripts/setup-github.ps1

# 自检菜单（Unity 里）
Tools/DSH Pet/Validate Wiring       # 宠物侧完整状态报告
Tools/DSH Pet/Build Pet Scene       # 重建场景里的房间（改了 PetRoom 的摆放逻辑后必须跑）
Tools/DSH Pet/Fix Script Encodings  # 补 UTF-8 BOM（中文乱码时先跑这个）
Tools/DSH Pet/Clear Pet Save        # 清存档（含日记文件）
Tools/DSH Runner/Validate Wiring    # 跑酷侧自检
Tools/DSH Mini/Build CatchFruit Scene   # 从代码重建接果子场景并注册到 Build Settings
Tools/DSH Mobile/Report Mobile Status          # 平台/触控/缩放/安全区/包名/架构
Tools/DSH Mobile/Toggle Touch Preview          # 编辑器里用手机布局（鼠标当手指），Ctrl+Shift+T
Tools/DSH Mobile/Preview/Phone Portrait 1080x2400   # 把 Game 视图切成真机尺寸，Ctrl+Shift+1..4
Tools/DSH Mobile/Preview/Report Current Viewport    # 打印当前视口 / 方向 / 缩放 / 设计尺寸
Tools/DSH Mobile/Build APK                     # → UnityMCPProject\Builds\Android\RoomPet.apk

# 跑测试（命令行风格，实际用 MCP 的 run_tests）
EditMode，期望 248/248

# 存档
%USERPROFILE%\AppData\LocalLow\DefaultCompany\UnityMCPProject\
```
