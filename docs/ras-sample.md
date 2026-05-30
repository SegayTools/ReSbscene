# Ras 样本记录

当前已用本地样本完成解析：

- `.sbscene`: `D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras__Ras_00.sbscene`
- `.svo`: `D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras.svo`

## 运行命令

```powershell
dotnet run --project src/SbScene.Cli -- inspect <path-to-ras.sbscene>
dotnet run --project src/SbScene.Cli -- dump <path-to-ras.sbscene> --json out/ras.json --markdown out/ras.md
```

输出文件会使用 UTF-8 without BOM。

## Snapshot

| 项 | 值 |
| --- | --- |
| 文件大小 | 998,712 bytes |
| 根块数量 | 1 |
| 根块 ParamRawHex | `0x0100004C`（`ParamLow=1` / `ParamHigh=19456`，用途仍 unknown） |
| 总块数量 | 13,789 |
| NODE 记录数量 | 428 |
| TRS2 记录数量 | 428 |
| 可恢复场景树 | Yes |
| ANIM 数量 | 32 |
| MOT 数量 | 2,736 |
| 动画到节点绑定数量 | 2,736 |
| TRK 数量 | 5,193 |
| `TRK.0x57` 与 key count 匹配 | 5,193 / 5,193 |
| KEY 块数量 | 5,193 |
| KEY 关键帧数量 | 19,634 |
| CIMG 数量 | 304 |
| DATA ParamLow / primary resource blocks | 304 / 304，匹配；Ras 中也等于 CIMG 数 |
| `CIMG.0x44` count 校验 | 304 / 304，匹配 |
| `CIMG.0x45` index 范围 | 311 / 311 非空 CREF 组，0 越界 |
| CREF block 数量 | 311 |
| Image cast 数量 | 304 |
| Crop reference 记录数量 | 350（primary 338，secondary 12） |
| 多 crop 引用 image cast | 35 |
| secondary CREF image cast | 12 |
| 可映射节点名 image cast | 304 |
| SVO AVTS 目录项 | 5 |
| SVO DDS 目录项 | 4 |
| SVO AVTS header unknown bytes | 50 个非零字节，按 unknown word 保留 |
| SVO YABX metadata | 1，84 个 ASCII string，14 个对象，`referenceBase=0x2711` |
| TEX 数量 | 4 |
| CROP 数量 | 4 |
| CAM 数量 | 1 |
| NCAT 记录数量 | 428，全部为 0 |
| Variant hints | 1,043 |
| Unknown type code 数量 | 0（当前类型表可完整走到 EOF） |
| 字段目录行数 | 82（按 `tag + field id + type` 汇总） |

完整报告：

- `out/ras.md`
- `out/ras.json`
- `out/ras-svo-extract/`

`out/ras.json` 包含完整原始字段和 raw bytes，当前大小约 170 MB（约 162 MiB）。
`out/ras.md` 包含“字段目录”和块参数 raw hex 分布，用于快速核对每个块的 field id、type、出现次数、count/stride 和常见值。例如根块显示 `raw=0x0100004C`，`TRK.0x54` 显示 `0x13/0x23/0x33/0x43`，`NODE.0x30` 显示 `0xF01/0xF00/0xE01/0xE00/...`，`CIMG.0x48` 显示 `0x408000/0x908000/0x408001/...`。

## 节点分组摘要

| Group | Count |
| --- | ---: |
| `(ungrouped)` | 90 |
| `uniform` | 54 |
| `plain` | 53 |
| `gorgeous` | 50 |
| `present` | 19 |
| `mouth` | 9 |
| `accessory` | 5 |
| `face` | 1 |

存在 `unifrom_` 拼写组 5 个节点；这是源数据拼写错误候选，解析器先按原名保留。

## 场景树与 TRS2

`NODE` 的 child/sibling 索引已解析，可以恢复树结构。根节点为 `Ras_Scale`，其下为 `Ras_root`，主要分支包括 `effect_heart`、`Ras_null`、服饰/身体/表情等部件。

`TRS2` 与 `NODE` 按 index 一一对应，已解析字段包括：

- translation `(x, y)`
- rotation Z
- scale `(x, y)`
- display
- material color
- illumination color
- vertex colors
- multi position / size flags

样例：

| Node | Translation | RotationZ candidate | Scale | Display |
| --- | --- | ---: | --- | --- |
| `Ras_Scale` | `(0, 0)` | 0 | `(0.669, 0.669)` | true |
| `Ras_root` | `(537, 1450)` | 0 | `(1, 1)` | true |
| `effect_heart` | `(-26, -621)` | 0 | `(2.1, 2.1)` | true |
| `04_present_eff` | `(1, 32)` | 0 | `(1, 1)` | true |

`NODE.0x30` flags 与节点用途有明显相关性，但 bit 级含义仍保留为候选：

| Flags | Nodes | Image casts | Animated nodes | Display=false | 观察 |
| ---: | ---: | ---: | ---: | ---: | --- |
| `0x900` | 6 | 0 | 0 | 0 | present heart 的 null/container 节点。 |
| `0xE00` | 37 | 0 | 37 | 37 | 初始隐藏的 container/组合节点，animated/display=false 共现。 |
| `0xE01` | 69 | 69 | 63 | 69 | 初始隐藏且带 CIMG 的节点。 |
| `0xF00` | 80 | 0 | 59 | 0 | 普通 container/骨架节点。 |
| `0xF01` | 234 | 234 | 173 | 0 | 带 CIMG 且初始 display=true 的节点。 |
| `0x8F00` | 1 | 0 | 0 | 0 | 根缩放节点 `Ras_Scale`。 |
| `0x10F01` | 1 | 1 | 1 | 0 | `koukando02_mouth`，在 Ras 中唯一。 |

新增的 `flagBits` 输出给出以下 Ras 交叉统计：

| Bit | Mask | Nodes | Image casts | Animated nodes | Display=false | 候选语义 |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| 0 | `0x00000001` | 304 | 304 | 237 | 69 | CIMG-backed 节点候选。 |
| 8 | `0x00000100` | 322 | 235 | 233 | 0 | 常见节点属性；区分 `0xFxx` 与 `0xExx`。 |
| 9 | `0x00000200` | 422 | 304 | 333 | 106 | 常见节点属性；当前只缺于 `0x900` null 节点。 |
| 10 | `0x00000400` | 422 | 304 | 333 | 106 | 与 bit 9 分布相同，具体语义未知。 |
| 11 | `0x00000800` | 428 | 304 | 333 | 106 | 所有 Ras `NODE.0x30` 都设置的 common bit。 |
| 15 | `0x00008000` | 1 | 0 | 0 | 0 | `0x8F00` 根/控制节点候选。 |
| 16 | `0x00010000` | 1 | 1 | 1 | 0 | `0x10F01` 稀疏特例，当前仅 `koukando02_mouth`。 |

TRS2 字段统计：

| 字段 | Ras 分布 |
| --- | --- |
| `display` | true 322，false 106，unknown 0 |
| `materialColor` | `#FFFFFFFF`:398，`#00FFFFFF`:19，其他少量 alpha/色值 |
| `illuminationColor` | `#FF000000`:426，`#FFDF00FF`:2 |
| `vertexColors` | 每条 TRS2 都有 4 个 `0x39` 顶点色，共 1,712 条颜色字段 |
| `multiPosFlags` | 428 条全 0 |
| `multiSizeFlags` | 428 条全 1 |

`DATA` 块在 Ras 中为 `ParamLow=304`、`ParamHigh=0`、无字段、无 trailing bytes；304 与后续 `CIMG+CNUM+CRFD+CSLI` primary resource block 数一致。Ras/Chiffon/Otohime 中该数刚好也等于 `CIMG` image cast 数，但 full survey 显示 UI 场景会出现 `CNUM/CRFD/CSLI`，因此不能把 `DATA.ParamLow` 全局写成单纯 CIMG 数量。

## 动画清单

| Name | Motions | Tracks |
| --- | ---: | ---: |
| `FadeIn1` | 3 | 3 |
| `Change_Fashion` | 111 | 114 |
| `Change_Position` | 1 | 4 |
| `Change_Accessory` | 8 | 8 |
| `Effect_Heart1` | 15 | 61 |
| `Action_Wait1..4` | 717 合计 | 1,284 合计 |
| `Action_Joy1..3` | 568 合计 | 1,156 合计 |
| `Action_Happy1` | 191 | 361 |
| `Action_Sad1` | 183 | 343 |
| `Action_Determination1` | 191 | 354 |
| `Action_Touch1..2` | 347 合计 | 652 合计 |
| `Action_Change` | 185 | 374 |
| `DressChange` | 181 | 369 |
| `Mouth_*` | 36 合计 | 101 合计 |
| `FadeOut1` | 1 | 1 |

## 动画到节点绑定摘要

`MOT.0x51` 在 Ras 样本中可映射到 `NODE` 记录 index，当前 2,736 个 motion 都能生成动画到节点绑定。运行时 `AnimationContainer_BuildMotionLookup` 也按 `MOT` 前 2 字节目标节点索引建立 `castIndex x animationIndex` lookup，因此该字段可作为 motion target node index 使用。

| Animation | 绑定节点样例 | Track type 分布/线索 |
| --- | --- | --- |
| `Change_Fashion` | `plain_apron_ribon`、`uniform_cap_L`、`plain_onepiece_body`、`gorgeous_L_boots_*` | 114 条 track 中 105 条为 `11(Display)`；display 轨道集中在服饰部件，少量节点带 `18(PrimaryImageVariantIndexCandidate)`、`0/1(Translate)`、`5(RotateZ)` 轨道。 |
| `Change_Accessory` | `accessory_L_fruittea`、`accessory_L_coffe`、`accessory_L_kettle`、`tray`、`coffecup`、`bread`、`pancake` | 8 条 track 全部为 `11(Display)`，构成饰品 display 状态表。 |
| `Mouth_Wait1` | `koukando01_mouth`、`ef_action` | 嘴部节点含 `12(MouthShapeA)`、`13(MouthShapeB)`、`18(PrimaryImageVariantIndexCandidate)` 和局部 transform；`ef_action` 使用 `11(Display)`。 |
| `Action_Wait1` | `Ras_null`、`tail`、`plain_apron_ribon*`、`top03`、`hair_back_*` | 以 `5(RotateZ)` 为主，并包含 `0/1(Translate)`、`6/7(Scale)` 和部分 `11(Display)`。 |
| `DressChange` | `Ras_root`、`Ras_null`、身体与服饰部件 | 包含动作动画同类 transform track，另含 `24(AlphaOrOpacity)`；只记录为 transform 与 alpha/opacity 候选轨道共现。 |

## Ras 服饰/饰品运行时链路

运行时链路现在可以闭合到 Ras 的服饰样本：游戏侧 wrapper 通过 `SurfboardWrapper_EnableAnimationAtTime` (`sub_5C9440`) 映射逻辑 animation，调用 `SbPlayerMO_SetAnimationEnabled` 选择 animation，再由 `SbPlayerMO_SetAnimationTime` 把该 animation seek 到状态帧；`Layer_UpdateActiveAnimations` 再用该时间调用 `Cast_EvaluateMotionTracks`。因此 `Change_Fashion`/`Change_Accessory` 这类动画可以作为状态表使用；关键帧多数是 step/constant，frame 值本身就是状态编号。

Ras wrapper 里负责同步这些状态的是 `sub_609E20`。它固定刷新 logical animation `1/2/3/4`，在当前 Ras 样本中分别对应 `Change_Fashion`、`Change_Position`、`Change_Accessory`、`Effect_Heart1`：服饰使用 `this+0x198` 的服饰状态 index，经 `dword_F40714` 指向对象的 `+0x38` 表取 record `+0x44` 作为 `Change_Fashion` frame；如果 `this+0x224` 置位且 `this+0x228 != -1`，则直接用该 override frame。饰品使用 `this+0x1E8` 作为 `Change_Accessory` frame，站位使用 `this+0x1C8` 作为 `Change_Position` frame。`sub_60A080/sub_60A1B0` 在换装/切换入口会先按这些字段 seek `Change_Fashion` 和 `Change_Accessory`；`SbMMRasWrapper_Update` (`sub_60A850`) 的状态 4 还会在内部计时到 frame 45 时推进 `this+0x198/0x19C` 并重新 seek `Change_Fashion`。

`Change_Fashion` 中有 105 条 type 11 display 轨道。按 step/hold 方式在 frame `0..3` 复算，display=true 数分别为 `42/47/44/38`：frame 1 以 `plain_*` 为主，frame 2 以 `uniform_*` / `unifrom_*` 为主，frame 3 以 `gorgeous_*` 为主；frame 0 还包含部分 plain、腿部、耳部和高光/基础部件。少量 `upperarm/forearm/hand` 节点同时带 type 18 primary image slot 轨道，但这些只切图，不负责开关可见性。

| Frame | Fashion display=true 摘要 | type 18 primary slot 变化 |
| ---: | --- | --- |
| 0 | 42 个；`plain_lace_*`、`plain_wristband_*`、`plain_onepiece_*`、`plain_shoes_*` 等 | 6 条均为 slot `0` |
| 1 | 47 个；新增 `plain_apron_ribon`、`plain_apron_*` 头饰/围裙部件 | 6 条仍为 slot `0` |
| 2 | 44 个；`uniform_*` / `unifrom_*` 服饰、mofu、cap、apron 部件 | `forearm_L01`、`forearm_R03`、`forearm_L02a`、`hand_L02a` 切到 slot `1` |
| 3 | 38 个；`gorgeous_*` 翅膀、靴子、裙子、body/top 与右手部件 | `upperarm_L01`、`hand_L01`、`hand_L02a` 为 slot `1`，`forearm_*` 为 slot `2` |

`Change_Accessory` 的 8 条状态轨道全是 type 11 display。样本 key 可直接读作饰品状态表；其中 `tray` 在 frame 1 与 frame 3 都有 true key，按 step/hold 复算时 frame 2 也保持 true。

| Frame | Display=true 节点 |
| ---: | --- |
| 0 | `hand_L01` |
| 1 | `accessory_L_fruittea`、`tray`、`pancake` |
| 2 | `accessory_L_coffe`、`tray`、`coffecup` |
| 3 | `accessory_L_kettle`、`bread`、`tray` |

这说明 Ras 的多套服饰/饰品不是靠 `CIMG.0x45` 或 type 18/19 选择“当前套装”。上层只需要启用对应 `Change_*` animation 并设置状态帧，实际显隐由每个目标 cast 上的 type 11 写入本节点 display，再由 `Cast_UpdateRenderState` 结合父节点最终可见性和 cast/static record `+0x218` 得出最终 `Cast+0xD0`。`Change_Fashion` 里的 6 条 type 18 只在 `upperarm/forearm/hand` 节点切 primary image slot；`Change_Accessory` 没有 type 18/19。


## Track flags 与 KEY 插值摘要

`TRK.0x57` 已确认是 keyframe 数量；在 Ras 中 5,193 条 track 全部与 `KEY.ParamHigh / 5` 和实际解析出的 key 数一致。

Ras 的 `TRK.flags` 目前只出现四类。低 nibble 全为 `0x3`；高 nibble 与 `KEY.0x5B` 实际存储类型一一对应：

| Flags | Low | High | Storage | Tracks | `KEY.0x5B` 类型 | 主要用途 |
| ---: | ---: | ---: | --- | ---: | --- | --- |
| `0x13` | `0x3` | `0x1` | `Float32Curve` | 2,511 | `0x000A Float32` | 平移、缩放、口型 float、alpha/opacity 等。 |
| `0x23` | `0x3` | `0x2` | `Int32State` | 81 | `0x0008 Int32` | `18(PrimaryImageVariantIndexCandidate)`，key value 全部落在对应 CIMG primary CREF 组范围内。 |
| `0x33` | `0x3` | `0x3` | `BoolState` | 745 | `0x0001 Bool` | `11(Display)`，服饰/饰品开关主力。 |
| `0x43` | `0x3` | `0x4` | `PackedAngleCandidateCurve` | 1,856 | `0x000B PackedAngleCandidate` | `5(RotateZ)` 和少量 `3/4` 旋转候选；旋转上下文按 signed fixed-angle raw int 候选解释。 |

`0x0B` 旧称 `Int32/Float32/PackedFloat32` 不够准确。就 Ras 样本看，它在 `TRS2.0x32` 与 `KEY.0x5B` 的旋转轨道上下文中当前只按 signed fixed-angle raw int 候选解释，候选公式为 `degrees = raw * 180.0 / 32768.0`。典型换算：raw `910 ≈ 5 deg`、`1820 ≈ 10 deg`、`5461 ≈ 30 deg`、`16383 ≈ 90 deg`、`32767 ≈ 180 deg`。该结论仍是候选；`KEY.0x5B type 0x0B` 在 Ras 中主要出现在 track type `5(RotateZ)`，另有少量 type `3/4` 旋转候选。

Ras 的渲染校验样本是 `plain_leg_R1`：静态 `TRS2.0x32 raw=5461` 解码约为 `+30 deg`。在当前 2D 像素坐标渲染路径中，这个 scene/source 角度必须按 `-30 deg` 应用到本地矩阵；直接按 `+30 deg` 应用会把右小腿和鞋子甩到身体右侧。CLI PNG renderer 和 Viewer 现在都通过同一个转换约定处理这一步，避免 raw 角度解码与屏幕矩阵方向混在一起。

主 Markdown 报告现在还会输出统一的 Track type 证据表。Ras 中几个候选/状态 type 的值域如下：

| Type | Name | Tracks | Keys | Value range | 观察 |
| ---: | --- | ---: | ---: | --- | --- |
| 2 | `TranslateZCandidate` | 302 | 302 | `0` | Ras 与 full survey 的该 type key 均为默认值 0；没有可比 `TRS2` 初始通道，轴向命名仍只作为候选。 |
| 3 | `RotateXCandidate` | 21 | 21 | `0` | 少量 fixed-angle rotation 候选，全部值为 0。 |
| 4 | `RotateYCandidate` | 19 | 19 | `0` | 少量 fixed-angle rotation 候选，全部值为 0。 |
| 8 | `ScaleZCandidate` | 10 | 10 | `1` | 全部值为 1。 |
| 18 | `PrimaryImageVariantIndexCandidate` | 81 | 854 | `0,1,2,3` | 全部为 `0x23 + Int32`，且通过 CIMG primary CREF 组范围检查。 |
| 21 | `MaterialColorRCandidate` | 4 | 4 | `0,0.902` | `Action_Joy3 -> hart_*`，匹配 `TRS2.0x37` 材质色 R 通道候选。 |
| 22 | `MaterialColorGCandidate` | 4 | 4 | `0,0.914` | `Action_Joy3 -> hart_*`，匹配材质色 G 通道候选。 |
| 23 | `MaterialColorBCandidate` | 4 | 4 | `0,0.816` | `Action_Joy3 -> hart_*`，匹配材质色 B 通道候选。 |
| 24 | `AlphaOrOpacity` | 58 | 168 | `0..1` | Fade/effect/dress 相关透明度候选。 |
| 25 | `IlluminationColorRCandidate` | 4 | 4 | `0,0.875` | 匹配 `TRS2.0x38` illumination R 通道候选。 |
| 26 | `IlluminationColorGCandidate` | 4 | 4 | `0` | illumination G 通道候选。 |
| 27 | `IlluminationColorBCandidate` | 4 | 4 | `0,1` | illumination B 通道候选。 |
| 28 | `IlluminationAlphaCandidate` | 4 | 4 | `1` | illumination alpha 候选。 |

Ras heart 节点给出了颜色轨道的第一组强证据：`hart_R_01/hart_L_01` 的材质色为 `#FFE6E9D0`（按 `A,R,G,B` 候选为 `255,230,233,208`），归一化后约为 `0.902/0.914/0.816`，正好对应 type 21/22/23；`hart_R_02/hart_L_02` 的 illumination 色为 `#FFDF00FF`，对应 type 25/26/27/28 的 `0.875/0/1/1`。

type 24 的 Alpha/Opacity 表显示 Ras 中 58 条 track、168 个 key，其中 51 条目标是 CIMG 节点、34 条目标初始 display=false；56/58 条 track 至少有一个 key 按 survey 初始 alpha 匹配规则命中。`FadeIn1/FadeOut1` 作用在无 CIMG 的 `Ras_root/Ras_null`，而 `Effect_Heart1` 的 13 条目标全是 CIMG 且初始材质 alpha 为 0。当前仅将 type 24 作为有效 opacity/alpha 动画通道候选处理，不把它命名为单纯的静态材质 alpha 字段。

`KEY.0x5C` 插值候选分布：

| Value | Name | Keys |
| ---: | --- | ---: |
| 0 | `StepOrConstant` | 2,020 |
| 1 | `Linear` | 12,648 |
| 2 | `Spline` | 4,966 |

`KEY.0x5D` 与 `0x5E` 在 Ras 全部 19,634 个 key 上相等。Ras 中 941 个 key 有非零 tangent 候选，主要集中在 `RotateZ`、`ScaleY` 和 `TranslateY`。按 `0x5C` 插值值统计：`StepOrConstant` 非零 tangent 为 0，`Linear` 为 139，`Spline` 为 802。后续 full survey 显示 `0x5D != 0x5E` 在 JP/EN 各有 123 个 key、9 个场景，覆盖 RotateZ/TranslateY/ScaleX/ScaleY，因此两个字段不能全局合并，也不能只归因于 Otohime RotateZ。

## 状态/开关轨道表

主 Markdown 报告现在会输出“状态/开关轨道摘要”，从 `Display`、`PrimaryImageVariantIndexCandidate`、`SecondaryImageVariantIndexCandidate`、`AlphaOrOpacity` 生成可读状态表。Ras 样本没有 type 19 secondary variant 轨道。

Ras 总量：

| Track type | Name | Tracks | Keys |
| ---: | --- | ---: | ---: |
| 11 | `Display` | 745 | 1,185 |
| 18 | `PrimaryImageVariantIndexCandidate` | 81 | 854 |
| 24 | `AlphaOrOpacity` | 58 | 168 |

关键结论：

- `Change_Fashion` 有 105 条 display 轨道和 6 条 primary image slot 轨道；个别节点的 type 18 key value 落在多条 primary CREF 范围内。
- `Change_Accessory` 的 8 条状态轨道全是 display；未见 type 18/24。
- `Mouth_*` 口型动画普遍包含 type 18，关键帧值落在对应嘴部 CIMG primary CREF 组范围内。
- Ras 中没有 type 18 的 primary CREF group range mismatch；当前 `ImageRefCheck` 按 primary 组检查，legacy 合计 CREF 宽松统计也全部通过。
- 运行时代码已确认 `11(Display)` 写入本节点显示开关并最终决定 cast 可见性；`18` 写入 primary 图片 slot，`19` 写入 secondary 图片 slot。Ras 样本没有 type 19，但 full survey 和 runtime 分支都支持 secondary slot 解释。

## 纹理资源

`.svo` 文件头为 `AVTS`，大小 7,348,096 bytes。外部 SvoToolOutput 曾作为对照显示 4 个 DDS atlas；当前项目证据来自直接解析 `.svo` 的 AVTS 目录表，并按 `.sbscene` 的 `TEX/CROP` 表输出 PNG，不依赖 SvoToolOutput。
主 `dump` 现在也会把 `.sbscene` 内的 `TEXL/TEX/CROP/CIMG/CREF/CNUM/CRFD/TEXT/CSLI/SLIC` 解析结果写入 `surfboard.resources`。
SVO 解析器现在优先使用 `AVTS` 目录表中的 offset/length 定位 DDS，DDS magic 扫描只作为回退。
YABX metadata 也已恢复 schema 和对象表：对象表位于 YABX payload `0x361`，声明 14 个对象，包括 1 个 `stevia::Database`、2 个 `stevia::VertexDeclaration`、3 个 `stevia::VertexElement`、4 个 `stevia::Texture` 和 4 个 `stevia::Image`。
Ras 的 14 个 YABX object payload 现在全部被字段级解析覆盖：`parsedBytes=1330/1330`，`unparsedBytes=0`。两个 `stevia::VertexDeclaration` 除 `_vertexElement` 引用列表外，还解析出 Resource 基类字段，`_name/_fullName` 分别为 `P` 和 `PN`。
4 个 `stevia::Image` payload 的宽高与 dataSize 均和 DDS header / AVTS `dataLength` 一致。
当前还确认了 YABX 的引用列表容器：`byteLength:u32 + count:u32 + u16[count]`。顶层 database 的 `_texture` 为 `[0x2717, 0x2719, 0x271B, 0x271D]`，`_image` 为 `[0x2718, 0x271A, 0x271C, 0x271E]`；每个 `stevia::Texture._image` 正好指向后一组 image ref。
YABX header 的 hash/checksum 候选值为 `0xA4E986BC`。AVTS header 的 `0x08..0x7F` 仍作为 unknown words 保留；`inspect-svo` 现在会把非零 word 分类为 UTF-16 字符候选、小整数候选、文件内 offset 候选或 pointer/residue 候选。Ras 中这些值没有匹配到 AVTS 目录项的 payload 起止/长度，目录项 `+0x210..+0x3FF` reserved 区全 0。
YABX descriptor 也新增了 schema-to-object 使用证据：例如 `stevia::Database._image` 的 raw descriptor 是 `000000`，但实际 payload 是 `ReferenceList`；`stevia::Texture._image` 是 `010200` 且实际是 `ObjectReferenceId`。这说明 descriptor raw 值不能脱离 owning type 单独命名。

| DDS | YABX Texture/Image obj | Size | Format | Crops |
| --- | --- | ---: | --- | ---: |
| `MM_CH_Ras_000` | 6 / 7 | 1536x1536 | DXT5 / code 1 | 136 |
| `MM_CH_Ras_001` | 8 / 9 | 1536x1536 | DXT5 / code 1 | 93 |
| `MM_CH_Effect_000` | 10 / 11 | 512x512 | DXT5 / code 1 | 24 |
| `MM_CH_Ras_002` | 12 / 13 | 1536x1536 | DXT5 / code 1 | 48 |

`.sbscene` 的 `TEXL/TEX/CROP/CIMG/CREF` 与这些资源对应；UI 场景中还会出现同属 `DATA` primary resource 的 `CNUM/CRFD/CSLI` 以及不计入 `DATA.ParamLow` 的伴随 `TEXT`：

- `TEXL.0x60 = 4`
- `TEX.0x61` 给出 atlas 名称。
- `TEX.0x40/0x41` 给出宽高。
- `TEX.0x63` 给出 crop 数。
- `CIMG` 描述 image cast 的尺寸、pivot 和 crop 引用。
- `CREF` 指向 texture/crop 组合。

导出结果：

| 输出 | 数量 |
| --- | ---: |
| atlas PNG | 4 |
| crop PNG | 301 |
| image cast manifest 记录 | 304 |
| crop reference manifest 记录 | 350（primary 338，secondary 12） |

5 个 crop 矩形带有越界透明外延，导出时使用透明像素补齐，没有跳过。

`CIMG.0x44` 已更新为 primary/secondary 两组 CREF 记录数，而不是 flag。Ras 中 304 个 image cast 的 `0x44` 与后续 CREF 组记录数完全匹配，12 个 image cast 带 secondary CREF，主要出现在 eye/hair_front 节点。`CIMG.0x45` 是 primary/secondary 组内引用 index raw 值，所有值都小于对应组记录数。

`CIMG.0x45` 只有 8 个 image cast 非零，全部能指向有效引用；例如 `kirakira_eye` 的 `(4,1)/(2,0)` 可索引到 primary `0:47` 和 secondary `0:42`，`forearm_L02` 的 `(3,0)/(2,0)` 可索引到 `3:43`。因此当前确认 `0x45` 是 primary/secondary 组内 crop reference index，并在运行时作为 fallback/default slot index；它仍不能命名为当前或选中状态。

`CIMG.0x48` 已从 raw value 进一步拆成 bit 分布，并在主报告里输出节点 flags、节点组、初始 display、multi/secondary CREF、非零 `0x45` index 和 bit 共现表。后续 loader/xref 复核显示该字段不是 CIMG 私有 flags，而是跨 `CNUM/CSLI/TEX/SLIC` 等资源复用的 packed state word。Ras 中 bit 15 在 304 个 image cast 上全部设置；后续 full survey 显示 bit 15 是高覆盖 CIMG 位但非全局必有，因此不命名语义。bit 0 只出现在 9 个 `04_present_eff`、`*_add`、`hart_*` 节点上，且是 bit 22 的子集；这只作为名称/分组相关性，不确认混合或渲染语义。bit 20、21、22、23 仍只记录为 CIMG 样本高位共现关系。

| Bit | Image casts | Ras 观察 |
| ---: | ---: | --- |
| 0 | 9 | `04_present_eff`、`04_present_heart_*_add`、`hart_R_01`、`hart_L_01` 等节点名；只作样本定位，不确认运行时混合语义。 |
| 15 | 304 | 全部 CIMG 都设置。 |
| 20 | 121 | 常见于 hair/clothes/body 节点；117 个与 bit 23 共现。 |
| 21 | 6 | 只覆盖少量 tail/gorgeous/plain 特例。 |
| 22 | 182 | 常见于 expression/body/effect 节点。 |
| 23 | 117 | Ras 内是 bit 20 的子集，常见于 hair/clothes/body 节点；不是全局子集规则。 |

bit 共现重点：`0+22` 为 9，说明 bit 0 在 Ras 内完全落在 bit 22 内；`20+23` 为 117，说明 bit 23 在 Ras 内完全落在 bit 20 内。bit 0 与 multi/secondary CREF、非零 `0x45` index 均无交叉，因此不作为资源选择属性结论；相关节点名只用于样本定位。

## Camera 与 NCAT

Ras 样本有一个 camera：

| Name | Position | Target | Flags | Near | Far |
| --- | --- | --- | ---: | ---: | ---: |
| `default` | `(0, 0, 1000)` | `(0, 0, 0)` | `0x1FFF` | 10 | 100000 |

`CAM.0x14 = 0x1FFF` 仍按 flags-like / unknown 处理。虽然数值形态容易被误套入 fixed-angle 公式，但它不在 `TRS2.0x32` 或旋转 `KEY.0x5B` 上下文中，因此不应解释为角度。

`NCAT.0x0E` 在 Ras 中记录数为 428，与 NODE/TRS2 数量一致，值全部为 0。Chiffon/Otohime 分别为 297/501 条，也全部为 0。后续 full survey 已显示部分 Shama/UI 场景存在非零 category，整体分布扩展到 `0..8`，因此三角色样本全 0 只作为样本阶段观察；`0x0E` 仍只作为 raw 分类字段保留，不推断更具体语义。

## 需要重点核对的列表

- `Change_FashionV*`
- `Change_PositionV*`
- `Change_AccessoryV*`
- `DressChangeV*`
- `Action_*`
- `Mouth_*`
- `plain_`、`uniform_`、`gorgeous_`、`present_` 节点组
- visibility/state track 候选

## 当前限制

当前限制：

- YABX 对象表在 Ras 中已能完整覆盖 14/14 个对象 payload；full survey 的当前 resource skeleton（`Database / VertexDeclaration / VertexElement / Texture / Image`）也已字段级完整覆盖。仍未覆盖的是其它 YABX 版本或新增对象类型的 payload 泛化解码。
- `TRK.flags` bit 级含义仍待确认；`KEY.0x5C/0x5D/0x5E` 的 spline 分支已按运行时代码复现为三次 Hermite，更多 UI/特殊 track 的边界行为仍需继续验证。`TRK.0x57` 已可按 keyframe 数量处理，`0x5D/0x5E` 在 Ras 中相等，但 full survey 已确认 mismatch 覆盖 9 个场景和多个 track type。`TRK.flags 0x43` / high nibble `0x4` 当前只按 `PackedAngleCandidate` 记录，signed fixed-angle raw int 只是旋转上下文候选，不作为已确认 `PackedFloat32`。
