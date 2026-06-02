# CLI PNG 渲染

`render` 命令把 `.sbscene` 和对应 `.svo` 渲染为 PNG。它不依赖 Viewer/WPF，适合批量导出。

## 单文件

如果 `.sbscene` 所在目录只有一个 `.svo`，可以省略 SVO 参数：

```powershell
dotnet run --project src/SbScene.Cli -- render `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras__Ras_00.sbscene" `
  --out out\ras-default.png `
  --character-defaults
```

目录里没有 `.svo` 或有多个 `.svo` 时，需要显式传入：

```powershell
dotnet run --project src/SbScene.Cli -- render `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras__Ras_00.sbscene" `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras.svo" `
  --out out\ras-default.png `
  --character-defaults
```

## 批量目录

目录模式会递归查找 `.sbscene`，并要求每个 `.sbscene` 同目录下恰好有一个 `.svo`。输出文件名会按目录和文件名生成，避免同名覆盖。

```powershell
dotnet run --project src/SbScene.Cli -- render `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard" `
  --filter MM_CH_ `
  --out out\mmch-defaults `
  --character-defaults
```

如果目录里匹配到文件但全部因 SVO 缺失或不唯一而跳过，命令返回非零退出码。

## 默认角色状态

`--character-defaults` 会按 frame 0 应用这些动画：

- `Change_Fashion`
- `Change_Position`
- `Change_Accessory`
- `Action_Wait1`
- `Mouth_Wait1`

可以追加 `--anim Name[Frame]` 或旧写法 `--animation Name@Frame` 覆盖或补充状态；也可以用 `--anim #Index[Frame]` 精确指定 animation slot。CLI 会按 scene 中的 animation index 顺序叠加所有启用槽，同一槽多次指定时最后一次 frame 生效；这和 Viewer/运行时的 enabled animation slot 模型一致。

常用选项：

- `--background transparent|#RRGGBB|#AARRGGBB`
- `--scale <n>`：直接放大输出画布，例如 `--scale 2` 会输出 2 倍尺寸 PNG。
- `--sampling nearest|bilinear`：图层采样方式；默认 `nearest`，`bilinear` 可减少旋转/缩放时的锯齿。
- `--supersample <n>`：先按 `n` 倍内部尺寸渲染，再降采样回目标尺寸；范围 `1..8`，适合同尺寸抗锯齿输出。
- `--high-quality`：等价于 `--sampling bilinear --supersample 4`。
- `--padding <px>`
- `--show-hidden`
- `--render-secondary`（兼容旧参数；secondary CREF 只会在 `CIMG.0x48` surface mode 实际启用 secondary stage 时参与运行时渲染）

高清导出示例：

```powershell
dotnet run --project src/SbScene.Cli -- render `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Salt\MM_CH_Salt__Salt_00.sbscene" `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Salt\MM_CH_Salt.svo" `
  --out out\salt-touch1-hq.png `
  --anim #1[0] `
  --anim #2[0] `
  --anim #3[0] `
  --anim Action_Touch1[20] `
  --anim Mouth_Touch1[8] `
  --high-quality
```

例如只想指定服装状态和动作帧，可以写：

```powershell
--anim Change_Fashion[2] --anim Action_Joy3[10]
```

这会启用 `Change_Fashion` 和 `Action_Joy3` 两个槽；渲染时按 animation index 先应用 selector，再应用更高 index 的动作槽。若两个动画写同一个节点的同一类 track，后 index 的槽会覆盖前 index 的同类状态；未被 Action 覆盖的 `Change_*` 状态会保留在最终渲染状态中。

注意这里的覆盖顺序由 animation slot index 决定，而不是命令行顺序。标准角色样本中 `Change_Fashion` 在 slot 1，`Action_*` 通常在更高 slot，因此动作轨道会覆盖 selector 的同类轨道；如果同一个 slot 在命令行里出现多次，才由最后一次指定的 frame 覆盖前面的 frame。

如果需要更大的 PNG，同时保留抗锯齿，可以组合：

```powershell
--scale 2 --sampling bilinear --supersample 4
```

## 渲染约定

`TRS2.0x32` 和 RotateZ 动画 key 的 packed-angle 候选先按 `degrees = raw * 180.0 / 32768.0` 解码为 scene/source 角度。PNG renderer 在 2D 像素坐标（Y 向下）中构造矩阵时使用 `-degrees`，与 Viewer 的 Transform2D 绘制路径保持一致。

动画 track 求值按运行时代码的 `KEY.0x5C` 规则处理：`0` 为 step/hold，`1` 为 linear，`2` 为三次 Hermite spline。Hermite 段使用当前 key 的 `0x5E` 作为 outgoing tangent、下一 key 的 `0x5D` 作为 incoming tangent，因此中间帧的 Translate/Rotate/Scale 位移不再按简单线性插值估算。

节点树渲染会沿父链组合 display 和 opacity：父节点最终 hidden 时子节点不绘制；父节点 `TRS2.0x37` material alpha / type 24 alpha 为 0 时，子图片的 effective opacity 也为 0。`--show-hidden` 只绕过 display hidden，不绕过 alpha/opacity。

颜色管线按运行时已确认的路径处理：纹理颜色先乘 `TRS2.0x37` material RGB 和 `TRS2.0x39` 四角顶点色插值，再叠加 `TRS2.0x38` illumination RGB * illumination alpha；type 21/22/23、25/26/27/28 动画会分别更新 material / illumination 通道，type 29..44 会更新四角 vertex color RGBA。PNG renderer 按 `CIMG.0x48` 低 4 位识别 draw/blend mode，mode `1` 使用 additive/effect blend（RGB 累加，alpha 取 max）。

`CIMG.0x48` 的 surface mode 按 `flags & 0x7800` 解码：mode `0` 只使用 primary surface；mode `1` 使用 secondary surface 作为 stage0；mode `2/3/4` 使用 primary stage0 加 secondary stage1 组合。当前标准 6 个角色样本均为 mode `0`，因此带 secondary CREF 的 CIMG 默认不会把 secondary 当独立图层绘制。
