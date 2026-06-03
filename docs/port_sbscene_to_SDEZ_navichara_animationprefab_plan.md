# 将 sbscene 动画迁移到 SDEZ NaviChara 的计划

目标：把 `.sbscene + .svo` 中的 2D puppet 场景、部件图和动画，转换成 SDEZ/Unity 可使用的 `prefab + AnimatorController + AnimationClip + Sprite`。目标游戏的 Partner/NaviChara 使用 Unity 2018.4.7f1 体系，资源契约参考 `D:\sdez_165\docs\NaviChara-AssetGuide.md`。

## 1. 当前结论

- `SbScene.Cli dump --json` 已能输出完整解析 JSON，包含 nodes、CIMG/CREF 资源、animations/motions/tracks/keyframes；但这是 raw 解析/证据 JSON，不是 Unity importer 稳定中间格式。
- `SbScene.Cli extract-images` 已能从 `.sbscene + .svo` 导出裁剪 PNG 和 manifest，可作为 Sprite 导入输入。
- `SbScene.Cli render --anim ...`、Viewer 和 Core 已采用“每帧从静态/bind 状态重建，再按 animation index 叠加 enabled slots”的机制。动画结束后 slot 保持在最后一帧继续参与渲染，但不锁定字段；后续启用的动画只要写同一 channel，就按 slot/index 顺序覆盖。
- 参考目录 `C:\Users\mikir\Documents\Tencent Files\664659548\FileRecv\sbscene\sbscene` 的 `Surfboard.Runtime.AnimationPlayer` 也是同一模型：先 clone bind transform/cell/size，再逐 enabled animation 应用 track。咱们项目已经按这个方向同步。
- 需要新增面向 Unity 的导出命令，把 sbscene 动画 dump 成“Unity 可消费的中间 JSON + PNG”，而不是让 Unity Editor 直接解析 sbscene。

## 2. 首版边界

首版硬目标：

- 在 Unity Editor 内根据中间 JSON 和导出的 PNG，生成可播放的 NaviChara prefab、AnimatorController 和 6 个核心 AnimationClip。
- 能通过 Animator / `NaviCharaDebugView` 逐个播放验证。
- Full 模式默认生成 prefab/controller/clip；ClipsOnly 模式只生成 controller/clip，并可对已有 prefab 根做路径和组件绑定检查。

首版不作为硬验收：

- AssetBundle 打包、bundle manifest 依赖、数据表接入、放入 SDEZ 游戏实机加载。
- 目录批量导出。
- type 19 secondary surface 可见效果。
- type 29-44 vertex color、更完整的 `CIMG.0x48` blend/surface mode。

首版支持的动画 track：

| sbscene type | 当前语义 | Unity 绑定 | 首版状态 |
| ---: | --- | --- | --- |
| 0 | TranslateX | `RectTransform.m_AnchoredPosition.x` | 支持 |
| 1 | TranslateY | `RectTransform.m_AnchoredPosition.y` | 支持 |
| 5 | RotateZ | `Transform.localEulerAnglesRaw.z` / Euler Z 曲线 | 支持，默认乘 `-1` |
| 6 | ScaleX | `Transform.m_LocalScale.x` | 支持 |
| 7 | ScaleY | `Transform.m_LocalScale.y` | 支持 |
| 11 | Display | `GameObject.m_IsActive` | 支持，step 曲线 |
| 12 | Image width | `RectTransform.m_SizeDelta.x` | 支持 |
| 13 | Image height | `RectTransform.m_SizeDelta.y` | 支持 |
| 18 | Primary image index | `MultipleImage._selectSpriteIndex` | 支持，正式路径 |
| 19 | Secondary image index | 需要 secondary surface 组件/材质 | 首版 raw + diagnostics，后续 TODO |
| 21-24 | Material RGBA | `Graphic.m_Color.r/g/b/a` | 支持，与 illumination 合成输出 |
| 25-28 | Illumination RGBA | 烘焙到 `Graphic.m_Color.r/g/b/a` | 支持；按 `material + illumination * alpha` 合成 |
| 29-44 | Vertex color RGBA | UGUI Image 无直接等价 | raw + diagnostics |
| 2/3/4/8 | 3D Z/X/Y/ScaleZ 候选 | 通常 2D 不使用 | 非默认值写 diagnostics |

## 3. NaviChara 资源契约

基础 NaviChara 至少生成：

- 1 个 prefab：根节点 `UI_Navichara_XX`，挂 `Animator` 和 `NavigationCharacter`。
- 1 个 controller：`UI_NaviChara_XX.controller`。
- 6 个核心 clips：`Navi_Default`、`Navi_Welcom`、`Navi_Fun_Start`、`Navi_Fun_Loop_01`、`Navi_Fun_End`、`Navi_Sad_01`。
- 7 个 Animator states：上述 6 个加 `Navi_Fun_Loop_02`，其中 `Navi_Fun_Loop_02` 的 motion 复用 `Navi_Fun_Loop_01.anim`。
- bool 参数 `IsClear`。
- `NavigationCharacter` 字段绑定：Animator、`_emotionObject`、`_default`、`_funStart`、`_funLoop`、`_funEnd`、`_sad`。

首版 prefab 层级复刻 sbscene 原始节点树，只在外层补 SDEZ NaviChara 必需结构：

```text
UI_Navichara_XX
├── Null_UI_Navichara_XX
│   └── MoveObject
│       └── <sbscene 原始节点树>
└── Null_EFF_Emotion
```

这样 `nodeId -> unityPath` 可稳定从 sbscene 节点树推导，AnimationClip 曲线绑定不需要先做人工部件重组。贴近官方 NaviChara 命名或结构的重构放第二阶段。

Animator 模板：

| State | Motion | Loop | 说明 |
| --- | --- | --- | --- |
| `Navi_Default` | `Navi_Default.anim` | 是 | 默认待机 |
| `Navi_Welcom` | `Navi_Welcom.anim` | 否 | 播完保持末帧 |
| `Navi_Fun_Start` | `Navi_Fun_Start.anim` | 否 | 可 exit 到 `Navi_Fun_Loop_01` |
| `Navi_Fun_Loop_01` | `Navi_Fun_Loop_01.anim` | 是 | 过渡用循环 |
| `Navi_Fun_End` | `Navi_Fun_End.anim` | 否 | 由 `IsClear` 分支 |
| `Navi_Sad_01` | `Navi_Sad_01.anim` | 是 | 悲伤循环 |
| `Navi_Fun_Loop_02` | `Navi_Fun_Loop_01.anim` | 是 | 代码按 state hash 直接 Play |

## 4. CLI 设计

推荐新增命令：

```powershell
dotnet run --project src\SbScene.Cli -- export-unity-navichara `
  <scene.sbscene> <scene.svo> `
  --out out\navichara\<name> `
  --character-id 27 `
  --fashion 1 `
  --accessory 2 `
  --profile docs\navichara-export-profile.json `
  --extract-sprites
```

完整参数草案：

```text
SbScene.Cli export-unity-navichara <sbscene> <svo> --out <dir>
  [--character-id <n>]
  [--profile <json>]
  [--map <sourceAnimation=targetClip>]
  [--write-profile-template <json>]
  [--auto-map]
  [--fashion <frame>]
  [--accessory <frame>]
  [--position <frame>]
  [--allow-placeholder-clips]
  [--bake-sampled-curves]
  [--extract-sprites]
  [--write-validation-frames]
  [--strict]
  [--raw-json <out>]
```

规则：

- 首版只支持单个 `.sbscene + .svo` 输入，不做目录批量导出。
- `dump --json` 不改造成 Unity schema，继续作为 raw 解析证据。
- 正式动作映射以 `--profile` 或显式 `--map <sourceAnimation=targetClip>` 为准。
- CLI 可内置低置信度候选映射，但只在 `--write-profile-template` 或 `--auto-map` 的 diagnostics/template 中输出，不静默决定正式映射。
- `--write-profile-template out\profile.json` 只生成候选 profile，不导出 Unity 中间文件。模板应列出全部 animation 名、index、endFrame、`ANIM.0x5F` default repeat flag、track 摘要和候选目标 clip。
- `--fashion`、`--accessory`、`--position` 是常用固定状态 slot 的快捷参数。它们默认全局作用到 6 个核心目标 clip；`--position` 不默认启用，只有显式传入才加入 `Change_Position[frame]`。
- 如果同时提供 `--profile` 和快捷参数，快捷参数只覆盖对应固定状态 slot，并在 diagnostics 中记录最终展开后的 `sourceSlots`。
- 缺核心 clip 映射默认失败；只有显式 `--allow-placeholder-clips` 时才生成 1 帧静态/bind pose 占位 clip，且 diagnostics 必须标明占位。
- unsupported track 默认写 diagnostics 并继续导出；`--strict` 下只要存在 `warning/high` 级 unsupported 或映射不完整就失败。
- `--bake-sampled-curves` 输出 `curveBakeMode = "sampled60"`，按 60fps 采样动态曲线，作为 keyed/tangent 不匹配时的视觉兜底。
- `--write-validation-frames` 为每个目标 clip 输出 CLI renderer 基准 PNG，必须按目标 clip 的最终 `sourceSlots` 合成结果渲染。

输出目录：

```text
out/navichara/<name>/
├── navichara-export.json
├── raw-dump.json                 # 可选，等价 dump --json
├── sprites/
│   ├── Mouth_primary_000.png
│   ├── node_0042_primary_001.png
│   └── ...
├── validation/
│   └── Navi_Fun_Start/
│       ├── f000.png
│       └── f015.png
└── diagnostics.md
```

## 5. Profile 与 Clip 合成语义

基础 profile 示例：

```json
{
  "settings": {
    "pixelsPerUnit": 1.0,
    "curveBakeMode": "keyed",
    "rootTransform": {
      "scale": 1.0,
      "offset": { "x": 0, "y": 0 }
    }
  },
  "clips": {
    "Navi_Default": {
      "loop": true,
      "durationFrames": "autoMax",
      "sourceSlots": [
        { "animation": "Change_Fashion", "frame": 1 },
        { "animation": "Change_Accessory", "frame": 2 },
        { "animation": "Action_Wait1", "frame": "curve", "repeat": false }
      ]
    },
    "Navi_Fun_Start": {
      "loop": false,
      "durationFrames": "autoMax",
      "sourceSlots": [
        { "animation": "Change_Fashion", "frame": 1 },
        { "animation": "Change_Accessory", "frame": 2 },
        { "animation": "Action_Joy3", "frame": "curve", "repeat": false }
      ]
    },
    "Navi_Fun_Loop_01": {
      "loop": true,
      "durationFrames": "autoMax",
      "sourceSlots": [
        { "animation": "Change_Fashion", "frame": 1 },
        { "animation": "Change_Accessory", "frame": 2 },
        { "animation": "Action_Joy3_Loop", "frame": "curve", "repeat": true }
      ]
    }
  }
}
```

合成规则：

- `Change_Position` 不作为默认 bake slot；确实需要站位切换时由 profile 或 `--position` 显式加入。
- `Mouth_*` 是否合入目标 clip 由 profile 显式指定，不自动推断。
- 固定状态 slot 是整条目标 clip 的静态基底，不是只写第 0 帧。导出器先在固定 slot 指定帧求状态，再让 `"curve"` slot 覆盖同一 node/channel。
- 固定 slot 写到、但动态 slot 未覆盖的属性，需要在 Unity clip 开头和结尾写同值，必要时在主动作关键时间点补同值，避免播放中回落到 prefab 默认值。
- 允许多个 `"curve"` slot，例如 `Action_*[curve] + Mouth_*[curve]`。导出器按 `sourceSlots` 数组顺序叠加，后面的 slot 覆盖前面的同一 node/channel，最终输出一个合成 AnimationClip。
- 目标 clip 长度默认 `durationFrames = "autoMax"`，取所有 `"curve"` source slot 的最大 duration/endFrame；profile 可用整数覆盖。
- 较短的 curve slot 超过自身 endFrame 后默认保持末帧；只有该 source slot 显式 `repeat: true` 时才按自身 duration wrap。
- source slot repeat 独立于目标 Unity clip loop。目标 clip `loop=true` 只控制 Unity state/clip 循环。
- sbscene `ANIM.0x5F default repeat flag` 只在 profile template 中作为提示展示，不作为 source slot repeat 默认值。
- Prefab 保存 sbscene 静态初始值。Unity clip 只写 source slot 实际触及的 node/channel；未触及属性不写进 clip，继续使用 prefab 默认值。

非 loop clip 播放到末帧后保持状态；切到其它 state 后，同属性可被新 state/clip 覆盖。这是保持状态，不是锁定。

## 6. 中间 JSON Schema

新增格式命名为 `sbscene.unityNavicharaExport.v1`，只暴露 Unity importer 需要的数据，同时保留 raw 追溯字段。

顶层示例：

```json
{
  "schema": "sbscene.unityNavicharaExport.v1",
  "source": {
    "sbscene": "MM_CH_Shama__Shama_00.sbscene",
    "svo": "MM_CH_Shama.svo",
    "sceneHash": "...",
    "exporterVersion": "..."
  },
  "settings": {
    "sampleRate": 60,
    "coordinateSystem": "sbscene-y-down-to-unity-y-up",
    "rotationZMultiplier": 1.0,
    "pixelsPerUnit": 1.0,
    "curveBakeMode": "keyed",
    "preserveSourceCoordinates": true,
    "rootTransform": {
      "scale": 1.0,
      "offset": { "x": 0, "y": 0 }
    }
  },
  "character": {
    "id": 27,
    "prefabName": "UI_Navichara_27",
    "controllerName": "UI_NaviChara_27"
  },
  "nodes": [],
  "sprites": [],
  "clips": [],
  "validation": {
    "frameStrategy": "autoQuarters",
    "referenceImageDirectory": "validation"
  },
  "animator": {},
  "diagnostics": []
}
```

### 6.1 `nodes`

Unity 对象名优先保留 sbscene 节点名；只有同级同名冲突时才追加稳定后缀 `__n{nodeId}`，例如 `Mouth__n42`。无冲突节点不加编号后缀。

```json
{
  "id": 42,
  "sbsceneName": "Mouth",
  "unityName": "Mouth__n42",
  "unityPath": "Null_UI_Navichara_27/MoveObject/Mouth__n42",
  "parentId": 12,
  "isImageCast": true,
  "static": {
    "anchoredPosition": { "x": 0, "y": 0 },
    "rotationZ": 0,
    "scale": { "x": 1, "y": 1 },
    "display": true,
    "size": { "x": 128, "y": 64 },
    "pivotPixels": { "x": 64, "y": 32 },
    "pivotNormalized": { "x": 0.5, "y": 0.5 },
    "materialColor": "#FFFFFFFF"
  },
  "image": {
    "component": "MultiSprites",
    "primarySprites": ["sprite_001", "sprite_002"],
    "secondarySprites": [],
    "defaultPrimaryIndex": 0,
    "defaultSecondaryIndex": 0
  }
}
```

CIMG/图片节点必须导出并使用 sbscene pivot。JSON 同时保存 `pivotPixels` 和 `pivotNormalized`。RectTransform 静态 `sizeDelta` 使用 sbscene CIMG 的静态 width/height，type 12/13 继续驱动动态 width/height。PNG crop 尺寸只描述 Sprite 纹理内容，不作为 RectTransform 默认尺寸。

### 6.2 `sprites`

Sprite 文件名首版优先使用 sbscene/SVO 中能追溯到的资源名或节点名。名称缺失、重复或不是安全文件名时退回稳定命名，例如 `node_0042_primary_000.png`。官方 `ui_navichara_XX_pN_MM.png` 风格只作为后续 profile 重命名规则，不默认自动推断。

```json
{
  "id": "sprite_001",
  "name": "Mouth_primary_000",
  "file": "sprites/Mouth_primary_000.png",
  "sourceTexture": "MM_CH_Shama_001",
  "cropIndex": 30,
  "nodeId": 42,
  "slot": "primary",
  "slotIndex": 0,
  "rect": { "x": 10, "y": 20, "w": 128, "h": 64 },
  "pivotPixels": { "x": 64, "y": 32 },
  "pivotNormalized": { "x": 0.5, "y": 0.5 }
}
```

### 6.3 `clips`

```json
{
  "name": "Navi_Fun_Start",
  "sourceSlots": [
    { "animation": "Change_Fashion", "frame": 1 },
    { "animation": "Change_Accessory", "frame": 2 },
    { "animation": "Action_Joy3", "frame": "curve", "repeat": false }
  ],
  "sampleRate": 60,
  "durationFrames": "autoMax",
  "loop": false,
  "validationFrames": [0, 38, 75, 113, 150],
  "curves": [
    {
      "nodeId": 42,
      "path": "Null_UI_Navichara_27/MoveObject/Mouth__n42",
      "sbsceneTrackType": 18,
      "unity": {
        "component": "MultipleImage",
        "property": "_selectSpriteIndex",
        "curveKind": "floatStep"
      },
      "keys": [
        { "frame": 0, "time": 0.0, "value": 0, "interp": "step" },
        { "frame": 8, "time": 0.1333333, "value": 3, "interp": "step" }
      ]
    }
  ],
  "unsupportedTracks": []
}
```

### 6.4 `diagnostics`

导出器和 importer 使用统一 diagnostics 结构。每条至少包含：

```json
{
  "severity": "high",
  "code": "UnsupportedSecondaryImageSlot",
  "targetClip": "Navi_Fun_Start",
  "sourceAnimation": "Action_Joy3",
  "nodeId": 42,
  "nodeName": "Mouth",
  "trackType": 19,
  "message": "type 19 secondary image slot is not imported in v1",
  "suggestion": "Enable future secondary surface support or remove this source slot."
}
```

`severity` 使用 `info`、`warning`、`high`、`error`。CLI 非 strict 模式遇到 `warning/high` 可继续导出；`error` 或 strict 模式下的 `warning/high` 应失败。

## 7. 曲线与坐标规则

坐标：

- 首版保留 sbscene 原始像素坐标和尺寸，不归一化到 NaviChara 官方画布。
- 目标画面适配只通过 `Null_UI_Navichara_XX` 或 `MoveObject` 根节点的 `rootTransform.scale/offset` 调整。
- 不在导出曲线时做 per-node 归一化，否则会破坏与 CLI renderer 的逐帧对比。
- Sprite import 默认 `pixelsPerUnit = 1`，因为 RectTransform `sizeDelta` 使用 sbscene 像素尺寸；目标工程规范要求其它 PPU 时由 profile 显式覆盖。

插值：

- `KEY.0x5C = 0`：step/hold。
- `KEY.0x5C = 1`：linear。
- `KEY.0x5C = 2`：Hermite，当前 key 的 `0x5E` 是 outgoing tangent，下一 key 的 `0x5D` 是 incoming tangent。
- Unity importer 把 frame 转为 `time = frame / 60.0`，并尽量保留 tangent。
- 默认 `curveBakeMode = "keyed"`，保留原始 key/tangent。
- `sampled60` 模式按 60fps 对曲线求值并烘焙 dense keys，用于 Unity tangent 或旋转曲线行为不匹配时的视觉兜底。

旋转：

- RotateZ 首版使用 Euler Z 曲线，不主动生成 quaternion 曲线。
- 默认应用 `rotationZMultiplier = 1.0`，坐标导出会把 Y 向下 scene/source 空间转换为 Unity UI 的 Y 向上空间；PNG renderer/Viewer 的 Y 向下旋转取负规则不能直接复用到 Unity UI。
- Unity 2018 保存 `.anim` 后是否自动改写旋转曲线需要验证；如果发生改写，再补专门写入策略。

图片变体：

- 首版正式路径使用 SDEZ 现有 `MultipleImage/MultiSprites`。
- 多变体 CIMG 填 `MultiSprites[]`，type 18 曲线驱动 `_selectSpriteIndex`。
- 单图节点可以用普通 `Image`。
- 多变体节点找不到 `MultipleImage/MultiSprites` 脚本时，Full 模式默认失败；只有用户显式启用 fallback 才退化为 `Image.sprite` / PPtr 曲线，并写 diagnostics。

Material RGBA：

- type 21-24 首版映射到 `Graphic.m_Color.r/g/b/a`。
- Prefab 写静态 material color；clip 只写 source slot 触及的 channel。
- 需要用 validation PNG 做色彩差异校准。

## 8. Unity Editor Importer

首版在本仓库维护 Unity Editor importer 源码，不由 CLI 自动写入 `D:\sdez_165`。用户手动把 `.cs` 放进 SDEZ Unity 工程的 `Assets/Editor/` 后执行导入。

建议仓库路径：

```text
tools/unity/SbSceneNaviCharaImporter.cs
```

放入 Unity 工程后新增菜单：

```text
Tools/SbScene/Import NaviChara Export...
```

Importer 行为：

1. 选择 `navichara-export.json`。
2. 导入 `sprites/*.png`，设置 TextureImporter 为 Sprite，默认 `pixelsPerUnit = 1`。
3. 根据 `nodes` 构建 prefab 层级：空节点用 `RectTransform`，CIMG 节点优先挂 `MultipleImage/MultiSprites`。
4. 给 MultiSprites 节点填充 `MultiSprites[]` 和默认 `_selectSpriteIndex`。
5. 根据 `clips` 生成 `.anim`：sampleRate 60、loop、曲线绑定。
6. 默认生成 AnimatorController：按 NaviChara 7-state 模板创建 state、transition 和 `IsClear` 参数。
7. Full 模式生成/更新 prefab，绑定 `Animator` 和 `NavigationCharacter` 字段。
8. 可设置 AssetBundle name，但 AB 打包不是首版硬验收。

Importer 模式：

- `Full`（默认）：导入 sprites，生成/更新 prefab、6 个 clip、AnimatorController，并绑定 `Animator` / `NavigationCharacter`。
- `ClipsOnly`：导入 sprites，生成/更新 6 个 clip 和 AnimatorController，不生成或覆盖 prefab。该模式应允许用户指定已有 prefab/根对象用于校验 `unityPath` 和组件绑定是否存在。

`Full` 模式自动绑定 `NavigationCharacter`：

- `_characterNaviAnimator` 指向根 Animator。
- `_animationLayerIndex = 0`。
- `_emotionObject = Null_EFF_Emotion`。
- `_default/_funStart/_funLoop/_funEnd/_sad` 指向对应 clips。
- `Navi_Welcom` 没有对应字段，但仍生成 clip/state。

实现约束：

- Importer 不强类型依赖 `NavigationCharacter` 或 `MultipleImage` 的 C# 类型。
- 通过字符串组件名查找组件，并用 `SerializedObject.FindProperty(...)` 按字段名绑定，例如 `_characterNaviAnimator`、`_emotionObject`、`_selectSpriteIndex`、`MultiSprites`。
- 读取 JSON diagnostics 后默认不阻止 `high` 级问题，但导入完成后必须显示汇总。
- 提供 `Fail on high diagnostics` 选项；开启后遇到 `high/error` 停止导入。
- Importer 不直接读取 `.sbscene/.svo`，只读中间 JSON 和 PNG。

## 9. Validation

CLI validation：

- `--write-validation-frames` 对每个目标 clip 输出基准 PNG。
- 默认 `frameStrategy = "autoQuarters"`，按 clip duration 取 `0, 25%, 50%, 75%, endFrame`，四舍五入为整数帧后去重。
- profile 可覆盖固定帧列表。
- validation PNG 必须按目标 clip 的最终 `sourceSlots` 合成结果渲染，固定 slot、多个 curve slot、repeat 规则都与导出 clip 一致。

本项目侧验证：

- `dotnet build .\SbScene.sln --no-restore`
- `dotnet test .\SbScene.sln --no-restore`
- 对 Ras/Shama/Otohime 样本执行 `export-unity-navichara`，确认 JSON 可序列化、sprites 文件存在、diagnostics 可读。
- 使用 `--strict` 验证已支持 track 范围内没有意外 high severity；非 strict 模式确认有 unsupported 时仍可生成资源。

Unity 侧验证：

- Importer `Full` 模式能在空白测试工程生成 prefab/controller/clip。
- `ClipsOnly` 模式能只生成 controller/clip，并对已有 prefab 根做路径/组件绑定检查。
- `NaviCharaDebugView` 能枚举 clip，逐个 `Animator.Play` 不报错。
- `NavigationCharacter.Play(Default/FunStart/FunStartLoop/Sad01)` 能命中 state。
- 非 loop clip 播放到末帧后保持状态；切到其它 state 后同属性可被覆盖。
- `Navi_Fun_Loop_02` state 存在且复用 `Navi_Fun_Loop_01.anim`。

视觉对比：

- 每个目标 clip 使用 validation frames 对比 Unity GameView 截图与 CLI renderer 基准 PNG。
- 重点检查部件位置、pivot、旋转方向、显隐、表情/口型 index、Material RGBA。
- 若整体左右/上下或旋转方向相反，优先调 export JSON 的坐标策略，不改 raw parser。

## 10. 风险

- Unity text `.anim` 中自定义 MonoBehaviour 字段可能显示成 `script_<hash>`；Editor API 中应使用实际字段名 `_selectSpriteIndex`。
- Importer 使用字符串组件名和 `SerializedObject` 字段绑定，需在目标工程验证字段名。
- RotateZ 输出 Euler Z 曲线；Unity 2018 可能在保存/import 后改写旋转曲线。
- Hermite tangent 到 Unity tangent 的映射需要实测；如 keyed 模式视觉不一致，使用 `sampled60` 验证。
- Material RGBA 与 illumination RGBA 合成后映射到 `Graphic.color`，需要视觉对比校准色彩差异。
- secondary surface、vertex color 和 `CIMG.0x48` blend/surface mode 与 UGUI Image 不完全等价。
- CIMG pivot 不能默认中心点。必须使用导出的 sbscene pivot，并验证 type 12/13 宽高动画围绕同一 pivot 变化。
- `ANIM.0x56` 是 playback duration/end-frame 候选，不一定覆盖所有 key 的最大帧。导出 clip 长度默认用它，但 diagnostics 要列出超出 end frame 的 track/key。
- Sprite 命名首版优先 sbscene/SVO 可追溯名称，冲突退回稳定命名；不要自动伪造官方 `ui_navichara_XX_pN_MM` 分组。

## 11. 推荐实施顺序

1. 固化 `export-unity-navichara` DTO、profile schema 和 diagnostics schema。
2. 复用 `extract-images` 导出 sprites，并在 JSON 中写入 sprite id/path/crop/pivot。
3. 实现节点路径生成、同级重名 `__n{nodeId}` 去重、CIMG size/pivot 输出。
4. 实现 sourceSlots 合成：固定 slot、多个 curve slot、duration autoMax、repeat、placeholder。
5. 实现首批 track 映射：0/1/5/6/7/11/12/13/18/21/22/23/24。
6. 实现 `--write-profile-template`、`--map`、`--fashion/--accessory/--position`、`--strict`。
7. 实现 validation frame 输出，并用 CLI renderer 对合成 clip 出基准 PNG。
8. 在本仓库维护 `tools/unity/SbSceneNaviCharaImporter.cs`。
9. 用户手动放入 SDEZ Unity 工程后生成 prefab/clip/controller。
10. 做关键帧视觉对比，校准 Y/rotation/pivot/size/color。

## 12. 后续 TODO

- 支持 type 19 secondary image slot 的可见效果：结合 `CIMG.0x48` surface mode、primary/secondary stage 组合、blend/UV 规则，决定扩展 `MultipleImage` 还是新增 secondary surface 组件/材质。
- 若烘焙到 `Graphic.color` 仍无法覆盖特殊效果，再为 illumination type 25-28 评估目标侧 `BlendImage`、自定义 shader 或额外组件字段。
- 支持 vertex color 和更完整的 `CIMG.0x48` blend/surface mode。
- 接入 AssetBundle 打包、bundle manifest 依赖和 SDEZ 游戏实机加载链路。
- 支持目录批量导出：每个角色使用独立 profile 或 profile 匹配规则。
