# Agent 对抗校验结果

本文件记录 `code-check-battle.md` 要求的 SbSceneAgent / SurfboardAgent 对抗式校验摘要、裁决依据和当前验证结果。

## 执行方式

- SbSceneAgent 读取 `F:\resbscene\docs`，按文档章节提出格式、解析、渲染、动画、CLI、Viewer 行为的可验证主张。
- SurfboardAgent 读取 `C:\Users\mikir\Documents\Tencent Files\664659548\FileRecv\sbscene\sbscene`，以外部参考源码、样本字节、参考文档和 IDA 线索作为更权威依据。
- 对存在冲突的项目，使用 IDA MCP 抽查关键函数，并用当前 `F:\resbscene` 的代码、测试和样本命令复核。

SbSceneAgent 覆盖的 docs 文件：

- `docs/agent-check-result.md`
- `docs/animation-system.md`
- `docs/chiffon-sample.md`
- `docs/cli-render.md`
- `docs/code-check-battle.md`
- `docs/groovemaster.ini.md`
- `docs/mode-design-viewer.md`
- `docs/otohime-sample.md`
- `docs/ras-sample.md`
- `docs/sbscene-format.md`
- `docs/svo-resources.md`
- `docs/system-config.md`
- `docs/viewer-animation-playback.md`

## 对抗焦点与裁决

| 主题 | SbSceneAgent 质询 | SurfboardAgent / IDA 裁决 | 处理结果 |
| --- | --- | --- | --- |
| VTBF chunk 结构 | 文档曾把真实样本描述为线性 `vtc0` chunk 流，需确认 `ParamLow/ParamHigh` 是否实际为计数。 | 真实格式是预序递归树；块头为 `u16 childCount` 后接 `u16 fieldCount`，块长度只覆盖本块字段，children 紧随其后。外部参考目录里个别旧文档把顺序写反，本轮以 `VtbfReader.cs` 和样本字节为准。 | `VtbfParser` 已按 child count 递归；`sbscene-format.md` 已改为 `ChildCount/FieldCount`，保留 legacy `ParamLow/ParamHigh` 作为别名。 |
| 字符串编码 | 旧描述偏向宽松 UTF-8，需核对 `TEXT.0x7A` 和长注释。 | 参考实现按 CP932/Shift-JIS 解码并在 NUL 处截断；raw bytes 仍保留作低层证据。 | `VtbfParser` 使用 code page 932；`sbscene-format.md` 已移除“最终文本编码未确认”的冲突措辞，只保留布局/渲染语义未知边界。 |
| `CIMG.0x48` | 旧文档把它主要写成 unknown/shared packed state，需确认 draw 语义。 | CIMG draw 路径已确认：低 4 位为 draw/blend mode，mode `1` 为 additive/effect；`0x10/0x20` 为 flipU/flipV；`0xC0` 为 UV permutation；`0x7800` 为 surface mode。其它 owner 不外推这些 CIMG draw 语义。 | Core/PNG renderer 实现 draw mode、UV permutation、surface mode；文档清理旧 bit0 未确认表述。 |
| secondary CREF | 旧 `--render-secondary` 行为可能把 secondary 当独立图层。 | surface mode `0` 只用 primary；mode `1` 用 secondary 作为 stage0；mode `2/3/4` 才组合 primary + secondary stage1。标准角色样本当前 surface mode 主要覆盖 mode `0`，其它 mode 依据 IDA/代码路径确认。 | PNG renderer 只在 surface mode 启用时使用 secondary。 |
| additive blend | 旧实现曾接近 alpha-over 变体。 | additive/effect 是 RGB 累加、alpha 取 max；判定条件是 draw mode `1`，不是简单“bit0 语义泛化到所有 owner”。 | PNG renderer 已改为 RGB 累加、alpha max；测试覆盖。 |
| illumination 默认值 | 缺字段时是否为透明黑。 | 默认应为 `#FF000000`，即黑色、alpha 255；父子继承为饱和加。 | Core / Viewer 默认 illumination 已改为 opaque black。 |
| 动画叠加 | 多动画是否单槽覆盖，CLI 顺序是否等同实机。 | 运行时每帧先 reset bind/static 状态，再按 animation index 遍历 enabled 槽；后槽只覆盖自己含有的轨道。CLI 也按 animation index 归并/排序 enabled 槽，同一槽多次指定时最后一次 frame 生效。 | Viewer 和 CLI 均按 animation index 叠加；CLI 支持 `--anim #Index[Frame]` 精确指定槽。 |
| TrackType 24 | 是否只是模糊 opacity/alpha 候选。 | type 24 写入 `MaterialColor.A`，通过父链乘法形成 effective opacity。 | 语义名称收窄为 `MaterialAlpha`；文档和输出文字已更新。 |
| Viewer 能力边界 | Viewer 文档残留“只按静态节点构建，后续再接动画”的设计文字。 | 当前 `SceneRenderBuilder` 已应用 `SbSceneAnimationFrameBuilder`，支持动画叠加、display、transform、material alpha、illumination、vertex color、动态宽高和 primary image reference。Viewer bitmap 预览仍不等同于完整 PNG renderer：UV permutation、secondary surface stage、additive blend 只确认在 Core/PNG renderer。 | `viewer-animation-playback.md` 已改为当前实现状态，并收窄 Viewer 渲染能力边界。 |

## IDA MCP 取证摘要

- `sub_88EC90`：读取 CIMG 字段后保存 `0x48` raw packed state，并调用 `sub_88E6E0`、`sub_88E730`、`sub_88EB50/60/70` 等 decoder。
- `sub_88E6E0`：对 `flags & 0xF` 解码 draw/blend mode `0..3`，其它值回落到 `0`。
- `sub_88E730`：对 `flags & 0x7800` 解码 surface mode `0..4`。
- `sub_88EB50` / `sub_88EB60` / `sub_88EB70`：分别解码 `0x10` flipU、`0x20` flipV、`0xC0` UV permutation mode。
- `sub_7DAE10`：按 flip 参数和 UV permutation mode 重排 TL/BL/TR/BR 的 UV 输出。
- `sub_7D15E0`：每帧对每个 cast 先调用 reset，再遍历 animation 槽并在 enabled 时调用 track evaluator。
- `sub_7D30C0`：track type table 中 `24` 指向 material alpha 偏移，`25..28` 指向 illumination RGBA，`29..44` 指向四角 vertex color。

## 外部参考证据

- `SurfboardPlayer/src/Surfboard.Formats/Vtbf/VtbfReader.cs`：chunk 注释和实现均为 `childCount` 后 `fieldCount`，并在 fields 后递归读取 children。
- `SurfboardPlayer/src/Surfboard.Formats/Vtbf/VtbfModel.cs`：注册 code page provider，使用 code page 932，并在首个 NUL 截断字符串。
- `SurfboardPlayer/src/Surfboard.Formats/SbScene/SbSceneModel.cs`：`ImageCast.DrawMode`、`IsAdditive`、`FlipU/FlipV`、`UvMode`、`SurfaceMode` 和 opaque black illumination 默认值与当前裁决一致。
- `SurfboardPlayer/src/Surfboard.Runtime/Compositor.cs`：surface mode `1` 选择 secondary 为 primary stage；mode `>=2` 才解析 secondary stage；UV 构建按 flip + permutation 输出。
- `SurfboardPlayer/tools/Surfboard.Cli/SoftRaster.cs`：secondary surface mode `2/3/4` 的组合路径，以及 additive blend 的 RGB 累加、alpha max。
- `SurfboardPlayer/src/Surfboard.Runtime/AnimationPlayer.cs`：动画帧先从 bind/static state clone，再按传入 animation 槽顺序应用 track；type 24 写入 material alpha，25..28 写入 illumination，29..44 写入 vertex color。

## 本仓库出处映射

| 主张 | 文档出处 | 实现 / 测试出处 |
| --- | --- | --- |
| VTBF 真实样本为 `childCount/fieldCount` 预序树 | `docs/sbscene-format.md` | `src/SbScene.Core/Vtbf/VtbfParser.cs`、`tests/SbScene.Core.Tests/VtbfParserTests.cs` |
| 字符串按 CP932/Shift-JIS 解码并 NUL 截断 | `docs/sbscene-format.md` | `src/SbScene.Core/Vtbf/VtbfParser.cs`、`src/SbScene.Core/Resources/SbSceneTextureParser.cs`、`tests/SbScene.Core.Tests/VtbfParserTests.cs` |
| `CIMG.0x48` draw/flip/UV/surface mode | `docs/sbscene-format.md`、`docs/svo-resources.md`、`docs/cli-render.md` | `src/SbScene.Core/Rendering/SbSceneImageCastConventions.cs`、`tests/SbScene.Core.Tests/SbSceneImageCastConventionsTests.cs` |
| secondary CREF 只在 surface mode 启用时参与 PNG renderer | `docs/cli-render.md`、`docs/svo-resources.md` | `src/SbScene.Core/Rendering/SbScenePngRenderer.cs`、`tests/SbScene.Core.Tests/SbSceneRenderTreeTests.cs` |
| additive blend 为 RGB 累加、alpha 取 max | `docs/cli-render.md`、`docs/svo-resources.md` | `src/SbScene.Core/Rendering/SbScenePngRenderer.cs`、`tests/SbScene.Core.Tests/SbScenePngRendererTests.cs` |
| illumination 默认 opaque black，父子饱和加 | `docs/sbscene-format.md`、`docs/cli-render.md`、`docs/ras-sample.md` | `src/SbScene.Core/Rendering/SbSceneColorConventions.cs`、`src/SbScene.Core/Rendering/SbSceneAnimationFrameBuilder.cs`、`tests/SbScene.Core.Tests/SbSceneColorConventionsTests.cs`、`tests/SbScene.Core.Tests/SbSceneAnimationFrameBuilderTests.cs` |
| 动画每帧 reset 后按 animation index 叠加 enabled 槽 | `docs/animation-system.md`、`docs/viewer-animation-playback.md` | `src/SbScene.Core/Rendering/SbSceneAnimationFrameBuilder.cs`、`src/SbScene.Viewer/MainWindow.xaml.cs`、`tests/SbScene.Core.Tests/SbSceneAnimationFrameBuilderTests.cs` |
| TrackType 24 为 `MaterialAlpha` | `docs/animation-system.md`、`docs/ras-sample.md` | `src/SbScene.Core/Semantics/SbSceneParser.cs`、`src/SbScene.Core/Rendering/SbSceneAnimationFrameBuilder.cs`、`tests/SbScene.Core.Tests/SbSceneAnimationFrameBuilderTests.cs` |
| Viewer 已应用动画状态，但 bitmap 预览能力边界小于 PNG renderer | `docs/viewer-animation-playback.md` | `src/SbScene.Viewer/SceneRenderBuilder.cs`、`src/SbScene.Viewer/MainWindow.xaml.cs` |

## 已实施修改

- `src/SbScene.Core/Vtbf/VtbfParser.cs`：真实样本走 `childCount/fieldCount` 递归解析；字符串改用 CP932。
- `src/SbScene.Core/Rendering/*`：补齐 draw mode、UV permutation、surface mode、secondary surface 组合、additive blend、opaque black illumination 默认值、动态宽高 pivot 缩放、vertex color track。
- `src/SbScene.Core/Semantics/SbSceneParser.cs`：track type 24 命名为 `MaterialAlpha`。
- `src/SbScene.Viewer/*`：Viewer 使用 `SbSceneAnimationFrameBuilder` 应用 active animation states，并按 animation index 排序叠加。
- `docs/sbscene-format.md`、`docs/animation-system.md`、`docs/ras-sample.md`、`docs/svo-resources.md`、`docs/cli-render.md`、`docs/viewer-animation-playback.md`：同步上述裁决，删除旧的线性 VTBF、bit0 未确认 additive、type 24 模糊命名和 Viewer 尚未接动画等冲突表述。
- `tests/SbScene.Core.Tests/*`：增加 VTBF 递归、surface/draw/UV、additive blend、illumination 默认值、动画状态 clone、动态宽高与 vertex color 覆盖。

## 验证结果

- `dotnet test SbScene.sln`：通过，77/77。
- `dotnet build SbScene.sln`：因正在运行的 `SbScene.Viewer` 与 Visual Studio 锁定 `src/SbScene.Viewer/bin/Debug/net8.0-windows/SbScene.Core.dll` 失败；这不是编译错误。
- `dotnet build src\SbScene.Viewer\SbScene.Viewer.csproj -p:OutputPath=..\..\out\agent-check\viewer-build\`：通过，0 warning / 0 error。
- `dotnet build src\SbScene.Cli\SbScene.Cli.csproj -p:OutputPath=..\..\out\agent-check\cli-build\`：通过，0 warning / 0 error。
- Ras 样本解析冒烟：`MM_CH_Ras__Ras_00.sbscene` 可解析为 13,789 blocks、428 nodes、304 image casts、32 animations。
- Ras PNG 渲染冒烟：`render MM_CH_Ras__Ras_00.sbscene MM_CH_Ras.svo --character-defaults --anim Action_Wait1[0]` 成功输出 `out\agent-check\MM_CH_Ras_Action_Wait1.png`，当前尺寸为 551x1071；仅报告一个已知越界 crop 透明 padding warning。

## 结论

本轮对抗校验发现的实质冲突已经处理。当前 `F:\resbscene` 的核心 parser、Core/PNG renderer、secondary surface、illumination 默认值、动画叠加、type 24 语义和 Viewer 动画状态应用与 Surfboard 参考源码、真实样本和 IDA 取证一致。

仍保留的边界：Viewer bitmap 预览不声明完整支持 PNG renderer 的 UV permutation、secondary surface stage 和 additive blend；非 CIMG owner 的 shared packed state 只按 raw/decoder 记录，不外推 CIMG draw 语义。
