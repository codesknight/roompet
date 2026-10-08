# 着色器与材质

项目里四个自定义着色器都在 `UnityMCPProject/Assets/Shaders/`，
材质在 `Assets/Materials/`（跑酷用的在 `Assets/Resources/Runner/`）。

| 着色器 | 用途 |
|---|---|
| `DSH/Shell Fur`（`ShellFur.shader`） | 几何着色器分层毛发：沿法线复制 N 层壳，越靠外越短越稀 |
| `DSH/Rainbow Fur`（`RainbowFur.shader`） | 在毛发基础上叠加随时间流动的七彩光 |
| `DSH/Dreamy Skybox`（`DreamySkybox.shader`） | 三段渐变 + 彩虹极光 + 星空天空盒 |
| `DSH/Neon`（`Neon.shader`） | 房间/跑道的平涂光照材质（本项目大部分物件用它） |

## 毛发与流光（`Cube` 那个演示）

材质：`Assets/Materials/Fur.mat`（纯毛发）、`Assets/Materials/RainbowFur.mat`（毛发 + 七彩，当前赋给 `Cube`）。

| 参数 | 作用 |
|---|---|
| `_FurLength` | 毛发长度 |
| `_ShellCount` | 壳层数（上限见下） |
| `_Density` | 毛发密度/覆盖率 |
| `_FlowSpeed` | 色相流动速度 |
| `_HueSpread` | 同一层内的色相展开 |
| `_HueTipShift` | 从根到尖的色相偏移 |
| `_GlowStrength` | 发光强度 |
| `_RainbowMix` | 七彩与底色的混合比例 |

## 天空盒

材质：`Assets/Materials/DreamySky.mat`，已挂在 `RenderSettings.skybox` 上。

| 参数 | 作用 |
|---|---|
| `_ZenithColor` / `_MidColor` / `_HorizonColor` | 三段渐变（紫 → 青 → 粉） |
| `_NebulaStrength` / `_AuroraStrength` / `_StarBrightness` | 星云 / 极光 / 星空亮度 |

天空盒的色相算法和 `DSH/Rainbow Fur` 是同一套（`_HueShift` / `_HueFlow` / `_HueSpreadY`），
所以天空和 cube 共享色系；两者用同一个 `_FlowSpeed` 类参数各自漂移。

## 三个必须记住的约束

1. **`_ShellCount` 上限是 31。**
   D3D 要求 `maxvertexcount × 输出结构分量数 ≤ 1024`，该 shader 的输出结构是 11 个分量，
   所以 `[maxvertexcount(93)]` 就是天花板。**改动 `g2f` 结构后必须同步调整 maxvertexcount**，
   否则 shader 编译失败、物体渲染成洋红。

2. **编辑器非播放模式下 `_Time` 只在编辑器重绘时推进。**
   所以静态截图可能看起来"不动"，进入 Play 模式或让 Game 视图处于前台就能看到流动。
   （用 `_Time` 做动画的自定义 shader 都有这个现象，不是 bug。）

3. **环境光跟着天空盒走**（`RenderSettings.ambientMode = Skybox`）。
   换天空盒会连带改变整个场景的环境光——这既是"色调统一"的来源，
   也意味着天空盒变暗时物体会一起变暗。改完天空盒记得调 `DynamicGI.UpdateEnvironment()` 刷新环境探针。

## 诊断脚本

`scripts/` 下有几个用 Unity 的 `execute_code` 跑的探针，用来**量化**视觉改动而不是靠肉眼：

| 脚本 | 量什么 |
|---|---|
| `probe_fur.js` | 毛发的渲染与覆盖率 |
| `probe_rainbow.js` | 色相分布 |
| `probe_sky.js` | 天空区域的配色 |
| `probe_contrast.js` | 物体与天空的亮度对比 |
| `diff_two.js` | 两帧之间的像素差异（用于验证"动画确实在动"） |

渲染图默认写到 `logs/`（工程内但被 git 忽略）；需要留档的证据图放进 `docs/evidence/`。
