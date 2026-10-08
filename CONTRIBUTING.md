# 参与开发

## 环境

- Unity **2022.3.62f3c1**（其它 2022.3.x 一般可用），Built-in 渲染管线。
- 打开仓库里的 `UnityMCPProject/`。
- 可选：按 [docs/MCP_SETUP.md](docs/MCP_SETUP.md) 接上 MCP for Unity，让 AI 助手直接操作编辑器。

## 改代码前请先读

1. **[docs/DEVLOG.md](docs/DEVLOG.md)** —— 代码地图 + **22 条真实踩过的坑**。
   里面每一条都是花了时间才找到的，尤其是 IMGUI 布局、Unity 序列化和编码这三类。
2. **[docs/REQUIREMENTS.md](docs/REQUIREMENTS.md)** —— 每条需求对应哪个实现、哪条测试在守它。

## 提交前必须做的三件事

1. **跑测试**：Unity 的 Test Runner → EditMode → Run All，应 **84/84 通过**。
2. **看控制台**：`refresh_unity` 编译后，Console 里不能有 error（warning 也尽量清）。
3. **能看画面就看画面**：界面/相机/材质类改动**一定要截图确认**。
   这个项目里至少有 5 个 bug 是"数值上没错、看图才发现"的（面板重叠、浮层半透明、
   玩家被面板挡住、道具飘在半空、中文乱码）。

## 编码规矩（会咬人的那条）

含中文的脚本文件**必须带 UTF-8 BOM**。Windows 上"按系统代码页读文件"的程序有两个：

| 文件 | 读它的程序 | 不带 BOM 的后果 |
|---|---|---|
| `.cs` | Unity 的编译器 | 该文件里所有中文字面量变乱码（实测「全景」→ `锟斤拷`） |
| `.ps1` | Windows PowerShell **5.1** | 脚本直接语法报错（中文变乱码后把引号吃掉） |

规矩：

- 用编辑工具改过这类文件后，`.cs` 跑一次 **`Tools/DSH Pet/Fix Script Encodings`** 补 BOM；
  `.ps1` 用下面的片段补（`scripts/setup-github.ps1` 本身就是这么存的）：

  ```powershell
  $p = 'scripts\your-script.ps1'
  $text = [System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)
  [System.IO.File]::WriteAllText($p, $text, (New-Object System.Text.UTF8Encoding($true)))
  ```

- **不要用 PowerShell 的 `Get-Content` / `Set-Content` 往返编辑任何含中文的文件**：
  会把多字节字符换成 `U+FFFD`，BOM 也救不回来，只能重新打字。用文件工具，或
  `[System.IO.File]::ReadAllText/WriteAllText` 显式指定 UTF-8。
- 向 GitHub API 发中文 JSON 时，`Invoke-RestMethod` 的 Body 必须传 **UTF-8 字节**，
  否则 5.1 会按 Latin-1 发出去、服务端存成乱码（`setup-github.ps1` 里有正确写法）。

## 代码风格

- 注释写**为什么**，不写**是什么**。特别是"这里为什么不那样写"——踩过的坑值得留一句。
- 纯逻辑要能被单元测试覆盖：把规则写成纯函数（`PetBehavior.Score`、`PetUtil`、
  `PetHud.ComputeLayout`、`PetRoom.ShouldShowFace` 都是这个路子）。
- 界面全部用 IMGUI（`PetHud`），并且**布局常量集中在 `ComputeLayout()`**，
  因为"面板重叠导致按钮点不到"这个问题已经犯过两次。

## 文档往哪写

| 改了什么 | 更新哪个文档 |
|---|---|
| 新增/修改需求 | `docs/REQUIREMENTS.md` |
| 新功能或修了 bug | `CHANGELOG.md` |
| 宠物设计取舍 | `PET_DESIGN.md` |
| 又踩了一个坑 | `docs/DEVLOG.md` 的坑清单 |
| 着色器/材质参数 | `docs/VISUALS.md` |
| 待办变动 | `docs/ROADMAP.md` |
