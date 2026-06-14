# SbScene

SbScene 是一组用于解析、检查、渲染和导出 `.sbscene` / `.svo` 资源的 .NET 工具。当前代码主要面向 surfboard / NaviChara 类 UI 场景：把 VTBF 结构解析成强类型模型，从 SVO 中读取 DDS 图像资源，并进一步输出 PNG、GIF、JSON、Markdown 或 Unity NaviChara 导出清单。

更底层的格式调查记录在 [docs/sbscene-format.md](docs/sbscene-format.md)、[docs/svo-resources.md](docs/svo-resources.md) 和 [docs/animation-system.md](docs/animation-system.md)。README 只说明仓库构成、常用入口和整体架构。

## 项目构成

| 路径 | 项目 | 用途 |
| --- | --- | --- |
| `src/SbScene.Core` | `SbScene.Core` | 核心库。解析 VTBF / sbscene、解析 SVO 资源、解码 DDS/PNG/GIF、构建语义模型、渲染 PNG/GIF、导出 Unity NaviChara 数据。 |
| `src/SbScene.Cli` | `SbScene.Cli` | 命令行工具。封装 inspect、dump、survey、render、extract-images、inspect-svo、export-unity-navichara 等命令。 |
| `src/SbScene.Viewer` | `SbScene.Viewer` | WPF 查看器。用于交互式打开场景、加载 SVO、查看节点树、播放动画、预览渲染结果并执行导出。 |
| `src/NavigationCharacterPatcher` | `NavigationCharacterPatcher` | 独立 AssetBundle 修补工具。用于改写 NavigationCharacter MonoBehaviour 的 `m_Script.m_PathID`。 |
| `tests/SbScene.Core.Tests` | `SbScene.Core.Tests` | Core 单元测试，覆盖解析、渲染、动画、资源和导出相关逻辑。 |
| `docs` | 文档资料 | 格式调查、渲染说明、动画系统、Viewer 模式和样本分析记录。 |
| `New Unity Project (4)` | Unity 工程文件 | Unity 侧参考工程，不在 `SbScene.sln` 主解决方案中。 |

## 环境要求

- .NET 8 SDK。
- `SbScene.Viewer` 目标框架为 `net8.0-windows`，需要 Windows/WPF 环境。
- `NavigationCharacterPatcher` 使用 `AssetsTools.NET` 处理 Unity AssetBundle。

常用验证命令：

```powershell
dotnet test SbScene.sln --no-restore
dotnet build SbScene.sln --no-restore
```

首次拉取或依赖变化后，先执行：

```powershell
dotnet restore SbScene.sln
```

## CLI 基本示例

以下示例使用占位路径：

- `scene.sbscene`：场景文件。
- `resource.svo`：与场景对应的 SVO 资源包。
- `out`：输出目录。

### 查看 sbscene 摘要

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- inspect scene.sbscene
```

`inspect` 会解析 `.sbscene` 并输出块、节点、动画、资源和警告等可读信息。

### 导出 JSON / Markdown

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- dump scene.sbscene --json out\scene.json --markdown out\scene.md
```

`dump` 用于保存完整解析结果，适合做 diff、格式调查或后续工具输入。

### 检查 SVO

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- inspect-svo resource.svo
```

`inspect-svo` 会输出 AVTS 头部、目录项、DDS 纹理、YABX 元数据和资源映射线索。

### 提取图像

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- extract-images scene.sbscene resource.svo --out out\images
```

默认会写出 atlas 和 crop PNG；如果只需要 crop，可加 `--no-atlas`。

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- extract-images scene.sbscene resource.svo --out out\images --no-atlas
```

### 渲染 PNG

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- render scene.sbscene resource.svo --out out\scene.png --background transparent --padding 80
```

常用渲染参数：

- `--show-hidden`：渲染隐藏节点。
- `--render-secondary`：渲染 secondary image 引用。
- `--background transparent|#RRGGBB|#AARRGGBB`：设置背景色。
- `--scale <n>`：输出缩放。
- `--supersample <n>` 或 `--high-quality`：提升采样质量。
- `--anim <name[frame]|#index[frame]>`：指定动画和帧。
- `--character-defaults`：套用 NaviChara 默认角色动画槽位。

示例：

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- render scene.sbscene resource.svo --out out\pose.png --character-defaults --anim Action_Wait1[30] --high-quality
```

### 渲染 GIF

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- render scene.sbscene resource.svo --out out\scene.gif --gif --fps 30 --frames 0:120 --gif-compress
```

GIF 缩放保持比例，只能指定一边：

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- render scene.sbscene resource.svo --out out\scene.gif --gif --gif-width 512
```

### 批量 survey

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- survey .\samples --filter UI_ --limit-scenes 20 --limit-svos 20 --out out\survey.json
```

`survey` 用于目录级格式调查，会汇总 sbscene 和 SVO 的字段、标签、动画、资源等统计信息。

### 导出 Unity NaviChara 清单

先生成配置模板：

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- export-unity-navichara scene.sbscene --write-profile-template out\navichara-profile.json --auto-map
```

编辑模板后执行正式导出：

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- export-unity-navichara scene.sbscene resource.svo --out out\navichara --profile out\navichara-profile.json --extract-sprites --write-validation-frames
```

也可以直接用 `--map <sourceAnimation=targetClip>` 指定映射：

```powershell
dotnet run --project src\SbScene.Cli\SbScene.Cli.csproj -- export-unity-navichara scene.sbscene resource.svo --out out\navichara --map Action_Wait1=Navi_Default --character-id 27
```

输出通常包含：

- `navichara-export.json`：导出清单。
- `diagnostics.md`：诊断报告。
- `sprites`：可选裁剪精灵。
- `validation`：可选验证帧。

## Viewer 基本用法

启动 Viewer：

```powershell
dotnet run --project src\SbScene.Viewer\SbScene.Viewer.csproj
```

Viewer 面向交互式检查：

- 打开 `.sbscene` 并加载匹配的 `.svo`。
- 查看节点树、节点显隐状态和选中节点预览。
- 播放动画、选择动画槽位和帧。
- 调整渲染质量、缩放、背景和隐藏节点显示。
- 从 UI 执行 GIF 导出或 Unity NaviChara 导出。
- 在 `Unity 导出` 菜单执行 `修补 NavigationCharacter prefab AB...`，对 prefab AssetBundle 做 dry-run 或写出修补后的 AB。

CLI 更适合批处理和可复现输出；Viewer 更适合人工检查节点、动画和资源对应关系。

## Core 基本示例

`SbScene.Core` 可以直接被其他 .NET 程序引用。

### 解析 sbscene

```csharp
using SbScene.Core.Semantics;

var scene = new SbSceneParser().ParseFile("scene.sbscene");

Console.WriteLine(scene.Summary.NodeCount);
Console.WriteLine(scene.Summary.AnimationCount);
foreach (var warning in scene.Summary.Warnings)
{
    Console.WriteLine(warning);
}
```

### 输出 JSON

```csharp
using System.Text.Json;
using SbScene.Core.Output;
using SbScene.Core.Semantics;

var scene = new SbSceneParser().ParseFile("scene.sbscene");
var json = JsonSerializer.Serialize(scene, SbSceneJson.CreateOptions(indented: true));
File.WriteAllText("scene.json", json);
```

### 渲染 PNG

```csharp
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;

var scene = new SbSceneParser().ParseFile("scene.sbscene");
var result = SbScenePngRenderer.Render(
    scene,
    "resource.svo",
    new SbSceneRenderOptions
    {
        Padding = 80,
        Scale = 1.0,
        TextureSampling = SbSceneTextureSampling.Bilinear,
    });

File.WriteAllBytes("scene.png", result.PngBytes);
```

### 采样动画帧

```csharp
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;

var scene = new SbSceneParser().ParseFile("scene.sbscene");
var frameState = SbSceneAnimationFrameBuilder.Build(
    scene,
    new[]
    {
        new SbSceneAnimationSelection("Action_Wait1", 30) { HasExplicitFrame = true },
    });
```

## NavigationCharacterPatcher 示例

该工具不依赖 `SbScene.Core`，用于 Unity AssetBundle 修补：

也可以在 Viewer 的 `Unity 导出` 菜单中使用 `修补 NavigationCharacter prefab AB...` 图形入口；命令行仍适合批处理。

```powershell
dotnet run --project src\NavigationCharacterPatcher\NavigationCharacterPatcher.csproj -- UI_Navichara_27.ab --dry-run
```

写出修补后的 bundle：

```powershell
dotnet run --project src\NavigationCharacterPatcher\NavigationCharacterPatcher.csproj -- UI_Navichara_27.ab --output UI_Navichara_27.patched.ab --compression keep
```

可选参数：

- `--path-id <long>`：目标 `m_Script.m_PathID`。
- `--script-name <name>`：要定位的脚本类名，默认 `NavigationCharacter`。
- `--compression keep|none|lz4|lzma`：输出压缩方式。
- `--dry-run`：只报告将修改的对象数量，不写文件。

## sbscene 架构组成

当前解析器把 sbscene 分成三层理解：VTBF 容器层、Surfboard 语义层、资源/渲染层。

### 1. VTBF 容器层

`.sbscene` 的外层是 VTBF 文档：

- 文件头使用 ASCII `VTBF`。
- 根结构指向 `SRFF`。
- 真实样本按 `vtc0` 预序 chunk 树解析。
- 每个块包含：
  - `Tag`：4 字节块名，例如 `SRFF`、`SCN `、`LAYR`、`NODE`、`TRS2`、`ANIM`、`MOT `、`TRK `、`KEY `、`DATA`、`CIMG`。
  - `ChildCount`：紧随当前块字段之后的子块数量。
  - `FieldCount`：当前块字段数量。
  - `Fields`：紧凑字段或记录数组。
  - `Children`：按预序紧跟在字段之后的子块。

Core 中对应的主要类型：

- `VtbfDocument`
- `VtbfBlock`
- `VtbfField`
- `VtbfFieldTypes`
- `VtbfParser`

VTBF 层尽量保留原始偏移、原始字节、字段类型和值预览，以便对未知字段继续做调查。

### 2. Surfboard 语义层

`SbSceneParser` 会把 VTBF 块转换成更接近业务含义的 Surfboard 模型：

- `SbSceneFile`：一次解析的顶层结果，包含源文件信息、VTBF 文档、Surfboard 模型和摘要。
- `SurfboardModel`：语义层主模型。
- `SceneObjectInfo`：原始结构对象摘要。
- `NodeInfo`：节点索引、层级、分组、类别、变换和资源关系。
- `Transform2DInfo`：节点的平移、旋转、缩放、显示状态、颜色和透明度。
- `CameraInfo`：场景相机字段。
- `AnimationInfo` / `MotionInfo` / `TrackInfo` / `KeyframeInfo`：动画、motion、轨道和关键帧。
- `VariantHint`：根据节点分组、动画名称和轨道推断出的变体线索。
- `UnknownFieldInfo` / `FieldValueSummary`：保留未命名字段的来源、类型和值摘要。

语义层的目标不是丢弃未知结构，而是在能命名的地方提供强类型入口，在不能命名的地方保留证据。

### 3. SVO 资源层

`.sbscene` 描述场景结构和引用，图像数据通常来自独立 `.svo`：

- SVO 外层为 AVTS 资源包。
- 目录项记录资源名称、类型、偏移、长度和 magic。
- DDS payload 被解析为 `SvoTextureResource`。
- YABX 元数据用于补充资源名称、atlas 信息和对象字段。
- sbscene 资源块会映射到：
  - `SbSceneTextureAtlas`
  - `SbSceneImageCast`
  - `SbSceneCropReference`
  - `SbSceneResourceMap`

图像解码由 `DdsDecoder` 和 `RgbaImage` 相关类型完成。`SvoImageExtractor` 可以把 atlas 和 crop 输出为 PNG。

### 4. 渲染层

渲染流程大致为：

1. 解析 `.sbscene` 得到 `SbSceneFile`。
2. 从 `.svo` 解析 DDS 纹理和 atlas。
3. 使用 `SbSceneAnimationFrameBuilder` 构建指定帧的节点和 image cast 状态。
4. 使用 `SbSceneRenderTree` 计算父子继承后的可见性、不透明度和颜色。
5. 使用 `SbSceneImageCastConventions` 处理 image cast 几何、翻转、UV 和 draw mode。
6. 使用 `SbScenePngRenderer` 输出 PNG，或由 `SbSceneGifRenderer` 按帧采样输出 GIF。

渲染相关核心类型：

- `SbSceneRenderOptions`
- `SbSceneRenderResult`
- `SbScenePngRenderer`
- `SbSceneGifRenderer`
- `SbSceneGifAnimationSampler`
- `SbSceneAnimationEvaluator`
- `SbSceneAnimationFrameBuilder`

### 5. Unity NaviChara 导出层

Unity 导出建立在语义层、SVO 资源层和动画采样之上：

- 将节点层级转换为 Unity NaviChara 节点描述。
- 将 atlas crop 转换为 sprite 描述或可选 PNG 文件。
- 将 sbscene 动画轨道转换为 Unity 曲线。
- 用 profile 或 `--map` 把源动画映射到目标 NaviChara 剪辑。
- 输出 `navichara-export.json` 和诊断报告。

相关类型集中在 `src/SbScene.Core/Unity`：

- `UnityNavicharaExporter`
- `UnityNavicharaExportOptions`
- `UnityNavicharaExport`
- `UnityNavicharaProfile`
- `UnityNavicharaProfileTemplate`
- `UnityNavicharaDiagnostic`

## 目录内文档

- [docs/sbscene-format.md](docs/sbscene-format.md)：VTBF / sbscene 格式事实和字段调查。
- [docs/svo-resources.md](docs/svo-resources.md)：SVO / AVTS / DDS / YABX 资源结构。
- [docs/animation-system.md](docs/animation-system.md)：动画、motion、track、key 的解析和推断。
- [docs/cli-render.md](docs/cli-render.md)：渲染命令细节。
- [docs/viewer-animation-playback.md](docs/viewer-animation-playback.md)：Viewer 动画播放行为。
