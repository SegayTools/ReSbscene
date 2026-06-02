# sbscene 格式记录

本文档记录当前解析器使用的 `.sbscene` 结构知识。状态分为：

- Confirmed：可由当前解析器、survey JSON 或样本 raw bytes 复算的结构事实。
- Confirmed(raw)：字段边界、原始值或值域已确认；运行时语义只在说明中明确确认时才成立。
- Candidate：候选命名、候选公式或候选解释，仅用于导航和排查，不作为正式协议语义。
- Unknown semantics：保留原始字节和值分布，不强行解释运行时用途。

## 文件头

| 字段 | 大小 | 状态 | 说明 |
| --- | ---: | --- | --- |
| Magic | 4 | Confirmed | ASCII `VTBF`。非该值直接拒绝解析。 |
| RootLength | 4 | Confirmed | surfboard/surfboard_EN full survey 中 314/314 与 318/318 个样本均为 `0x10`。老的合成测试格式可省略该根头。 |
| RootTag | 4 | Confirmed | surfboard/surfboard_EN full survey 中 314/314 与 318/318 个样本均为 `SRFF`。 |
| RootParam | 4 | Unknown | full survey 中 314/314 与 318/318 个样本均为 raw `0x0100004C`，拆分为 `ParamLow=1 / ParamHigh=19456`；用途未确认。 |
| Blocks | variable | Confirmed | 从 `RootLength` 指向的位置开始，是 `vtc0` 预序 chunk 树；每个块声明自己的 child count 与 field count，children 紧随本块字段 payload 之后。 |

## vtc0 块

当前真实样本按 `vtc0` 预序 chunk 树解析。块长度只覆盖 `Tag + childCount/fieldCount + 本块字段 payload`，不包住子块；解析器先读取本块字段，再按 `childCount` 递归读取紧随其后的 children。旧报告中的 `ParamLow/ParamHigh` 现在仅作为兼容别名保留，真实含义分别是 `ChildCount/FieldCount`。

| 字段 | 大小 | 状态 | 说明 |
| --- | ---: | --- | --- |
| Marker | 4 | Confirmed | ASCII `vtc0`。 |
| Length | 4 | Confirmed | little endian `int32`，表示 marker+length 之后的块内容长度。 |
| Tag | 4 | Confirmed | 4 字节 ASCII 块名，例如 `SRFF`、`NODE`、`ANIM`、`MOT `。 |
| ParamRawHex | 4 | Confirmed | 原始 `childCount/fieldCount` 字节顺序；JSON/Markdown 均保留，避免字节序解释覆盖原始证据。full survey 根块均为 `0x0100004C`。 |
| ChildCount / legacy ParamLow | 2 | Confirmed | little endian `uint16`，声明紧随本块字段之后的 child block 数量。语义层中常对应 motion 数、texture 数或 resource group 数。 |
| FieldCount / legacy ParamHigh | 2 | Confirmed | little endian `uint16`，声明本块 compact fields/record fields 数量；记录块中常是记录字段/条目计数。 |
| Fields/Records | `Length - 8` | Confirmed | 紧凑字段或记录数组。 |

解析器仍保留旧的 `vtc0 + propertyCount + childCount` 合成测试格式，以便做异常测试；真实样本走 `childCount/fieldCount` 预序解析路径。

关键块的 `ChildCount/FieldCount` 观察（输出中的 legacy `ParamLow/ParamHigh` 与其同值）：

| 块 | ParamLow | ParamHigh |
| --- | --- | --- |
| `ANIM` | raw count-like value；full survey 中 JP/EN 分别有 1,167/1,170 个等于实际 motion 数、412/444 个等于 motion 数 + 1，不能全局命名为 motion 数。 | 字段数量，full survey 中 JP/EN 的 1,579/1,614 个 `ANIM` 块恒为 4。 |
| `MOT ` | track 数量；full survey 中 JP/EN 的 32,428/32,742 个 motion 均与 `MOT.0x52` 和实际 track 数一致。 | 字段数量，full survey 中恒为 2。 |
| `TRK ` | full survey 中 JP/EN 的 94,611/95,558 个 `TRK` 块恒为 1。 | 字段数量，full survey 中 JP/EN 的 94,611/95,558 个 `TRK` 块恒为 5。 |
| `KEY ` | full survey 中 JP/EN 的 94,611/95,558 个 `KEY` 块恒为 0。 | KEY 字段数量；full survey 中 JP/EN 的 94,611/95,558 个 `KEY.ParamHigh % 5` 全为 0，且 `ParamHigh / 5` 等于实际 key 数。 |
| `NODE` | full survey 中恒为 0。 | 记录字段条目数；JP/EN 的 314/322 个 `NODE` 块中 `ParamHigh` 均等于实际 parsed field count。 |
| `TRS2` | full survey 中恒为 0。 | 记录字段条目数；JP/EN 的 314/322 个 `TRS2` 块中 `ParamHigh` 均等于实际 parsed field count。 |
| `NCAT` | full survey 中恒为 0。 | 记录字段条目数；JP/EN 的 314/322 个 `NCAT` 块中 `ParamHigh` 均等于实际 parsed field count。语义层聚合所有 `NCAT` 块后，`NCAT.0x0E` 记录数等于 NODE 数量的场景为 314/314 与 318/318。 |
| `DATA` | 后续 primary resource block 数 | 空 payload；按文件内 DATA 顺序逐块校验，`ParamLow` 均等于对应区间内后续 `CIMG+CNUM+CRFD+CSLI` 块数，文件级匹配为 314/314 与 318/318。EN 有 2 个文件各含 3 个 `DATA` 块，因此 EN `DATA` tag 总数为 322。`CREF/SLIC/TEXT` 不计入。 |
| `SCN ` | scene/layer count-like value；full survey 中 JP 为 `1` x314，EN 为 `1` x316、`3` x2；与 `SCN.0x10` 和同场景 `LAYR` 块数全量一致。 | 字段数量；full survey 中 JP/EN 的 314/318 个 `SCN ` 块均为 `ParamHigh=6`，实际 parsed field count 也为 6，trailing bytes 为 0。 |
| `LAYR` | raw count-like value；多数块满足 `ParamLow = 0x22 + 1`，JP 313/314、EN 321/322。唯一例外为 `MM_UI_Entry__MM_UI_Select_EntryName_ALL.sbscene`，`ParamLow=6`、`0x22=4`。 | 字段数量；full survey 中 JP/EN 的 314/322 个 `LAYR` 块均为 `ParamHigh=4`，实际 parsed field count 也为 4，trailing bytes 为 0。 |
| `CIMG` | 后续 CREF 组数量；full survey 中只见 0/1/2，JP 为 49/6,608/406，EN 为 49/6,773/406。 | 字段数量，full survey 中恒为 10。 |
| `CREF` | full survey 中恒为 0。 | packed CREF 记录数量；full survey 中记录数为 1、2、3、4、5、6、7、8、9、10、11、12、14、16、32 或 70。 |
| `CNUM` | full survey 中 JP/EN 各 217 个 `CNUM` 块均为 1。 | raw `ParamHigh=26`；实际 parsed field count 为 27，因为 declared high count 后还会解析 zero marker 和后续 compact 字段。运行时语义未命名。 |
| `CRFD` | full survey 中 JP/EN 的 614/622 个 `CRFD` 块均为 0。 | 字段数量；full survey 中 JP/EN 的 614/622 个 CRFD 均为 `ParamHigh=7`。 |
| `CSLI` | full survey 中 JP/EN 的 26/26 个 `CSLI` 块均为 2。 | 字段数量；full survey 中 JP/EN 的 26/26 个 CSLI 均为 `ParamHigh=13`，实际 parsed field count 也为 13。 |
| `SLIC` | full survey 中 JP/EN 的 26/26 个 `SLIC` 块均为 0。 | 记录字段条目数；full survey 中 JP/EN 均为 `ParamHigh=33` x21、`ParamHigh=99` x5，实际 parsed field count 全部匹配。 |
| `TEXT` | full survey 中 105/131 均为 0。 | raw `ParamHigh=7`；实际 parsed field count 为 8，因为 zero marker 后还会解析 `0x7C/0x41` 等 compact 字段。运行时语义未命名。 |
| `CAM ` | full survey 中 JP/EN 的 314/318 个 `CAM` 块均为 0。 | 字段数量；full survey 中 JP/EN 均为 `ParamHigh=6`，实际 parsed field count 也为 6，trailing bytes 为 0。 |

survey aggregate 现在直接输出 `VtbfTagCounts`、`VtbfTagParamRawCounts`、`VtbfTagParamLowHighCounts`、`VtbfTagParamHighPropertyCountCounts`、`VtbfTagTrailingByteCounts`、`VtbfKeyParamHighModulo5Counts` 和全量 `VtbfFieldDirectory*`。surfboard/surfboard_EN full survey 中 JP/EN 均解析成功 314/318 个场景，observed tag 为 26 类，字段目录为 158 类；JP/EN 的 `VtbfFieldDirectoryCounts`、`VtbfFieldDirectoryBlockCounts`、`VtbfFieldCountValueCounts`、`VtbfFieldStrideValueCounts` key 集合完全一致。未知 type code 为空，所有块 trailing bytes 为 0。`DATA` tag 数为 JP 314、EN 322；EN 的 `MM_UI_CommonWindow.sbscene` 与 `MM_UI_NameEntry.sbscene` 各含 3 个 `DATA`，其 `ParamLow` 序列分别为 `3,6,2` 与 `126,14,2`，均与对应后续 primary resource block 数匹配。`KEY.ParamHigh % 5` 在 JP/EN 的 94,611/95,558 个 KEY 块上均为 0，且 `KEY.ParamHigh` 与实际解析字段数无 mismatch；`TRK` 在 JP/EN 的 94,611/95,558 个块上均为 `ParamLow=1 / ParamHigh=5 / Fields=5`。`MOT.ParamLow` 与 `MOT.0x52` 在 JP/EN 的 32,428/32,742 个 motion 中均等于实际 track 数；`ANIM.ParamLow` 则有 412/444 个动画比实际 motion 数多 1，不能全局写成 motion 数。`ANIM.0x50` 在 1,578/1,579 与 1,613/1,614 个动画中等于实际 motion 数，唯一异常为 `MM_UI_Entry__MM_UI_Select_EntryName_ALL.sbscene` 的 `NameFadeIn`：`ParamLow=4`、`0x50=6`、实际 motion 数为 4，且该动画下 4 个 `MOT` 的 track 数为 `6,2,6,2`。新增 `AnimationField50MotionOrMaxTrackRelationCounts` 显示这个异常是 JP/EN 唯一 `equalsMaxMotionTrackCountOnly`，所以 `0x50` 也不能改名为 max track count。`ANIM.0x5F` 是 Byte/Bool-like raw 字段，JP/EN 只见 `0/1`，分布为 `0=1438/1469`、`1=141/145`；运行时将其复制为 default repeat flag，因此它不是 motion-presence flag、active flag 或 count modifier。这些是结构证据，不把 `ParamLow/ParamHigh` 写成全局统一语义。

## 紧凑字段

当前真实样本的字段头按紧凑格式解析：

| 字段 | 大小 | 状态 | 说明 |
| --- | ---: | --- | --- |
| FieldId | 1 | Confirmed | 字段编号。 |
| TypeCode | 1 | Confirmed | 字段类型。 |
| Payload | variable | Confirmed | 长度由 type code 和部分 field id 决定。 |

`TypeCode = 0x02` 的字符串字段使用 1 字节长度。Ras 中存在非 ASCII 长注释 raw bytes，并使用 `0x80 <length>` 扩展长度前缀。对抗校验后按参考实现收紧为 CP932/Shift-JIS 解码：解析器仍保留 `Raw` 原始字节，`StringValue` 使用 CP932 截断到首个 NUL 后的文本；`TEXT.0x7A` 报告继续同时输出 raw hex 以便排查编码边界。

主 Markdown 报告会输出单文件“字段目录”，按 `tag + field id + type` 汇总出现次数、所属块数、count/stride 分布和值样例。survey aggregate 另输出 full-survey 字段目录；JP/EN 当前均为 158 类字段目录项，且字段目录 key 集合完全一致。flags 类字段样例按 hex 输出，便于和 bit 表交叉核对。

## 紧凑 type code

| TypeCode | 名称 | 解码 |
| --- | --- | --- |
| `0x00` | ZeroLengthMarker | 0 字节 payload；full survey 中见于 `CNUM.0x02/0x0A/0x0C/0x0D` 与 `TEXT.0x00`，后面可接普通 compact 字段。只确认字段边界，不命名运行时语义。 |
| `0x01` | Byte/Bool | 1 字节。动画 key 中常见布尔值。 |
| `0x02` | String | 长度前缀字符串；节点名/动画名多为 ASCII。对抗校验后解析显示按 CP932/Shift-JIS 解码并在 NUL 处截断；`TEXT.0x7A` 在当前 full survey 中 raw content 可严格按 CP932/Shift-JIS 解码。报告仍保留 raw bytes 作为低层证据。 |
| `0x03` | RawByte03 | 1 字节 raw 值；当前已见于 `CNUM.0x00A7/0x00A9..0x00AC`、`NCAT.0x0F` 等字段。`NCAT.0x0F` 的 RawByte03 当前只见 `0/1`，`239/244/248` 等高值来自 CNUM raw byte 字段。语义未命名。注意这与 field id `0x03` 字符串字段不是同一概念。 |
| `0x04` | RawByte | 1 字节 raw 值；当前见于 `NCAT.0x0D`、`CATR.0x0D`、`CNUM.0xA6/0xA8/0xAD`、`CSLI.0x81/0x82/0x84/0x85` 等字段。`CATR.0x0D` 在 full survey 中为 `9/5/7`，语义未命名。 |
| `0x05` | Int16 | little endian `int16`。 |
| `0x06` | UInt16 | little endian `uint16`。 |
| `0x08` | Int32 | little endian `int32`。 |
| `0x09` | UInt32 | little endian `uint32`，flags 常用。 |
| `0x0A` | Float32 | little endian `float`。 |
| `0x0B` | PackedAngleCandidate | 4 字节；旧称 `Int32/Float32/PackedFloat32` 不准确。当前仅在 `TRS2.0x32` 与 `KEY.0x5B` 的旋转轨道上下文中作为 signed fixed-angle raw int 候选记录，公式候选为 `degrees = raw * 180.0 / 32768.0`；其他上下文仍保留 raw/int/float 观察，不全局套用角度语义。 |
| `0x0C` | Color32 | 4 字节颜色/packed 值；`TRS2.0x37/0x38/0x39` 当前按 `A,R,G,B` 候选解释。 |
| `0x45` | Int16VectorRecord | compact header value `0x45 = 0x40 | 0x05`，低 6 bit 为 `Int16`，`0x40` 表示 payload 首字节选择 component count。已核对的 loader/xref 中 `0x01 -> 3` 个 int16，`0x02 -> 4` 个 int16；该首字节不是业务 kind/enum。当前 `CREF` 为 1 字节 selector + 3 个 int16，`CROP` 为 1 字节 selector + 4 个 int16。 |
| `0x4A` | VectorFloat32 | 1 字节前缀 + 2 或 3 个 `float`。 |

## 记录块

`NODE`、`TRS2`、`NCAT`、`SLIC` 都是 record-oriented compact block，但首条记录 marker 不完全相同：

| 标记 | 适用范围 | 说明 |
| --- | --- | --- |
| `FC 00` | `NODE` / `TRS2` / `NCAT` | 第一条记录开始；当前 JP/EN full survey 的 `SLIC` 块未见该 marker。 |
| `FE 00` | `NODE` / `TRS2` / `NCAT` / `SLIC` | 后续记录开始。 |
| `FD 00` | `NODE` / `TRS2` / `NCAT` / `SLIC` | 记录区结束。 |

Ras 中 `NODE` 和 `TRS2` 都恢复出 428 条记录。`SLIC` 记录块已在 full survey 中覆盖 JP/EN 各 26 个块、108 条记录，字段顺序唯一且 trailing bytes 为 0；首条记录可直接从字段开始，后续记录由 `FE 00` 分隔，末尾以 `FD 00` 结束。

字段目录中可见记录块的 marker 分布：`NODE/TRS2/NCAT` 每个块都有 1 个 `RecordStartFirst`、`recordCount - 1` 个 `RecordStart`、1 个 `RecordEnd`；`SLIC` 在 JP/EN full survey 的 26/26 个块中没有 `RecordStartFirst`，首条记录直接从业务字段开始，后续记录由 `RecordStart` 分隔，末尾有 `RecordEnd`。实际业务字段如 `NODE.0x03/0x30/0x3B/0x3C`、`TRS2.0x31..0x3E`、`NCAT.0x0E` 的记录数会在 survey 中与 NODE 数量交叉校验。

`NCAT` 现在同时输出简单分类数组和 detail record，并会聚合一个文件中的所有 `NCAT` 块。full survey 中日文/EN detail records 为 11845/12076，全部带 `0x0E` 分类；非零分类记录为 2190/2260。primary `0x03` kind 分布如下，key 在 survey aggregate 中统一小写以避免 JSON 消费端大小写冲突，单条 detail record 仍保留原始字符串：

| Kind | surfboard | surfboard_EN |
| --- | ---: | ---: |
| `(none)` | 9655 | 9816 |
| `extparamdata` | 1602 | 1640 |
| `fontparamdata` | 402 | 431 |
| `mkrubytextdata` | 155 | 155 |
| `fontslot` | 28 | 28 |
| `fontnlo` | 3 | 6 |

`NCAT.0x0D` 在 detail aggregate 中按 raw byte 输出：日文/EN 为 `0x9` 2161/2228、`0x5` 29/32、无值 9655/9816。`NCAT.0x0E` 分类值目前只确认分布，不命名运行时类别：

| Category | surfboard | surfboard_EN |
| ---: | ---: | ---: |
| 0 | 9,655 | 9,816 |
| 1 | 874 | 907 |
| 2 | 916 | 927 |
| 3 | 291 | 291 |
| 4 | 78 | 104 |
| 5 | 11 | 11 |
| 6 | 5 | 5 |
| 7 | 8 | 8 |
| 8 | 7 | 7 |

survey JSON 现在输出 `NcatKind*` / `NcatTypeByte*` 交叉聚合。当前 full survey 中 `(none)` 全部为 no type byte 且 category `0`；非空 kind 主要为 `0x09`，只有 `fontslot/fontnlo` 主要落在 `0x05`。主要组合如下：

| 组合 | surfboard | surfboard_EN |
| --- | ---: | ---: |
| `(none)|?` | 9655 | 9816 |
| `extparamdata|0x9` | 1602 | 1640 |
| `fontparamdata|0x9` | 402 | 431 |
| `mkrubytextdata|0x9` | 155 | 155 |
| `fontslot|0x5` | 26 | 26 |
| `fontnlo|0x5` | 3 | 6 |
| `fontslot|0x9` | 2 | 2 |

`NcatKindCategoryCounts` 显示 `(none)` 全部绑定 category `0`；非空 kind 则跨多个 category，不能把 `0x0E` 单独命名为 kind 枚举：

| Kind | surfboard categories | surfboard_EN categories |
| --- | --- | --- |
| `extparamdata` | `1=865, 2=602, 3=110, 4=22, 6=3` | `1=898, 2=607, 3=110, 4=22, 6=3` |
| `fontparamdata` | `2=308, 3=54, 4=34, 5=4, 6=2` | `2=314, 3=54, 4=57, 5=4, 6=2` |
| `mkrubytextdata` | `3=118, 4=16, 5=7, 7=7, 8=7` | `3=118, 4=16, 5=7, 7=7, 8=7` |
| `fontslot` | `1=9, 2=6, 3=9, 4=3, 7=1` | `1=9, 2=6, 3=9, 4=3, 7=1` |
| `fontnlo` | `4=3` | `4=6` |

`NCAT.0x0F` 是混合 payload 参数字段，不只是字符串。新增 occurrence-level `NcatParameterFieldTypeCounts` 与字段目录一致：JP/EN 分别为 `String` 3852/3959、`RawByte03` 39/39、`Int32` 136/188、`Float32` 61/61；另有 `(missing)` 9655/9816，均来自 `kind=(none)`、category `0` 的 detail record。同一 detail record 可保留多个 `0x0F` 字段，例如 `extparamdata` 的 `0x0F String` occurrence 为 2487/2530，但 `extparamdata` record 只有 1602/1640。`NcatKindParameterFieldTypeCounts` 显示非字符串 `0x0F` 分布在 `extparamdata/fontparamdata/fontslot/mkrubytextdata/fontnlo`，不是单一 kind 私有字段。现有 `NcatParameterStringCounts` 只统计语义层 `ParameterString` 的字符串子集：`(empty)` 为 9839/10003，`blendMode#0,layerLevel#128,layer#-1,layerKind#0` 为 1602/1640，其余为 font 参数字符串和 raw 字符串 `2`。这只说明字符串模板分布稳定，不确认参数字段名或非字符串值的运行时效果。

按 detail index 绑定同 index NODE 后，`NcatKind*` 交叉统计显示所有 NCAT detail 均能绑定到节点，没有 missing node。JP/EN 中 `(none)` 绑定 CIMG target 为 5793/5914、非 CIMG 为 3862/3902；`extparamdata` 绑定 CIMG target 为 682/689、非 CIMG 为 920/951；`fontparamdata` 基本绑定 CIMG target（402/426，EN 另有 5 个非 CIMG）；`mkrubytextdata/fontslot/fontnlo` 主要绑定 CIMG target。上述统计只记录结构绑定关系，不把 kind/category 命名为具体 UI 功能。

`CATR` 是独立 compact block，不使用 `NODE/TRS2/NCAT/SLIC` 的 record marker。JP/EN full survey 中分别有 538/571 个 `CATR`，trailing bytes 均为 0；`ParamLow` 均为 0，`ParamHigh` 与 parsed field count 全部匹配。常见 shape 为 4 个字段，JP/EN 覆盖 537/570 个块：`0x0E UInt16 > 0x03 String > 0x0D RawByte > 0x0F String`。唯一 7 字段例外在两组相同场景 `MM_UI_Entry__MM_UI_Select_EntryName_ALL.sbscene`，shape 为 `0x0E UInt16 > 0x03 String > 0x0D RawByte > 0x0F Float32 > 0x03 String > 0x0D RawByte > 0x0F Int32`，两个 `0x0F` preview 均为 `0`。

新增 `CatrField*` survey 聚合显示：`CATR.0x03` 为 raw string，JP/EN 分布为 `AnimParamData=412/444`、`SlotTemplate0=125/126`、`font_scroll_spead=1/1`、`font_slot=1/1`；`0x0D` 为 raw byte，`9=537/570`、`5=1/1`、`7=1/1`；`0x0E` 为 `1=537/570`、`2=1/1`；`0x0F` payload 为 `String=537/570`、`Int32=1/1`、`Float32=1/1`。字符串 preview 分布为 `nextAnim#,callbackName#,callbackFrame#-1` 412/444 与 `name#,action#0,data#,type#s32` 125/126。上述名称/模板只作为 raw 字段内容和结构分布，不确认 callback、slot 或 font 参数的运行时语义。

## 已知块顺序

```text
VTBF/SRFF
  SRCK
  PROJ
  SCN
  LAYR
  CAST
  NODE
  TRS2
  DATA
  CIMG/CNUM/CRFD/CSLI with CREF/SLIC/TEXT companions ...
  NCAT
  (CAST/NODE/TRS2/DATA/NCAT resource region may repeat before ANIM)
  ANIM
  MOT/TRK/KEY ...
  CATR (optional standalone compact attribute blocks)
  TEXL
  TEX/CROP ...
  CAM
```

`DATA` 块自身没有 payload。按文件顺序从每个 `DATA` 后扫描到下一个 `DATA`/`NCAT`/结构边界，`ParamLow` 等于该区间内 `CIMG+CNUM+CRFD+CSLI` 四类 primary resource block 的数量；`CREF`、`SLIC`、`TEXT` 是伴随块，不计入该数。该结构关系在 surfboard/surfboard_EN full survey 中按文件级 all-DATA 校验为 314/314 与 318/318；EN tag 总数为 322，是因为 `MM_UI_CommonWindow.sbscene` 和 `MM_UI_NameEntry.sbscene` 各含 3 个 `DATA` 块。

`CNUM` 是 `DATA` 区间内的 primary resource block 之一。full survey 中 JP/EN 均为 217 条 `CNUM`，后续绑定的 `CREF` records 均为 2422；`CNUM.0x44` 与后续一个 `CREF` 块的记录数 217/217 匹配，`CNUM.0x51` 在 217/217 中均落在 NODE index 范围内。`CNUM` 的 zero-length marker field id 分布在 JP/EN 均为 `0x02:8, 0x0A:19, 0x0C:20, 0x0D:170`。新增 raw/cross survey 显示 `0x48=32768` 覆盖 JP/EN 217/217；已核对的装载路径会将该字段送入 shared packed state decoder。`0xA0` 有 24 个 raw int 取值，top 为 `65:70, 1:45, 64:21`；`0xA1` 有 42 个 raw 字符串值，top 为 `6:25, 100:19, 99:19`。`0xA1|0x44` 与 `0xA1|后续 CREF record count` 的聚合完全一致，继续支持 `0x44` 只作为后续 CREF 记录数。只读模型和 JSON/Markdown/inspect 输出现在逐记录暴露 `0x40/0x42/0x43` float、`0x39 x4` Color32 raw hex/`#AARRGGBB` 显示值、`0xA2..0xAD` raw int/byte 值、`0xAE` raw hex + float vector values、`0xAF` raw hex + packed values。当前只确认这些结构关系和 raw/机械解码值；`0xA1` 字符串、`0xA0`、`0xA2..0xAF` 等字段不命名运行时语义。

record shape profile 显示 JP/EN 的 217 条 `CNUM` 只有 4 种字段顺序，差异仅是最后的 zero-length marker field id：`0x0D` 170 条、`0x0C` 20 条、`0x0A` 19 条、`0x02` 8 条。共同字段顺序为 `0x48,0x51,0x40,0x42,0x43,0x44,0x39 x4,0xA0,0xA2,0xA3,0xA4,0xA5,0xA6,0xA7,0xA8,0xAA,0xAB,0xA9,0xAC,0xAD,0xAE,0xAF,<zero marker>,0xA1`。这只确认字段顺序和 marker 位置，不命名 marker 语义。

`CRFD` 是 `DATA` 区间内的 primary resource block 之一。full survey 中 JP/EN 分别为 614/622 个 CRFD，出现在 36/37 个文件中；DATA following tag 中 CRFD 合计同为 614/622。已观察 CRFD 均为 `ParamLow=0 / ParamHigh=7`，字段集合固定为 `0x51/0x90/0x91/0x92/0x93/0x94/0x95`。`0x51` 在 614/614 与 622/622 中均落在本文件 NODE index 范围内。`0x90/0x91` 现在已有稳定的文件名机械关系，但运行时解析语义仍未命名：`0x90` 在 JP/EN 全部 614/622 条均等于 owner scene prefix，按大小写不敏感比较也全部等于 owner directory；`0x91` 按大小写不敏感比较全部等于同目录 sibling scene suffix，exact 比较为 573/614 与 581/622。组合 `0x90 + "__" + 0x91` 按大小写不敏感比较全量命中同目录 sibling scene stem，exact 比较同为 573/614 与 581/622。exact 未全量命中的 41 条来自大小写差异，例如 `Reference_New_Icon` 与文件 stem 中的 `Reference_new_icon`。只读模型、JSON、Markdown 和 inspect 输出现在逐记录保留 `0x90/0x91` raw hex，并输出 `CrfdStringFieldRelationCounts`、`CrfdStringFieldTargetTypeCounts`、`CrfdField90Field91RelationCounts`、`CrfdField90Field91EqualityCounts` 与 `CrfdField90Field91Field92RelationCounts` 作为复算入口。新增 `CrfdField90Field91Counts` 与 `CrfdField90Field91Field92Counts` 后，JP/EN 均为 94 个 `0x90|0x91` 组合，且每个组合只对应一个 `0x92` 值；两组唯一计数差异是 `MM_UI_NameEntry|Reference_Player_Name_Parts|2` 从 JP 8 条增至 EN 16 条，其余组合计数一致。`0x92` 为小整数分布，且与 `0x90/0x91` 均不相等；`0x93=-1`、`0x94=0`、`0x95=0` 在当前 full survey 恒定，语义仍 unknown。

`CRFD.0x92` 分布：JP 为 `1:17, 2:94, 3:17, 4:34, 5:50, 6:45, 7:47, 8:64, 9:73, 10:40, 11:19, 12:31, 13:7, 14:9, 15:15, 16:23, 17:2, 18:13, 19:13, 20:1`；EN 与 JP 相同，但 `2:102`，多出的 8 条来自 EN NameEntry。该字段当前不命名。

`CSLI` 是 `DATA` 区间内的 primary resource block 之一。当前只确认它与后续 `CREF/SLIC` 的结构关系：full survey 中 JP/EN 均为 26 个 `CSLI`、106 条 `CSLI` 后续 `CREF`、108 条 `SLIC` record；`CSLI.0x44` 与后续 `CREF` 记录数 26/26 匹配，target index 26/26 均在 NODE 范围内。`CSLI.0x44` 与 `SLIC` record 数仅 24/26 匹配，不能将其正式命名为 SLIC record count。新增 target cross survey 显示 JP/EN 各 26 个 target node 的 `NODE.0x30` 均为 `0xF02`，初始 display 均为 `true`，且均不是 `CIMG` target；这些仍只作为 JP/EN full-survey 分布，不命名 `CSLI.0x51` 的运行时角色。

record shape profile 显示 JP/EN 的 26 个 `CSLI` 字段顺序完全一致：`0x80,0x51,0x40,0x41,0x42,0x43,0x44,0x81,0x82,0x84,0x85,0x86,0x87`。只读模型、Markdown 和 inspect 输出现在逐记录暴露 `0x40..0x43` 与 `0x80..0x87` 的机械解码值。字段集合与顺序只作为结构事实；`0x80..0x87` 的运行时语义仍 unknown。

`TEXT` 是 `DATA` 区间附近的伴随块，不计入 `DATA.ParamLow`。full survey 中 JP/EN 为 105/131 条 `TEXT`，`TEXT.0x7A` 字符串字段 present 为 105/131，zero-length marker field id 均为 `0x00`。`TEXT.0x79` 在 JP/EN 均为 `-1`，`TEXT.0x7C` 分布为 JP `-1:7, 0:94, 1:4`、EN `-1:7, 0:120, 1:4`；`0x78|0x79` top 分布为 JP `23|-1:44, 2|-1:17, 3|-1:15`，EN `23|-1:70, 2|-1:17, 3|-1:15`。raw string survey 显示 `TEXT.0x7A` 中 JP 为 strict UTF-8 invalid 97 / valid 8，EN 为 invalid 123 / valid 8；所有 invalid UTF-8 项都符合 Shift-JIS byte-shape with non-ASCII。严格 CP932/Shift-JIS 解码统计为 JP `validShiftJis=105/105`、EN `validShiftJis=131/131`，并新增 `TextField7AShiftJisStringCounts` 输出可读 decoded value 分布，例如 `ADVANCEDをプレイする人にオススメです。`、`ネットワークに接続されていません ...`、`楽曲タイトル１６文字...` 等样本字符串。解析模型和 JSON/Markdown/inspect 输出现在逐记录保留 `field7ARawHex` 与 `field7AShiftJis`。`TEXT.0x33` 以 `field33Vector` 和 `field33RawHex` 暴露，且 full survey 现已汇总其 vector/raw-hex 分布；`TEXT.0x7B` 以 `field7BPackedValues` 和 `field7BRawHex` 暴露，且 full survey 现已汇总其 packed-values/raw-hex 分布。这里的 Shift-JIS string、vector 和 packed values 都是只读机械解码证据，不命名业务语义。当前确认字段边界、raw bytes、严格 UTF-8 校验结果和 CP932/Shift-JIS 解析规则；布局、换行和运行时渲染语义未确认。

record shape profile 显示 `TEXT` 在 JP 105/105、EN 131/131 上只有一种字段顺序：`0x78,0x79,0x7A,0x33,0x7B,0x00(zero marker),0x7C,0x41`。`0x33` 为 `type=0x4A` VectorFloat32，`0x7B` 为 `type=0x45` packed record，已在只读模型中暴露 raw hex 和解码值。这只确认记录布局、原始值和 CP932/Shift-JIS 文本解码，不确认换行、对齐或渲染行为。

`CNUM.0xA1` 的 raw string survey 显示 JP/EN 217/217 均为 strict UTF-8 valid 和 ASCII-only，raw/content length 分布为 `2:65, 1:52, 3:38, 4:27, 6:15, 5:11, 7:9`。解析模型和 JSON/Markdown/inspect 输出现在逐记录保留 `fieldA1RawHex`，它与宽松显示用的 `fieldA1` 分开；同一层输出也保留 `field39RawHexValues`、`fieldAERawHex`、`fieldAFRawHex` 等不透明 payload 证据。这只确认 raw 字节形态与低层解码结果，不把字段命名为数字控件、格式字符串、布局参数或计数值。

`SLIC` record raw/cross survey 显示 JP/EN 两组一致：108/108 条记录只有一种字段顺序：`0x83,0x40,0x41,0x45,0x37,0x39 x4,0x38`；`0x37` Color32 为 `#FFFFFFFF` 108/108，`0x38` Color32 为 `#FF000000` 108/108，`0x39` 每条 record 均有 4 个 Color32 值；`0x45` 分布为 `0:28, 1:26, 2:24, 3..8:各5`。`0x83|0x40|0x41|0x45|0x39-count|0x37|0x38` shape tuple 在 JP/EN 均有 45 种。只读模型、JSON、Markdown 和 inspect 输出现在逐记录暴露 `0x37/0x38/0x39` 的 Color32 显示值与 raw hex。整个小节只确认 raw payload、记录边界和当前 full-survey 分布，不把 `0x37/0x38/0x39` 命名为 material/illumination/vertex color，也不把 `0x83` 命名为 slice type enum。

`PROJ` 在 JP/EN full survey 中每个场景各 1 个块，共 314/318 个；字段顺序固定为 `0x0000:0x0006>0x0001:0x0006>0x0005:0x0006>0x0055:0x0008>0x0056:0x0008`，trailing bytes 为 0。`ParamLow/ParamHigh` 分布为 JP `(3,5)=185, (4,5)=124, (2,5)=5`，EN `(3,5)=188, (4,5)=125, (2,5)=5`。raw 值层面，`0x00=1`、`0x05=0`、`0x55=0` 全覆盖；`0x01` 为 JP `1=308, 0=6`、EN `1=312, 0=6`。`0x56` 是 scene/project 级 frame end 或 duration 候选 raw 值，JP/EN 均有 17 种，范围 `50..3000`；高频值 JP 为 `300:57, 3000:34, 50:29, 200:28, 100:27, 500:22`，EN 为 `3000:34, 500:34, 150:33, 300:30, 200:29, 50:29`。它不是所有动画或 track/key 的严格上界：相对最大 `TRK.0x59`，JP 为 `less=25, equals=23, greater=247, missing=19`，EN 为 `less=23, equals=20, greater=255, missing=20`；相对最大 `KEY.0x5A` 同样不是全覆盖关系。CLI survey 现在直接输出 `ProjectField00Counts`、`ProjectField01Counts`、`ProjectField05Counts`、`ProjectField55Counts`、`ProjectField56Counts`、`ProjectField56TrackLastRelationCounts`、`ProjectField56KeyMaxRelationCounts`、`ProjectField56DeltaToTrackLastCounts`、`ProjectField56DeltaToKeyMaxCounts`、`ProjectFieldSequenceCounts` 和 `ProjectFieldSetCounts`，这些聚合是上述结论的复算入口。

`SCN` 在 JP/EN full survey 中每个场景各 1 个块，共 314/318 个；字段顺序固定为 `0x0003:0x0002>0x0010:0x0006>0x0011:0x0006>0x0004:0x000C>0x0040:0x0006>0x0041:0x0006`，trailing bytes 为 0。`ParamHigh=6` 与 parsed field count 全量匹配；`ParamLow` 与 `0x10` 和同场景 `LAYR` 块数全量一致，JP 为 `1` x314，EN 为 `1` x316、`3` x2，两个 `3` 场景为 `MM_UI_CommonWindow.sbscene` 与 `MM_UI_NameEntry.sbscene`。`0x03` 为场景名；`0x04` 为 Color32 raw hex，JP/EN 均有 34 种 raw 值；高频值为 `FF818181` 46/46、`FF787878` 39/39、`FF659AD4` 29/29、`FF4F5356` 27/27。`0x11=0`、`0x40=1080` 全覆盖；`0x41` 为 `1080` 287/291 与 `450` 27/27，`450` 只见于一组 `MM_UI_Upper*` 场景。当前只确认 raw 字段和计数/尺寸状分布，不命名背景色、视口或分辨率等运行时语义。

`LAYR` 在 JP full survey 中为 314 个块，每场景 1 个；EN 为 322 个块，其中 316 个场景各 1 个，`MM_UI_CommonWindow.sbscene` 与 `MM_UI_NameEntry.sbscene` 各 3 个。字段顺序固定为 `0x0003:0x0002>0x0020:0x0009>0x0021:0x0006>0x0022:0x0006`，字段类型分别为 `String/UInt32/UInt16/UInt16`，trailing bytes 为 0。`0x20` 只见 raw `0` 与 `0x100`：JP 为 `0:94, 0x100:220`，EN 为 `0:97, 0x100:225`，bit 分布只有 bit 8；该字段仍按 shared packed state raw word 处理，不命名运行时 flag。`0x21` raw 范围为 `2..574`，JP/EN 均有 86 种；按 scene 对所有 `LAYR.0x21` 求和后，与 scene 的 `NODE` 总数完全一致（JP 314/314，EN 318/318）。运行时 `Layer_InitRuntimeFromData` (`sub_7DC8D0`) 将对应计数复制为 layer cast count，并据此创建 cast runtime 数组。`0x22` raw 范围为 `1..32`，JP/EN 均有 16 种；`ParamLow - 0x22` 为 `1` 覆盖 JP 313/314、EN 321/322，唯一 `2` 例外同为 `MM_UI_Entry__MM_UI_Select_EntryName_ALL.sbscene`（`ParamLow=6`、`0x22=4`）。运行时 `AnimationContainer_BuildMotionLookup` 将对应计数复制为 animation count，并建立 `castIndex x animationIndex` motion lookup。`0x20` 的业务语义仍未确认。

运行时 layer/cast 静态记录还有一个已核对的可见性组合字段：cast/static record `+0x218` 由 `Cast_UpdateRenderState` (`sub_7D4720`) 读取。type 11 写入本节点 display 为假时最终 `Cast+0xD0=0`；display 为真时，`+0x218=1` 表示最终可见性继承父 cast 的 `+0xD0`，`+0x218=0` 表示本节点 local true 直接可见。该偏移属于 cast/static record，不是 `ANIM` entry 字段。

`CAM` 在 JP/EN full survey 中每个场景各 1 个块，共 314/318 个；字段顺序固定为 `0x0003:0x0002>0x0012:0x004A>0x0013:0x004A>0x0014:0x000B>0x0015:0x000A>0x0016:0x000A`，trailing bytes 为 0。`0x03` 名称全为 `default`；`0x12` position-like vector 有 16 种，`0x13` target-like vector 有 3 种。`0x14` 虽然使用 compact type `0x0B`，但 JP/EN 全量均为 raw `8191` / `0x1FFF`，bit `0..12` 全部置位；该字段仍按 flags-like raw 值保留，不套用 packed-angle 候选公式。`0x15=10`、`0x16=100000` 全覆盖，当前模型把它们显示为 near/far clip candidate，但运行时 camera 语义仍未进一步确认。

## 关键字段映射

| 块 | 字段 | 状态 | 说明 |
| --- | --- | --- | --- |
| `PROJ` | `0x00` / `0x01` / `0x05` / `0x55` / `0x56` | Confirmed(raw)/Candidate semantics | scene/project 级 raw 参数与 frame end/duration 候选；full survey 中字段顺序固定，`0x55=0` 全覆盖，`0x56` 范围为 `50..3000`。`0x56` 可小于、等于或大于最大 track/key frame，因此不写成严格播放边界。 |
| `SCN ` | `0x03` / `0x04` / `0x10` / `0x11` / `0x40` / `0x41` | Confirmed(raw)/Unknown semantics | 场景名、Color32 raw hex、UInt16 raw count/size-like 字段。full survey 中字段顺序固定；`0x10` 与 `ParamLow` 和同场景 `LAYR` 块数全量一致，`0x11=0`、`0x40=1080`，`0x41` 只见 `1080/450`。运行时用途未确认。 |
| `LAYR` | `0x03` / `0x20` / `0x21` / `0x22` | Confirmed(raw/count) | 图层名、shared packed state raw word、node/cast count、animation count。full survey 中字段顺序固定；`0x20` 只见 `0/0x100`，`0x21` 按 scene 求和等于 `NODE` 总数，运行时用于 layer cast count；`0x22` 与 `ParamLow` 只有一个 `+2` 例外，运行时用于 animation count。`0x20` 运行时语义未确认。 |
| `NODE` | `0x03` / `0x30` / `0x3B` / `0x3C` | Confirmed(raw)/Candidate flags semantics | 节点名、child index、sibling index 已确认；`0x30` 为 raw flags 字段，bit 级语义仍按候选/unknown 分层。 |
| `TRS2` | `0x31` / `0x32` / `0x33` / `0x3A` | Confirmed(raw)/Candidate transform semantics | transform/display raw 字段已解码；translation / rotation / scale / display 为当前模型命名，`0x32` 的 signed fixed-angle 换算仍按候选处理。 |
| `TRS2` | `0x37` / `0x38` / `0x39` | Confirmed(raw)/Candidate semantics | 字段边界和值已解析；material color / illumination color / vertex colors 与通道顺序 `A,R,G,B` 仍是候选解释，输出 `Hex=#AARRGGBB` 仅为候选显示格式。 |
| `TRS2` | `0x3D` / `0x3E` | Confirmed(raw) | multi position / multi size raw flags。 |
| `CIMG` | `0x48` / `0x51` / `0x40` / `0x41` / `0x42` / `0x43` | Confirmed(raw)/Runtime draw fields | raw packed state / cast index / width / height / pivot 字段已解码。`0x48` 是 shared packed state word；对 CIMG draw 路径，低 4 位是 draw/blend mode（`1` additive/effect），`0x10/0x20` 是 flipU/flipV，`0xC0` 是 UV permutation，`0x7800` 是 surface mode。其它高位和非 CIMG owner 仍不命名业务语义。 |
| `CIMG` | `0x44` / `0x45` | Confirmed(raw/default fallback) | `0x44` 是 primary/secondary 两组 CREF 记录数；full survey 中 7063/7063 与 7228/7228 个 image cast 与后续 CREF 组完全匹配。`0x45` 为两个组内 crop reference index，且已核对的范围检查逻辑按 `0x44` 两组 count 对其做范围校验。运行时会用它作为静态 fallback/default slot index；`ImageVariantGroupCimg45FirstKey*` 显示它与 type 18/19 最早 key 经常一致但不是全覆盖，因此不能命名为动画当前或选中引用。 |
| `CREF` | `0x49` | Confirmed(raw) | crop 引用 compact record；现已按 CIMG/CNUM/CSLI 全 owner 聚合。已核对 loader/xref：field id `0x49` 连续读取 3 个 int16，保存为 `(textureListIndex, textureIndex, cropIndex)`。full survey 中布局 selector 首字节均为 `1`，合计 19,597 与 19,734 条；owner 分布为 JP `CIMG=17069, CNUM=2422, CSLI=106`，EN `CIMG=17206, CNUM=2422, CSLI=106`。selector 不是业务枚举。 |
| `CNUM` | `0x39` / `0x40` / `0x42` / `0x43` / `0x44` / `0x48` / `0x51` / `0xA0..0xAF` | Confirmed(raw)/Unknown semantics | `0x44` 与后续 `CREF` 记录数匹配，full survey 为 217/217；`0x48=32768` 为 217/217，在已核对装载路径中进入 shared packed state decoder；`0x51` 在 217/217 个 CNUM 中落在 NODE index 范围内。record shape 只有 4 种，差异仅为 zero marker id。只读模型已逐记录暴露 float、Color32 raw/display、raw byte/int、`0xAE` vector values 和 `0xAF` packed values；这些仍是 raw/机械解码字段。`0xA1` 为字符串 raw 值，运行时语义未确认。 |
| `CRFD` | `0x51` / `0x90` / `0x91` / `0x92` / `0x93` / `0x94` / `0x95` | Confirmed(raw)/Unknown semantics | full survey 中字段集合固定，614/614 与 622/622 个 `0x51` 均落在 NODE index 范围内。`0x90` 全量命中 owner scene prefix；`0x91` 与 `0x90 + "__" + 0x91` 按大小写不敏感比较全量命中同目录 sibling scene suffix/stem，exact 比较为 JP 573/614、EN 581/622，差异来自 `Reference_New_Icon`/`Reference_new_icon` 这类大小写不一致。JP/EN 均为 94 个 `0x90|0x91` 组合，且每个组合只对应一个 `0x92` 值。`0x92` 为小整数候选；`0x93=-1`、`0x94=0`、`0x95=0` 当前恒定但语义 unknown。 |
| `CSLI` | `0x44` / `0x51` | Confirmed(raw)/Unknown semantics | `0x44` 与后续 `CREF` 记录数匹配，full survey 为 26/26；`0x51` 在 26/26 个 CSLI 中落在 NODE index 范围内，JP/EN full survey 中 target node 均为 `NODE.0x30=0xF02/display=true/non-CIMG target`。运行时 slice 语义未确认。 |
| `CSLI` | `0x40..0x43` / `0x80..0x87` | Confirmed(raw)/Unknown semantics | JP/EN 的 26 个 `CSLI` 字段顺序完全一致，已按字段类型解码并在只读模型、Markdown 和 inspect 中输出 raw 数值。`0x80` 进入 shared packed state decoder；`0x81..0x87` 用途未确认。 |
| `SLIC` | `0x83` / `0x40` / `0x41` / `0x45` / `0x37` / `0x38` / `0x39` | Confirmed(raw)/Unknown semantics | `SLIC` record 字段已解析；JP/EN 108/108 条记录字段顺序完全一致。`0x83` 分布为 `0x0:10, 0x1:23, 0x2:14, 0x3:61`，并在已核对 decoder 中使用 shared packed state decoder 的低位 helpers；`0x37=#FFFFFFFF`、`0x38=#FF000000`、每条 `0x39` 为 4 个 Color32。只读模型/JSON/Markdown/inspect 现保留这些 Color32 的 raw hex。Color32 与 `0x83` 业务语义未确认。 |
| `TEXT` | `0x33` / `0x7A` / `0x7B` / `0x41` / `0x78` / `0x79` / `0x7C` | Confirmed(raw)/Unknown semantics | JP/EN 的 105/131 条 `TEXT` 字段顺序完全一致，`0x7A` 字符串字段均存在；`0x33` 已按 raw VectorFloat32 暴露为 vector + raw hex，`0x7B` 已按 raw packed record 暴露为 raw hex 与 prefix+uint16 解码值列表，且 full survey 已汇总这两组分布。JP/EN 分别有 97/123 条 `0x7A` strict UTF-8 invalid；所有 `0x7A` raw content 均可严格按 CP932/Shift-JIS 解码，并已在模型/JSON/Markdown/inspect 中以 `field7AShiftJis` 暴露只读 decoded preview，full survey 另有 `TextField7AShiftJisStringCounts`。其它字段按 raw int 分布输出。当前确认 `0x7A` 的 CP932/Shift-JIS 解码规则，但不确认 `0x33/0x7B` 业务含义、文本布局和渲染语义。 |
| `NCAT` | `0x0E` | Confirmed(raw)/Unknown semantics | 分类 raw 值；full survey 中聚合所有 `NCAT` 块后，记录数等于 NODE 的场景为 314/314 与 318/318。Shama 和部分 UI 样本出现非零值；分类语义未确认。 |
| `NCAT` | `0x03` / `0x0D` / `0x0F` | Confirmed(raw)/Unknown semantics | detail record 附加字段：primary `0x03` kind 已见 `(none)`、`ExtParamData`、`FontParamData`、`MkRubyTextData`、`fontSlot/fontslot`、`fontNLO`；`0x0D` 为 raw byte，`0x0F` 为混合 payload 参数字段，已见 `String/RawByte03/Int32/Float32`。运行时语义未确认。 |
| `CATR` | `0x03` / `0x0D` / `0x0E` / `0x0F` | Confirmed(raw)/Unknown semantics | 独立 compact attribute-like block；full survey 中 JP/EN 为 538/571 个块，trailing bytes 均为 0。常见字段 shape 为 `0x0E,0x03,0x0D,0x0F String`，另有 1/1 个双 `0x03/0x0D/0x0F` 的 7 字段例外。字符串和值只按 raw payload 保留，不命名 callback/slot/font 语义。 |
| `TEXL` | `0x03` / `0x60` | Confirmed | texture list 名称 / texture 数量。 |
| `TEX ` | `0x61` / `0x40` / `0x41` / `0x62` / `0x63` | Confirmed(raw)/Unknown semantics | texture 名称 / width / height / raw packed state / crop 数。已核对 loader/xref：`0x62` 会用 shared packed state decoder 的 `0xF0`、`0xF00` helpers 拆分。只读模型、JSON、Markdown、inspect 和 full survey 现暴露 `0x62` raw value 与置位 bit；JP/EN 只见 `0x110` 与 `0x0`，分布为 JP `0x110:2625, 0x0:281`，EN `0x110:2638, 0x0:284`，bit 分布只有 bit 4 与 bit 8。运行时 sampler/layout/blend 语义未确认。 |
| `CROP` | `0x65` | Confirmed(raw) | crop compact record；已核对 loader/xref：field id `0x65` 按 component count 读取 4 个 int16，并用 TEX width/height 归一化为 4 个 float。full survey 中布局 selector 首字节均为 `2`，合计 79,801 与 80,217 条。所有 atlas 的 declared crop count 均匹配实际 CROP 数；crop size 无非正数。selector 不是业务枚举。 |
| `CAM ` | `0x03` / `0x12` / `0x13` / `0x14` / `0x15` / `0x16` | Confirmed(raw)/Candidate semantics | full survey 中每场景 1 个 `CAM`，字段顺序固定；`0x03=default`、`0x14=0x1FFF`、`0x15=10`、`0x16=100000` 全覆盖。position / target / near clip / far clip 为当前模型命名，`0x14` 仍只按 flags-like raw 字段处理。 |
| `ANIM` | `0x03` / `0x50` / `0x56` / `0x5F` | Confirmed(raw)/Runtime playback fields | 动画名 / declared motion count candidate / playback duration/end frame / default repeat flag。运行时 ANIM entry `+0x200/+0x204` 是 motion count 和 MOT table pointer，`+0x208` 被复制为 duration/end frame 并用于 `Layer_SetAnimationTime` wrap/clamp，`+0x20C` 被复制为 default repeat flag。`0x50` 与实际 motion 数匹配 1,578/1,613 个动画，但 full survey 有 1 个 raw `+2` 异常，因此静态字段仍不写成无条件 confirmed count。`0x56` 是播放 duration/end-frame 候选，不要求等于所有 track/key 的严格上界。 |
| `MOT ` | `0x51` / `0x52` | Confirmed(raw/runtime target) | `0x52` 为 track 数量；full survey 中与 `MOT.ParamLow` 和实际 track 数在 JP/EN 32,428/32,742 个 motion 中全量一致。`0x51` 在当前 full survey 中落在 NODE index 范围内；运行时 `AnimationContainer_BuildMotionLookup` 也按 MOT 前 2 字节目标节点索引把 motion 绑定到 cast，因此可作为 motion target node index。 |
| `TRK ` | `0x53` / `0x57` / `0x54` / `0x58` / `0x59` | Confirmed(raw)/Candidate semantics | 字段顺序、keyframe 数量字段和 first/last frame raw 值已确认。track type / flags / 播放边界语义仍按候选处理；`0x54` 拆为 `base byte` 和 `extra mask`，base byte high nibble 对应 `KEY.0x5B` 存储类型。 |
| `KEY ` | `0x5A` / `0x5B` / `0x5C` / `0x5D` / `0x5E` | Confirmed(raw)/Candidate semantics | key frame 与 key value raw 字段已确认。`0x5C` 的插值标签、`0x5D/0x5E` 的 tangent/附加参数语义仍为候选；full survey 中字段顺序固定，差异只在 `0x5B` type。 |

`CROP.0x65` 和 `CREF.0x49` 的 compact raw bytes 现在保留为 `RawHex`。已核对 loader/xref 显示它们共享 compact `0x45` 布局：首字节是 component-count selector，不是 `kind` 枚举；`CREF.0x49` 的 `0x01` 选择 3 个 int16，`CROP.0x65` 的 `0x02` 选择 4 个 int16。`CropReference*` full survey 覆盖 CIMG/CNUM/CSLI 三类 CREF owner，而旧的 CIMG-only CREF 计数只作为 owner 子集理解。JP/EN 中 CREF texture/crop index in range 为 19,595/19,595 与 19,734/19,734；JP 另有 2 条 `textureListIndex=65535`、texture index out-of-range、crop index missing-texture 的 raw 特例，`CropReferenceOutOfRangeOwnerCounts` 为 `CIMG=2`。这两条 sample 分别位于 `MM_UI_Common__MM_UI_Common_Ranking_ADV.sbscene` 的 `ranking_base` 和 `MM_UI_SimpleOption__Reference_TitleFrame_Small_ALL.sbscene` 的 `hard_text`，raw 均为 `01FFFFFFFFFFFF`；EN 无 CREF out-of-range。

CROP 坐标越过 atlas 边界的记录为 JP/EN 259/191，并已输出 `CropRectOutOfAtlasBoundsReasonCounts` 与逐场景 `CropRectOutOfAtlasBoundsSamples`。原因分布为 JP `right>width=104, left<0=51, right>width+bottom>height=46, left<0+top<0+right>width+bottom>height=34, top<0=15, bottom>height=6, top<0+right>width+bottom>height=3`；EN `right>width=83, right>width+bottom>height=47, left<0+top<0+right>width+bottom>height=36, top<0=15, bottom>height=6, top<0+right>width+bottom>height=3, left<0=1`。这些只确认 raw index/坐标关系和样本定位，不能命名 sentinel/空引用、运行时采样或裁剪规则。

`CIMG.0x45` 的两个 `u16` raw 值现在按 primary/secondary CREF group 分开统计。非空 group 全部 in-range：JP 7315/7315，EN 7454/7454；out-of-range 与 empty group non-zero 均为 0。JP 非零 group 为 131（primary 127、secondary 4），EN 为 124（primary 120、secondary 4）。JP primary index 分布为 `0=6936, 1=86, 2=11, 3=14, 5=5, 6=6, 9=4, 64=1`，secondary 为 `0=7059, 1=4`；EN primary 为 `0=7108, 1=83, 2=8, 3=14, 5=5, 6=6, 9=4`，secondary 为 `0=7224, 1=4`。`sub_7CE8B0` 对这两个值使用与 `0x44` 相同的 primary/secondary 分组范围校验；`Cimg45NonZeroSamples` 会给出 indexed raw CREF，例如 JP 的 `primary|70|64` 位于 `MM_UI_ButtonCounter__MM_UI_Common_Switch.sbscene` 的 `text`，raw 为 `01000000001D00`。这些确认它们是组内引用 index。

`ImageVariantGroupCimg45FirstKey*` 进一步把 `CIMG.0x45` 与 type 18/19 image variant track 的最早 key 做静态比较。JP 中 `18 primary` 为 match 1533 / mismatch 258，`19 secondary` 为 match 332 / mismatch 1；EN 中 `18 primary` 为 match 1534 / mismatch 257 / multi-CIMG target 1，`19 secondary` 为 match 332 / mismatch 1。`firstKey - cimg45` 的差值在 primary 中覆盖负值、0 和正值，secondary 只有 `0` 与 `-1`。因此 `0x45` 可与很多变体轨道起始值对齐，运行时也会把它作为 fallback/default slot index；但它不能全局写成动画初始 key、当前引用或选中引用。

## compact warning 状态

full survey 当前没有未知 VTBF type code，`ScenesWithWarnings` 也为 0/314 与 0/318。此前剩余 warning 集中在 `CNUM/TEXT/SLIC` 的 `type=0x00` 或 compact trailing bytes；现已结构化解析为：

| 区域 | 结构结论 | 语义状态 |
| --- | --- | --- |
| `CNUM` | `0x02/0x0A/0x0C/0x0D` 可出现 `type=0x00` 零长度字段；后面常接 `0xA1 type=0x02` 字符串，例如 `"88"`、`"14.12"`、`"999.99"`。 | 字段边界与原始字符串 confirmed，运行时语义 unknown。 |
| `TEXT` | `0x33 type=0x4A` vector 与 `0x7B type=0x45` packed record 已结构化暴露；`0x00 type=0x00` 零长度字段之后可接 `0x7C type=0x05` 和 `0x41 type=0x05` 等字段。 | 字段边界和 raw/int/vector/packed 值 confirmed，运行时语义 unknown。 |
| `SLIC` | 按记录块解析；后续记录 marker 为 `FE 00`，结束 marker 为 `FD 00`。full survey 中不再产生 SLIC trailing warning。 | 记录边界 confirmed；`SLIC.0x83/0x40/0x41/0x45/0x37/0x38/0x39` 语义仍需确认。 |

解析失败时的兜底逻辑仍会从未知字段头开始保留 `TrailingBytes`，包含 `field id/type` 本身；单元测试覆盖该行为。

未知字段不会丢弃。JSON 中保留字段 raw bytes，Markdown 中输出 owner tag、offset、field id、type code、count、stride 和 preview。只有名称模式、track 形态和样本可见值支持的内容会生成 variant hint，不会直接标记为运行时事实。

## 场景树

`NODE` 记录按 index 组织树结构：

| 字段 | 说明 |
| --- | --- |
| `0x03` | 节点名。 |
| `0x30` | flags。 |
| `0x3B` | first child index，`-1` 表示无子节点。 |
| `0x3C` | next sibling index，`-1` 表示无兄弟节点。 |
| `0x07` | 可选注释文本；Ras 中 `Ras_root` 有非 ASCII 长注释 raw bytes，解析器按 CP932/Shift-JIS 解码并保留 raw bytes。 |

Ras 样本开头：

```text
Ras_Scale
  Ras_root
    effect_heart
      04_present_eff
      04_present_heart_00_null
        04_present_heart_00
        04_present_heart_00_add
    Ras_null
      under02a
        under02
```

`TRS2` 记录与 `NODE` 记录按 index 一一对应，Ras 中均为 428 条。

Ras 中 `NODE.0x30` flags 分布和观察：

| Flags | Nodes | Image casts | Display=false | 当前观察 |
| ---: | ---: | ---: | ---: | --- |
| `0x900` | 6 | 0 | 0 | null/container 节点。 |
| `0xE00` | 37 | 0 | 37 | 初始隐藏 container/组合节点。 |
| `0xE01` | 69 | 69 | 69 | 初始隐藏且带 CIMG 的节点。 |
| `0xF00` | 80 | 0 | 0 | 普通 container/骨架节点。 |
| `0xF01` | 234 | 234 | 0 | 带 CIMG 且初始 display=true 的节点。 |
| `0x8F00` | 1 | 0 | 0 | 根缩放节点。 |
| `0x10F01` | 1 | 1 | 0 | 唯一嘴部节点特例。 |

这支持低 bit `0x1` 与 image/CIMG 绑定存在交叉证据，`0xE00/0xE01` 与初始 hidden/display=false 存在交叉证据；但 bit 级语义仍需运行时用途证据或更多 flags pattern，当前只按交叉统计保留。

解析器现在为每个节点输出 `flagBits`，Markdown/inspect 也会给出 bit 与 CIMG、动画绑定、初始 display 的交叉统计。Ras/Chiffon/Otohime 三个样本的 bit 级观察：

| Bit | Mask | Ras nodes / CIMG / hidden | Chiffon nodes / CIMG / hidden | Otohime nodes / CIMG / hidden | 候选语义 |
| ---: | --- | --- | --- | --- | --- |
| 0 | `0x00000001` | 304 / 304 / 69 | 207 / 207 / 52 | 380 / 380 / 99 | CIMG-backed 节点候选；三个样本中 image cast 计数完全一致。 |
| 8 | `0x00000100` | 322 / 235 / 0 | 226 / 155 / 0 | 370 / 281 / 0 | 常见节点属性；区分 `0xFxx` 与 `0xExx`，也出现在 `0x900/0x901/0x8F00` flags。 |
| 9 | `0x00000200` | 422 / 304 / 106 | 291 / 207 / 71 | 490 / 376 / 131 | 常见节点属性；这三个角色样本中缺于 `0x900/0x901`。 |
| 10 | `0x00000400` | 422 / 304 / 106 | 291 / 207 / 71 | 490 / 376 / 131 | 与 bit 9 分布相同，具体分工未知。 |
| 11 | `0x00000800` | 428 / 304 / 106 | 297 / 207 / 71 | 501 / 380 / 131 | 三个角色样本内全覆盖的 common node/control bit 候选；full survey 后文已显示它不是全局必有。 |
| 15 | `0x00008000` | 1 / 0 / 0 | 1 / 0 / 0 | 1 / 0 / 0 | `0x8F00` 根/控制节点候选。 |
| 16 | `0x00010000` | 1 / 1 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 稀疏特例；Ras 中仅见于 `0x10F01` 嘴部节点。 |

Otohime 新增 `NODE.0x30=0x901`，共 4 个 present 节点，bit 组合为 `0,8,11`，均有 CIMG 且初始 display=true。它说明 bit 0 的 CIMG-backed 候选比 `0xE01/0xF01` exact flags 更稳定。

MM_CH 9 场景 survey 最早补充观察到 exact flags `0x800`、`0xF03`、`0xF04`；full survey 后，当前 exact flag 分布如下。它只说明已观察 bit 组合，不命名 bit 业务语义：

| Flags | surfboard nodes | surfboard_EN nodes |
| ---: | ---: | ---: |
| `0x700` | 1 | 1 |
| `0x701` | 2 | 2 |
| `0x800` | 38 | 38 |
| `0x801` | 7 | 7 |
| `0x900` | 138 | 138 |
| `0x901` | 38 | 38 |
| `0xA00` | 42 | 42 |
| `0xA01` | 46 | 46 |
| `0xB00` | 222 | 225 |
| `0xB01` | 718 | 751 |
| `0xB03` | 72 | 72 |
| `0xB04` | 3 | 3 |
| `0xD01` | 8 | 8 |
| `0xD03` | 4 | 4 |
| `0xE00` | 388 | 397 |
| `0xE01` | 537 | 535 |
| `0xE03` | 1 | 1 |
| `0xE04` | 76 | 76 |
| `0xF00` | 2,771 | 2,807 |
| `0xF01` | 5,687 | 5,815 |
| `0xF02` | 26 | 26 |
| `0xF03` | 537 | 545 |
| `0xF04` | 138 | 138 |
| `0x8E00` | 4 | 6 |
| `0x8F00` | 251 | 257 |
| `0x8900` | 61 | 62 |
| `0x8B00` | 9 | 10 |
| `0x10F01` | 20 | 26 |

full survey 中 NODE observed bits 为 `0/1/2/8/9/10/11/15/16`。日文 surfboard 计数为 bit0=7677、bit1=640、bit2=217、bit8=10706、bit9=11551、bit10=10451、bit11=11842、bit15=325、bit16=20；EN 对应为 7850/648/217/10928/11781/10644/12073/335/26。bit 11 高覆盖，但不是全局必有：`MM_UI_UpperGame__MM_UI_UpRES_JudgeStyle_BG.sbscene` 中有 2 个 `0x701` 节点，`MM_UI_Common__MM_UI_Common_1PModeWarning_ALL.sbscene` 中有 1 个 `0x700` 节点，这 3 个节点不带 bit 11。其它 bit 只记录分布，不命名运行时语义。

full survey 现在还输出 `NodeFlagBit*` 节点级交叉聚合：`NodeFlagBitDisplayFalseNodeCounts`、`NodeFlagBitCimgTargetNodeCounts`、`NodeFlagBitAnimatedNodeCounts`、`NodeFlagBitDataNodeCounts`、`NodeFlagBitCategoryRecordNodeCounts`、`NodeFlagBitCategoryNonZeroNodeCounts`、`NodeFlagBitExactFlagCounts`、`NodeFlagBitGroupCounts`、`NodeFlagBitImageCastFlagBitCounts`、`NodeFlagBitTrackTypeCounts` 和 `NodeFlagBitPairCounts`。这些字段均按 NODE 计数，`CimgTargetNode` 表示目标节点至少绑定一个 CIMG，不等同于 image cast 记录数。JP/EN 中 bit0 的 CIMG target node 为 7063/7214，bit11 为 7061/7214，bit16 为 20/26；bit0 与 bit11 共现为 7675/7848，bit8/9/10 各有 3 个不带 bit11 的 UI 节点例外。

Ras 中 TRS2 每条记录都有 `0x31/0x32/0x33/0x37/0x38/0x3A/0x3D/0x3E`，且每条有 4 个 `0x39` vertex color。`multiPosFlags` 全 0，`multiSizeFlags` 全 1；`display` 为 true 322、false 106。

颜色字段的字节序已有动画轨道交叉证据：Ras 的 `Action_Joy3 -> hart_*` 中 type 21/22/23 对应 `TRS2.0x37` 材质色 RGB，type 25/26/27/28 对应 `TRS2.0x38` illumination RGBA；Otohime 的 `Change_Fashion -> momo_circle_*` 也给出 type 21/22/23 与材质色 RGB 的候选对应，部分通道相差一个 8-bit step。因此当前模型把 4 字节颜色解释为 `A,R,G,B`，但仍保留为候选而非已确认渲染格式。

## Shared packed state words

`CIMG.0x48` 不是 image cast 私有 flags；它属于一组 shared packed state word。已核对的 loader/xref 中，`sub_88EC90` 将它保存到 CIMG 结构首个 dword，然后调用一组小 decoder 拆成多个 enum/bool-like 中间值；同一组 decoder 也被 `sub_88E120` (`CSLI.0x80`)、`sub_88F740` (`CNUM.0x48`)、`sub_88BDB0` (`TEX.0x62`)、`sub_88C670` (`LAYR.0x20` raw state) 和 `sub_88E7D0` (`SLIC.0x83` 低位状态) 复用。

2026-05-31 对抗校验后，CIMG draw 路径的低位语义可提升为已确认：低 4 位是 draw/blend mode，其中 mode `1` 是 additive/effect；`0x10` 是 flipU，`0x20` 是 flipV，`0xC0` 是 UV permutation mode；`0x7800` 是 surface mode，`0/0x0800/0x1000/0x1800/0x2000` 分别解码为 `0/1/2/3/4`。其它 owner（例如 TEX/CNUM/CSLI/SLIC）仍只按 shared packed state raw/decoder 记录，不把这些 CIMG draw 语义直接外推。

已核对的 decoder 位段拆分如下。表中的输出只说明 loader/decoder 结构，不命名业务语义：

| Mask | Decoder output | 已知调用范围 |
| --- | --- | --- |
| `0x0000000F` | CIMG: draw/blend mode；mode `1` = additive/effect。其它 owner 只记录 enum `0..3` | `CIMG.0x48`、`CSLI.0x80`、`CNUM.0x48` |
| `0x00000010` | CIMG: flipU；其它 owner 只记录 bool | `CIMG.0x48`、`SLIC.0x83` |
| `0x00000020` | CIMG: flipV；其它 owner 只记录 bool | `CIMG.0x48`、`SLIC.0x83` |
| `0x000000C0` | CIMG: UV permutation mode `0..3`；其它 owner 只记录 enum | `CIMG.0x48`、`SLIC.0x83` |
| `0x000000F0` | enum `0..2` for `0/0x10/0x20`，其它值落回 `0` | `CIMG.0x48`、`CSLI.0x80`、`TEX.0x62` |
| `0x00000100` | bool | `CIMG.0x48`、`CSLI.0x80`、`LAYR.0x20` raw state |
| `0x00000F00` | enum `0..2` for `0/0x100/0x200`，其它值落回 `0` | `CIMG.0x48`、`CSLI.0x80`、`TEX.0x62` |
| `0x00007800` | CIMG: surface mode `0..4` for `0/0x800/0x1000/0x1800/0x2000`；其它 owner 只记录 enum | `CIMG.0x48`、`CSLI.0x80`、`CNUM.0x48` |
| `0x00018000` | bool | `CIMG.0x48`、`CSLI.0x80`、`CNUM.0x48` |
| `0x00F00000` | enum `0..9` for `0x0..0x900000`，其它值落回 `0` | `CIMG.0x48` |
| `0x01000000` | bool | `CIMG.0x48` |
| `0x02000000` | bool | `CIMG.0x48` |

2026-05-29 复核 decoder xref 时，直接调用关系仍停留在装载/结构化阶段。2026-05-31 进一步结合 `sub_88E730`、`sub_7DAE10`、draw/raster 对照后，只把 CIMG 的 blend/flip/UV/surface mode 提升为运行时语义；`0xF0/0xF00`、`0xF00000/0x01000000/0x02000000` 以及非 CIMG owner 的业务含义仍未确认。

full survey 现在输出统一的 `SharedPackedStateOwner*` 聚合：owner 总数、owner+raw、owner+bit、low nibble、`0xF0`、`0xF00` 和 upper mask。JP/EN owner 总数分别为 `CIMG.0x48=7063/7228`、`CNUM.0x48=217/217`、`CSLI.0x80=26/26`、`LAYR.0x20=314/322`、`SLIC.0x83=108/108`、`TEX.0x62=2906/2922`。raw distinct 分布为：`CIMG.0x48` 78/82 种，top 为 `0x408000:3331/3409`、`0x408001:1208/1221`；`CNUM.0x48` 恒为 `0x8000`；`CSLI.0x80` 为 `0x8000:19`、`0x8001:6`、`0x8002:1`；`LAYR.0x20` 为 `0x100:220/225`、`0x0:94/97`；`SLIC.0x83` 为 `0x3:61`、`0x1:23`、`0x2:14`、`0x0:10`；`TEX.0x62` 为 `0x110:2625/2638`、`0x0:281/284`。这些聚合只用于复算 packed layout 与跨 owner 对比，不把任何 bit 命名为业务状态。

TEX full survey 现输出 `TextureAtlasField62*` 聚合。JP/EN atlas 总数为 2,906/2,922，`TEX.0x62` 只见 `0x110` 与 `0x0`；`0x110` 对应 bit 4 + bit 8，计数为 2,625/2,638，`0x0` 为 281/284。`TextureAtlasField62CropCountCounts` 显示所有 atlas 的 declared/parsed crop count 仍全量匹配（2,906/2,906 与 2,922/2,922）。这些统计只确认 raw packed state 分布和 crop 绑定结构，不命名 texture wrap/filter/layout 业务语义。

CIMG full survey 将 observed bits 扩展为 `0/1/4/5/6/7/8/9/11/12/13/15/20/21/22/23`。日文 surfboard 计数为 bit0=1719、bit1=131、bit4=338、bit5=86、bit6=25、bit7=20、bit8=105、bit9=8、bit11=10、bit12=10、bit13=380、bit15=7039、bit20=1415、bit21=766、bit22=5481、bit23=830；EN 对应为 1734/130/356/88/25/20/131/8/10/10/380/7191/1432/779/5609/839。bit 15 在 full survey 中仍是高覆盖率 CIMG 位，但不是全局必有，也不能命名为 image-cast marker。

解析器当前仍保留 legacy JSON 名称 `imageCastFlags` / `imageCastFlagBits`，但含义应按 `CIMG.0x48` raw packed state word / packed state bits 理解，不表示 image-cast 私有 flags。主报告还输出 `CIMG.0x48` full value、节点 flags、节点组、初始 display、multi/secondary CREF、非零 `0x45` group index 的交叉统计，以及 bit 共现表。Ras 中 `0+22` 共现 9 次、`20+23` 共现 117 次；Chiffon 中 `0+22` 共现 11 次、`20+23` 共现 44 次；Otohime 中 `0+22` 共现 23 次、`20+23` 共现 161 次。Ras/Chiffon 中 bit 23 是 bit 20 子集，但 Otohime 有 1 个 `0x00808000`（bit 23 不带 bit 20），所以 bit20/23 只能记录为 CIMG 样本共现关系。

full survey 现在也输出 `CimgFlagBit*` 交叉聚合。JP/EN 的 CIMG 总数分别为 7,063/7,228；bit 15 为 7,039/7,191，仍是高覆盖但分别有 24/37 个 CIMG 不带 bit 15。主要 bit 的交叉证据如下：

| Bit | JP | EN | 观察 |
| ---: | ---: | ---: | --- |
| 0 | 1,719 | 1,734 | 与 bit 22 强共现：`0+22` 为 1,624/1,639；非零 `0x45` index 为 32/38。 |
| 13 | 380 | 380 | 380/380 均有 multi/secondary CREF；非零 `0x45` index 为 29/35。 |
| 15 | 7,039 | 7,191 | 高覆盖 CIMG 位，但不是全局必有。 |
| 20 | 1,415 | 1,432 | 与 bit 23 强共现：`20+23` 为 559/567。 |
| 21 | 766 | 779 | 与 bit 20 共现 577/580。 |
| 22 | 5,481 | 5,609 | 覆盖大多数 bit 0，并与 bit 15 强共现。 |
| 23 | 830 | 839 | 大多数与 bit 20 共现，但不能固定为子集语义。 |

bit 与资源选择的交叉统计显示：JP/EN 的非零 `0x45` group index 只覆盖 bit 15 的 131/124、bit 22 的 126/119、bit 0 的 32/38 等少量 CIMG；因此不能把这些 bit 简单命名为 `0x45` group index 相关字段。所有 bit 的 `MissingNode` 计数为 0，说明当前 full survey 中 CIMG 均能绑定到目标 NODE。

## TRK / KEY storage

`TRK.0x54` 是 track flags。Ras 中只出现四类 full value；Chiffon 额外出现 `0x113/0x133/0x143`，说明需要拆成 `base byte = flags & 0xFF` 和 `extra mask = flags & ~0xFF`。base byte 的 low nibble 固定为 `0x3`，base byte 的 high nibble 与 `KEY.0x5B` 字段类型对应：

| Base byte | Low | Storage nibble | `KEY.0x5B` 类型 | 候选 storage |
| ---: | ---: | ---: | --- | --- |
| `0x13` | `0x3` | `0x1` | `0x000A Float32` | Float32 curve |
| `0x23` | `0x3` | `0x2` | `0x0008 Int32` | Int32 state / image variant |
| `0x33` | `0x3` | `0x3` | `0x0001 Bool` | Bool state / display |
| `0x43` | `0x3` | `0x4` | `0x000B PackedAngleCandidate` | PackedAngleCandidate curve；旋转上下文按 signed fixed-angle raw int 候选解释 |

Chiffon 中 extra mask `0x100` 出现 6 条 track，全部属于 `Action_Wait3 -> smile` 的 transform/display 轨道，目标节点为 hidden CIMG 节点。Otohime 中 `extra=0x100` 扩展到 63 条 track，全部目标都是 CIMG 节点，node flags 分布为 `0xF01:61, 0xE01:2`，groups 为 `eye:48, puru:13, arm:2`，初始 display=false 为 2/63；仍不改变 `KEY.0x5B` 存储类型。这两个角色样本支持“动作局部 CIMG 控制”候选，但 full survey 已显示不能把该候选全局化。

full survey 中 base byte 仍只出现 `0x13/0x23/0x33/0x43`，extra mask 只出现 `0x0/0x100`；`extra=0x100` 为 3063 与 3066 条。`TrackFlagExtra*` 交叉聚合显示：`0x100` 出现在 JP/EN 的 123/124 个场景；base 分布为 `0x13=1980/1983`、`0x23=177/177`、`0x33=51/51`、`0x43=855/855`；主要 track type 为 `5(RotateZ)=851/851`、`24(MaterialAlpha)=468/468`、`7(ScaleY)=438/438`、`6(ScaleX)=379/379`、`18(PrimaryImageVariantIndexCandidate)=174/174`。`0x100` 下 key value type 仍按 base byte 对应 Float32/PackedAngle/Int32/Bool，说明 extra mask 不改变 value storage。

`0x100` 的目标不是全 CIMG：JP/EN 中 CIMG target 为 1818/1821 条 track，非 CIMG target 为 1245/1245；初始 display=false 为 214/214。animation 名称覆盖 `Loop`、`Action`、`TrackSkip_Loop`、`loop_SSS_Plus`、`AdvertiseLoop`、`Action_Wait*`、`DressChange` 等 UI/loop/action 混合上下文。因此当前只能把 `0x100` 记录为 raw extra mask；不能命名为 action-local 或 special-effect flag。

transform track 现在单独输出 `TransformTrack*` full survey 聚合。JP/EN transform track 为 68,482/69,174 条，key 为 124,712/125,579 个；所有 transform key 都有可解析 value。type `0/1/5/6/7` 能绑定目标 `TRS2` 初始通道，至少一个 key 匹配初始通道为 64,375/65,001 条 track，mismatch 为 3,543/3,609 条 track；这些 mismatch 说明不能把初始通道匹配写成初始化规则。type `2/3/4/8` 无可比 `TRS2` 初始通道，但全部 key 等于候选默认值 `0/0/0/1`，只确认存储和值域。`TransformTrackStorageCounts` 显示 type `0/1/2/6/7/8` 使用 Float32 曲线，type `3/4/5` 使用 PackedAngleCandidate 曲线；这仍不确认 Z 轴或 X/Y rotation 的运行时语义。

颜色相关 track type 现在按样本证据提升为候选命名：`21/22/23 = MaterialColor R/G/B`，`25/26/27/28 = IlluminationColor R/G/B/A`。这些 track 都走 `0x13 + Float32` 存储，值域按 0..1 的颜色通道归一化处理。full survey 中 JP/EN 分别有 5,726/5,774 条颜色 track、11,157/11,231 个 key；所有 key 都在 `0..1`，所有目标节点都有对应初始 TRS2 通道。至少一个 key 匹配初始通道的 track 为 5,371/5,437；不是全覆盖，因此只作为通道关联证据。

type 18 / 19 当前分别命名为 `PrimaryImageVariantIndexCandidate` / `SecondaryImageVariantIndexCandidate`。full survey 中 JP/EN 分别有 1,791/1,792 条 type 18 track、6,986/6,988 个 key；legacy `ImageVariant*` 字段继续保留 type 18 的 primary+secondary 合计数量宽松检查，仅用于兼容早期统计。新增 `ImageVariantGroup*` 聚合按 primary/secondary CREF 组分别复核：`18 primary` 在 JP/EN 为 1,791/1,792 条 track、6,986/6,988 个 key，全部目标节点有 CIMG、全部 track/key in range；`19 secondary` 在 JP/EN 均为 333 条 track、9,769 个 key，同样全部有 CIMG 且全部 in range。type 19 secondary 的 CREF count 分布为 `2:41, 5:16, 32:276`，key value 分布为 0..31。`ImageVariantGroupCimg45FirstKey*` 显示 type 18/19 最早 key 与 `CIMG.0x45` 的对应关系不是全覆盖：primary JP/EN 为 1533/1534 match、258/257 mismatch，secondary 两组都是 332 match、1 mismatch。`sub_7CE8B0` 对 type 18 使用 CIMG primary CREF count 校验，对 type 19 使用 secondary CREF count 校验；运行时 `sub_7D4F50/sub_7D50A0` 也把 type 18/19 分别写入 primary/secondary 图片 slot，并经 `sub_7D5590/sub_7D56A0` 解析到 CREF 图块坐标。因此 type 18 不应命名为跨 primary/secondary 两组的统一选择，`CIMG.0x45` 是静态 fallback/default group index，但不能全局命名为动画起始 key、当前引用或选中引用。

type 24 与 `TRS2.0x37` 的 alpha 通道有交叉证据：Ras/Chiffon/Otohime 中分别有 56/58、34/34、37/37 条 type 24 track 至少一个 key 与目标节点初始材质 alpha 匹配。full survey 中 JP/EN 为 9,849/9,977 条 type 24 track、19,242/19,427 个 key，所有 key 都在 `0..1`，所有目标节点都有 material alpha；初始 alpha 匹配为 8,142/8,229 条 track。参考运行时将 type 24 写入 `MaterialColor.A`，因此当前按 material alpha / effective opacity 动画通道处理；它也用于无 CIMG 的 root/null/control 节点，父级 effective opacity 会继续向子树相乘。

当前 PNG renderer / Viewer 按节点树累计 effective opacity：本节点 material alpha（含 type 24 覆盖后的值）会与父级 effective opacity 相乘。Milk 默认状态中 `Tail_Toge_NUL`、`Arm_2_ALL` 这类无 CIMG 父节点 alpha 为 0，子 CIMG 因父级 effective opacity 为 0 而不应绘制。

结构层 full survey 现在输出 `TrackKeyStorageMatrixCounts`、`TrackFieldSequenceCounts`、`KeyFieldSequenceCounts`、`TrackFrameRangeRelationCounts`、`TrackKeyFrameOrderCounts`、`TrackKeyFrameDuplicateCounts`、`TrackFirstFrameDeltaCounts` 和 `TrackLastFrameDeltaCounts`。JP/EN 的 `TRK` 字段顺序各只有一种：`0x0053:0x0006>0x0057:0x0006>0x0054:0x0009>0x0058:0x0008>0x0059:0x0008`，覆盖 94,611/95,558 条 track。`KEY` 字段顺序各只有 4 种，差异仅为 `0x5B` type：Float32 为 113,316/114,370，PackedAngleCandidate 为 45,602/45,690，Int32 为 16,755/16,757，Bool 为 11,107/11,185；其余字段保持 `0x5A Int32`、`0x5C UInt16`、`0x5D Float32`、`0x5E Float32`。

key frame 序列在 JP/EN 的 94,611/95,558 条 track 中全部为非递减。`TRK.0x58 first frame` 与最小 key frame 的差值全部为 0；`TRK.0x59 last frame` 等于最大 key frame 的 track 为 91,185/92,114，另外 3,426/3,444 条为 last frame 大于最大 key frame，仍包含全部 key。重复 frame 只观察到 2/2 条 track，均在 `MM_UI_DetailOption__Base_UI.sbscene` 与 `MM_UI_Shiborikomi__Base_UI.sbscene` 的 `FadeIn -> cover`、type 24 `MaterialAlpha`，frame 4 有两个不同 alpha key。因此只能确认 key frame 非递减，不能写成严格递增，也不能把重复 frame 的运行时选择规则当作已确认。

`TRK.0x57` 在 full survey 中没有 key count mismatch。`KEY.0x5C` 已按参考运行时收紧为 interpolation selector：`0` 为 step/hold，`2` 为 Hermite spline，其它非零值按 linear 处理。full survey 的 `0x5C` 分布为 JP `Linear=107658, Spline=51674, StepOrConstant=27448`，EN `Linear=108687, Spline=51787, StepOrConstant=27528`。非零 tangent 为 9757/9777，按 interpolation 为 `Spline=8293/8312`、`Linear=1464/1465`、`StepOrConstant=0/0`。

`0x5D != 0x5E` 在 JP/EN 中均为 123 个 key、9 个场景；按 track type 为 `5(RotateZ)=104`、`1(TranslateY)=11`、`6(ScaleX)=4`、`7(ScaleY)=4`，按 extra mask 为 `0x0=102`、`0x100=21`。运行时 Hermite 段使用当前 key 的 `0x5E` 作为 outgoing tangent、下一 key 的 `0x5D` 作为 incoming tangent，因此二者不能合并；duplicate frame 的选择规则仍单独保留为边界未知。

新增 KEY tangent 位置/符号聚合显示，非零 tangent 的 key 序列位置为 JP/EN `first=3003/3021`、`middle=4648/4649`、`last=2037/2038`、`single=69/69`；`0x5D != 0x5E` 只出现在 `middle=116/116` 和 `last=7/7`。mismatch 中 `0x5D` 与前一段 value delta 同号为 106/106，`0x5E` 与后一段 value delta 同号为 90/90；但整体非零 tangent 中两侧都与相邻 delta 同号仅 486/486，因此仍不能把 `0x5D/0x5E` 写成简单相邻差分。

### Type `0x0B` packed angle 候选

`0x0B` 过去在文档和输出中曾被临时称为 `Int32/Float32` 或 `PackedFloat32`。结合 Ras 中 `TRS2.0x32` 和 `KEY.0x5B` 的旋转轨道，当前只作为 signed fixed-angle raw int 候选解释，而不是已确认的 packed float。候选公式：

```text
degrees = raw * 180.0 / 32768.0
```

渲染时要把上面解出的 scene/source 角度再转换到 2D 像素坐标系。当前 CLI PNG renderer 和 Viewer 都在屏幕坐标（Y 向下）中用 `-degrees` 构造本地旋转矩阵；这一步与 `raw -> degrees` 的数值解码分开记录。Ras 的 `plain_leg_R1` 静态 `TRS2.0x32 raw=5461` 是当前校验样本：raw 解码约为 `+30 deg`，在屏幕矩阵中按 `-30 deg` 应用后右腿链回到身体下方；直接按 `+30 deg` 应用会把右小腿/鞋子甩到身体右侧。

该解释只用于旋转相关上下文：`TRS2.0x32` 的 rotation 候选，以及 `KEY.0x5B` 在 `TRK.flags = 0x43` / high nibble `0x4` 的旋转轨道中。示例换算：

| Raw | Degrees candidate |
| ---: | ---: |
| 910 | 约 5 deg |
| 1820 | 约 10 deg |
| 5461 | 约 30 deg |
| 16383 | 约 90 deg |
| 32767 | 约 180 deg |

Ras 中 `KEY.0x5B type 0x0B` 主要出现在 track type `5(RotateZ)`，并有少量 type `3/4` 旋转候选。`CAM.0x14` 在 JP/EN full survey 中全量为 `0x1FFF`，仍按 flags-like / unknown 处理；它不是该旋转轨道上下文，不应套用上述角度公式。

full survey 将 `KEY.0x5B type 0x0B` 扩展到 surfboard/surfboard_EN 全量样本：JP 为 16,265 条 track / 45,602 个 key，EN 为 16,305 条 track / 45,690 个 key；两组均只出现在 track type `3/4/5`。raw distinct 两组均为 1,092，raw range 均为 `-75548..83740`，按 `raw * 180 / 32768` 换算约 `-414.9976..459.9976 deg`。对抗校验后，旋转渲染按 signed binary angle 处理，并在 2D 屏幕坐标中取负号；raw 不应先截断为 16-bit，超过一圈的值按同一比例进入矩阵。
