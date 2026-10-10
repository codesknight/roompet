# 第三方资源与许可

## 本项目代码的许可

**待定** —— 还没有为项目代码选定开源许可（MIT / Apache-2.0 / 私有）。
在选定之前，默认保留所有权利；如果你想公开分发，建议加一个 `LICENSE` 文件。

## 素材

### Kenney（CC0 1.0）

跑酷场景的装饰与部分道具素材来自 [Kenney](https://kenney.nl/) 的免费素材包，
许可是 **CC0 1.0（公共领域贡献）**，可商用、可修改、无需署名（此处署名是出于礼貌）。

| 用途 | 位置 |
|---|---|
| 自然装饰（树/灌木/花/蘑菇等 40 个 FBX） | `UnityMCPProject/Assets/Resources/Runner/Nature/` |
| 道具物件（8 个 FBX） | `UnityMCPProject/Assets/Resources/Runner/Items/` |
| 家具（Furniture Kit，140 个 FBX，露台沙发/茶几/盆栽/落地灯等） | `UnityMCPProject/Assets/Resources/Kenney/Furniture/` |
| 原始下载留档 | `.assets/kenney/` |

CC0 全文：<https://creativecommons.org/publicdomain/zero/1.0/>

### 背景音乐（OpenGameArt.org，CC0 1.0）

小游戏、场景与开始界面的背景音乐来自 [OpenGameArt.org](https://opengameart.org/) 上的
**11 首 CC0 1.0** 曲目（公共领域贡献），可商用、可修改、无需署名（此处署名是出于礼貌）。

| 用途 | 曲目 | 作者 |
|---|---|---|
| 开始界面 | A New Town | cynicmusic |
| 宠物房间 / 小屋 | Napping on a Cloud | congusbongus |
| 花园 | Flowerbed Fields | Zane Little Music |
| 夜晚露台 | Somewhere in the Elevator | You're Perfect Studio |
| 森林奔跑 | Free Run | TAD |
| 小鸟飞行 | Funky Disco Beats | Fupi |
| 跳一跳 | Party Sector | Joth |
| 接果子 | Raspberry Jam | congusbongus |
| 切水果 | The Rush | tebruno99 |
| 弹弓小鸟 | Toy Soldiers | Zane Little Music |
| 拼图 / 记忆配对 | Contemplation | Joth |

音频文件在 `UnityMCPProject/Assets/Resources/Music/`，逐首的出处 URL、许可证原文与
校验过程见 [docs/MUSIC_SOURCES.md](docs/MUSIC_SOURCES.md)。

### 无外部依赖的部分

以下内容**全部由代码生成，不含任何第三方素材**，因此没有许可问题：

- 虚拟宠物的房间、家具、可交互物件（`Pet/Scripts/World/PetRoom.cs`）——包括花园与露台的
  栅栏、栏杆、花、串灯、藤架、天际线，都是程序化生成（`Pet/Scripts/World/RoomDecor.cs` /
  `RoomTextures.cs`）
- 所有角色外观：宠物四个物种、玩家角色（`PetAvatar.cs` / `AnimalAvatar.cs` / `PlayerAvatar.cs`）
- 全部音效与"说话"音（`Audio/ProceduralAudio.cs` / `Audio/PetAudioDirector.cs`，波形合成）
- 界面（IMGUI，`UI/PetHud.cs`，不需要字体资源）

## 开发工具

### MCP for Unity

[github.com/CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)（MIT 许可），
用于让 AI 助手直接操作 Unity 编辑器。**仓库不包含它的代码**：`unity-mcp/` 检出、
`.venv/`、`.tools/` 都被 `.gitignore` 忽略，按 [MCP_SETUP.md](MCP_SETUP.md) 在本地重建。

### DeepSeek / OpenAI 兼容接口

宠物"大脑"通过 HTTP 调用大模型（默认指向 DeepSeek 官方接口，任何 OpenAI 兼容端点都可填）。
**仓库里不含任何 API key**；key 由使用者自己配置（界面里填，或环境变量
`DEEPSEEK_API_KEY` / `OPENAI_API_KEY`），存在 Unity 的 PlayerPrefs 里，不会进版本库。

## 字体

界面用的是 Unity 内置的 IMGUI 皮肤字体，没有额外引入字体文件。
