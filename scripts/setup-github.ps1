# Provision the GitHub side of the project: description, topics, labels, milestones and
# the issue backlog. Idempotent — safe to re-run; it skips anything that already exists.
#
# Auth: uses the credential git already has for github.com (the same one `git push`
# uses), via `git credential fill`. No token is written anywhere, and none is printed.
#
#   pwsh -File scripts/setup-github.ps1
#   pwsh -File scripts/setup-github.ps1 -WhatIfOnly      # show what would be created

[CmdletBinding()]
param(
    [string]$Repo = 'codesknight/roompet',
    [switch]$WhatIfOnly
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Windows PowerShell 5.1 still defaults to TLS 1.0 in places, and GitHub refuses that.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# --------------------------------------------------------------------------- auth

function Get-GitHubToken {
    $cred = ("protocol=https`nhost=github.com`n`n" | git credential fill) 2>$null
    foreach ($line in $cred) {
        if ($line -like 'password=*' -and $line.Length -gt 12) { return $line.Substring(9) }
    }
    throw "No GitHub credential found for github.com. Run a 'git push' once, or set one up with 'git credential approve'."
}

$script:Token = Get-GitHubToken
$script:Headers = @{
    Authorization          = "Bearer $script:Token"
    Accept                 = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent'           = 'roompet-setup'
}

$script:Created = 0
$script:Skipped = 0

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body)

    $uri = "https://api.github.com$Path"
    if ($WhatIfOnly -and $Method -ne 'GET') {
        Write-Host ("  [dry-run] {0} {1}" -f $Method, $Path)
        return $null
    }

    $params = @{ Method = $Method; Uri = $uri; Headers = $script:Headers }
    if ($null -ne $Body) {
        # UTF-8 bytes, not a string: PowerShell would otherwise send Chinese as Latin-1
        # and GitHub would store mojibake.
        $params.ContentType = 'application/json; charset=utf-8'
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 6 -Compress))
    }
    return Invoke-RestMethod @params
}

# ------------------------------------------------------------------------ metadata

Write-Host "==> Repository metadata"
Invoke-Api PATCH "/repos/$Repo" @{
    description = 'Unity 虚拟宠物空间：会记事、看得见房间、听得懂指令的宠物，外加七个动物主题小游戏和安卓端。'
    has_issues  = $true
    has_wiki    = $false
    has_projects = $true
} | Out-Null

Invoke-Api PUT "/repos/$Repo/topics" @{
    names = @('unity', 'unity3d', 'game', 'virtual-pet', 'csharp', 'imgui',
              'procedural-generation', 'llm', 'deepseek', 'mcp', 'android', 'minigames')
} | Out-Null
Write-Host "  description + topics set"

# -------------------------------------------------------------------------- labels

Write-Host "==> Labels"
$labels = @(
    @{ name = '宠物';        color = 'F9A8D4'; description = '宠物本身：需求、情绪、行为、外观' }
    @{ name = '记忆';        color = 'A78BFA'; description = '短期对话、长期事实、记事本/日历' }
    @{ name = '大脑/提示词'; color = '38BDF8'; description = '大模型接口、提示词、回复解析' }
    @{ name = '界面';        color = 'FBBF24'; description = 'IMGUI 面板、布局、输入' }
    @{ name = '相机';        color = '34D399'; description = '视角模式与取景' }
    @{ name = '声音';        color = 'FB7185'; description = '程序化音效与语音' }
    @{ name = '跑酷';        color = 'F97316'; description = '跑酷小游戏与赛道生成' }
    @{ name = '玩法';        color = '22D3EE'; description = '互动方式与游戏性' }
    @{ name = '工具链';      color = '94A3B8'; description = 'MCP for Unity、脚本、CI' }
    @{ name = '文档';        color = '64748B'; description = 'README 与 docs/' }
    @{ name = '技术债';      color = '78716C'; description = '已知的将就与隐患' }
    @{ name = '待验证';      color = 'EAB308'; description = '已实现但验证方式有限，需要人工确认' }
    @{ name = 'P0';          color = 'B91C1C'; description = '优先级：影响体验，先做' }
    @{ name = 'P1';          color = 'D97706'; description = '优先级：重要但不紧急' }
    @{ name = 'P2';          color = '0E7490'; description = '优先级：有空再说' }
)

$existingLabels = @()
if (-not $WhatIfOnly) {
    $existingLabels = (Invoke-Api GET "/repos/$Repo/labels?per_page=100") | ForEach-Object { $_.name }
}
foreach ($label in $labels) {
    if ($existingLabels -contains $label.name) { $script:Skipped++; continue }
    Invoke-Api POST "/repos/$Repo/labels" $label | Out-Null
    $script:Created++
}
Write-Host ("  {0} created, {1} already present" -f $script:Created, $script:Skipped)

# ---------------------------------------------------------------------- milestones

Write-Host "==> Milestones"
$milestones = @(
    @{ title = 'v0.2 — 让它更像活的'; description = '语音、手感、以及宠物的更多反应'; state = 'open' }
    @{ title = 'v0.3 — 内容扩展';     description = '更多小游戏、物种、房间互动，为《虚拟小镇》铺路'; state = 'open' }
    @{ title = 'v1.0 — 可发布';       description = '云存档、CI、打包、许可'; state = 'open' }
)

$existingMilestones = @()
if (-not $WhatIfOnly) {
    $existingMilestones = (Invoke-Api GET "/repos/$Repo/milestones?state=all&per_page=100") | ForEach-Object { $_.title }
}
$milestoneNumber = @{}
foreach ($m in $milestones) {
    if ($existingMilestones -contains $m.title) {
        $found = (Invoke-Api GET "/repos/$Repo/milestones?state=all&per_page=100") |
                 Where-Object { $_.title -eq $m.title } | Select-Object -First 1
        $milestoneNumber[$m.title] = $found.number
        $script:Skipped++
        continue
    }
    # Careful: at script scope `` and `` are the SAME variable,
    # because PowerShell variable names are case-insensitive. Hence the distinct name.
    $newMilestone = Invoke-Api POST "/repos/$Repo/milestones" $m
    if ($newMilestone) { $milestoneNumber[$m.title] = $newMilestone.number }
    $script:Created++
}
Write-Host ("  milestones ready: {0}" -f (($milestoneNumber.Keys) -join ', '))

# -------------------------------------------------------------------------- issues

Write-Host "==> Issue backlog"

$v02 = 'v0.2 — 让它更像活的'
$v03 = 'v0.3 — 内容扩展'
$v10 = 'v1.0 — 可发布'

$issues = @(
    @{
        title = '语音：让宠物开口说话（TTS）'
        labels = @('声音', 'P0')
        milestone = $v02
        body = @'
现在宠物"说话"只有按心情变调的程序化音，没有真正的声音。

**做法建议**：接 Edge TTS 或 Windows SAPI —— 两者都免费、支持中文、不需要国外信用卡，
和现有音效"零素材零 key"的路子一致。合成本地缓存成 `AudioClip`，避免每次对话都请求网络。

**验收**：宠物回话时能听到对应语气的中文语音；离线大脑也能出声。
'@
    }
    @{
        title = '扔球：地面上显示落点指示'
        labels = @('玩法', 'P1')
        milestone = $v02
        body = @'
现在有力蓄条和准星，但看不到球会落在哪，扔出去才知道。对着空的房间尤其难瞄准。

**做法**：用 `PetBall.PredictLanding()`（已经存在，宠物捡球就是用它）画一个地面标记，
随蓄力实时更新。

**验收**：蓄力时地面上能看到落点圈，实际落点与预测一致。
'@
    }
    @{
        title = '宠物对玩家行为的更多反应'
        labels = @('宠物', 'P1')
        milestone = $v02
        body = @'
目前只有"靠近时看着你"和"想你了会走过来"。可以加：

- 玩家蹲下 → 它凑过来
- 玩家跑起来 → 它跟着跑
- 玩家一直不动 → 它过来看看你

做法：`PlayerRoomController` 暴露"当前速度/是否蹲下"，
行为表加对应的行（`PetBehaviorLibrary`）。

**验收**：三种反应都能稳定触发，且不会打断正在进行的活动。
'@
    }
    @{
        title = '自由视角：保证看得见房间的软约束'
        labels = @('相机', 'P2')
        milestone = $v02
        body = @'
自由模式完全交给玩家，好处是自由，代价是镜头压低时房间近处会被聊天面板挡住，
看起来像"穿模"。可选的软约束：只在玩家目标超出 HUD 区域时才轻微修正俯角，
其余时候不干预。

**验收**：自由模式下随便怎么转，两个角色都不会长时间被面板挡住。
'@
    }
    @{
        title = '更多小游戏（例如小鸟飞行）'
        labels = @('跑酷', 'P1')
        milestone = $v03
        body = @'
门的框架已经就绪：`MiniGameLibrary.All` 加一行就是新活动，往返存档（`FlushToDisk` + `dshpet.away`）
已经处理好了。

**验收**：从门里能选到新活动，玩完回来宠物状态与日记连续。
'@
    }
    @{
        title = '更多宠物物种'
        labels = @('宠物', 'P2')
        milestone = $v03
        body = @'
现在是狐/猫/兔/熊。加一个物种 = 在 `PetSpecies.All` 加一项（外观参数 + 人格 + 说话风格），
换动物面板会自动多一项，提示词会带上新人格。

注意：`All[0]` 是默认物种，且**存进字段一定要用 `Copy()`**（见 docs/DEVLOG.md 坑 19）。
'@
    }
    @{
        title = '更多房间互动物件'
        labels = @('玩法', 'P2')
        milestone = $v03
        body = @'
现在能互动的是饭碗、水碗、小床、小球、梳子、门。加一个物件 =
新的 `InteractableKind` + `PetRoom` 里搭出来 + 行为表里加一行让它能自己用。

可以加的：猫抓板、饮水机、玩具箱、窗台（看窗外）。
'@
    }
    @{
        title = '《虚拟小镇》雏形：多个房间与走动'
        labels = @('玩法', 'P2')
        milestone = $v03
        body = @'
现在的框架已经把"活动"抽象成行为表 + 小游戏注册表，扩成多房间的路径大致是：

1. 房间定义数据化（尺寸、家具、可交互物）
2. 门变成"场景/房间切换"而不是只连跑酷
3. 宠物的需求与记忆跨房间连续

这一步会比较大，先做最小的两房间 + 一扇门。
'@
    }
    @{
        title = '云存档：日记与记忆不再只存本地'
        labels = @('记忆', 'P2')
        milestone = $v10
        body = @'
现在记忆在 PlayerPrefs，日记在 `persistentDataPath` 下的 JSON。要跨设备就得上传。

`PetJournal` 已经是版本化的 JSON（`PetJournal.cs`），加一个同步层即可；
冲突策略建议"按条目时间戳合并"，钉住的条目优先保留。
'@
    }
    @{
        title = 'CI：配置 Unity 许可后自动跑测试'
        labels = @('工具链', 'P1')
        milestone = $v10
        body = @'
`.github/workflows/unity-tests.yml` 已经写好，但**故意只在手动触发时运行**：
没有 Unity 许可的 CI 会在每次 push 时失败，那比没有 CI 更糟。

要做的事：在仓库 Settings → Secrets 里配置 `UNITY_LICENSE`（见 https://game.ci/docs/github/activation ），
然后把触发条件改成 `push` / `pull_request`。
'@
    }
    @{
        title = '打包发布：Windows / macOS 构建'
        labels = @('工具链', 'P2')
        milestone = $v10
        body = @'
目前只能在编辑器里跑。要发布需要：

- 一个入口场景选择（现在是 PetRoom）
- 分辨率/画质设置
- 构建产物与版本号跟着 `CHANGELOG.md` 走
- 若是 macOS，注意 IMGUI 的 Retina 缩放
'@
    }
    @{
        title = '选定开源许可'
        labels = @('文档', 'P2')
        milestone = $v10
        body = @'
项目代码还没有 LICENSE，默认"保留所有权利"。素材那边已经清楚了：
Kenney 的是 CC0，已在 `THIRD_PARTY.md` 列明。

**验收**：仓库根目录有 `LICENSE`，README 与 `THIRD_PARTY.md` 的说明一致。
'@
    }
    @{
        title = '技术债：宠物房间的植物仍在共用跑酷的材质'
        labels = @('技术债', 'P2')
        body = @'
`PropTint` 组件已经用材质属性块给每株植物自己的颜色，**观感问题解决了**，
但底下的材质资产仍然是跑酷那一份（Kenney 草地）。这意味着改跑酷的配色仍然会
影响宠物房间的植物基色（只是被属性块盖住了）。

彻底的做法：给宠物房间的植物各自一份材质资产，或统一走一套"道具调色板"。
'@
    }
    @{
        title = '技术债：编辑器偶尔卡住脚本域，需要重启 Unity'
        labels = @('工具链', 'P2')
        body = @'
现象：`Library/ScriptAssemblies/*.dll` 已经更新，但运行时行为仍是旧的
（新写的字符串没有生效、删掉的字符串还在）。`refresh_unity`、`RequestScriptReload`、
进出 Play 模式都无法触发域重载。

**目前处理**：重启 Unity 编辑器（重启前先保存场景）。详见 `docs/DEVLOG.md` 坑 2 与坑 3。
值得留意是否有更轻的恢复手段（例如清 `Library/Bee` 后重新导入）。
'@
    }
    @{
        title = '待验证：设置面板输入框的点击路径没有做过真实点击测试'
        labels = @('待验证', '界面')
        body = @'
"设置里的 API 输入框打不了字" 这个问题已经修了（根因是浮层自己铺了一块隐形的
"挡点击"按钮，把面板内所有控件的点击都吃掉了）。

**但验证方式有限**：当时用 Windows 的 `SetCursorPos`/`mouse_event` 注入真实鼠标键盘
没能在这个环境里送达游戏（抢不到前台窗口），所以只验证了
"打开面板时输入框会自动获得焦点" + "没有任何控件压在输入框上"。

**验收**：人工点一次输入框，确认光标出现、能打字。
'@
    }
)

$existingIssues = @()
if (-not $WhatIfOnly) {
    # Both open and closed, so a re-run does not resurrect a finished item.
    $existingIssues = (Invoke-Api GET "/repos/$Repo/issues?state=all&per_page=100") |
                      ForEach-Object { $_.title }
}

$issueCreated = 0; $issueSkipped = 0
foreach ($issue in $issues) {
    if ($existingIssues -contains $issue.title) { $issueSkipped++; continue }

    $payload = @{ title = $issue.title; body = $issue.body; labels = $issue.labels }
    if ($issue.milestone -and $milestoneNumber.ContainsKey($issue.milestone)) {
        $payload.milestone = $milestoneNumber[$issue.milestone]
    }
    Invoke-Api POST "/repos/$Repo/issues" $payload | Out-Null
    $issueCreated++
}
Write-Host ("  {0} issues created, {1} already present" -f $issueCreated, $issueSkipped)

# ------------------------------------------------------------------------- release

Write-Host "==> Release for the v0.1.0 tag"
$hasRelease = $false
if (-not $WhatIfOnly) {
    try { $null = Invoke-Api GET "/repos/$Repo/releases/tags/v0.1.0"; $hasRelease = $true } catch { $hasRelease = $false }
}
if ($hasRelease) {
    Write-Host "  already exists"
} else {
    Invoke-Api POST "/repos/$Repo/releases" @{
        tag_name = 'v0.1.0'
        name     = '0.1.0 — 宠物房间 + 跑酷'
        body     = @'
第一个可玩的版本：一间房、一只宠物、一局跑酷。

- 虚拟宠物房间：文字陪伴 + 环境互动、需求与情绪、四个物种、日程表式持久记忆（月历记事本）、
  主动与被动的两层行为表、可换的大模型大脑（断网自动落离线）、扔球捡球。
- 动物主题跑酷：左右换道 + 跳跃、无限赛道、距离计分与难度曲线、关卡模式、道具系统。
- 全部程序化生成：房间、角色、毛发、音效；84 条 EditMode 测试。

已知限制见 README 与 issue 列表。
'@
        draft      = $false
        prerelease = $false
    } | Out-Null
    Write-Host "  created"
}

Write-Host ""
Write-Host "==> Done"
Write-Host "    https://github.com/$Repo"
