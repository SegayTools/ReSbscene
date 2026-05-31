# Viewer 动画播放实现设计

本文记录 `SbScene.Viewer` 后续加入动画播放 UI 的实现方案。当前变更只新增设计文档，不修改 Viewer、Core 或 CLI 的公开 API。

## 范围

- Viewer 一次只选择并播放一个动画，不支持多动画叠加播放。
- 默认按 `60.0` sbscene 帧/秒推进播放时间。
- 内部帧值一律使用 `double`，UI 可以显示整数或小数帧，为后续高刷新率和小数帧插值保留空间。
- 动画轨道求值与应用规则应复用或抽取 Core 现有逻辑，不在 Viewer 里重新实现插值。

## UI 布局

动画控制区放在当前主三栏内容区下方、状态栏上方，横向铺满窗口。现有根 `Grid` 可从三行调整为四行：

1. 顶部工具栏。
2. 主三栏内容区：节点树、场景预览、选中节点/子树预览。
3. 动画控制区。
4. 状态栏。

动画控制区建议保持一行优先，窗口较窄时允许换行或压缩选择器宽度。控件从左到右为：

- 动画总数文本：例如 `12 animations`；无动画时显示 `0 animations`。
- 当前动画 `ComboBox`：切换 `_scene.Surfboard.Animations` 中的动画。
- 当前帧文本：例如 `Frame 24.5 / 150`。
- 帧进度 `Slider`：`Minimum = 0`，`Maximum = EndFrame`，`Value = CurrentFrame`。
- 播放按钮、暂停按钮、停止按钮。
- 循环播放 `CheckBox`。

无动画时，选择器、进度条、播放、暂停、停止和循环控件全部禁用；底部区域仍保留并显示 `0 animations`，避免布局跳变。

停止按钮语义是“暂停并回到第 0 帧”：停止后 `IsPlaying = false`、`CurrentFrame = 0`，并立即重建预览。

## 动画元数据

动画列表来自：

```csharp
_scene.Surfboard.Animations
```

UI 显示名优先使用 `AnimationInfo.Name`。当名称为空或全空白时，使用：

```text
ANIM@0x{Offset:X}
```

内部选择应保存动画索引，而不是只保存显示名。这样即使多个动画同名，Viewer 仍能稳定选择正确的 `AnimationInfo`。

### 结束帧

每个动画的 `EndFrame` 按以下顺序计算：

1. 优先读取该动画 `NumericFields` 中 `IdHex == "0x0056"` 的第一个整数值。该值来自 `ANIM.0x56`，语义上是 playback duration/end-frame 候选。值缺失、为负数、无法转换为有限数值时视为非法。
2. 若 `ANIM.0x56` 不可用，回退到该动画所有 motion track 的最大 `TrackInfo.LastFrame`。
3. 若所有 track 都没有有效 `LastFrame`，回退到所有 key 的最大 `KeyframeInfo.KeyFrame`。
4. 若仍无可用帧值，`EndFrame = 0`。

`EndFrame` 也使用 `double` 保存。当前字段多为整数，但不应把后续 API 限制为整数帧。

### 默认循环

选择动画时，循环勾选框默认读取 `ANIM.0x5F`：

- `0x5F == 1`：默认循环开启。
- `0x5F == 0`、缺失或非法：默认循环关闭。

用户可以在 UI 中覆盖该值。切换到另一个动画时，再按新动画的 `ANIM.0x5F` 重新初始化循环状态。

## Viewer 播放状态

Viewer 内部建议新增以下状态：

```csharp
private int? _selectedAnimationIndex;
private double _currentFrame;
private double _endFrame;
private bool _isPlaying;
private bool _isLooping;
private const double PlaybackFramesPerSecond = 60.0;
```

播放推进使用 WPF `DispatcherTimer` 加 `Stopwatch`：

- `DispatcherTimer` 只负责定期触发 UI 线程更新。
- `Stopwatch` 计算真实经过时间，避免依赖 timer 间隔精度。
- 每次 tick 执行 `CurrentFrame += elapsedSeconds * 60.0`。

状态机规则：

- 加载 scene 后若有动画，默认选中第一个动画；`CurrentFrame = 0`，不自动播放。
- 切换动画时暂停播放，`CurrentFrame = 0`，刷新 `EndFrame` 和 `IsLooping`，并立即重建预览。
- 点击播放时，如果没有选中动画则无操作；否则启动 timer 和 stopwatch。
- 点击暂停时停止 timer，保留当前帧并重建当前预览。
- 点击停止时停止 timer，`CurrentFrame = 0`，立即重建第 0 帧预览。
- 播放到 `EndFrame` 时：
  - 循环开启：`CurrentFrame = 0`，继续播放。
  - 循环关闭：`CurrentFrame = EndFrame`，并暂停。
- 手动拖动进度条会暂停播放，并立即按拖动后的 `CurrentFrame` 重建预览。
- `EndFrame <= 0` 时，播放应保持在第 `0` 帧；点击播放可以直接停回暂停状态，避免 timer 空转。

进度条的程序化更新和用户拖动要区分处理，避免 timer 更新 `Slider.Value` 时误触发“用户 seek 后暂停”。可用 `_isUpdatingAnimationControls` 这类布尔 guard 包住 UI 同步。

## 渲染数据流

当前 Viewer 的 `SceneRenderBuilder.Build(SbSceneFile scene, SvoRenderResources? resources, RenderSceneOptions options)` 只按静态节点数据构建 render items。后续实现应让 `RenderSceneOptions` 接收当前动画选择和当前帧，例如：

```csharp
internal sealed record RenderSceneOptions(
    bool ShowHiddenNodes,
    bool ShowNodeMarkers,
    IReadOnlySet<int> HiddenNodeIndexes,
    IReadOnlySet<int> ShownNodeIndexes,
    SbSceneAnimationSelection? Animation);
```

也可以使用列表形式以贴近 `SbSceneRenderOptions.Animations`，但 Viewer 本期只传单个选择。无论形式如何，帧值必须是 `double`。

`SceneRenderBuilder.Build` 的顺序应调整为：

1. 从 `scene.Surfboard.Nodes` 和 `scene.Surfboard.Resources.ImageCasts` 构建初始节点状态和图片状态。
2. 如果存在动画选择，在构建 render items 前应用当前帧动画状态。
3. 用动画后的 display 参与 `SbSceneRenderTree.BuildFinalVisibility`。
4. 用动画后的 transform 构建 world transform。
5. 用动画后的 material alpha 计算有效透明度。
6. 用动画后的 primary/secondary image reference index 选择 CIMG crop。
7. 再进入现有 render item、命中测试和子树预览流程。

现有“隐藏节点也绘制”、手动显示/隐藏子树、节点选择预览都应继续生效。最终可见性建议按以下优先级理解：

- 手动隐藏子树优先隐藏。
- 手动显示子树优先显示。
- 否则使用动画后的 display 与父级可见性计算最终可见性。
- `ShowHiddenNodes` 仍作为调试选项允许绘制静态或动画隐藏的节点。

未绑定 SVO 时仍允许切换动画和拖动帧。渲染继续显示现有 CIMG 占位颜色；动画 transform、display、alpha 和 image reference index 仍应影响占位图的位置、可见性和选择信息。

## Core 复用方向

不要在 Viewer 中复制 `SbScenePngRenderer` 的动画应用逻辑。当前 Core 已有：

- `SbSceneAnimationEvaluator.EvaluateTrack(TrackInfo track, double frame)`：负责轨道求值和插值。
- `SbScenePngRenderer` 内部的动画应用流程：把 track type 应用到节点 transform、display、material alpha、primary/secondary image reference index 等状态。

推荐后续抽取可复用 Core 类型，例如：

- `SbSceneAnimationFrameState`
- `SbSceneAnimationFrameBuilder`
- 或等价的内部状态构建接口

该类型输入 `SbSceneFile`、动画选择和 `double frame`，输出：

- 每个节点的 translate X/Y、rotate Z、scale X/Y。
- 每个节点的 display。
- 每个节点的 material RGBA 和 illumination RGBA。
- 每个 image cast 的 primary/secondary image reference index。

抽取后：

- `SbScenePngRenderer.Render` 使用该公共 builder。
- `SceneRenderBuilder.Build` 使用同一 builder。
- Viewer 只负责选择动画、推进帧、请求重建，不负责解释 track type 和插值细节。

这样可以避免 Viewer 和 PNG renderer 在 display、alpha、图片变体索引等行为上产生分歧。

## 边界行为

- 没有动画：底部区域显示 `0 animations`，所有动画操作控件禁用，渲染保持静态。
- 动画名称为空：显示 `ANIM@0x{Offset:X}`，内部仍按索引选择。
- 动画 `EndFrame` 为 `0`：进度条固定在 `0`，播放不会推进。
- `ANIM.0x56` 小于部分 track/key 的最大帧：UI 仍以 `ANIM.0x56` 为结束帧，因为运行时证据显示它是 playback duration/end-frame 候选，而不是所有 key 的严格最大值。
- `ANIM.0x56` 缺失或非法：才使用 track/key 最大帧回退。
- 无 SVO：仍可 seek 和播放，CIMG 继续以占位方式绘制。
- 选中子树预览：应基于同一帧的 `_renderScene` 过滤，不单独重建另一套静态场景。
- 手动显示/隐藏子树：在动画播放过程中持续生效，不因 tick 重建而丢失。

## 实现步骤建议

1. 在 Viewer XAML 中新增动画控制区，并把状态栏移动到下一行。
2. 在 `MainWindow` 中新增动画选择、帧、播放和 loop 状态。
3. 加载 scene 后刷新动画列表；无动画时禁用控件。
4. 实现 `EndFrame` 和默认 loop 的元数据读取。
5. 先让进度条 seek 触发 `RebuildRender`，但仍渲染静态画面。
6. 从 Core 抽取动画帧状态 builder，并让 PNG renderer 迁移到该 builder。
7. 扩展 Viewer `RenderSceneOptions` 和 `SceneRenderBuilder.Build`，在构建 render items 前应用动画状态。
8. 接入 `DispatcherTimer` + `Stopwatch` 播放推进。
9. 补充 Core 动画帧状态测试和 Viewer 手动验证。

## 验证计划

后续实现至少验证以下场景：

- 加载有动画的 sbscene 后，底部区域显示正确动画总数。
- 切换动画会刷新显示名、结束帧、当前帧和默认 loop 状态。
- 播放、暂停、停止按钮状态正确；停止后画面回到第 `0` 帧。
- 循环关闭时播放到结束帧后暂停并夹到 `EndFrame`。
- 循环开启时播放到结束帧后自动回到 `0` 并继续播放。
- 拖动进度条能立即渲染对应帧。
- 小数帧能通过现有插值逻辑渲染，不被 UI 或 options 截断成整数。
- 无动画、无 SVO、隐藏节点、手动显示/隐藏子树、选中子树预览这些现有场景不回归。
- `dotnet build .\SbScene.sln --no-restore` 通过。
- 现有测试通过；若 Debug 输出被正在运行的 Viewer 锁定，可使用 Release 构建验证。

