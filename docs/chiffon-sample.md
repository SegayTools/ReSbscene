# Chiffon 样本记录

当前新增第二个本地样本用于交叉验证：

- `.sbscene`: `D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard_EN\MM_CH_Chiffon\MM_CH_Chiffon__Chiffon_00.sbscene`
- `.svo`: `D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard_EN\MM_CH_Chiffon\MM_CH_Chiffon.svo`

## Snapshot

| 项 | 值 |
| --- | --- |
| 文件大小 | 1,544,291 bytes |
| VTBF 根 | `SRFF raw=0x0100004C low=1 high=19456` |
| 总块数量 | 32,150 |
| NODE/TRS2/NCAT | 297 / 297 / 297 |
| ANIM | 32 |
| TRK | 14,254 |
| `TRK.0x57` 与 key count 匹配 | 14,254 / 14,254 |
| Image cast | 207 |
| DATA ParamLow / primary resource blocks | 207 / 207，匹配；该样本中也等于 CIMG 数 |
| `CIMG.0x44` count 校验 | 207 / 207，匹配 |
| `CIMG.0x45` index 范围 | 207 / 207 非空 CREF 组，0 越界 |
| CROP | 205 |
| Crop references | 225（primary 225，secondary 0） |
| Multi-reference image cast | 13 |
| Variant hints | 6,592 |
| Unknown type code | 0 |

## 新增格式证据

Chiffon 首次暴露 `TRK.0x54` 的 extra mask：除 Ras 已见的 `0x13/0x23/0x33/0x43` 外，还出现 `0x113/0x133/0x143`。

| Flags | Base byte | Extra mask | Tracks | Storage |
| ---: | ---: | ---: | ---: | --- |
| `0x113` | `0x13` | `0x100` | 4 | `Float32Curve+Extra0x100` |
| `0x133` | `0x33` | `0x100` | 1 | `BoolState+Extra0x100` |
| `0x143` | `0x43` | `0x100` | 1 | `PackedAngleCandidateCurve+Extra0x100` |

这说明 `TRK.flags` 不能再简单按 `flags >> 4` 当整体 high nibble。当前解析器按 `base byte = flags & 0xFF` 判断 key value storage，并把 `extra mask = flags & ~0xFF` 单独输出。

`extra=0x100` 的 6 条 track 全部位于 `Action_Wait3 -> smile`，节点 index 为 289，`NODE.0x30=0xE01`，group 为 `(ungrouped)`，初始 `display=false`，6/6 条 track 的目标都是同一个 CIMG 节点（`CIMG.0x48=0x00408000`）。这 6 条 track 覆盖同一节点的 `TranslateX/TranslateY/RotateZ/ScaleX/ScaleY/Display`：

| Track type | Flags | Frames | Keys |
| --- | ---: | --- | --- |
| `0(TranslateX)` | `0x113` | `0..50` | `0:-7` |
| `1(TranslateY)` | `0x113` | `0..50` | `0:0` |
| `5(RotateZ)` | `0x143` | `0..50` | `0:-19.995deg, 27:-19.995deg, 28:-29.998deg, 41:-29.998deg, 49:-29.998deg, 50:-19.995deg` |
| `6(ScaleX)` | `0x113` | `0..50` | `0:1` |
| `7(ScaleY)` | `0x113` | `0..50` | `0:1, 7:1.1, 14:1, 27:1, 34:1.1, 41:1, 50:1` |
| `11(Display)` | `0x133` | `0..0` | `0:false` |

在 Chiffon 单样本内，`0x100` 与该 hidden smile CIMG 节点的 transform/display 轨道共现；但 full survey 已显示 `0x100` 也覆盖 UI/loop 和非 CIMG 目标，因此这里不再给它命名为 per-node/action special flag，只记录 raw extra mask。

Chiffon 的 `NODE.0x30` flags 与 Ras 同构，但没有 `0x10F01` 特例：

| Flags | Nodes | Image casts | Animated nodes | Display=false | 观察 |
| ---: | ---: | ---: | ---: | ---: | --- |
| `0x900` | 6 | 0 | 0 | 0 | present heart 的 null/container 节点。 |
| `0xE00` | 19 | 0 | 19 | 19 | 初始隐藏的 container/组合节点。 |
| `0xE01` | 52 | 52 | 52 | 52 | 初始隐藏且带 CIMG 的节点。 |
| `0xF00` | 64 | 0 | 61 | 0 | 普通 container/骨架节点。 |
| `0xF01` | 155 | 155 | 151 | 0 | 带 CIMG 且初始 display=true 的节点。 |
| `0x8F00` | 1 | 0 | 1 | 0 | 根/控制节点 `Chiffon_Root`。 |

`flagBits` 交叉统计：

| Bit | Mask | Nodes | Image casts | Animated nodes | Display=false | 候选语义 |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| 0 | `0x00000001` | 207 | 207 | 203 | 52 | CIMG-backed 节点候选。 |
| 8 | `0x00000100` | 226 | 155 | 213 | 0 | 常见节点属性；区分 `0xFxx` 与 `0xExx`。 |
| 9 | `0x00000200` | 291 | 207 | 284 | 71 | 常见节点属性；当前只缺于 `0x900` null 节点。 |
| 10 | `0x00000400` | 291 | 207 | 284 | 71 | 与 bit 9 分布相同，具体语义未知。 |
| 11 | `0x00000800` | 297 | 207 | 284 | 71 | 所有 Chiffon `NODE.0x30` 都设置的 common bit。 |
| 15 | `0x00008000` | 1 | 0 | 1 | 0 | `0x8F00` 根/控制节点候选。 |

## 交叉验证结论

- `0x0B` fixed-angle 候选继续成立：`KEY.0x5B type 0x0B` 有 5,981 keys / 2,805 tracks，主要是 `5(RotateZ)`；raw `910/-910/1820` 继续对应约 `5/-5/10 deg`。
- `KEY.0x5D == 0x5E` 在 Chiffon 20,840 个带 tangent 字段的 key 上全部成立；非零 tangent 为 2,351 个，其中 interpolation 0 为 0、1 为 485、2 为 1,866。
- `CAM.0x14` 仍为 `0x1FFF`，继续排除出角度换算上下文。
- `CIMG.0x48` 在 Chiffon 内继续呈现局部共现：bit 0 是 bit 22 子集，bit 23 是 bit 20 子集；后续 Otohime/full survey 已证明这些不能写成全局子集语义。
- 与 Ras 不同，Chiffon 没有 secondary CREF image cast；`0x45` 非零仍全部落在 primary refs 范围内。
- `TRK.flags` low nibble 仍全为 `0x3`。

## SVO 资源

`inspect-svo` 解析结果：

| 项 | 值 |
| --- | --- |
| AVTS directory entries | 5 |
| DDS textures | 4 |
| AVTS header unknown non-zero bytes | 51 |
| YABX headerHashCandidate | `0x69AE571D` |
| YABX objects | 14 / 14 |
| YABX object coverage | `parsedBytes=1434/1434`, `unparsedBytes=0` |

切图结果：

| Atlas | Crops |
| --- | ---: |
| `MM_CH_Chiffon_000_0` | 55 |
| `MM_CH_Chiffon_001_1` | 95 |
| `MM_CH_Chiffon_002` | 31 |
| `MM_CH_Effect_000` | 24 |

`extract-images` 输出 4 张 atlas PNG 和 205 张 crop PNG，映射 207 个 image cast。两个 effect crop 越界，已按透明 padding 处理。
