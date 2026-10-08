# RoomPet ·「虚拟宠物空间」

> 一个 Unity 小游戏项目：**和一只会记事、会主动找你说话的宠物住在同一个房间里**。
> 附带一个同世界观的动物主题**跑酷**小游戏，从房间的门出去就能玩。

![宠物房间](docs/evidence/rename.png)

| 宠物房间（PetRoom） | 跑酷（Main） |
|---|---|
| ![房间](docs/evidence/hud_fixed.png) | ![跑酷](docs/evidence/forest.png) |

---

## 这是什么

两个场景，一套世界：

- **`Assets/Pet/Scenes/PetRoom.unity`** —— 虚拟宠物。文字陪伴 + 环境互动：
  宠物有自己的需求（饱食/精力/开心/清洁）、情绪、**日程表式的持久记忆**（UI 做成月历「记事本」）、
  **主动与被动两层行为表**（它会自己饿了去吃饭、自己凑过来找你），以及一个接大模型的可换"大脑"。
  房间里的饭碗、水碗、小床、小球、梳子都能点，玩家角色可以在屋里自由走动、把球扔出去让它捡。
- **`Assets/Scenes/Main.unity`** —— 动物主题跑酷。键盘左右换道 + 跳跃，无限循环赛道随机生成障碍，
  距离越远难度越高分越高，有关卡模式与道具系统。

两者通过房间里的**门**连接：门 → 选择去玩 → 跑酷 → 回来时宠物还记得你。

### 特点

- **全部程序化生成**：房间、家具、角色、毛发、音效都是代码生成的，
  除少量 CC0 素材外不依赖任何外部资源（见 [THIRD_PARTY.md](THIRD_PARTY.md)）。
- **声音零资源**：16 个音效 + 按心情变调的"说话"音全部由波形合成，不需要下载也不需要 API key。
- **大脑可换**：默认指向 DeepSeek 官方接口，任何 OpenAI 兼容端点都能填；
  网络不可用时自动落到离线大脑并**把错误显示出来**，不会让你对着空白发呆。
- **84 条单元测试**覆盖逻辑（行为打分、日记裁剪、赛道可通过性、界面几何、相机取景）。

---

## 快速开始

1. **装 Unity**：`2022.3.62f3c1`（其它 2022.3.x 一般也行）。Built-in 渲染管线，不需要 URP/HDRP。
2. **克隆并打开工程**：

   ```powershell
   git clone https://github.com/codesknight/roompet
   ```

   用 Unity Hub 打开仓库里的 `UnityMCPProject/` 目录。首次打开会导入资源，几分钟。

3. **跑起来**：打开 `Assets/Pet/Scenes/PetRoom.unity`（或 `Assets/Scenes/Main.unity`），按 Play。

> 仓库里的 `UnityMCPProject/Packages/manifest.json` **不包含** AI 开发工具（MCP for Unity）的依赖：
> 那是本机的开发环境，不是游戏的一部分，所以克隆下来直接打开就是一个干净、零报错的工程。
> 想接上这套工具链（让 AI 直接操作编辑器），按 [docs/MCP_SETUP.md](docs/MCP_SETUP.md) 做，
> 其中第 3.3 节就是"加一行依赖"。

---

## 操作

| 场景 | 按键 | 作用 |
|---|---|---|
| 宠物房间 | `W A S D` | 走动（相机相对） |
| 宠物房间 | `E` | 和身边的东西互动（喂饭/喝水/梳毛/睡觉/拿球/开门） |
| 宠物房间 | 鼠标左键（拿着球时） | 按住蓄力，松开扔出去 → 宠物会跑去捡回来 |
| 宠物房间 | 右键拖动 / 滚轮 / 中键 | 「自由」视角下转视角 / 缩放 / 平移 |
| 宠物房间 | `Esc` | 关掉当前面板 |
| 跑酷 | `A` `D` / `←` `→` | 左右换道 |
| 跑酷 | `空格` / `W` / `↑` | 跳跃 |

界面都在屏幕上：左上角是宠物状态与**名字输入框**，右上角是**换动物**和**视角切换**，
底部是聊天框（打字和它说话）。左下角四个按钮：**记事本**（日历形式的记忆）、
**设置**（大模型接口）、**提示词**（看/追加它实际收到的提示词）、**重置**。

---

## 文档导航

| 文档 | 内容 |
|---|---|
| [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) | **需求与实现对照**：每条需求 → 怎么实现的 → 验收证据；含被测试钉住的 15 条不变量 |
| [docs/DEVLOG.md](docs/DEVLOG.md) | **开发日志与交接**：代码地图、环境事实、**22 条踩过的坑**、验证工作流 |
| [docs/MCP_SETUP.md](docs/MCP_SETUP.md) | **AI 开发工具链**：用 MCP for Unity 让 AI 直接操作编辑器，以及别的工程怎么复用 |
| [docs/ROADMAP.md](docs/ROADMAP.md) | 路线图与待办（也是开 issue 的来源） |
| [docs/VISUALS.md](docs/VISUALS.md) | 毛发/彩虹/天空盒等着色器与材质参数 |
| [PET_DESIGN.md](PET_DESIGN.md) | 虚拟宠物分轮设计记录（含设计理由与实测数据） |
| [GAMEPLAY.md](GAMEPLAY.md) | 跑酷玩法与关卡设计说明 |
| [CHANGELOG.md](CHANGELOG.md) | 变更记录 |

---

## 项目结构

```
roompet/
├─ UnityMCPProject/          Unity 工程（用 Hub 打开这个目录）
│  ├─ Assets/
│  │  ├─ Scripts/            跑酷：核心/玩家/赛道/道具/相机/界面
│  │  ├─ Pet/Scripts/        虚拟宠物：核心/大脑/记忆/行为/世界/界面/音频
│  │  ├─ Shaders/            ShellFur / RainbowFur / DreamySkybox / Neon
│  │  ├─ Scenes/Main.unity   跑酷场景
│  │  └─ Pet/Scenes/PetRoom.unity  宠物房间
│  ├─ Packages/              包依赖（含可选的 MCP 开发工具那一行）
│  └─ ProjectSettings/       工程设置
├─ docs/                     文档与证据截图（见上表）
├─ scripts/                  部署与诊断脚本（install-server / verify_mcp / 探针）
├─ .assets/                  CC0 素材的原始下载（留档用）
└─ README.md
```

---

## 项目管理

计划与待办都在 GitHub 上，用**里程碑 + 标签 + issue** 组织，源头是 [docs/ROADMAP.md](docs/ROADMAP.md)：

| 里程碑 | 内容 |
|---|---|
| **v0.2 — 让它更像活的** | 语音（TTS）、扔球落点指示、宠物对玩家的更多反应、自由视角约束 |
| **v0.3 — 内容扩展** | 更多小游戏/物种/房间互动，为《虚拟小镇》铺路 |
| **v1.0 — 可发布** | 云存档、CI、打包、选定许可 |

标签按**领域**分（宠物／记忆／大脑·提示词／界面／相机／声音／跑酷／玩法／工具链／文档），
外加 `技术债`、`待验证` 和优先级 `P0`–`P2`。

这套配置是**可复现**的：跑一次

```powershell
powershell -File scripts/setup-github.ps1
```

就会写好仓库简介与 topics、建好标签与里程碑、把 `docs/ROADMAP.md` 里的待办开成 issue，
并为 `v0.1.0` 建一个 release。脚本是幂等的，重复跑只补缺的（认证用的是 git 已有的
github.com 凭据，脚本里不存任何 token）。

## 测试与验证

单元测试在 Unity 里跑：**Window → General → Test Runner → EditMode → Run All**，应 **84/84 通过**。

工程里还带了两个自检菜单（比手写脚本快）：

- `Tools/DSH Pet/Validate Wiring` —— 打印房间/角色/相机/小游戏/行为表/球/界面几何/大脑状态的完整报告。
- `Tools/DSH Runner/Validate Wiring` —— 跑酷侧的同类报告。

以及两个维护用的菜单：

- `Tools/DSH Pet/Build Pet Scene` —— 房间是代码生成的，改了摆放逻辑后跑它重建场景。
- `Tools/DSH Pet/Fix Script Encodings` —— 补 UTF-8 BOM / 报告被损坏的中文（见 DEVLOG 坑 1）。

---

## 用 AI 驱动 Unity 开发（可选）

这个项目是用 [MCP for Unity](https://github.com/CoplayDev/unity-mcp) 配合 AI 助手开发的：
AI 可以直接在编辑器里建对象、改资源、跑编译、跑测试、截图看结果。
想在自己机器上复现这套工具链，看 **[docs/MCP_SETUP.md](docs/MCP_SETUP.md)**（含"新工程如何复用，不用重新部署"）。

仓库里**不包含**这份工具链本身（`unity-mcp/` 检出与 Python 环境都被 git 忽略），
所以克隆下来的是一个干净的游戏工程。

---

## 许可与第三方资源

见 [THIRD_PARTY.md](THIRD_PARTY.md)。简要说明：项目代码尚未选定开源许可（**待定**），
素材里有 CC0 的 Kenney 资源需要署名（已列出）。
