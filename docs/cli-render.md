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

可以追加 `--animation Name@Frame` 覆盖或补充状态；后指定的动画会在默认动画之后应用。

常用选项：

- `--background transparent|#RRGGBB|#AARRGGBB`
- `--scale <n>`
- `--padding <px>`
- `--show-hidden`
- `--render-secondary`

## 渲染约定

`TRS2.0x32` 和 RotateZ 动画 key 的 packed-angle 候选先按 `degrees = raw * 180.0 / 32768.0` 解码为 scene/source 角度。PNG renderer 在 2D 像素坐标（Y 向下）中构造矩阵时使用 `-degrees`，与 Viewer 的 Transform2D 绘制路径保持一致。

动画 track 求值按运行时代码的 `KEY.0x5C` 规则处理：`0` 为 step/hold，`1` 为 linear，`2` 为三次 Hermite spline。Hermite 段使用当前 key 的 `0x5E` 作为 outgoing tangent、下一 key 的 `0x5D` 作为 incoming tangent，因此中间帧的 Translate/Rotate/Scale 位移不再按简单线性插值估算。

节点树渲染会沿父链组合 display 和 opacity：父节点最终 hidden 时子节点不绘制；父节点 `TRS2.0x37` material alpha / type 24 alpha 为 0 时，子图片的 effective opacity 也为 0。`--show-hidden` 只绕过 display hidden，不绕过 alpha/opacity。
