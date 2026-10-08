# MCP for Unity：部署与复用指南

> 目的：把 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) 接到 DeepSeek Harness（DSH）上，
> 让 AI 直接读写 Unity 编辑器。**本文的重点是"第二个工程怎么复用，不用重新部署"。**
>
> 目标读者：换一台机器重新部署的人、想把这个能力用在新 Unity 工程上的人、以及将来接手排查的人。
>
> **关于路径**：文中的 `D:\projects\dsh-unity` 是本机实际部署位置，作为**示例**保留 ——
> 换机器时把它整体替换成你自己的路径即可，其余步骤不变。

---

## 1. 架构：为什么"不用重新部署"

```
DSH (dsh-mcp-client 插件)
   │  stdio (JSON-RPC)                       ← DSH 自己拉起子进程，不用手动启动
   ▼
<workspace>\.venv\Scripts\mcp-for-unity.exe  ← Python 服务端（从本地 checkout 安装）
   │  TCP 127.0.0.1:6400 (stdio bridge)      ← 编辑器监听，服务端主动连
   ▼
Unity 编辑器 (com.coplaydev.unity-mcp 包)     ← 每个 Unity 工程各装一次（一行 manifest）
```

三层里只有**第三层是"每个工程一份"**，而且只是一行 manifest。
Python 服务端（第二层）和 DSH 挂载（第一层）是**机器级一次性配置**：

- 新工程 → 只需要加一行包引用 + 用同一个 Unity 版本打开。
- 不用重装 Python、不用改 DSH 配置、不用重启 DSH。

---

## 2. 本机现状（已部署，可直接复用）

| 项 | 值 |
|---|---|
| 部署位置 | `D:\projects\dsh-unity` |
| checkout | `unity-mcp\`（`main`，`v10.3.0-111-gef713afd`，commit `ef713afd`） |
| Python 服务端 | `.venv\Scripts\mcp-for-unity.exe`（从 `unity-mcp\Server` 安装，自包含） |
| Unity 工程（示例） | `UnityMCPProject\`，Unity **2022.3.62f3c1** |
| 包版本 | `com.coplaydev.unity-mcp` **10.3.1-beta.6** |
| 传输方式 | **stdio**，编辑器监听 **6400** |
| DSH 挂载 | `C:\Users\yanhong.liu\.dsh\profiles\web\cordis.patch.yml` |
| 状态文件 | `%USERPROFILE%\.unity-mcp\unity-mcp-status-<hash>.json` |
| 已装工具数 | 50 个 MCP 工具 / 24 个资源（模型侧命名为 `mcp__unity__<工具名>`） |

当前状态文件实测内容（可用于自检比对）：

```json
{"unity_port":6400,"reloading":false,"reason":"ready","seq":1,
 "project_path":"D:/projects/dsh-unity/UnityMCPProject/Assets",
 "project_name":"UnityMCPProject","unity_version":"2022.3.62f3c1",
 "project_scoped_tools":false}
```

---

## 3. 从零部署（换机器时照做）

### 3.1 拉包

```powershell
# schannel 在受限环境下会报 SEC_E_NO_CREDENTIALS，用 openssl 后端绕开
git -c http.sslBackend=openssl clone https://github.com/CoplayDev/unity-mcp D:\projects\dsh-unity\unity-mcp
```

### 3.2 装 Python 服务端（一次性，机器级）

用仓库里的脚本（它把 uv 的缓存写在工程内，避免污染用户目录）：

```powershell
& D:\projects\dsh-unity\scripts\install-server.ps1
```

脚本做三件事：`uv venv .venv` → `uv pip install <checkout>\Server` → 跑一次 `mcp-for-unity.exe --help` 验证。

> **为什么不用 `uvx --from <本地 Server>`**：那样每次启动都要现建 wheel，而受限沙箱里
> setuptools 无法写入自己刚创建的临时目录，会构建失败。这里改成一次性 venv，
> 运行期完全不依赖 uv（uv 只需在重装时存在）。

### 3.3 把包装进 Unity 工程

在工程的 `Packages/manifest.json` 的 `dependencies` 里加**一行**：

```json
"com.coplaydev.unity-mcp": "file:D:/projects/dsh-unity/unity-mcp/MCPForUnity",
```

用 `file:` 本地引用（而不是从 git URL 拉）有两个好处：改包源码立刻生效、离线可用。

> **注意**：这个仓库提交的 `manifest.json` **故意不含这一行** —— 它是开发环境，不是游戏依赖，
> 否则别人克隆下来一打开就是一个"包找不到"的报错工程。本机的 `manifest.json` 与
> `packages-lock.json` 已经用 `git update-index --skip-worktree` 隐藏了本地改动，
> 所以这一行不会被误提交。想看到/提交它：
> `git update-index --no-skip-worktree UnityMCPProject/Packages/manifest.json`。
>
> **如果哪天这一行丢了**（`git reset --hard`、`git checkout -- .` 之类会覆盖
> skip-worktree 文件），一条命令就能装回来：
>
> ```powershell
> powershell -File scripts/enable-mcp-package.ps1            # 装上（幂等）
> powershell -File scripts/enable-mcp-package.ps1 -Remove    # 拆掉
> ```

### 3.4 把编辑器传输方式切成 stdio

Unity 面板默认走 HTTP；这个部署用传统 stdio 桥接。编辑器偏好（**全局**，不随工程）：

```
HKCU\Software\Unity Technologies\Unity Editor 5.x
  MCPForUnity.UseHttpTransport_h3850471145 = 0    (0 = stdio, 1 = HTTP)
```

也可以打开 Unity 的 `Window → MCP for Unity`，在连接区直接切换。

### 3.5 把服务端挂到 DSH

在 profile 的 patch 层里加一段 insert（本机已写入）：

```yaml
- insert:
    - id: unity-mcp
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: unity          # 工具前缀 mcp__unity__
        transport: stdio
        command: 'D:\projects\dsh-unity\.venv\Scripts\mcp-for-unity.exe'
        args: []
        cwd: 'D:\projects\dsh-unity\UnityMCPProject'
        toolCallTimeoutMs: 300000  # 默认 60s 不够：重编译/跑测试会超
        failOnStartupError: false  # Unity 没开也不阻塞 DSH 启动
```

该 profile 的 `patchReload: live` → **改完即热生效**，不用重启 DSH。

---

## 4. 新工程复用（核心流程，3 步）

1. **加一行包引用**：把 `3.3` 那行 JSON 加进新工程的 `Packages/manifest.json`
   （路径指向同一份 `unity-mcp\MCPForUnity`，不用复制包）。
2. **用 Unity 2022.3.62f3c1 打开该工程**。包会编译，编辑器加载时自动在 6400 起 stdio 桥接。
3. **直接用**：DSH 里 `mcp__unity__*` 工具会自动指向当前打开的这个编辑器。

不需要：重装 Python 服务端、改 DSH 配置、重启 DSH、重启浏览器。

### 服务端是怎么找到编辑器的

Python 服务端不进编辑器，只做两件事：读 `%USERPROFILE%\.unity-mcp\unity-mcp-status-*.json`
拿到端口，然后 TCP 连上去。所以：

- `cwd` 配成哪个工程**不影响**它连谁（那只是子进程的工作目录）。
- 编辑器没开时工具照样列出来，调用会返回"找不到实例"——这是设计如此，不会阻塞 DSH。

### 同时开多个 Unity 编辑器

多个编辑器会各写一个状态文件。用工具切换目标：

```
mcp__unity__set_active_instance   (传 Name@hash / hash 前缀 / 端口号)
```

---

## 5. 日常使用

### 5.1 常用工具分组

| 目的 | 工具 |
|---|---|
| 看编辑器状态/日志 | `read_console`、`manage_scene`(get_active/get_hierarchy)、`execute_code` |
| 改场景对象 | `manage_gameobject`、`manage_components`、`find_gameobjects` |
| 改资源 | `manage_asset`、`manage_material`、`manage_texture`、`manage_prefab` |
| 改脚本 | `script_apply_edits`（结构化）、`apply_text_edits`（按坐标）、`create_script`、`validate_script` |
| 编译 | `refresh_unity`（`compile: request` + `wait_for_ready`） |
| 跑测试 | `run_tests` → `get_test_job` 轮询 |
| 截图 | `manage_camera`(screenshot，`capture_source: game_view` 会连 IMGUI 一起截) |
| 跑任意 C# | `execute_code`（在编辑器里执行，可返回字符串；诊断利器） |
| 执行菜单项 | `execute_menu_item`（工程自定义的 `Tools/...` 菜单） |

命名分隔符是**双下划线**：`mcp__` + `unity` + `__` + 工具名。

### 5.2 三个提高成功率的使用习惯

1. **改完代码先 `refresh_unity` 再 `read_console`**，用 `error` 过滤确认零编译错误，
   再跑测试。别直接跑测试——编译错误时的测试结果没有意义。
2. **超时不是失败**：`refresh_unity` 会触发域重载，期间 stdio 桥接会断开重连
   （返回里会出现 `recovered_from_disconnect`），这是正常的。
3. **能用 `execute_code` 就先用它做定量验证**：比截图可靠得多
   （例如"面板重叠没有""角色在不在聊天面板之上"这类都能用投影坐标直接量）。

---

## 6. 自检与排错

### 6.1 三层逐层自检

```powershell
# 1) 编辑器桥接是否就绪
Get-Content "$env:USERPROFILE\.unity-mcp\unity-mcp-status-*.json"
#    unity_port 应为 6400，reason 应为 "ready"

# 2) 端口是否在听
Test-NetConnection 127.0.0.1 -Port 6400 -InformationLevel Quiet   # 期望 True

# 3) 传输出口没被改回 HTTP
reg query "HKCU\Software\Unity Technologies\Unity Editor 5.x" /v MCPForUnity.UseHttpTransport_h3850471145
#    期望 0x0
```

端到端自检脚本（会启动无头编辑器、建一个对象、退出）：

```powershell
$env:UNITY_MCP_STATUS_DIR = 'D:\projects\dsh-unity\.unity-mcp-status'
& 'D:\projects\dsh-unity\.venv\Scripts\python.exe' 'D:\projects\dsh-unity\scripts\verify_mcp.py'
```

### 6.2 常见故障

| 现象 | 原因 / 处理 |
|---|---|
| 工具都在，但调用返回"找不到实例" | Unity 没开，或开的工程没装包。看状态文件的 `last_heartbeat`。 |
| 调用超时 | Unity 正在重编译/跑测试。把 `toolCallTimeoutMs` 调大（本机 300000），或等重载结束后重试。 |
| `git clone` 报 `SEC_E_NO_CREDENTIALS` | 用 `git -c http.sslBackend=openssl`。 |
| `install-server.ps1` 失败 | 需要在**沙箱外**运行（沙箱会拦写工作区外路径与带管道的子进程）。 |
| **改了代码但运行时行为没变** | 见下条，这是本机真实踩过的坑。 |
| 中文在 HUD 里显示成 `锟斤拷` | 脚本缺 UTF-8 BOM。跑 `Tools/DSH Pet/Fix Script Encodings`。 |
| 脚本报 `CS1010: Newline in constant` | 文件被"读成文本再写回"的往返操作损坏了（多字节字符变成 `U+FFFD`，还常吞掉引号）。**BOM 补不回来**，要重新打字那几处。跑同一个菜单项，它现在会列出含 `U+FFFD` 的文件与行号。**别用 PowerShell 的 `Get-Content`/`Set-Content` 编辑 `.cs`。** |

### 6.3 踩过的两个编辑器级坑（重要）

**坑 A：脚本域卡住，DLL 更新了但运行时还是旧程序集。**
现象：`Library/ScriptAssemblies/*.dll` 的时间戳变了、磁盘上的 DLL 内容也对，
但运行时行为仍是旧的（实测：新写的字符串不在运行时的程序集里，源码里删掉的字符串还在）。
`refresh_unity`、`EditorUtility.RequestScriptReload()`、进出 Play 模式**都没能**触发域重载。
**处理：重启 Unity 编辑器。** 重启前先 `EditorSceneManager.SaveOpenScenes()` 保住场景。

**坑 B：Bee 增量编译有时会复用某个文件的旧产物。**
现象：同一个文件里其它改动生效了，只有它没生效（实测过一次 `PetSpecies.cs`：
源码里第一个物种是狐狸，运行时装进来的却是兔子）。
**处理：改一下该文件的 mtime（加个注释保存即可）再重新编译**，强制它重编。
如果还不行，按坑 A 重启编辑器。

**坑 C：中文源码必须带 UTF-8 BOM，而且不能被"文本往返"损坏。**
两种失败模式要分清：

- **缺 BOM**：中文区域设置的 Windows 上，Unity 编译器按 GBK 解释没有 BOM 的 `.cs`，
  该文件里所有中文字面量都变乱码（实测：「全景」编译后成了 `锟斤拷`）。
  Unity 自己创建脚本时是带 BOM 写的，这不是巧合。**任何外部工具改过含非 ASCII 的脚本后
  都要重新补 BOM** —— 本工程提供菜单项 `Tools/DSH Pet/Fix Script Encodings` 一键修复并报告清单。
- **被有损往返损坏**：用 PowerShell 的 `Get-Content`/`Set-Content` 编辑 `.cs` 会把多字节字符
  换成 `U+FFFD`（常连带吞掉引号），BOM 救不回来，只能重新打字；症状是
  `error CS1010: Newline in constant`。同一个菜单项也会**报告**含 `U+FFFD` 的文件与行号。

---

## 7. 更新与回退

```powershell
# 更新 checkout
git -C D:\projects\dsh-unity\unity-mcp pull

# 重建 Python 服务端（checkout 变了一定要重跑）
& D:\projects\dsh-unity\scripts\install-server.ps1

# Unity 侧：本地 file: 引用，刷新一下包即可（Window → MCP for Unity → 重连）
```

回退到官方 HTTP 传输：把第 3.4 的注册表值改成 `1`，或用 Unity 面板切换传输方式。
（注意该偏好是**全局**的，会影响这台机器上所有 Unity 工程。）
