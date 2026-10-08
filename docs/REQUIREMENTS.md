# 需求与实现对照文档

> 用途：**复盘与复现**。每条需求记录三件事——你当时怎么说的、我怎么实现的、用什么证明它真的能用。
>
> 相关文档：`docs/DEVLOG.md`（交接与坑清单）、`docs/MCP_SETUP.md`（工具链）、`PET_DESIGN.md`（宠物设计细节）。
>
> 命令里出现的 `D:\projects\dsh-unity` 是本机示例路径，换机器时整体替换成你自己的即可。

**状态图例**：✅ 已实现且已验证 ｜ ⚠️ 已实现但验证方式有限 ｜ ⏸ 明确推迟 ｜ ❌ 不做（附原因）

---

## 阶段 0：工具链（一切的前提）

| 编号 | 需求 | 实现 | 验收证据 |
|---|---|---|---|
| R0.1 | 部署 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)，接到 DSH 上 | 本地 checkout + 独立 Python venv 服务端 + 工程内 `file:` 包引用 + DSH patch 层 insert | 包 `10.3.1-beta.6` 编译通过；stdio 桥接就绪（`unity_port: 6400`，`reason: ready`）；注册 50 个工具 / 24 个资源；实际用 MCP 建了一个 `DshBridgeCube` 并读回层级 ✅ **详见 `docs/MCP_SETUP.md`** |

---

## 阶段 1：视觉原型（先把"好看"立住）

| 编号 | 需求 | 实现 | 验收证据 |
|---|---|---|---|
| R1.1 | 创建一个 cube | 场景 `Main`，`Cube` 对象 | 渲染图 `docs/evidence/fur_final.png` ✅ |
| R1.2 | 给它一个毛茸茸的材质 | `DSH/Shell Fur`：几何着色器分层壳（31 层上限） | 渲染图 + `scripts/probe_fur.js` 量化毛发覆盖 ✅ |
| R1.3 | 加动态的七彩炫光 | `DSH/Rainbow Fur`：在毛发上叠加随时间流动的色相 | `scripts/probe_rainbow.js` 测色相分布；受控实验（改 `_HueShift` → 像素变化 28.9~29.9%，对照组 0）✅ |
| R1.4 | 梦幻色彩天空盒，和 cube 配色搭配 | `DSH/Dreamy Skybox`：三段渐变 + 极光 + 星空，色相算法与流光同源 | `docs/evidence/sky_cube.png` / `sky_wide.png` + `probe_sky.js` / `probe_contrast.js` ✅ |
| R1.5 | 场景始终保存 | 约定：每次改完场景都保存 | 全程遵守 ✅ |

---

## 阶段 2：跑酷玩法

| 编号 | 需求 | 实现 | 验收证据 |
|---|---|---|---|
| R2.1 | 键盘控制左右移动（跳跃） | `PlayerController`：三车道横向移动 + 跳跃；输入在 IMGUI 有焦点时自动屏蔽 | 运行实测 + 单测（车道夹取、跳跃落地）✅ |
| R2.2 | 循环赛道 + 随机障碍（Rolling Sky / Subway Surfers 风格） | `TrackPlanner` 规划赛段 + `TrackManager` 生成/回收 | 单测保证"同一排不会填满所有车道""行距 ≥ 0.6s 行程""拾取物不生成在障碍里" ✅ |
| R2.3 | 距离越远难度越高、分越高 | `RunnerData` 难度曲线 + `ScoreKeeper` | 单测覆盖难度分段与计分 ✅ |
| R2.4 | 关卡模式 | `LevelLibrary` + 进度存档 | 单测 ✅ |
| R2.5 | 道具系统 | `PowerUpSystem`（磁铁/护盾/加速等） | 单测 + 运行实测 ✅ |
| R2.6 | 修 bug：移动时 cube 一直翻滚，很影响体验 | 把"翻滚"从位移派生改为可控参数（并在**场景实例**上显式置 0，见 DEVLOG 坑 4） | 运行实测 ✅ |
| R2.7 | 用可下载的素材把场景改成动物主题跑酷 | 下载 CC0 的 Kenney 自然素材（41 个 FBX 场景道具 + 8 个道具物件），`PropLibrary` 负责实例化与缩放修正 | 渲染图 `docs/evidence/forest.png` / `animal_avatar.png` ✅ |

---

## 阶段 3：虚拟宠物（当前主线）

| 编号 | 需求 | 实现 | 验收证据 |
|---|---|---|---|
| R3.1 | 新建一个场景开始搭框架和 demo | `Assets/Pet/Scenes/PetRoom.unity` + `Assets/Pet/Scripts/` 全套；房间程序化搭建 | 渲染图 `docs/evidence/pet_room.png`；`Tools/DSH Pet/Validate Wiring` 全绿 ✅ |
| R3.2 | 对话陪伴 + 环境互动 | 聊天（LLM 驱动）+ 可点击物件（饭碗/水碗/床/球/梳子/门） | 聊天记录与日记实测；宠物会走到物件旁并使用 ✅ |
| R3.3 | 动物主题、可换动物 | 4 个物种（狐/猫/兔/熊），各有外观、人格与说话风格；右上角换动物面板 | 换物种实测（外观与提示词人格一起变）；`Validate Wiring` 列出 4 个物种 ✅ |
| R3.4 | 接 DeepSeek API 模拟宠物行为 | `PetBrainConfig` + `OpenAiClient` + `DeepSeekPetBrain`（任意 OpenAI 兼容端点）+ `LocalPetBrain` 离线兜底；回复格式带 `<action>`/`<remember>` 契约 | 内网网关实测跑通；离线兜底在断网/超时/401 时都会回话并显示错误 ✅ |
| R3.5 | 先只做文字交互 | 未接 TTS；宠物"说话"只有按心情变调的程序化音 | 遵守 ✅ |
| R3.6 | 本地持久记忆，索引像日程表，UI 做成日历（"智能记事本"） | `PetJournal`：按天/月索引的 JSON 存档，支持"钉住"；HUD 有月历视图 + 当天条目列表 + 分色标签 | 日历截图 `docs/evidence/calendar_fixed.png`；单测：钉住条目不会被裁剪、跨月/跨天查询正确 ✅ |
| R3.7 | 互动分主动/被动，"不那么死板" | `PetBehaviorLibrary` 行为表（主动 8 条 + 被动 9 条）+ `PetBehaviorScheduler` 两条独立计时器 | 单测：被动小动作不会连续重复；长时间空闲确实会触发；主动行按需求/时间窗打分 ✅ |
| R3.8 | 参考《会说话的汤姆猫》但更聪明；框架要能长成《虚拟小镇》；先把眼前的功能做好 | 行为表 + `InteractableKind` + `MiniGameLibrary` 作为扩展点；每轮优先修眼前的 bug | 设计约束（非功能需求）✅ |

---

## 阶段 4：修 bug + 补功能

| 编号 | 需求 | 实现 | 验收证据 |
|---|---|---|---|
| R4.1 | bug：按 Play 显示"大脑离线"，怎么配 LLM？ | 根因是**保存过的空 Key 盖住了环境变量** → 改成"只在非空时覆盖" + `FromEnvironment()` + 「读环境变量」「测试连接」按钮 + 可执行的 `StatusDetail()` 文案 | 实测变为"在线 · …" ✅ |
| R4.2 | 实现玩家角色进屋自由移动 | `PlayerRoomController`（相机相对移动）+ `PlayerAvatar` + `RoomCameraRig` | 运行实测；相机取景有单测 ✅ |
| R4.3 | 宠物主动发起更多对话 | `TickNudges`：按"最紧迫的需求/玩家沉默时长"生成**开口理由**再交给模型，而不是空喊"说点什么" | 运行实测（宠物会自己开口，并在状态面板显示"主动开口：<理由>"）✅ |
| R4.4 | 小游戏：一扇门问要不要去玩跑酷，为后续内容（如小鸟飞行）打基础 | `MiniGameLibrary`（一行定义一个活动）+ 门浮层 + 往返存档（`FlushToDisk` + `dshpet.away`） | 实测往返：PetRoom → 跑酷 → PetRoom，回来时需求/记忆/日记都在 ✅ |

---

## 阶段 5：本轮清单（UI / 声音 / 玩法）

| 编号 | 需求 | 实现 | 验收证据 |
|---|---|---|---|
| R5.1 | 左上角 UI 显示不完整，可以加个小滑轮 | 根因：状态面板与聊天面板**高度各自独立算**，窗口矮于约 630px 就重叠，聊天面板（后绘制）盖住按钮并吃掉点击。改为统一 `ComputeLayout()`：详情区可滚动、按钮钉页脚、两面板永不重叠 | 7 种视口尺寸的布局单测；实机 `overlap=no`（1100×619 下从 −19px 变成 +21px 间隙）；截图 `docs/evidence/hud_fixed.png` ✅ |
| R5.2 | 大脑离线但没有可以点的设置 UI | 同 R5.1 的遮挡问题 + 环境变量读取时机。现在按钮永远可点，离线时额外高亮「⚙ 配置大脑连接（当前离线）」 | 实测在线；面板截图 `docs/evidence/settings_final.png` ✅ |
| R5.3 | 日历也没有显示 | 根因：`GUI.skin.box` **半透明**，浮层下面的房间/宠物/提示全透过来，看着像没打开；且固定 720×560 居中在矮窗口会渲染到屏幕外。改为遮罩 + 不透明填充 + `OverlayRect()` 夹进视口 + 格子高度自适应 | 截图 `docs/evidence/calendar_fixed.png`（不透明、完整可读）✅ |
| R5.4 | 先把声音补上 | `ProceduralAudio` 波形合成 + `PetAudioDirector`：16 个音效、按心情变调的"说话"、环境音与随机鸟叫、静音与音量（存档） | 实测 16 个音效波形峰值 0.8（非静音）、4 个音源在播、1 个监听器 ✅ |
| R5.5 | 实现扔捡球 | `PetBall`（手写积分物理 + 状态机）+ `PetController.Mode.Fetch`（跑**预测落点** → 叼起 → 跑回玩家 → 放下）+ 行为表 `fetch` 行 + 蓄力/准星 UI | 端到端实测两次：球落下 → 宠物叼回放在玩家脚下 1.15m → 写入记忆与日记；且不会重复叼球 ✅ |
| R5.6 | bug：API 设置框不能输入文字 | 根因：**上一轮我自己加的"铺满面板的隐形 Button"抢走了面板内所有控件的点击**。改为浮层打开时对下层面板 `GUI.enabled = false`；顺带修掉"面板太矮导致操作按钮被裁出可点区域"；打开面板自动聚焦输入框 | 焦点实测（`GetNameOfFocusedControl() == "PetBaseUrl"`）；几何实测（输入框与按钮都在面板内）；截图 `docs/evidence/settings_final.png` ⚠️ **点击路径是靠"没有任何控件压在输入框上"保证的，没做真实鼠标点击验证**（合成输入在本机到不了游戏） |
| R5.7 | 新功能：视角可切换（全景 / 自由 / 跟随主角） | `RoomCameraRig` 三种模式 + 右上角切换面板（选择存 PlayerPrefs）；自由模式=右键转视角/滚轮缩放/中键平移 | 实测投影：全景下整个可行走地面落在 77~414px（HUD 区域 0~461）；跟随主角下玩家始终在区域内；单测覆盖 ✅ |
| R5.8 | bug：提示词面板 UI 出来了既不能修改也不能返回 | 根因两条：`ExpandHeight(true)` 把「关闭」按钮挤出可点区域（**同一个坑第二次**）；TextArea 返回值被丢弃所以打字立刻还原。改为内容滚动 + 按钮钉页脚 + Esc 关闭；并把"改不了"升级成可编辑的**「额外要求」**（保存后追加进系统提示词），另加「复制全部提示词」 | 截图 `docs/evidence/prompt_panel.png`（滚动条、可编辑框、三个按钮完整）；单测：额外要求进提示词且格式契约仍在；配置存读往返 ✅ |
| R5.9 | 新功能：左上角面板给小动物起名字，记录保存到系统提示词 | 名字行 = 输入框 + 「改名」（回车也提交）；名字走三条路进提示词：人格行 `你是一只名叫「X」的…`、长期记忆事实 `我的名字是X…`、日记钉住里程碑；按不可信输入清洗（换行→空格、长度 12、空名回退） | 实测：`RenamePet("  豆豆 \n")` → `豆豆`；提示词含 `「豆豆」` 且含长期事实；写入 PlayerPrefs；日记多一条「有了名字：豆豆」；截图 `docs/evidence/rename.png`（聊天里已变成「豆豆：…」）✅ |

---

## 阶段 6：安卓手机端（分支 `feature/android-mobile`）

| 编号 | 需求 | 实现 | 验收证据 |
|---|---|---|---|
| R6.1 | 新建分支做一版安卓手机适配端 | 分支 `feature/android-mobile`；新增独立程序集 `Assets/Mobile/`（`DshMobile`），被宠物与跑酷两个程序集引用，两者仍互不引用 | 分支与 asmdef 图；`DshMobile` 单测 23 条 ✅ |
| R6.2 | 所有 UI 设计都要手机端友好 | ① HUD 改为"设计像素 + `GUI.matrix` 整体缩放"（`MobileUi.UiScale` 按屏高与 DPI 计算，夹在 0.9~1.8），字号、间距、命中区一起放大；② 按 `Screen.safeArea` 内缩，避开刘海与圆角；③ 触摸目标不小于 44 设计像素；④ 宠物端聊天默认收成 54px 一条，状态面板默认只显示要点（可切「详情」），把底部三分之一让给拇指；⑤ 跑酷端提示行、道具条、「返回宠物小屋」全部让开按钮行 | 5 种手机设计尺寸下的布局单测（控件可点、不出屏、互不重叠、不压聊天条）；截图 `docs/evidence/mobile_pet_hud.png` / `mobile_runner_hud.png` ✅ |
| R6.3 | 小游戏（跑酷）也要手机端友好 | **滑动为主**：左右滑换道、上滑/点击跳（按住更高）、下滑滑铲；**按钮为辅**：◀ ▶ 跳 滑 + 暂停。两者可同时用，因为输入走自己的多点触控层而不是 IMGUI | 手势识别单测（阈值按屏短边、快甩即使距离短也算、慢拖不算）；截图 `docs/evidence/mobile_runner_hud.png` ✅ |
| R6.4 | 多指同时操作 | IMGUI 只认一个指针，所以自建 `MobileTouch`：直接读 `Input.touches`，自己做矩形命中与手指归属（`btn:<id>` / `stick` / `gesture` / `ui`）；`MobileWidgets` 只负责画，命中区单独注册 | 归属逻辑单测；编辑器里用鼠标当手指的合成通道（`ForceTouchControls`）实测 ✅ |
| R6.5 | 打包出 APK | `Tools/DSH Mobile/Build APK`：包名 `com.codesknight.roompet`、强制横屏、IL2CPP + ARM64、minSdk 24、`INTERNET` 权限、`renderOutsideSafeArea=false`；产出 `UnityMCPProject/Builds/Android/RoomPet.apk` | APK 实测 16.0 MB，内含 `lib/arm64-v8a/{libil2cpp,libunity}.so`、`global-metadata.dat`；合并后的清单含 `INTERNET`、`userLandscape`、`usesCleartextTraffic`；`resources.arsc` 里 `app_name` = `RoomPet` ✅ |
| R6.6 | 桌面端不能被改坏 | 所有触控分支都在 `MobileUi.UseTouchControls` 之下；键鼠代码一条没删；`ForceTouchControls` 只用于编辑器预览与测试 | 桌面路径实测（编辑器未开预览时行为与上一轮一致）；115/115 单测含原有宠物/跑酷用例 ✅ |
| R6.7 | 完善安卓版：**竖屏** | ① HUD 缩放改按屏幕**短边**（`MobileUi.ScaleFor`）——按高度算时竖屏 1080x2400 会被夹到 1.8、只剩 600px 宽的设计空间；② 玩家设置放开竖屏（清单里是 `fullUser`）；③ 两套相机加"宽度也要装得下"的闭环（`MobileUi.RequiredDistanceForWidth` + `RoomCameraRig.FitSubjectsToWidth`）；④ 跑酷提示行不再用固定 46% 宽度（竖屏会切尾）；⑤ 全景相机的 `RoomLimit` 会随宽度适配放宽（否则房间角差 16px 出屏） | 4 档竖屏设计尺寸的布局单测（按钮够大 / 在屏内 / 不重叠 / 不高于屏高 70% / 摇杆归左手 / 两侧面板不撞）；宽度适配单测（16:9 必须**不动**、越窄退得越远、有上限）；竖屏实测：房间四角落在 x=71..1009（视口 1080），跑酷三条车道全在画面内；截图 `docs/evidence/mobile_{pet,runner}_portrait.png` ✅ |
| R6.8 | 完善安卓版：**振动** | `MobileHaptics`：安卓 `Vibrator` + `VibrationEffect`（API 26 前后分支），无插件、只走 `AndroidJavaObject`；轻/中/重三档；`HapticGate` 限流 45ms + 开关；开关进「设置」面板并存 PlayerPrefs；接入换道/跳跃/滑铲、拿球/扔球、宠物叼球回来、护盾挡下撞击、撞车、通关；清单补 `VIBRATE` 权限 | 单测：限流会吞掉 10ms 内的第二次、关掉后一律不响、三档时长递增且在 10~120ms 内、**编辑器里 `Supported == false` 且一枚脉冲都不会发出去**；合并后的清单含 `VIBRATE` ✅ ⚠️ **手感只能真机验，本轮没上手感** |
| R6.9 | 真机反馈：**弹窗不在中央、按钮被遮挡** | 根因：浮层拿 `Screen.width/height`（真机像素）居中，却画在 `GUI.matrix` 的设计像素空间里，位置被放大了一个缩放系数（竖屏 1.63 倍），整体偏右下、底部按钮被推出屏幕。改为全部走设计像素：新增 `PetHud.DesignWidth/DesignHeight`、`ScreenToDesign()`、`WorldLabelRect()`，浮层/遮罩/准星/头顶心情标签/提示行一并修掉；`OverlayRect` 的手机边距也从 16 提到 24 | 3 条新单测：6 档手机设计尺寸 × 4 种浮层请求，断言**中心 = 视口中心**（±0.5px）、四边都不出屏、面板高度容得下页脚、边距被正确夹紧；真机尺寸（1080x2400 @420dpi → 设计 665x1477）截图：设置面板与日历都居中且四个页脚按钮全可见（`docs/evidence/mobile_modal_settings.png`、`mobile_modal_journal.png`）✅ |
| R6.10 | 要求：**编译时设置相同的预览大小** | 预览只对了一半：像素尺寸对了，但编辑器 96dpi、手机 420dpi，而缩放含 dpi 修正 → 设计空间差了 20%（编辑器 800 宽 vs 真机 665 宽），"在编辑器里验证过的布局"并不是真机跑的那套。新增 `MobileUi.ReferenceDpi` 覆盖值，预览菜单选尺寸时一起模拟密度并记进 `EditorPrefs`（`[InitializeOnLoadMethod]` 重放，因为进 Play 会重载脚本域把静态值清掉），另有 `Use Platform DPI` 还原 | `Report Current Viewport` 打印 `dpi=420 (simulated) scale=1.63 design=665x1477`，与真机 1080x2400/420dpi 的计算完全一致；停掉模拟则回到 96dpi/1.35/800x1778 ⚠️ **420dpi 是从常见机型取的估值，不是从用户那台机器读出来的** |

---

## 非功能需求 / 设计约束

| 约束 | 落地方式 |
|---|---|
| 不需要国外信用卡也能跑 | 音效程序化合成、素材用 CC0、大脑支持任意 OpenAI 兼容端点 + 离线兜底。**明确不用 fal**（需国外卡） |
| 无外部资产依赖即可复现 | 房间/角色/毛发/音效全部代码生成；只有跑酷的装饰用了 CC0 素材 |
| 每个物种"像不同的动物" | 外观 + 人格 + 说话风格都进提示词，不只是换个颜色 |
| 交互不能"死板" | 行为表分主动/被动；宠物会自己开口并带理由；反应有随机延迟（扔球后 0.25~0.95s，心情差更慢） |
| 出错不能让玩家对着空白 | 网络失败自动落离线大脑，并把错误显示在聊天区 |

---

## 被测试钉住的不变量（回归防线）

**跑酷**
- 同一排障碍不会填满所有车道（否则必死）
- 障碍行距 ≥ 0.6 秒行程
- 拾取物不会生成在障碍内部

**宠物逻辑**
- 钉住的日记条目在整理时不会被裁掉
- 调度器在宠物忙碌时不消耗计时
- 被动小动作不会连续重复两次
- `fetch` 只在"球被扔出去且没人捡"时触发；压过无聊/梳毛/求关注，但让位给濒死级的饿和困
- 名字清洗：换行/控制字符压成空格、长度截断、空名回退
- 大脑配置每个字段都能存读往返

**界面（数值化，不靠肉眼）**
- 状态面板与聊天面板在 7 种视口尺寸下永不重叠
- 浮层始终完整落在视口内
- 日历：6 行月历 + 当天条目列表都能放进被夹紧的面板里
- 相机：全景装得下整个可行走房间；跟随主角时玩家在全屋任意位置都在 HUD 面板之上；HUD 高度 35%~80% 都成立

**手机端**
- 5 种手机设计尺寸下：动作/扔球/聊天按钮都够大（≥ 44 设计像素）、都在屏内、互不重叠、都不压住收起的聊天条
- 摇杆区始终归左手（中心在屏幕左半边）
- 手势阈值跟着屏幕短边走（不读全局 `Screen`，可注入 `ReferenceSize` 才能测）
- **设计像素 ↔ 屏幕像素只换算一次**：控件在带 `GUI.matrix` 的绘制里用设计矩形，注册命中区时才乘缩放；摇杆从屏幕坐标回到设计坐标后必须落在按住它的那根手指下
- 缩放取屏幕**短边**：同一台手机转屏后缩放不变，且竖屏仍留得下 620px 以上的设计宽度
- 相机宽度适配：16:9 及更宽**必须一点不动**；越窄退得越远且单调；有上限（不能把房间缩成邮票）
- 竖屏布局：按钮在屏高 70% 以下、摇杆区在屏高 50% 以下（够得着），状态面板不越过屏高 75%
- 振动：编辑器/桌面端 `Supported == false` 且绝不发脉冲；限流吞掉 45ms 内的重复；关掉就全静音

---

## 复现步骤（从零到这个状态）

```powershell
# 1) 工具链（详见 docs/MCP_SETUP.md）
git -c http.sslBackend=openssl clone https://github.com/CoplayDev/unity-mcp D:\projects\dsh-unity\unity-mcp
& D:\projects\dsh-unity\scripts\install-server.ps1

# 2) Unity 侧：工程 Packages/manifest.json 加一行
#    "com.coplaydev.unity-mcp": "file:D:/projects/dsh-unity/unity-mcp/MCPForUnity"
#    并把编辑器传输方式切成 stdio（见 MCP_SETUP 第 3.4 节）

# 3) DSH 侧：profile 的 patch 层 insert 一段 mcp-client 配置（见 MCP_SETUP 第 3.5 节）

# 4) Unity 里重建场景（房间是代码生成的，场景只是它的快照）
Tools/DSH Pet/Build Pet Scene
Tools/DSH Pet/Add Scenes To Build Settings
Tools/DSH Runner/Build Scene

# 5) 自检
Tools/DSH Pet/Validate Wiring      # 应全绿：房间/角色/相机/小游戏/行为表/球/HUD 几何/大脑
Tools/DSH Pet/Fix Script Encodings # 若中文显示成乱码，先跑这个
EditMode 测试                      # 应 107/107 通过

# 6) 安卓端（可选；Unity Hub 里要先装 Android Build Support，且装完重启编辑器）
Tools/DSH Mobile/Configure Android Player Settings
Tools/DSH Mobile/Build APK         # → UnityMCPProject/Builds/Android/RoomPet.apk
```

---

## 明确推迟 / 不做

| 项 | 状态 | 原因 |
|---|---|---|
| 语音 TTS | ⏸ | 建议 Edge TTS 或 Windows SAPI（都不需要国外卡），本轮未接 |
| 云存档 | ⏸ | 日记与记忆目前是本地文件 + PlayerPrefs |
| 扔球的地面落点指示 | ⏸ | 已有蓄力条与准星，缺"球会落在哪"的地面标记 |
| 宠物对玩家的更多反应 | ⏸ | 目前只有"靠近时看你""想你了会走过来" |
| 更多小游戏（如小鸟飞行） | ⏸ | 框架已就绪（`MiniGameLibrary` 加一行即一个活动） |
| fal 生成图像/音频 | ❌ | 需要国外信用卡；已用程序化合成与 CC0 素材替代 |
| 自由视角的"保证看得见房间" | ⏸ | 自由模式故意完全交给玩家，代价是近处可能被聊天面板挡住 |
