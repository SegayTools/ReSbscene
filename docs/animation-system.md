# 动画系统记录

解析器保留结构字段和候选语义；PNG renderer 已实现当前用于导出的播放求值与渲染路径。

## 块结构

| 块 | 状态 | 说明 |
| --- | --- | --- |
| `ANIM` | Confirmed(raw structure) | 动画容器结构已确认。语义层取第一个字符串字段作为动画名候选。 |
| `MOT ` / `MOT` | Confirmed(raw structure) | motion 容器结构已确认。`0x51` 保留为 NODE-index candidate，`0x52` 的 track count 关系见下表。 |
| `TRK ` / `TRK` | Confirmed(raw structure) | track 容器结构已确认。记录 track type、flags、value type、帧范围和 key。 |
| `KEY ` / `KEY` | Confirmed(raw structure) | 关键帧容器结构已确认。字段按数值候选输出，不强行解释 payload。 |

## Motion 和 Track 候选字段

字段边界以 Ras 样本为起点；已被 full survey 补强的计数、范围或异常会直接写在说明中：

| 块 | 字段 | 说明 |
| --- | --- | --- |
| `ANIM` | `0x03` | 动画名，文件内常以 `V` 结尾，CLI 语义层会去掉该尾缀。 |
| `ANIM` | `0x50` | declared motion count candidate。full survey 中除 1 个 `+2` 异常外均等于实际 motion 数；该异常不支持改名为 max track count，仍不能全局写成 confirmed count。 |
| `ANIM` | `0x56` | playback duration/end-frame 候选；full survey 显示它常等于最大 track/key frame，但不是所有 track/key 的严格上界。 |
| `ANIM` | `0x5F` | default repeat flag；运行时复制到 layer repeat 数组初值，随后可由 `SbPlayerMO_SetAnimationRepeat` 覆盖。 |
| `MOT ` | `0x51` | motion target node index；full survey 中 JP/EN 32,428/32,742 个 motion 均落在本文件 `NODE` 记录范围内，运行时也按该 index 绑定 cast。 |
| `MOT ` | `0x52` | track 数量；full survey 中与 `MOT.ParamLow` 和实际 track 数全量一致。 |
| `TRK ` | `0x53` | track type。 |
| `TRK ` | `0x57` | keyframe 数量。Ras/Chiffon/Otohime 与 `KEY.ParamHigh / 5` 及实际解析 key 数完全一致；surfboard/surfboard_EN full survey 未发现 mismatch。 |
| `TRK ` | `0x54` | flags。full survey 中字段顺序为 `0x53,0x57,0x54,0x58,0x59`。 |
| `TRK ` | `0x58` | first frame。 |
| `TRK ` | `0x59` | last frame。 |
| `KEY ` | `0x5A` | key frame。 |
| `KEY ` | `0x5B` | key value；type 可为 bool/float 等。 |
| `KEY ` | `0x5C` | 插值/状态字段候选。 |
| `KEY ` | `0x5D` / `0x5E` | tangent 或附加值候选。full survey 中 `0x5D != 0x5E` 为 JP/EN 各 123 个 key、9 个场景，覆盖 RotateZ/TranslateY/ScaleX/ScaleY，因此保留为两个独立 raw 字段。 |

`KEY.ParamHigh` 是字段数量；常见 key 由 `0x5A..0x5E` 5 个字段组成，因此 `ParamHigh / 5` 是 key count。部分 track type 的 key value 在现有 SvoToolDump 中为空，但文件内仍有 key 字段，本解析器会保留。

full survey 的结构聚合显示，JP/EN 的 `ANIM` 字段顺序只有一种：`0x0050:0x0006>0x0003:0x0002>0x0056:0x0008>0x005F:0x0001`，覆盖 1,579/1,614 个动画。`ANIM.0x50` 与实际 motion 数匹配 1,578/1,613 个动画；唯一异常在 `MM_UI_Entry__MM_UI_Select_EntryName_ALL.sbscene` 的 `NameFadeIn`，`ParamLow=4`、`0x50=6`、实际 motion 数为 4。对该文件的原始 dump 显示 `NameFadeIn` 下 4 个 `MOT` 的 track 数为 `6,2,6,2`，因此 `0x50=6` 只是在这个异常里等于最大 motion track 数；新增 full-survey 交叉字段 `AnimationField50MotionOrMaxTrackRelationCounts` 显示 JP/EN 只有 1/1 个动画属于 `equalsMaxMotionTrackCountOnly`，其余为 `equalsMotionCountOnly` 1255/1280、`equalsMotionCountAndMaxMotionTrackCount` 278/287、`noMotions` 45/46。`ANIM.ParamLow` 与实际 motion 数匹配 1,167/1,170 个动画，另有 412/444 个动画为 `ParamLow = motion count + 1`。因此 `ANIM.ParamLow` 和 `ANIM.0x50` 只保留为 raw/count candidate，不作为全局硬规则。

`ANIM.0x5F` 在静态层面是 Byte/Bool-like raw flag。JP/EN full survey 中 `AnimationField5FCounts` 为 `0=1438/1469`、`1=141/145`，未见其它值；`0x5F=1` 中有 motion 的动画为 136/140 个，无 motion 的动画为 5/5 个，因此它不是 motion-presence flag。运行时 `AnimationContainer_BuildMotionLookup` 将对应 entry 的 `+0x20C` 复制到 default repeat 数组，`Layer_InitRuntimeFromData` (`sub_7DC8D0`) 再把它作为 `Layer+0x2C repeat[animation]` 初值；之后 `SbPlayerMO_SetAnimationRepeat` 可覆盖该值。因此它可命名为 default repeat flag，而不是 active flag 或 count modifier。

`ANIM.0x56` 运行时被复制为 playback duration/end-frame，并由 `Layer_SetAnimationTime` 用于 wrap/clamp。它相对最大 `TRK.0x59` 的 full-survey 关系为 JP/EN `endEqualsMaxTrackLast=1396/1428`、`endContainsMaxTrackLast=91/93`、`endBeforeMaxTrackLast=47/47`、`noTrackLastFrames=45/46`；相对最大 `KEY.0x5A` 的 delta 统计为 `0=1394/1426`、negative total `47/47`、positive total `93/95`、`noKeyFrames=45/46`。Shama 样本中多个 `Action_*` 动画为 `0x56=150/200`，但 track/key 最大帧可延伸到 `154..227`；多个 `Mouth_*` 动画则在 `146` 提前结束。因此 `0x56` 不写成所有 track/key 的严格最大帧或裁剪边界。

`MOT` 的结构更稳定：JP/EN 的 32,428/32,742 条 `MOT` 字段顺序只有一种 `0x0051:0x0005>0x0052:0x0006`，`MOT.ParamLow`、`MOT.0x52` 和实际 track 数三者全量一致，`0x51` 全部在 NODE index 范围内。JP/EN 的 94,611/95,558 条 `TRK` 只有一种字段顺序：`0x0053:0x0006>0x0057:0x0006>0x0054:0x0009>0x0058:0x0008>0x0059:0x0008`。实际 key record 为 JP/EN 186,780/188,002 条，各自只有 4 种字段顺序，差异只在 `0x5B` 的 type code；`0x5A/0x5C/0x5D/0x5E` 的字段类型保持一致。key frame 序列全部非递减，但不是全局严格递增：JP/EN 各有 2 条 duplicate-frame track，均来自两个 UI 场景的 `FadeIn -> cover` type 24 alpha 轨道。

## TRK.0x57 key count

Ras 样本中 5,193 条 `TRK` 的 `0x57` 都与后续 `KEY` 块声明的 key 数一致。分布如下：

| Key count | Track count |
| ---: | ---: |
| 1 | 2,497 |
| 2 | 185 |
| 3 | 560 |
| 4 | 364 |
| 5 | 332 |
| 6 | 382 |
| 7 | 180 |
| 8 | 213 |
| 9 | 95 |
| 10 | 170 |
| 11 | 65 |
| 12 | 9 |
| 13 | 43 |
| 14 | 14 |
| 16 | 3 |
| 18 | 24 |
| 36 | 7 |
| 37 | 50 |

## Ras track type 分布

| TrackType | Count | CLI 候选名 | 初步用途 |
| ---: | ---: | --- | --- |
| 0 | 664 | `TranslateX` | 平移 X 候选。 |
| 1 | 676 | `TranslateY` | 平移 Y 候选。 |
| 2 | 302 | `TranslateZCandidate` | 与 `0/1` 或 `0/1/5` 成组出现，Ras 中全为单 key 且值为 0。 |
| 3 | 21 | `RotateXCandidate` | 与 `3/4/5` 三轴旋转组出现，Ras 中全为单 key 且值为 0。 |
| 4 | 19 | `RotateYCandidate` | 与 `3/4/5` 三轴旋转组出现，Ras 中全为单 key 且值为 0。 |
| 5 | 1816 | `RotateZ` | 旋转 Z 候选；动作动画中大量出现。 |
| 6 | 225 | `ScaleX` | scale X 候选。 |
| 7 | 401 | `ScaleY` | scale Y 候选。 |
| 8 | 10 | `ScaleZCandidate` | 与 `7/8` 缩放组出现，Ras 中全为单 key 且值为 1。 |
| 11 | 745 | `Display` | 运行时 display/visibility 开关；`Change_Fashion` 和 `Change_Accessory` 中大量出现。 |
| 12 | 74 | `ImageWidthCandidate` | CIMG 动态宽度候选；Ras `Action_Joy3 -> kirakira_eye` 在闭眼帧保持 `150`。 |
| 13 | 73 | `ImageHeightCandidate` | CIMG 动态高度候选；Ras `Action_Joy3 -> kirakira_eye` 在 frame `11..34` 从 `65` 切到 `26`，配合 type 18 选择闭眼 crop。 |
| 18 | 81 | `PrimaryImageVariantIndexCandidate` | Int32 状态轨道，20 个目标节点均有 CIMG；Ras 中 key value 全部落在对应 CIMG primary CREF 组范围内。 |
| 21 | 4 | `MaterialColorRCandidate` | Float32 材质色 R 候选；Ras heart 节点与 `TRS2.0x37` 的 R 通道匹配。 |
| 22 | 4 | `MaterialColorGCandidate` | Float32 材质色 G 候选；与 type 21/23 成组。 |
| 23 | 4 | `MaterialColorBCandidate` | Float32 材质色 B 候选；与 type 21/22 成组。 |
| 24 | 58 | `MaterialAlpha` | Float32 值域在 0..1，写入 `MaterialColor.A` 并参与父链 effective opacity。 |
| 25 | 4 | `IlluminationColorRCandidate` | Float32 illumination 色 R 候选；Ras heart 节点与 `TRS2.0x38` 的 R 通道匹配。 |
| 26 | 4 | `IlluminationColorGCandidate` | Float32 illumination 色 G 候选。 |
| 27 | 4 | `IlluminationColorBCandidate` | Float32 illumination 色 B 候选。 |
| 28 | 4 | `IlluminationAlphaCandidate` | Float32 illumination alpha 候选；Ras 中恒为 1。 |

Ras 中 `TRK.flags` 只观察到 `0x13`、`0x23`、`0x33`、`0x43` 四类。低 nibble 均为 `0x3`；高 nibble 与 `KEY.0x5B` 的实际字段类型一一对应。低 nibble 的具体 bit 语义仍待确认，高 nibble 在 Ras 中可作为 key value storage 处理。

## TRK.flags 与 key value 存储

Ras 中 `TRK.flags` 的低位固定带 `0x03`，高位与 `KEY.0x5B` 的存储类型按样本一一对应。Chiffon 进一步显示 flags 需要拆为 `base byte = flags & 0xFF` 和 `extra mask = flags & ~0xFF`；`0x113/0x133/0x143` 的 base byte 仍分别是 `0x13/0x33/0x43`。

| Flags / base | Low | Storage nibble | Count | CLI 存储候选名 | `KEY.0x5B` 类型 | 主要 TrackType |
| ---: | ---: | ---: | ---: | --- | --- | --- |
| `0x13` | `0x3` | `0x1` | 2,511 | `Float32Curve` | `0x000A Float32`，8,295 keys | `0/1/2` 平移、`6/7/8` 缩放、`12/13` 口型、`24` alpha/opacity 等。 |
| `0x23` | `0x3` | `0x2` | 81 | `Int32State` | `0x0008 Int32`，854 keys | `18(PrimaryImageVariantIndexCandidate)`。 |
| `0x33` | `0x3` | `0x3` | 745 | `BoolState` | `0x0001 Bool`，1,185 keys | `11(Display)`。 |
| `0x43` | `0x3` | `0x4` | 1,856 | `PackedAngleCandidateCurve` | `0x000B PackedAngleCandidate`，9,300 keys | `5(RotateZ)`、`3/4` 旋转候选；旋转上下文按 signed fixed-angle raw int 候选解释。 |

Chiffon extra mask `0x100` 覆盖 6 条 track：`Float32Curve+Extra0x100` 4 条、`BoolState+Extra0x100` 1 条、`PackedAngleCandidateCurve+Extra0x100` 1 条。它不改变 `KEY.0x5B` 的字段类型，暂记为额外 track flag。6 条 track 全部位于 `Action_Wait3 -> smile`，覆盖 `TranslateX/TranslateY/RotateZ/ScaleX/ScaleY/Display`；`smile` 节点为 `NODE.0x30=0xE01`、初始 hidden，并带一个普通 CIMG。

Otohime extra mask `0x100` 扩展为 63 条 track：`0x113` 60 条、`0x133` 1 条、`0x143` 2 条；全部目标都是 CIMG 节点，node flags 分布为 `0xF01:61, 0xE01:2`，group 分布为 `eye:48, puru:13, arm:2`，初始 display=false 为 2/63。在 Chiffon/Otohime 两个角色样本内，已观察到的 6+63 条 `0x100` track 都落在动作局部 CIMG 节点；但 full survey 显示该样本内分布不能全局化，具体运行时含义仍未确认。

surfboard/surfboard_EN full survey 中，`TRK.flags` base byte 仍只出现 `0x13/0x23/0x33/0x43`，extra mask 只出现 `0x0/0x100`。日文计数为 `0x13:69084, 0x23:2124, 0x33:7138, 0x43:16265`，extra mask 为 `0x0:91548, 0x100:3063`；EN 计数为 `0x13:69928, 0x23:2125, 0x33:7200, 0x43:16305`，extra mask 为 `0x0:92492, 0x100:3066`。`0x100` 仍不改变 `KEY.0x5B` 的存储类型。

survey JSON 现在输出 `TrackFlagExtra*` 交叉聚合。`0x100` 出现在 JP/EN 的 123/124 个场景；base 分布为 `0x13=1980/1983`、`0x23=177/177`、`0x33=51/51`、`0x43=855/855`。按 track type 计数，主要为 `5(RotateZ)=851/851`、`24(MaterialAlpha)=468/468`、`7(ScaleY)=438/438`、`6(ScaleX)=379/379`、`18(PrimaryImageVariantIndexCandidate)=174/174`。按 key value type 计数，`0x100` 下 JP/EN 为 Float32 `6785/6794` keys、PackedAngleCandidate `3316/3316` keys、Int32 `634/634` keys、Bool `61/61` keys；这再次说明 extra mask 不改变 value storage。

`0x100` 的目标并不全是 CIMG：JP/EN 中 CIMG target 为 1818/1821 条 track，非 CIMG target 为 1245/1245；初始 display=false 仅 214/214。animation 名称也覆盖 `Loop`、`Action`、`TrackSkip_Loop`、`loop_SSS_Plus`、`AdvertiseLoop`、`Action_Wait*`、`DressChange` 等 UI/loop/action 混合上下文。因此当前只能把 `0x100` 记录为 raw extra mask；不能命名为 action-local 或 special-effect flag。type 28 在 full survey 中扩展到 JP/EN 各 29 条 track、31 个 key，分布在 9 个场景，全部有 illumination alpha 初始通道且至少一个 key 匹配；仍按 `IlluminationAlphaCandidate` 保留，不提升为已确认渲染 alpha 规则。

这使 `11(Display)` 的服饰/饰品开关和 `18(PrimaryImageVariantIndexCandidate)` / `19(SecondaryImageVariantIndexCandidate)` 的图片变体/裁剪索引轨道可以与普通 float 曲线区分开。`0x43` 不再写成已确认 `PackedFloat32`；在 Ras 的旋转轨道上下文中只按 packed/fixed angle 候选记录，具体 bit 语义仍待确认。

## 运行时轨道求值证据

已在 32-bit `maimai_dump_.exe` 中复核播放器路径：`Player_UpdateLayers` (`sub_891FB0`) 遍历 `Player` 持有的 layer handle，调用 `LayerHandle_Update` (`sub_7CCDA0`)；后者进入 `Layer_UpdateActiveAnimations` (`sub_7D15E0`)。`Layer_UpdateActiveAnimations` 只在 `Layer+0xD0` enabled 为真时工作，逐 cast、逐 animation index 取 motion lookup，然后用 `Layer+0x28` 的 animation enabled 数组和 `Layer+0x24` 的当前 animation time 调用 `Cast_EvaluateMotionTracks` (`sub_7D30C0`)。

`SbPlayerMO_SetLayerEnabled` (`sub_896560`) 是 layer 开关 API；`SbPlayerMO_SetAnimationEnabled` (`sub_8967A0`) 写入 `Layer+0x28 enabled[animation]`；`SbPlayerMO_SetAnimationRepeat` (`sub_896680`) 写入 `Layer+0x2C repeat[animation]`；`SbPlayerMO_SetAnimationTime` (`sub_896710`) 写入 `Layer+0x24 time[animation]`。设计查看器的 `sub_40B520` 选择动画时正是启用目标 animation、关闭其它 animation、设置 repeat 并把 time seek 到 `0.0`。这些 API 不是设计查看器专用：xref 中 `SetLayerEnabled` 有 102 处引用/42 个 caller，`SetAnimationEnabled` 有 93 处引用/46 个 caller。例如 `SurfboardWrapper_EnableAnimationAtTime` (`sub_5C9440`，536 处引用/261 个 caller) 这个通用 helper 会启用逻辑 animation 并设置时间；`SbMMRasWrapper_Update` (`sub_60A850`) 内有 9 处调用该 helper，用状态帧推进 Ras 的角色动画。

2026-05-31 复核播放/切换时的 reset 行为：`SetAnimationEnabled` 最终只到 `sub_7D1A30` 写 enabled byte，`SetAnimationRepeat` 到 `sub_7D1A00` 写 repeat byte，`SetAnimationTime` 到 `sub_7D1AE0` clamp/wrap 后写 time float；这些 setter 本身没有重置 cast transform、display、图片 slot、颜色或 alpha。真正的复位在 `Layer_UpdateActiveAnimations` 每帧发生：它对每个 cast 先调用 `Cast_ResetAnimatedFields` (`sub_7D2C70`)，把上一帧被动画写脏的字段恢复到 cast/static record 或静态 fallback（例如 transform、scale、display `this+0x168`、material/illumination color、vertex color、primary/secondary image slot 等），随后才遍历当前 enabled 的 animation 并调用 `Cast_EvaluateMotionTracks`。因此运行时模型是“每帧恢复静态值，再叠加所有当前仍启用的 animation”，不是播放结束后永久保留上一帧写入值。

这个 reset 结论解释了 `Change_*` 的持久表现：只要上层没有关闭 `Change_Fashion/Position/Accessory`，它们会在每帧复位之后再次按当前状态帧写回服装/饰品/站位状态，所以播放或切换 `Action_*` 不会抹掉这些状态。反过来，如果上层像设计查看器 `sub_40B520` 那样切换时显式关闭其它 animation，只留下一个目标 animation enabled，那么被关闭的 `Change_*` 不会再参与叠加；它曾写过的字段会在下一次 layer update 的 `sub_7D2C70` 中恢复为静态/fallback 状态。通用 helper `sub_5C9440` 与 Ras wrapper `sub_609E20/sub_60A850` 则是启用并 seek 指定逻辑 animation，不会自动禁用其它状态层。

`AnimationContainer_BuildMotionLookup` (`sub_7DC3B0`) 在 layer 初始化时建立 `castIndex x animationIndex` lookup：运行时 `ANIM+0x200` 是 motion 数，`ANIM+0x204` 是 `MOT` 表指针，每条 `MOT` 的前 2 字节目标节点索引用于把 motion 绑定到 cast。`ANIM+0x208` 被复制为 duration/default end frame，供 `Layer_SetAnimationTime` wrap/clamp；`ANIM+0x20C` 被复制为 default repeat flag。静态 `ANIM.0x56` 因而是播放 duration/end-frame 候选，`ANIM.0x5F` 可提升为 default repeat flag；`ANIM.0x50` 仍按 raw/declared motion count 处理，因为 full survey 中还有一个 raw mismatch。

`Cast_EvaluateMotionTracks` 为每条 `TRK` 建立目标字段表并调用 `Cast_EvaluateTrack` (`sub_7D4F50`) 求值。普通 track 按 `TRK+0x1C` 选择标量/向量求值，写入目标对象字段；`TRK+0x18` 继续对应 key value storage/interpolation 分支。

`type 12/13` 分别写入 cast 当前宽高 `this+0xD8/+0xDC`。`Cast_ResetAnimatedFields` (`sub_7D2C70`) 会把它们恢复到静态 CIMG 宽高；随后 `sub_7D6550/sub_7D8830` 构建普通 CIMG 顶点时，按 `currentWidth/staticWidth`、`currentHeight/staticHeight` 同步缩放静态 pivot，再用动态宽高和缩放后的 pivot 生成矩形。Ras `Action_Joy3 -> kirakira_eye` 在 frame `11..34` 将高度从 `65` 切到 `26`，因此 PNG renderer 和 Viewer 不能只替换高度并保留静态 pivot；共享几何实现需要同步缩放 pivot。

`type 11(Display)` 是运行时可见性轨道。`sub_7D30C0` 将它写到 cast 对象 `this+0x168`，并置 `this+0x11D` dirty；`sub_7D1FC0` 在遍历节点树时把父 cast 的最终可见字段 `this+0xD0` 传给子 cast；`Cast_UpdateRenderState` (`sub_7D4720`) 再组合本节点 display 与父级状态。组合规则是：`this+0x168` 为 0 时最终 `this+0xD0=0`；`this+0x168` 为 1 且 cast/static record `+0x218` 为 1 时，最终值继承父节点可见性；`+0x218` 为 0 时，本节点 local true 直接使最终值为 1。因此 `Display` 可从候选提升为已确认的本节点显示开关；它不是图片变体索引。

`type 18/19` 是两组图片引用槽的运行时索引。`sub_7D4F50` 对 `18` 操作 slot 0 的 `this+0xE0/+0xE2`，对 `19` 操作 slot 1 的 `this+0xF8/+0xFA`；若手动 override 字段为非负则直接使用 override，否则进入 `sub_7D50A0`。`sub_7D50A0` 先用关键帧求出组内 index，再经 `sub_7D5590` 映射到 CAST/CREF 图块并把 4 个图块坐标复制到 `this+0xE4..0xF0` 或 `this+0xFC..0x108`；`TRK+0x1C == 3` 时还会经 `sub_7D51D0/sub_7D5740` 在两组图块坐标之间插值。`sub_7D1FC0` 将两组 slot index 传入 `sub_7D4960`，输出构建函数 `sub_7DAE10/sub_7DB530` 再展开为 CAST index、CREF index 和 UV/矩形坐标。因此 `18` 是 primary image slot index，`19` 是 secondary image slot index。

静态 `CIMG.0x45` 与 type 18/19 最早 key 经常一致，是对应 primary/secondary 组的静态 fallback/default index；运行时 type 18/19 会在播放时覆盖当前 slot。由于 full survey 已显示二者不是全覆盖一致，`CIMG.0x45` 仍不能命名为动画当前值或选中状态。

type 18 / 19 现在也进入 group-specific full survey 校验，语义层命名分别为 `PrimaryImageVariantIndexCandidate` 和 `SecondaryImageVariantIndexCandidate`。旧的 `ImageVariant*` 字段仍保留 type 18 的 primary+secondary 合计宽松检查，仅用于兼容早期统计；新增 `ImageVariantGroup*` 字段按 CIMG 的 primary/secondary CREF 组分别检查 `18 primary` 和 `19 secondary`。校验规则是：用 motion target node 找到对应 CIMG，取目标节点对应 CREF 组的记录数，逐个 key value 检查它是否为整数且满足 `0 <= value < group CREF count`。结果如下：

| Survey | Group | Tracks | Keys | Tracks with CIMG | Track range matches | Keys in range | Out / non-int / missing |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| surfboard | `18 primary` | 1,791 | 6,986 | 1,791 | 1,791 | 6,986 | 0 |
| surfboard | `19 secondary` | 333 | 9,769 | 333 | 333 | 9,769 | 0 |
| surfboard_EN | `18 primary` | 1,792 | 6,988 | 1,792 | 1,792 | 6,988 | 0 |
| surfboard_EN | `19 secondary` | 333 | 9,769 | 333 | 333 | 9,769 | 0 |

type 18 primary 目标节点的 CREF count 分布覆盖 1..16，其中 2、3、4 和 16 最常见；key value 分布为 0..15。type 19 secondary 的 CREF count 分布为 `2:41, 5:16, 32:276`，key value 分布为 0..31。该结果把已核对的 `sub_7CE8B0` 分组范围检查逻辑和 JP/EN full survey 静态数据对齐：type 18 只按 primary 组范围检查，type 19 只按 secondary 组范围检查。

同一批 full survey 还把 type 18/19 的最早 key 与目标 `CIMG.0x45` 组内 index 做了静态比较：

| Survey | Group | First key == `CIMG.0x45` | Mismatch / other |
| --- | --- | ---: | ---: |
| surfboard | `18 primary` | 1,533 | 258 mismatch |
| surfboard | `19 secondary` | 332 | 1 mismatch |
| surfboard_EN | `18 primary` | 1,534 | 257 mismatch + 1 multi-CIMG target |
| surfboard_EN | `19 secondary` | 332 | 1 mismatch |

因此 `CIMG.0x45` 可以确认是静态存储的 primary/secondary 组内 fallback/default index，并且经常与变体轨道起始值一致；但该关系不是全覆盖，不能全局命名为动画初始 key、当前引用或选中引用。运行时代码确认 type 18/19 会覆盖两组当前图片 slot，display 则由 type 11 独立控制。

按 `base byte = flags & 0xFF` 的 nibble 汇总；extra mask 不参与 high nibble 计算：

| Half | Value | surfboard tracks | surfboard_EN tracks | 观察 |
| --- | ---: | ---: | ---: | --- |
| Low | `0x3` | 94,611 | 95,558 | 当前 full survey 中所有 track 都设置；仅作为通用 track/key 标记候选。 |
| High | `0x1` | 69,084 | 69,928 | 对应 `KEY.0x5B = 0x000A Float32`。 |
| High | `0x2` | 2,124 | 2,125 | 对应 `KEY.0x5B = 0x0008 Int32`。 |
| High | `0x3` | 7,138 | 7,200 | 对应 `KEY.0x5B = 0x0001 Bool`。 |
| High | `0x4` | 16,265 | 16,305 | 对应 `KEY.0x5B = 0x000B PackedAngleCandidate`，旋转轨道上下文中按 signed fixed-angle raw int 候选解释。 |

## Track type 证据

主 Markdown 报告现在会按 track type 输出 flags、`KEY.0x5B` 类型、插值分布、值域和动画/节点样例。Ras 中几个关键 type 的证据如下：

| Type | Name | Tracks | Keys | Flags / value type | Value range | 观察 |
| ---: | --- | ---: | ---: | --- | --- | --- |
| 2 | `TranslateZCandidate` | 302 | 302 | `0x13` / Float32 | `0` | 全部值为 0，常见于动作节点，仍是候选轴。 |
| 3 | `RotateXCandidate` | 21 | 21 | `0x43` / PackedAngleCandidate | `0` | 少量手部/帽子缎带节点，全部值为 0。 |
| 4 | `RotateYCandidate` | 19 | 19 | `0x43` / PackedAngleCandidate | `0` | 与 type 3 类似，全部值为 0。 |
| 8 | `ScaleZCandidate` | 10 | 10 | `0x13` / Float32 | `1` | 常见于 `Ras_null`，全部值为 1。 |
| 18 | `PrimaryImageVariantIndexCandidate` | 81 | 854 | `0x23` / Int32 | `0,1,2,3` | 851/854 keys 为 step，所有值均落在对应 CIMG primary CREF 组范围内。 |
| 21 | `MaterialColorRCandidate` | 4 | 4 | `0x13` / Float32 | `0,0.902` | `Action_Joy3 -> hart_*`，值与 `TRS2.0x37` 材质色 R 通道归一化结果一致。 |
| 22 | `MaterialColorGCandidate` | 4 | 4 | `0x13` / Float32 | `0,0.914` | `Action_Joy3 -> hart_*`，值与材质色 G 通道一致。 |
| 23 | `MaterialColorBCandidate` | 4 | 4 | `0x13` / Float32 | `0,0.816` | `Action_Joy3 -> hart_*`，值与材质色 B 通道一致。 |
| 24 | `MaterialAlpha` | 58 | 168 | `0x13` / Float32 | `0..1` | 写入 `MaterialColor.A`；Fade/effect/dress 等通过 effective opacity 生效。 |
| 25 | `IlluminationColorRCandidate` | 4 | 4 | `0x13` / Float32 | `0,0.875` | `Action_Joy3 -> hart_*`，值与 `TRS2.0x38` illumination R 通道一致。 |
| 26 | `IlluminationColorGCandidate` | 4 | 4 | `0x13` / Float32 | `0` | illumination G 通道候选。 |
| 27 | `IlluminationColorBCandidate` | 4 | 4 | `0x13` / Float32 | `0,1` | illumination B 通道候选。 |
| 28 | `IlluminationAlphaCandidate` | 4 | 4 | `0x13` / Float32 | `1` | illumination alpha 候选。 |

这说明 type 18 已经有强结构证据和运行时证据可按 primary 图片变体索引处理；新增 group-specific full survey 也确认了 type 19 的 secondary 组范围关系。运行时使用 `CIMG.0x45` 对应的静态组内 index 作为 fallback/default，再由 type 18/19 覆盖当前 slot；`CIMG.0x45` 仍不能直接命名为当前或选中状态。

full survey 现在输出 `TransformTrack*` 聚合。type `0/1/5/6/7` 能绑定目标 `TRS2` 初始通道，type `2/3/4/8` 没有可比的 `TRS2` 初始通道，但全部 key 都等于候选默认值：`2=0`、`3=0`、`4=0`、`8=1`。这只确认存储和值域，不确认 Z 轴或 X/Y rotation 的运行时语义，也不能写成初始化规则。

| Survey | Transform tracks | Keys | Initial channel present | Initial channel matched | Initial mismatch | Candidate default keys |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| surfboard | 68,482 | 124,712 | 67,918 | 64,375 | 3,543 | `2=345, 3=84, 4=81, 8=54` |
| surfboard_EN | 69,174 | 125,579 | 68,610 | 65,001 | 3,609 | `2=345, 3=84, 4=81, 8=54` |

transform track 的 value storage 也已分开：type `0/1/2/6/7/8` 使用 `0x000A Float32`，type `3/4/5` 使用 `0x000B PackedAngleCandidate`。type `0x0B` 旧称 `PackedFloat32` 不准确；在 type `5(RotateZ)` 和少量 type `3/4` 旋转候选中，当前只按 signed fixed-angle raw int 候选解释，候选公式为 `degrees = raw * 180.0 / 32768.0`。示例 raw `910/1820/5461/16383/32767` 分别约为 `5/10/30/90/180 deg`。该解释仅限旋转轨道候选，不应用到 camera flags 等字段。

渲染器还需要单独处理坐标系方向：上式得到的是 scene/source 角度；当前 CLI PNG renderer 和 Viewer 在 2D 像素坐标（Y 向下）中使用 `-degrees` 作为矩阵旋转角。Ras 的 `plain_leg_R1` 静态 `TRS2.0x32 raw=5461` 解码约为 `+30 deg`，按 `-30 deg` 应用后右腿链与身体对齐；直接按 `+30 deg` 应用会复现旧的右小腿/鞋子外甩问题。

full survey 现在把 `KEY.0x5B type 0x0B` 作为 packed-angle candidate 单独聚合。surfboard/surfboard_EN 两组结果如下：

| Survey | Tracks | Keys | Track type distribution | Key distribution | Raw distinct | Raw range |
| --- | ---: | ---: | --- | --- | ---: | --- |
| surfboard | 16,265 | 45,602 | `3:84, 4:81, 5:16100` | `3:84, 4:81, 5:45437` | 1,092 | `-75548..83740` |
| surfboard_EN | 16,305 | 45,690 | `3:84, 4:81, 5:16140` | `3:84, 4:81, 5:45525` | 1,092 | `-75548..83740` |

两组 top raw 值排序一致：`0` 最多（23,523 / 23,573 keys），其次是 `910/-910`（约 `+/-5 deg`）、`546/-546`（约 `+/-3 deg`）、`1820/-1820`（约 `+/-10 deg`）等。raw 值可超出 16-bit signed 范围；对抗校验后按 signed binary angle 比例换算，渲染矩阵不先做 16-bit 截断，超过一圈的值按同一公式参与旋转。

Otohime 补充了 type 21/22/23 的候选证据：`Change_Fashion -> momo_circle_*` 的 17 个 key 值与这些节点 `TRS2.0x37` 的材质色 RGB 通道对应，部分通道相差一个 8-bit step；同一批节点的 illumination RGB 为 0，type 25/26/27 也全为 0。当前颜色字段按 4 字节 `A,R,G,B` 候选解释，JSON/Markdown 中的 `Hex` 为 `#AARRGGBB`。

full survey 现在把颜色相关 track 与目标节点初始 `TRS2` 颜色通道交叉统计。所有 key value 都在 `0..1`，且所有目标节点都能取到对应初始通道；但并非每条 track 都包含等于初始通道的 key，因此这里只作为颜色通道关联证据，不写成初始化规则：

| Survey | Color tracks | Keys | Initial channel present | Initial channel matched | Key out-of-range |
| --- | ---: | ---: | ---: | ---: | ---: |
| surfboard | 5,726 | 11,157 | 5,726 | 5,371 | 0 |
| surfboard_EN | 5,774 | 11,231 | 5,774 | 5,437 | 0 |

按 track type 看，JP/EN 的初始通道匹配分别覆盖 `21:920/932`、`22:914/928`、`23:919/935`、`25:866/874`、`26:860/868`、`27:863/871`、`28:29/29` 条 track。type 28 在当前 full survey 中全部匹配 illumination alpha 初始值。

type 24 现在有单独的 MaterialAlpha 证据表。它与 `TRS2.0x37` 的材质 alpha 有匹配证据：Ras 58 条 type 24 中有 56 条至少一个 key 按 survey 初始 alpha 匹配规则命中，Chiffon 为 34/34，Otohime 为 37/37；同时它也作用于无 CIMG 的 root/null/control 节点，例如 `FadeIn1/FadeOut1 -> *_root`。参考运行时将 type 24 写入 `MaterialColor.A`，因此当前 renderer 将它作为 material alpha / effective opacity 动画通道处理。

PNG renderer / Viewer 当前按节点树累计 effective opacity：本节点 alpha 与父级 effective opacity 相乘，因此无 CIMG 的控制节点也能通过 type 24 或 material alpha 使整条子树透明。

full survey 中 type 24 的 key value 也全部在 `0..1`，所有目标节点都有 `TRS2.0x37` material alpha；初始 alpha 匹配不是全覆盖，说明它可以从 bind alpha 动画到其它 opacity：

| Survey | Type 24 tracks | Keys | With material alpha | Initial alpha matched | CIMG targets | display=false targets | Key out-of-range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| surfboard | 9,849 | 19,242 | 9,849 | 8,142 | 4,675 | 2,153 | 0 |
| surfboard_EN | 9,977 | 19,427 | 9,977 | 8,229 | 4,701 | 2,173 | 0 |

## KEY 字段与插值候选

一条 key 由以下字段组成：

| 字段 | 当前语义 |
| --- | --- |
| `0x5A` | key frame。 |
| `0x5B` | key value；类型由 track flags 决定。 |
| `0x5C` | interpolation selector：`0` hold，`2` Hermite，其它非零值 linear。 |
| `0x5D` | Hermite incoming tangent；由下一 key 作为段入斜率使用。 |
| `0x5E` | Hermite outgoing tangent；由当前 key 作为段出斜率使用。 |

Ras 中 `0x5C` 只观察到三类值：

| Value | CLI 候选名 | Key count | 观察 |
| ---: | --- | ---: | --- |
| 0 | `StepOrConstant` | 2,020 | 主要见于 display/bool state，tangent 通常为 0。 |
| 1 | `Linear` | 12,648 | 最常见曲线类型，tangent 可为 0。 |
| 2 | `Spline` | 4,966 | 常见非 0 tangent；PNG renderer 按运行时代码的三次 Hermite 公式求值。 |

按插值值统计非零 tangent：

| Value | Name | Keys | Non-zero tangent keys |
| ---: | --- | ---: | ---: |
| 0 | `StepOrConstant` | 2,020 | 0 |
| 1 | `Linear` | 12,648 | 139 |
| 2 | `Spline` | 4,966 | 802 |

`0x5D/0x5E` 的跨样本统计：

| Sample | Tangent keys | `0x5D == 0x5E` | `0x5D != 0x5E` | Non-zero tangent keys | Non-zero by interpolation |
| --- | ---: | ---: | ---: | ---: | --- |
| Ras | 19,634 | 19,634 | 0 | 941 | `0:0, 1:139, 2:802` |
| Chiffon | 20,840 | 20,840 | 0 | 2,351 | `0:0, 1:485, 2:1866` |
| Otohime | 38,518 | 38,436 | 82 | 1,575 | `0:0, 1:129, 2:1446` |

Otohime 的 82 个 `0x5D != 0x5E` key 全部属于 type 5 `RotateZ`，覆盖 64 条 track；按 interpolation 分布为 Linear 14、Spline 68。主 Markdown/inspect 报告会输出 mismatch 样例，例如 `Action_Wait2 -> uniform_lower_R01_a`、`Action_Joy1 -> sune_R_point`。

survey JSON 现在输出 `KeyInterpolation*` 和 `KeyTangent*` full-survey 交叉聚合。surfboard/surfboard_EN 中 `TRK.0x57` key count mismatch 均为 0；`0x5C` 分布为 JP `Linear=107658, Spline=51674, StepOrConstant=27448`，EN `Linear=108687, Spline=51787, StepOrConstant=27528`。所有 key 都带 `0x5D/0x5E` 候选值，tangent present 为 186780/188002；非零 tangent 为 9757/9777，出现在 45/46 个场景，按 interpolation 为 `Spline=8293/8312`、`Linear=1464/1465`、`StepOrConstant=0/0`。新增 `KeyTangentNonZeroFramePositionCounts`、`KeyTangentMismatchFramePositionCounts` 和 `KeyTangentDeltaSignCounts` 用于复算 key 序列位置与相邻 value 变化方向。

非零 tangent 的 key 序列位置为 JP/EN `first=3003/3021`、`middle=4648/4649`、`last=2037/2038`、`single=69/69`。`0x5D != 0x5E` mismatch 只出现在 `middle=116/116` 和 `last=7/7`，未见 first/single mismatch；因此差异不是单纯首帧初始化字段，也不是只在孤立 key 上出现。

survey JSON 同时输出 `TrackKeyStorageMatrixCounts`、`TrackFieldSequenceCounts`、`KeyFieldSequenceCounts`、`TrackFrameRangeRelationCounts`、`TrackKeyFrameOrderCounts`、`TrackKeyFrameDuplicateCounts`、`TrackFirstFrameDeltaCounts` 和 `TrackLastFrameDeltaCounts`。JP/EN 中 first frame 全部等于最小 key frame；last frame 等于最大 key frame的 track 为 91,185/92,114，last frame 大于最大 key frame的 track 为 3,426/3,444。该结果只确认 track range 与 key frame 的结构包含关系，不确认播放区间、hold 行为或插值端点规则。

full survey 的 `0x5D != 0x5E` mismatch 两组均为 123，出现在 9 个场景。mismatch 按 interpolation 为 `Spline=107`、`Linear=16`；按 track type 为 `5(RotateZ)=104`、`1(TranslateY)=11`、`6(ScaleX)=4`、`7(ScaleY)=4`；按 extra mask 为 `0x0=102`、`0x100=21`。场景包括 `MM_CH_Otohime__Otohime_00.sbscene` 的 82 个 RotateZ key，以及 `MM_UI_GameInfo__MM_UI_Shutter.sbscene`、`MM_UI_MusicSelect__Reference_another_OBJ_big.sbscene`、`MM_UI_Collection__Reference_Category_main.sbscene`、`MM_UI_MusicSelectPANDORA__Difficulty_UI.sbscene` 等 UI 场景。因此旧的“mismatch 只在 Otohime RotateZ”只能作为三样本阶段观察，不能作为 full-survey 结论；`0x5D` 和 `0x5E` 不能全局合并为一个字段，当前继续按 tangent in/out 或附加双参数候选保留。

`KeyTangentDeltaSignCounts` 以 `scope|interpolation|trackType|in/out sign|prev/nextDelta sign` 形式记录相邻 value 方向。mismatch 中 116/116 个 key 两侧均可比较，其中 `0x5D` 与前一段 delta 同号为 106/106，`0x5E` 与后一段 delta 同号为 90/90，两端都同号为 74/74；但整体非零 tangent 中两侧可比较为 4648/4649，只有 486/486 两端都同号，301/302 两端都不同号，说明非零 tangent 不能简化为相邻 key value 的普通差分。

Ras 的非零 tangent 候选主要集中在 type 5 `RotateZ`（778 个）、type 7 `ScaleY`（125 个）、type 1 `TranslateY`（34 个），另有少量 type 6 和 type 24。运行时代码的 spline 分支使用当前 key 的 `0x5E` 作为 outgoing tangent、下一 key 的 `0x5D` 作为 incoming tangent，公式等价于三次 Hermite；PNG renderer 已按该公式求中间帧。

## 动画到节点绑定

语义层现在会把 `MOT.0x51` 按节点索引候选解析，并生成 `AnimationBindingInfo`。Ras 样本中 2,736 个 motion 都能形成动画到节点绑定；full survey 中 JP/EN 32,428/32,742 个 motion 的 `0x51` 也全部为 in-range NODE index。

典型样例：

| Animation | Node | Track types | 结论 |
| --- | --- | --- | --- |
| `Change_Fashion` | `plain_apron_ribon`、`uniform_cap_L`、`plain_onepiece_body` 等 | 主要 `11(Display)`，少量 `18`、`0/1`、`5` | Ras 中 display 轨道集中在服饰部件，少量节点还带 primary image slot、位置/旋转轨道。 |
| `Change_Accessory` | `accessory_L_fruittea`、`tray`、`coffecup`、`bread` 等 | `11(Display)` | Ras 中这些饰品/道具节点只观察到 display 轨道。 |
| `Mouth_Wait1` | `koukando01_mouth` | `12`、`13`、`18`，并带 `0/1/6/7` | Ras 中嘴部节点带 mouth-shape 候选、primary image variant index 候选和局部 transform 候选轨道。 |
| `Action_Wait1` | `Ras_null`、`tail`、服饰飘带、身体部件等 | 主要 `5(RotateZ)`，并带 `0/1/6/7/11` | Ras 中以旋转候选轨道为主，同时有平移、缩放和 display 轨道。 |
| `DressChange` | `Ras_root`、`Ras_null`、身体/服饰部件 | 包含动作动画同类 transform track，并含 `24(MaterialAlpha)` | Ras 中同时出现 transform 与 material alpha 轨道；是否存在额外状态机仍未确认。 |

## 状态轨道识别

`TRK` 被标为 state track candidate 的条件：

- track 名称包含 `visible`、`visibility`、`display`、`alpha`、`opacity`、`hide`、`show`、`state`、`enable`、`disable`、`onoff`。
- 或该 track 的 key value 候选值按 survey 二值判定规则全部命中 `0` 或 `1`。

这类 track 会生成 `VariantHint`，`SourceKind = TrackState`。对 type 11 来说，运行时代码已确认它是 display/visibility 开关；对 type 18/19/24 等其它状态候选，仍需要按各自运行时消费者或样本交叉证据解释。

## 状态/开关轨道摘要

当前输出器会把 `11(Display)`、`18(PrimaryImageVariantIndexCandidate)`、`19(SecondaryImageVariantIndexCandidate)`、`24(MaterialAlpha)` 汇总成状态轨道表。Ras 样本没有 type 19，因此下表只出现三类。

Ras 中三类状态候选总量：

| Track type | Name | Tracks | Keys |
| ---: | --- | ---: | ---: |
| 11 | `Display` | 745 | 1,185 |
| 18 | `PrimaryImageVariantIndexCandidate` | 81 | 854 |
| 24 | `MaterialAlpha` | 58 | 168 |

重点动画中的状态轨道：

| Animation | Display | Primary variant | Secondary variant | Material alpha | 观察 |
| --- | ---: | ---: | ---: | ---: | --- |
| `Change_Fashion` | 105 | 6 | 0 | 0 | Ras 中服饰组主要带 display 轨道，少量节点带 type 18 primary image slot 轨道。 |
| `Change_Accessory` | 8 | 0 | 0 | 0 | Ras 中这 8 条状态轨道全是 display；未见 type 18/19/24。 |
| `FadeIn1` | 1 | 0 | 0 | 2 | Ras 中同时有 display 与 alpha/opacity 候选轨道。 |
| `Effect_Heart1` | 2 | 0 | 0 | 13 | Ras 中该动画含 13 条 alpha/opacity 候选轨道。 |
| `Mouth_*` | 多数动画 1 | 每个口型 motion 至少 1 | 0 | 0 | Ras 中口型 motion 普遍带 type 18，key value 落在嘴部 CIMG primary CREF 组范围内。 |
| `DressChange` | 40 | 3 | 0 | 0 | Ras 中同时有 display 和少量 primary image variant 候选轨道。 |

`PrimaryImageVariantIndexCandidate` 的 key value 在 Ras 中全部通过 primary CREF 组范围检查。结合已核对的分组范围检查逻辑和运行时 slot 写入逻辑，type 18 按 primary CREF 组范围理解；type 19 按 secondary CREF 组范围理解。例如 `forearm_L01` 的 values 为 `0,1,2`，对应 primary image refs；多个嘴部节点的 type 18 key 落在 2-4 条 primary 口型/表情 image refs 范围内。full survey 中 `CIMG.0x45` 与最早 key 只是高比例对齐，不是全覆盖关系，因此状态轨道摘要不把 `0x45` 当作当前/选中状态来源。

Ras 的服饰/饰品状态由“启用对应 animation + seek 到状态帧”驱动，而不是由 type 18/19 单独驱动。`Change_Fashion` 的 type 11 display keys 在 frame `0..3` 上切换 plain/uniform/gorgeous 等部件；`Change_Accessory` 同样用 frame `0..3` 切换手部道具，`tray` 在 frame 1 与 frame 3 为 true，按 step/hold 在 frame 2 也保持显示。Ras wrapper 的 `sub_609E20` 会刷新 logical animation `1/2/3/4`，在 Ras 样本中对应 `Change_Fashion/Change_Position/Change_Accessory/Effect_Heart1`；其中服饰 frame 来自 `this+0x198` 经 `dword_F40714` 指向对象的 `+0x38` 表 record `+0x44` 映射后的值或 `this+0x228` override，饰品 frame 直接来自 `this+0x1E8`。随后 `Layer_UpdateActiveAnimations` 把该时间传给 `Cast_EvaluateMotionTracks`，最终每个 cast 的 type 11 写入可见性。

`Change_*` 和 `Action_*` 的差异不只是名称。当前 6 个标准角色样本（Chiffon/Milk/Otohime/Ras/Salt/Shama，gage 的 `DressChange` 特例不计入标准角色动作组）中，`Change_Fashion`、`Change_Position`、`Change_Accessory` 都位于固定低 animation index：`1/2/3`，随后是 `Effect_Heart1`，再从 `Action_Wait1` 开始进入动作/口型序列。`Change_Fashion` 与 `Change_Accessory` 合计 665 条 track，其中 593 条是 `11(Display)`；key frame 基本集中在 `0..3`，Otohime 少量非 display 状态/颜色轨道覆盖 `-1..4`，Salt 饰品扩展到 `0..6`。这些轨道用于选择服装、配饰、手持道具以及少量图块/颜色状态。`Change_Position` 合计 29 条 track，集中在 root/body 的 `0/1/6/7` transform 状态，frame `0..5`，更像基础站位/比例状态。

同一批标准角色里，`Action_*` 合计 55,024 条 track，主导类型是 `5(RotateZ)`、`0/1(TranslateX/Y)`、`6/7(ScaleX/Y)`，时间轴覆盖 `-20..227` 一类动作范围；它们也有 2,030 条 `11(Display)`、623 条 `24(MaterialAlpha)` 和 381 条 `18(PrimaryImageVariantIndexCandidate)`，但这些主要服务动作局部特效、表情或姿态切换，不是服装/配饰选择。按 `(node, track type)` 交叉，`Change_Fashion/Accessory` 的 631 个状态目标中只有 22 个被任一 `Action_*` 的同类状态轨道覆盖；即使有 366 个 Change 状态节点也被 Action 的其它 transform 轨道触达，Action 通常不会写回同一 display/image/color 状态。因此实机上表现为：`Change_*` 被启用并 seek 到某个状态帧后，会在每帧 reset 之后作为基础状态重新叠加；切换或播放 `Action_*` 只覆盖它自己写到的字段，不会自动把服装或手持物恢复到初始文件状态。只有当上层显式禁用对应 `Change_*` 时，这些状态才会在下一次 layer update 中随 dirty reset 回落到静态/fallback 值。

`DressChange` / `Action_Change` 不等同于这类持久状态选择。它们的 track 分布接近动作动画，覆盖大量 transform，时间轴常到 `200+` frame，并包含 display、image variant 或 alpha 作为换装过程中的过渡效果。当前应把 `Change_Fashion/Position/Accessory` 理解为可长期启用的逻辑状态层，把 `Action_*` / `DressChange` 理解为动作或过渡层；Viewer 若要复现实机角色外观，应在播放 Action 时叠加当前 `Change_*` 状态帧，而不是只播放一个互斥动画。

## 命名模式

当前识别以下动画名模式：

| 模式 | Category | 说明 |
| --- | --- | --- |
| `Change_Fashion*` | Fashion | 服饰切换线索。 |
| `Change_Position*` | Position | 站位/姿态切换线索。 |
| `Change_Accessory*` | Accessory | 饰品切换线索。 |
| `DressChange*` | DressChange | 换装动画线索。 |
| `Action*` | Action | 动作动画线索。 |
| `Mouth*` | Mouth | 口型动画线索。 |

节点名前缀也参与分组：

| 前缀 | Category |
| --- | --- |
| `plain_`、`uniform_`、`gorgeous_`、`present_` | Fashion |
| `acc_`、`accessory_`、`acs_` | Accessory |
| `mouth_`、`lip_` | Mouth |
| `face_`、`eye_`、`brow_` | Expression |
| `pos_`、`position_` | Position |

## 待确认项

- `TRK.flags` base byte 的低 nibble `0x3` 的 bit 含义；base byte 高 nibble 与 key value storage 的关系在 JP/EN full survey 中已稳定，但仍只作为存储分类，不命名低位 bit 业务语义。
- `TRK.flags` base byte `0x43` / storage nibble `0x4` 的 storage 名称当前写作 `PackedAngleCandidate`；signed fixed-angle raw int 只是旋转上下文的解释候选，仍不是已确认 packed float。
- `KEY.0x5C/0x5D/0x5E` 在更多 UI/特殊 track 上的边界行为。
- `this+0x198/0x19C/0x1E8/0x224/0x228` 这些 Ras wrapper 状态字段的业务来源，以及 `dword_F40714` 指向对象的 `+0x38` 表 record `+0x44` 的数据含义。
- `CIMG.0x45` 与 type 18/19 起始 key 经常一致但不是全覆盖；运行时可作为静态 fallback/default slot index 理解，但不能命名为当前或选中状态。
- `DressChange` 是否只是复合动画，还是有额外状态机字段。
