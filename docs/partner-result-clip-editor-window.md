# PartnerResult Clip Editor Window 需求草案

## 目标

新增一个 Unity Editor 窗口，用于手动预览 Navichara prefab，并通过可拖动、可拉伸的 Clip 框截取生成 PartnerResult 图片。

## 已确认需求

- 当前版本只生成 PartnerResult，不生成 128x128 Partner 头像。
- 后续可能扩展为同时支持 Partner 头像生成。
- 保留现有 `Tools/SbScene/Build Partner(Result) Bundles` 菜单。
- 新窗口作为单个 prefab 的手动精修生成工具，不替代现有批量生成流程。
- EditorWindow 菜单入口为 `Tools/SbScene/PartnerResult Clip Editor`。
- 提供 prefab 选中器。
- prefab 选中器只接受 `Assets/AssetBundle/navichara/prefab/UI_Navichara_*.prefab`。
- 从 prefab 文件名中的 `UI_Navichara_{id}` 提取生成用 id。
- 非法 prefab 拖入或选择时，在窗口内显示错误提示，不执行生成。
- 支持将 prefab 拖入 Editor 窗口。
- 拖入 prefab 后，窗口显示整个 GameObject 人物。
- 预览区域需要显示整个人物，便于人工选择截取范围。
- 预览和导出使用离屏渲染，不向当前 Scene 或 Hierarchy 留下临时对象。
- EditorWindow 内部可以创建隐藏 Canvas、Camera 和 prefab 临时实例。
- 每次刷新预览或导出后需要清理临时对象，避免污染当前场景。
- 预览上叠加一个 Clip 框。
- Clip 框外的预览内容显示半透明暗遮罩。
- Clip 框内的预览内容正常显示。
- Clip 框支持拖动。
- Clip 框支持拉伸调整大小。
- Clip 框固定 1:1 宽高比。
- Clip 框用于截取生成 PartnerResult 图片。
- 生成的 PartnerResult 图片放置到 `Assets/AssetBundle/partner/`。
- 生成的 PartnerResult 图片保持透明背景。
- 生成图片时同时生成对应的 AssetBundle 文件。
- Sprite 导入设置复用现有 PartnerResult 生成流程。
- 预览和导出缩放采样使用 `Bilinear`。
- 导出图片沿用现有 PartnerResult 的 Linear 采样处理。
- AssetBundle 命名规则复用现有 PartnerResult 生成流程，使用 `partner/ui_partnerresult_{id:000000}.ab`。
- 实现时复用并适当拆分现有 `SbScenePartnerResultBuilder` 的公共生成逻辑。
- 生成图片尺寸暂定为 `512x512`。
- EditorWindow 提供 `Clip Rect JSON` 导入框，用于拖入通用 Clip Rect JSON 并应用到当前 Clip 框。
- EditorWindow 提供 Clip 框配置保存功能，保存为通用 JSON 预设。
- 选中 prefab 后，默认生成固定默认 Clip 框；不会再按 prefab 自动加载 JSON。
- 保存 Clip 框配置时，只保存 Clip 框的位置和大小。
- Clip 框配置保存到 `Assets/Editor/PartnerResultClipConfigs/`。
- Clip 框配置不再按 prefab 自动命名；保存时弹出保存对话框，由用户输入 JSON 文件名。
- Clip 框配置保存为 prefab 本地坐标空间中的中心点和边长。
- 预览显示 `Navi_Default` 第 0 帧。
- 导出 PartnerResult 图片时也使用 `Navi_Default` 第 0 帧。
- 窗口提供 `Generate Selected` 按钮，用当前 prefab 和当前 Clip 框生成 PartnerResult PNG 与 AssetBundle。
- 点击 `Generate Selected` 时直接生成 PartnerResult PNG 与 AssetBundle，不自动保存 Clip Rect JSON。
- 窗口提供 `Save Rect` 按钮，保存当前 Clip 框的位置和大小。
- 窗口提供 `Set Default Rect` 按钮，将当前 prefab 的 Clip 框重置为默认框。
- 窗口提供 `Center Horizontally` 按钮，将当前 Clip 框水平居中，保留当前垂直位置和大小。
- 窗口提供 `Refresh Preview` 按钮，用于重新渲染当前 prefab 预览。
- 拖入或选择 prefab 时自动生成一次预览。
- 拖动或拉伸 Clip 框时，只重绘遮罩和 Clip 框，不重新渲染人物。
- 只有选择 prefab、点击 `Refresh Preview` 或执行导出时才需要重新渲染人物。
- 生成成功后弹出对话框，显示 PNG 和 AssetBundle 输出路径。
- 生成失败时弹出错误对话框，并在窗口内保留错误信息。
- 默认 Clip 框使用固定位置和固定尺寸，不根据人物自动计算。
- 默认 Clip 框中心为 prefab 本地坐标 `(0, 60)`。
- 默认 Clip 框边长为 prefab 本地坐标 `420`。
- 用户通过拖动和拉伸 Clip 框手动调整截取范围。
- 预览区域支持缩放视图。
- 预览区域支持平移视图。
- 视图缩放和平移只影响 EditorWindow 显示，不影响 Clip 框保存坐标。
- 鼠标左键用于拖动和拉伸 Clip 框。
- 鼠标滚轮用于缩放预览视图。
- 鼠标右键拖动用于平移预览视图。
- 鼠标中键拖动也可作为平移预览视图的备用操作。
- Clip 框四个角用于拉伸缩放。
- Clip 框内部用于拖动整体移动。
- Clip 框边中点不提供拉伸控制。
- Clip 框需要限制最小尺寸，避免误操作缩到过小导致导出异常。
- Clip 框最小尺寸为最终输出 `128px` 等效边长。
- 生成时如果目标 PNG 或 AssetBundle 已存在，直接覆盖，不弹确认。
- 需求沟通过程中，本文件需要实时更新。

## 待确认问题


## 推荐默认方案

- 当前实现聚焦 PartnerResult，暂不混入 Partner 头像边框和头部截图逻辑。
- 代码结构预留后续增加 Partner 头像生成的扩展点。
- 旧批量菜单继续保留；新窗口只增加手动 Clip 调整能力。
- Clip 框固定 1:1，因为 PartnerResult 目标图片是 512x512。
- 预览区域显示全身，Clip 框默认覆盖上半身。
- 导出 PNG 暂定固定为 512x512。
- 导出 PNG 使用透明背景。
- 预览和导出使用隐藏 Canvas、Camera 与临时 prefab 实例完成，不修改当前场景。
- 预览中 Clip 框外使用半透明暗遮罩，便于确认最终截取区域。
- 输出路径使用 `Assets/AssetBundle/partner/UI_PartnerResult_{id:000000}.png`。
- AssetBundle 名称复用现有 `partner/ui_partnerresult_{id:000000}.ab`。
- Sprite 导入设置复用现有 PartnerResult 生成流程，避免游戏加载差异。
- 图片缩放采样使用 `Bilinear`，并保持现有 Linear 处理策略。
- 将现有 PartnerResult PNG 导出、Sprite 导入、AssetBundle 构建逻辑拆成可复用方法，供 EditorWindow 调用。
- Clip 框配置保存到 `Assets/Editor/PartnerResultClipConfigs/` 下的通用 JSON 文件，便于复用到不同 prefab。
- Clip 框配置建议保存字段为 `centerX`、`centerY`、`size`，坐标基准为 prefab 本地坐标空间。
- 预览和导出都采样 `Navi_Default` 第 0 帧，保持所见即所得。
- 窗口按钮使用 `Generate Selected`、`Save Rect`、`Set Default Rect`、`Center Horizontally`。
- 预览刷新按钮使用 `Refresh Preview`。
- `Save Rect` 弹出保存对话框，由用户输入 JSON 文件名。
- 生成完成后使用弹窗反馈结果。
- 拖框过程中不重新渲染人物，只刷新 EditorWindow 绘制层，保证交互流畅。
- 默认 Clip 框使用固定中心点 `(0, 60)` 和边长 `420`，避免隐式自动推断；实现后可按实际预览微调默认值。
- 预览使用鼠标滚轮缩放，右键拖动平移，中键拖动也支持平移。
- Clip 框使用四角缩放、内部拖动移动，减少边框误操作。
- Clip 框应设置最小尺寸限制，避免生成空图或极端放大图。
- Clip 框最小尺寸建议按最终输出 `128px` 等效边长处理。
- 生成时直接覆盖既有 PNG 和 AssetBundle。

## 已确认决策

- 当前版本暂不考虑 Partner 头像生成，但后续可能一起实现。
- 保留现有 `Build Partner(Result) Bundles` 菜单。
- 菜单入口为 `Tools/SbScene/PartnerResult Clip Editor`。
- 导出背景保持透明。
- EditorWindow 使用离屏渲染，不污染当前 Scene/Hierarchy。
- 预览中 Clip 框外压暗，Clip 框内正常显示；导出只取 Clip 框内内容。
- 仅允许选择或拖入 `Assets/AssetBundle/navichara/prefab/UI_Navichara_*.prefab`。
- 生成 id 从 prefab 文件名提取。
- Clip 框固定为正方形 1:1。
- PNG 输出目录为 `Assets/AssetBundle/partner/`。
- 生成 PNG 时同步生成对应的 AssetBundle 文件。
- Sprite 导入设置和 AssetBundle 命名规则复用现有 PartnerResult 生成流程。
- 预览和导出缩放采样使用 `Bilinear`，导出沿用 Linear 采样处理。
- 新 EditorWindow 复用现有 `SbScenePartnerResultBuilder` 的生成流程公共逻辑，不单独维护另一套导出实现。
- 输出图片尺寸暂定为 `512x512`。
- EditorWindow 需要支持通过 `Clip Rect JSON` 导入框加载通用 JSON，并支持保存通用 JSON。
- 选择 prefab 时，自动生成默认 Clip 框。
- Clip 框配置只保存位置和大小。
- Clip 框配置目录为 `Assets/Editor/PartnerResultClipConfigs/`。
- Clip 框配置文件由用户保存时输入名称，不按 prefab 自动命名。
- Clip 框配置坐标基准为 prefab 本地坐标空间，保存中心点和边长。
- 预览和导出使用 `Navi_Default` 第 0 帧。
- 需要的按钮为 `Generate Selected`、`Save Rect`、`Set Default Rect`、`Center Horizontally`、`Refresh Preview`。
- `Generate Selected` 不自动保存 Clip 框配置。
- 选择 prefab 时自动生成预览；点击 `Refresh Preview` 可手动重新生成预览；拖动 Clip 框不重新渲染人物。
- 生成成功弹窗显示输出路径；失败弹窗显示错误，并在窗口内保留错误信息。
- 默认 Clip 框采用固定位置和固定尺寸，由用户手动拖动和拉伸。
- 默认 Clip 框中心为 `(0, 60)`，边长为 `420`。
- 预览视图支持缩放和平移，且不改变 Clip 框保存坐标。
- 鼠标左键编辑 Clip 框，滚轮缩放预览，右键拖动平移预览，中键拖动也可平移预览。
- Clip 框四角拉伸，框内拖动移动，不做边中点拉伸。
- Clip 框最小尺寸为最终输出 `128px` 等效边长。
- 生成输出直接覆盖已有文件，不弹覆盖确认。

## 实现状态

- 已新增 `SbScenePartnerResultClipEditorWindow` EditorWindow。
- 已新增 `Tools/SbScene/PartnerResult Clip Editor` 菜单入口。
- 已实现 prefab 选择和拖入校验。
- 已实现 `Navi_Default` 第 0 帧离屏预览。
- 已实现预览缩放、平移、Clip 框拖动和四角拉伸。
- 已实现当前 Clip 框水平居中按钮。
- 已实现通用 Clip 框 JSON 保存和拖入导入。
- 已实现 `Generate Selected` 生成 PartnerResult PNG 和对应 AssetBundle。
- 已保留现有 `Build Partner(Result) Bundles` 菜单。
