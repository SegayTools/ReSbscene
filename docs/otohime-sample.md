# Otohime 样本记录

第三个本地样本用于交叉验证：

- `.sbscene`: `D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard_EN\MM_CH_Otohime\MM_CH_Otohime__Otohime_00.sbscene`
- `.svo`: `D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard_EN\MM_CH_Otohime\MM_CH_Otohime.svo`

## Snapshot

| 项 | 值 |
| --- | --- |
| 文件大小 | 2,962,449 bytes |
| VTBF 根 | `SRFF raw=0x0100004C low=1 high=19456` |
| 总块数量 | 63,501 |
| NODE/TRS2/NCAT | 501 / 501 / 501 |
| ANIM | 32 |
| TRK | 28,167 |
| `TRK.0x57` 与 key count 匹配 | 28,167 / 28,167 |
| Image cast | 380 |
| DATA ParamLow / primary resource blocks | 380 / 380，匹配；该样本中也等于 CIMG 数 |
| `CIMG.0x44` count 校验 | 380 / 380，匹配 |
| `CIMG.0x45` index 范围 | 380 / 380 非空 CREF 组，0 越界 |
| CROP | 348 |
| Crop references | 418（primary 417，secondary 1） |
| Multi-reference image cast | 22 |
| Variant hints | 14,602 |
| Unknown type code | 0 |

## 新增格式证据

Otohime 首次暴露 `NODE.0x30=0x901`：

| Flags | Bits | Nodes | Image casts | Animated nodes | Display=false | 观察 |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| `0x901` | `0,8,11` | 4 | 4 | 4 | 0 | present 派生节点，均有 CIMG 且初始显示。 |

这继续支持 bit 0 是 CIMG-backed 节点候选；`0x901` 没有 bit 9/10，说明 exact flags 不能只按 `0xE01/0xF01` 分类。

`CIMG.0x48` 也修正了 bit20/bit23 的关系：Ras/Chiffon 中 bit 23 是 bit 20 子集，但 Otohime 有 1 个 `0x00808000`（`hair_top_A01`），即 bit 23 可以不带 bit 20。后续 loader/xref 复核显示 `0x48` 是跨资源复用的 packed state word，因此当前只记录 bit20/bit23 常共现，不把子集方向或具体渲染语义写成事实。

| CIMG raw value | Bits | Casts | 观察 |
| ---: | --- | ---: | --- |
| `0x00408001` | `0,15,22` | 23 | bit 0 仍是 bit 22 子集；新增大量 eye highlight 与 `_add` 命名节点。 |
| `0x00808000` | `15,23` | 1 | 打破 bit 23 永远从属于 bit 20 的候选。 |
| `0x00908000` | `15,20,23` | 161 | 主要 hair/clothes/body 类节点。 |

## Track Extra

Otohime 将 `TRK.flags extra=0x100` 从 Chiffon 的 6 条扩展到 63 条：

| Flags | Tracks | Storage |
| ---: | ---: | --- |
| `0x113` | 60 | `Float32Curve+Extra0x100` |
| `0x133` | 1 | `BoolState+Extra0x100` |
| `0x143` | 2 | `PackedAngleCandidateCurve+Extra0x100` |

这些 track 分布于 `Action_Wait4`、`Action_Joy3`、`Action_Happy1`、`Action_Touch1/2`、`Action_Sad1`，主要节点是 `eye_hi_*` 高光点和 `puru_a`。新增交叉统计显示 63/63 条目标都是 CIMG 节点，node flags 为 `0xF01:61, 0xE01:2`，group 为 `eye:48, puru:13, arm:2`，初始 display=false 为 2/63。它不改变 key value storage；在 Otohime 单样本内，63/63 条目标都是上述动作局部 CIMG 节点，但 full survey 已显示该分布不能全局化。

## 颜色 Track 证据

Otohime 的 `Change_Fashion -> momo_circle_*` 首次把 type 21/22/23 扩展成多 key 颜色曲线。四个节点的 `TRS2.0x37` 材质色按 `A,R,G,B` 候选解释后，RGB 归一化值与 track value 集合形成候选对应；部分通道相差一个 8-bit step：

| Node | Material `#AARRGGBB` | R/G/B normalized | 对应 track value |
| --- | --- | --- | --- |
| `momo_circle_a` | `#FF413841` | `0.255 / 0.220 / 0.255` | type `21/22/23` 中可见 `0.259 / 0.220 / 0.259` |
| `momo_circle_b` | `#FF70181B` | `0.439 / 0.094 / 0.106` | type `21/22/23` 中可见 `0.439 / 0.094 / 0.106` |
| `momo_circle_c` | `#FF222125` | `0.133 / 0.129 / 0.145` | type `21/22/23` 中可见 `0.137 / 0.129 / 0.149` |
| `momo_circle_d` | `#FF2B2831` | `0.169 / 0.157 / 0.192` | type `21/22/23` 中可见 `0.169 / 0.157 / 0.196` |

同一批节点的 `TRS2.0x38` illumination 色都是 `#FF000000`，对应 Otohime 中 type 25/26/27 全部为 0。结合 Ras heart 节点，当前将 type `21/22/23` 命名为 `MaterialColorR/G/BCandidate`，type `25/26/27/28` 命名为 `IlluminationColorR/G/B/AlphaCandidate`。

## KEY tangent 例外

Otohime 首次发现 `KEY.0x5D` 与 `0x5E` 不总相等：38,518 个带 tangent 字段的 key 中，38,436 个相等，82 个不相等。不相等 key 全部属于 type 5 `RotateZ`，覆盖 64 条 track；按 interpolation 分布为 Linear 14、Spline 68。样例包括 `Action_Wait2 -> uniform_lower_R01_a` frame 83 的 `tan=(-1.025,-1.894)`、`Action_Joy1 -> sune_R_point` frame 67 的 `tan=(0,0.802)`。

因此 `0x5D/0x5E` 在格式文档中继续保留为两个字段，按 tangent in/out 或附加双参数候选处理，不再沿用 Ras/Chiffon 的全等关系作为全局规则。

## SVO 资源

`inspect-svo` 解析结果：

| 项 | 值 |
| --- | --- |
| AVTS directory entries | 4 |
| DDS textures | 3 |
| AVTS header unknown non-zero bytes | 51 |
| YABX headerHashCandidate | `0x06E77BC1` |
| YABX objects | 12 / 12 |
| YABX object coverage | `parsedBytes=1069/1069`, `unparsedBytes=0` |
| referenceBase | `0x2711` |

资源记录：

| Atlas | Size | CROP |
| --- | ---: | ---: |
| `MM_CH_Oto_000` | 1536x1536 | 150 |
| `MM_CH_Oto_001` | 1536x1536 | 173 |
| `MM_CH_Effect_000` | 512x512 | 25 |

`extract-images` 输出 3 张 atlas PNG 和 348 张 crop PNG，映射 380 个 image cast。`MM_CH_Oto_000[85]`、`MM_CH_Effect_000[7]`、`MM_CH_Effect_000[21]` 越界，已按透明 padding 处理。
