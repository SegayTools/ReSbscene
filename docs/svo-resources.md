# SVO 纹理资源记录

Ras 对应纹理容器：

```text
D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras.svo
```

## 当前观察

| 项 | 值 |
| --- | --- |
| 文件头 | `AVTS` |
| 文件大小 | 7,348,096 bytes |
| 目录项数量 | 5 |
| 内含 DDS 数 | 4 |
| DDS magic offsets | `0x1D80`, `0x241E00`, `0x481E80`, `0x4C1F00` |

当前解析器不依赖外部 SvoToolOutput，而是直接解析 `.svo` 的 `AVTS` 目录表定位 DDS payload，再用 `.sbscene` 的 `TEX/CROP` 表命名和切图。
主 `.sbscene` dump 现在也会把 `TEXL/TEX/CROP/CIMG/CREF/CNUM/CRFD/TEXT/CSLI/SLIC` 解析结果写入 `surfboard.resources`，不必只通过 `extract-images` 查看资源映射。
其中 `CRFD.0x90/0x91` 已在 surfboard/surfboard_EN full survey 中确认到 sibling scene 文件名的机械关系：`0x90 + "__" + 0x91` 按大小写不敏感比较全量命中同目录 scene stem；详细 exact/ignore-case 统计见 `docs/sbscene-format.md`。

最早用于交叉验证的是 Ras、Chiffon、Otohime 三个 SVO。三者 AVTS directory 与 YABX schema 均可完整解析：

| 样本 | Directory entries | DDS | Reserved non-zero | YABX objects | Object coverage | Reference base | Descriptor distribution |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| Ras | 5 | 4 | 0 / 5 | 14 / 14 | `1330/1330`, unparsed 0 | `0x2711` | `000000:12, 000400:1, 010000:8, 010200:2, 010400:18` |
| Chiffon | 5 | 4 | 0 / 5 | 14 / 14 | `1434/1434`, unparsed 0 | `0x2711` | 同 Ras |
| Otohime | 4 | 3 | 0 / 4 | 12 / 12 | `1069/1069`, unparsed 0 | `0x2711` | 同 Ras |

上表只是最早的三样本交叉验证 checkpoint。后续 MM_CH 7 个 SVO 与 surfboard/surfboard_EN full survey 已把 `referenceBase=0x2711` 扩展为 `7/7`、`47/47 + 47/47`；当前 `Database / VertexDeclaration / VertexElement / Texture / Image` resource skeleton 内可按 `referenceId - 0x2711` 静态反查对象 index。schema descriptor raw 分布和 payload 容器关系也已有 full-survey 观察证据，但 descriptor 的精确命名仍保持候选。
三份样本合计 14 个 AVTS directory entry 的 `+0x210..+0x3FF` reserved 区均为 0；这只是三样本阶段观察，不代表该区域已可删除或忽略。

MM_CH 7 个 SVO survey 进一步确认：7/7 个 SVO 可完整解析，AVTS directory reserved 区没有非零项，YABX object payload 未解析剩余字节为 0，7/7 个样本的 `referenceBase` 均为 `0x2711`，schema descriptor 第三字节 287/287 为 `0`。这仍是样本事实，不把 reserved 字段命名为已知语义。

surfboard/surfboard_EN full survey 进一步扩展到 47 + 47 个 SVO：两组均 47/47 解析成功，AVTS directory `+0x210..+0x3FF` reserved 区没有非零项，YABX object payload 未解析剩余字节为 0。47 + 47 个 SVO 均仍有 AVTS header unknown 字节：日文合计 2576 bytes、EN 合计 2567 bytes。Header unknown word class 汇总为：

| Class | surfboard | surfboard_EN |
| --- | ---: | ---: |
| `in-file-offset-candidate` | 440 | 448 |
| `pointer-or-residue-candidate` | 136 | 134 |
| `small-scalar-candidate` | 179 | 185 |
| `unknown` | 298 | 287 |
| `utf16-chars-candidate` | 36 | 32 |

这些 class 名称只用于排查，不是已确认字段语义。survey 现在同时输出 `HeaderUnknownWordOffsetValueCounts`、`HeaderUnknownWordOffsetClassCounts`、`HeaderUnknownNonZeroOffsetCounts`、`HeaderUnknownWordRelationCounts`、`HeaderUnknownWordOffsetRelationCounts`、`HeaderUnknownWordPayloadLocationCounts` 和 `HeaderUnknownWordOffsetPayloadLocationCounts`，用于检查每个 header offset 的 raw value 稳定性以及它与目录项、payload 区间和文件大小的机械关系。full survey 中没有任何 unknown word offset 出现“全 47 个 SVO 都相同且非零”的值；唯一全组稳定的 word 是 `0x70=0`。几个高频但非全覆盖的 raw 值如下，仍只能作为 residue/候选证据：

| Offset | surfboard top value | surfboard_EN top value | 说明 |
| ---: | --- | --- | --- |
| `0x18` | `0x0018FA40` x37 | `0x0018FA40` x38 | 文件内 offset candidate，但不是全覆盖。 |
| `0x24` | `0x00000015` x38 | `0x00000015` x39 | small-scalar candidate，仍有多种其它值。 |
| `0x44` | `0x0018FB28` x35 | `0x0018FB28` x36 | 文件内 offset candidate，不匹配已确认目录 payload 起止。 |
| `0x58` | `0x0018FB28` x29 | `0x0018FB28` x29 | 同上，只是高频 raw 值。 |
| `0x5C` | `0x0018FB28` x18 | `0x0018FB28` x18 | 同上，只是高频 raw 值。 |
| `0x6C` | `in-file-offset-candidate` x47 | `in-file-offset-candidate` x47 | class 全覆盖且 relation 全部落入 payload，但 raw value 与相对位置均变化，不能命名为固定 offset 字段。 |

relation 聚合进一步把 `in-file-offset-candidate` 拆成目录表、payload 区间和其它文件内 offset 等机械关系。当前 full survey 的 non-zero unknown word relation 汇总如下：

| Relation | surfboard | surfboard_EN |
| --- | ---: | ---: |
| `inside-payload:DDS` | 441 | 450 |
| `inside-payload:YABX` | 6 | 6 |
| `inside-directory-table` | 74 | 72 |
| `other-in-file-offset` | 108 | 114 |
| `entry-sequence` | 6 | 6 |
| `pointer-or-residue` | 136 | 134 |
| `out-of-file-or-unknown` | 318 | 304 |

其中 `0x64/0x68/0x6C` 的 offset relation 已收紧为：

| Header offset | surfboard | surfboard_EN | 观察 |
| ---: | --- | --- | --- |
| `0x64` | `pointer-or-residue` 39、`out-of-file-or-unknown` 8 | `pointer-or-residue` 37、`out-of-file-or-unknown` 10 | 未见文件内 payload/目录关系。 |
| `0x68` | `out-of-file-or-unknown` 37、`pointer-or-residue` 10 | `out-of-file-or-unknown` 32、`pointer-or-residue` 15 | 未见文件内 payload/目录关系。 |
| `0x6C` | `inside-payload:DDS` 46、`inside-payload:YABX` 1 | `inside-payload:DDS` 46、`inside-payload:YABX` 1 | 47/47 都落入 payload 区间，但不是 payload 起点、长度或末尾。 |

`HeaderUnknownWordOffsetPayloadLocationCounts` 还会输出 payload magic、目录 entry index、相对 payload 起点偏移和到 payload 末尾距离。`0x6C` 在当前两组 full survey 中的 entry 分布为：

| `0x6C` payload location | surfboard | surfboard_EN |
| --- | ---: | ---: |
| `DDS entry-index:1` | 35 | 34 |
| `DDS entry-index:2` | 6 | 6 |
| `DDS entry-index:3` | 3 | 4 |
| `DDS entry-index:4` | 2 | 2 |
| `YABX entry-index:0` | 1 | 1 |

两组的 `relative/end-minus` 组合各有 44 种；只有 `DDS entry-index:1 relative=0x3E84B end-minus=0x201835` 出现 4 次，其余主要为单例。因此 `0x6C` 当前只能写成“payload 内地址/残留关系稳定”，不能写成固定 payload 内偏移、固定 DDS 子结构或固定资源索引。

已见 DDS pixel format 均已命名并可解码：

| Format | surfboard | surfboard_EN | 说明 |
| --- | ---: | ---: | --- |
| DXT5 | 149 | 153 | BC3/DXT5 压缩纹理。 |
| A8R8G8B8 | 104 | 100 | 32bpp 未压缩，DDS mask 为 `A=0xFF000000,R=0x00FF0000,G=0x0000FF00,B=0x000000FF`。 |
| DXT1 | 13 | 13 | BC1/DXT1 压缩纹理。 |
| A4R4G4B4 | 7 | 7 | 16bpp 未压缩，DDS mask 为 `A=0xF000,R=0x0F00,G=0x00F0,B=0x000F`。 |
| R5G6B5 | 7 | 7 | 16bpp 未压缩，DDS mask 为 `R=0xF800,G=0x07E0,B=0x001F`。 |
| R8G8B8 | 1 | 1 | 24bpp 未压缩，DDS mask 为 `R=0x00FF0000,G=0x0000FF00,B=0x000000FF`。 |

这些格式在 YABX `stevia::Image._format` 中当前均显示 code `1`，因此 `_format=1` 不能命名为 DDS pixel format。

## AVTS 目录名

Ras 样本中 `AVTS+0x04` 是目录项数量，值为 5。三样本阶段已按 `0x80` header、`0x80` 起目录表、每项 `0x400` 字节成功解析；`0x08..0x7F` 仍为 unknown words。

| Header offset | 字段 | 类型 | Ras 值 |
| ---: | --- | --- | --- |
| `0x00` | magic | ASCII[4] | `AVTS` |
| `0x04` | directoryCount | `u32` | 5 |
| `0x08..0x7F` | unknownWords | `u32[30]` | 50 个非零字节；当前原样输出并附候选分类。多个值未匹配文件内 offset 证据，仅按 header residue / unknown raw data 处理。 |

Ras header 的非零 unknown word 已在 `inspect-svo` 中全量输出并做轻量分类。该分类只用于排查，不作为 confirmed 语义：

| 分类 | Offset / Value | 观察 |
| --- | --- | --- |
| `utf16-chars-candidate` | `0x0C=0x005F005F` | 小端字节为 `5F 00 5F 00`，符合 UTF-16 `"__"` 字节形态候选。 |
| `small-scalar-candidate` | `0x24=0x15`、`0x28=0x12B`、`0x2C=0x15`、`0x40=0x0F` | 小整数；仅按计数/flags/状态值候选分类，语义未确认。 |
| `in-file-offset-candidate` | `0x18=0x18FA40`、`0x44=0x18FB28`、`0x58=0x18FB28`、`0x6C=0x2400CB` | 落在文件大小范围内。Ras 的 `0x6C` 进一步落入 DDS payload，但不等于 AVTS 目录项的 payload 起点、长度或末尾。 |
| `pointer-or-residue-candidate` | `0x20/0x30/0x38/0x50/0x54/0x60/0x74/0x78/0x7C` 的 `0x00A5....`，以及 `0x48/0x5C/0x64/0x68` 的 `0x77/0x67....` | 仅按 pointer-like residue / 未清零 residue 候选分类；暂不参与资源定位。 |

Chiffon/Otohime 的 header unknown words 与 Ras 只有部分 offset 稳定相同，例如 `0x18=0x0018FA40`、`0x24=0x15`、`0x2C=0x15`、`0x40=0x0F`、`0x44=0x0018FB28`、`0x48=0x77B8FB02`、`0x64=0x77BA3476`。大量 `0x00A5.... / 0x0090.... / 0x0088....` 类值随样本变化，仍按 unknown/residue 处理，不用于资源定位。

`0x80 + index * 0x400` 处是 null-terminated ASCII 名称：

| Index | Name | 说明 |
| ---: | --- | --- |
| 0 | `__HmfToSvo__MM_CH_Ras.svo` | 容器名。 |
| 1 | `__HmfToSvo__MM_CH_Ras_000.dds` | DDS 0，对应 `TEX MM_CH_Ras_000`。 |
| 2 | `__HmfToSvo__MM_CH_Ras_001.dds` | DDS 1，对应 `TEX MM_CH_Ras_001`。 |
| 3 | `__HmfToSvo__MM_CH_Effect_000.dds` | DDS 2，对应 `TEX MM_CH_Effect_000`。 |
| 4 | `__HmfToSvo__MM_CH_Ras_002.dds` | DDS 3，对应 `TEX MM_CH_Ras_002`。 |

当前实现用目录名、AVTS offset/size 和 DDS magic 三者交叉确认资源顺序。

## AVTS 目录表

目录项从 `0x80` 开始，每项大小 `0x400`。Ras 样本中目录项字段如下：

| Entry offset | 字段 | 类型 | 说明 |
| ---: | --- | --- | --- |
| `+0x000` | name | null-terminated ASCII | 资源名，最多按 `0x200` 字节读取。 |
| `+0x200` | kind | `u32` | 第 0 项为 0，DDS 项为 1。 |
| `+0x204` | sequence | `u32` | 第 0 项为 0，DDS 项为 1..4。 |
| `+0x208` | dataLength | `u32` | 资源 payload 长度。 |
| `+0x20C` | dataOffset | `u32` | 资源 payload 文件偏移。 |

Ras 目录项：

| Index | Name | Kind | Seq | Offset | Length | Magic |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| 0 | `__HmfToSvo__MM_CH_Ras.svo` | 0 | 0 | `0x1480` | `0x900` | `YABX` |
| 1 | `__HmfToSvo__MM_CH_Ras_000.dds` | 1 | 1 | `0x1D80` | `0x240080` | `DDS ` |
| 2 | `__HmfToSvo__MM_CH_Ras_001.dds` | 1 | 2 | `0x241E00` | `0x240080` | `DDS ` |
| 3 | `__HmfToSvo__MM_CH_Effect_000.dds` | 1 | 3 | `0x481E80` | `0x40080` | `DDS ` |
| 4 | `__HmfToSvo__MM_CH_Ras_002.dds` | 1 | 4 | `0x4C1F00` | `0x240080` | `DDS ` |

解析器现在优先使用 AVTS 目录表定位 DDS。只有目录表不可用或没有 DDS 项时，才回退到 DDS magic 扫描。
Ras/Chiffon/Otohime 中每个目录项 `+0x210..+0x3FF` 的 reserved 区全为 0；当前 CLI 输出目录级 `Directory reserved summary`，三份样本分别为 `entriesWithNonZero=0/5`、`0/5`、`0/4`。

## YABX 元数据

第 0 个目录项指向一个 `YABX` payload：

| 字段 | 值 |
| --- | --- |
| offset | `0x1480` |
| length | `0x900` |
| magic | `YABX` |
| version | `1` |
| declared payload length | `0x8F0` |
| header hash candidate | `0xA4E986BC` |

full survey 中 `declared payload length == YABX directory entry length - 16` 在 surfboard/surfboard_EN 两组各为 47/47，mismatch 为 0。该长度字段可以按 header 后 payload 长度读取。

最后 4 字节字段在三份角色样本中不同：Ras `0xA4E986BC`、Chiffon `0x69AE571D`、Otohime `0x06E77BC1`。full survey 中 JP/EN 各有 47 个候选值，组内均不重复；两组 union 为 53 个值，intersection 为 41 个值。已用脚本排查若干常见 payload 变体 checksum：CRC32、CRC32C、Adler32、FNV1a32 对 `payload[16:]`、`payload[:12]+payload[16:]` 等输入均为 0/94 匹配。因此目前仍只保留为 `headerHashCandidate`，不确认它一定是 hash/checksum。

当前解析器会提取 YABX header、类型 schema、对象表和资源记录。Ras 中可见：

| 类别 | 数量 | 样例 |
| --- | ---: | --- |
| strings | 84 | `HmfToSvo/svo`、`MM_CH_Ras_000.dds` |
| type names | 7 | `stevia::Database`、`stevia::Resource`、`stevia::Texture`、`stevia::Image` |
| field names | 40 | `_name`、`_flag`、`_image`、`_height`、`_width`、`_fileName` |
| resource names | 14 | `MM_CH_Ras_000`、`MM_CH_Ras_000.dds`、`__HmfToSvo__MM_CH_Ras_000.dds` |
| object records | 14 | 1 database、2 vertex declarations、3 vertex elements、4 textures、4 images |

YABX schema 区从 `yabukita::Object` 开始，当前恢复出的 type index 与字段如下。字段 descriptor label 来自字段名后的 3 字节描述符，仍只是排查标签：

| Type index | Type | Fields |
| ---: | --- | --- |
| 1 | `yabukita::Object` | none |
| 2 | `stevia::Resource` | `_name`、`_flag`、`_fullName`、`_userParameter` |
| 3 | `stevia::Database` | `_state`、`_mesh`、`_batch`、`_vertexBuffer`、`_indexBuffer`、`_vertexDeclaration`、`_texture`、`_image`、`_tree` |
| 4 | `stevia::VertexDeclaration` | `_vertexElement` |
| 5 | `stevia::VertexElement` | `_semantics`、`_elementType`、`_index` |
| 6 | `stevia::Texture` | `_wrapU`、`_wrapV`、`_minFilter`、`_magFilter`、`_mipFilter`、`_anisoNumber`、`_lodBias`、`_id`、`_uvSetIndex`、`_uvSetName`、`_attributeName`、`_textureType`、`_image` |
| 7 | `stevia::Image` | `_height`、`_width`、`_maxMipmapLevel`、`_format`、`_compressCustomOption`、`_alphaMode`、`_fileName`、`_chunkFileName`、`_file`、`_mipmapFileName`、`_dataSize` |

上表是 schema 区可见的 class-local 字段。对象 payload 中还会串接 `stevia::Resource` 基类字段；当前解析器已在 `Database`、`VertexDeclaration`、`Texture`、`Image` 对象末尾解析 `_name/_flag/_fullName/_userParameter`。

字段描述符目前按 3 字节 raw 值保留，同时拆成 flags/valueKind/reserved 候选。Ras/Chiffon/Otohime 三份样本每份 raw 分布相同：

| Raw descriptor | Count | 观察 |
| --- | ---: | --- |
| `000000` | 12 | 数据库空引用列表、Resource 字符串等，不能简单等同为 string。 |
| `000400` | 1 | `stevia::Resource._flag`。 |
| `010000` | 8 | 字符串或引用列表容器候选。 |
| `010200` | 2 | `_image`、`_file`，对象引用候选。 |
| `010400` | 18 | `i32`/枚举值候选。 |

三份样本每份 descriptor 字节分布也相同：

| Byte | Distribution |
| --- | --- |
| flags byte | `0x00:13, 0x01:28` |
| valueKind byte | `0x00:20, 0x02:2, 0x04:19` |
| reserved byte | `0x00:41` |

合计 123/123 个 descriptor 的第三字节为 0。该字节仍按 raw/reserved 输出；目前只确认观察值，不命名运行时语义。

full survey 中 surfboard/surfboard_EN 两组各有 1927 个 descriptor，第三字节均为 0。两组 raw descriptor 聚合相同：

| Raw descriptor | surfboard | surfboard_EN |
| --- | ---: | ---: |
| `000000` | 564 | 564 |
| `000400` | 47 | 47 |
| `010000` | 376 | 376 |
| `010200` | 94 | 94 |
| `010400` | 846 | 846 |

拆分后的 byte 分布也相同：flags 为 `0x0:611, 0x1:1316`，valueKind 为 `0x0:940, 0x2:94, 0x4:893`，reserved 为 `0x0:1927`。

survey JSON 现在还会输出 descriptor usage 聚合：`YabxDescriptorUsageCounts` 按 `owner.field + raw descriptor + 实际 object payload kind + payload length` 统计，`YabxDescriptorRawObjectKindCounts` 按 `raw descriptor + 实际 object payload kind` 统计。surfboard/surfboard_EN 两组各 1927 个 descriptor 的 raw-to-payload-kind 聚合完全一致：

| Raw descriptor | Object payload kind | surfboard | surfboard_EN |
| --- | --- | ---: | ---: |
| `000000` | `ReferenceList` | 423 | 423 |
| `000000` | `String` | 141 | 141 |
| `000400` | `Int32` | 47 | 47 |
| `010000` | `ReferenceList` | 47 | 47 |
| `010000` | `String` | 329 | 329 |
| `010200` | `ObjectReferenceId` | 94 | 94 |
| `010400` | `Int32` | 846 | 846 |

这个聚合只确认“同一个 raw descriptor 在 surfboard/surfboard_EN full survey 范围内实际被哪些 payload 容器承载”。例如 `000000` 同时覆盖 `ReferenceList` 和 `String`，`010000` 也同时覆盖 `ReferenceList` 和 `String`；因此解析器把 `valueKind=0x00` 的 label 收窄为 `OwnerDependent`，表示实际容器要结合 owning type / field 顺序判断，不能脱离上下文命名成唯一容器类型。

`inspect-svo` 还会输出 descriptor usage evidence，将 schema descriptor 和实际 object payload kind 交叉核对。Ras 中关键样例如下：

| Schema field | Raw descriptor | Descriptor label | Object payload kind | 观察 |
| --- | --- | --- | --- | --- |
| `stevia::Database._image` | `000000` | `OwnerDependent` | `ReferenceList` | database 内 `_image` 是 4 个 image object ref 的列表。 |
| `stevia::Texture._image` | `010200` | `ObjectReference` | `ObjectReferenceId` | 每个 texture 指向对应 image object。 |
| `stevia::Image._file` | `010200` | `ObjectReference` | `ObjectReferenceId` | Ras 中值全为 0，按 null reference 处理。 |
| `stevia::VertexDeclaration._vertexElement` | `010000` | `OwnerDependent` | `ReferenceList` | 指向 1 或 2 个 vertex element object。 |
| `stevia::Resource._name` | `000000` | `OwnerDependent` | `String` | 由继承 Resource 的对象使用，包括 `MM_CH_Ras`、`P`、`PN`、texture/image 名称。 |

因此 raw descriptor 只能给出候选编码类别；实际容器还要结合 owning type 和字段顺序判断。

schema 后存在对象表。Ras 样本中对象表偏移为 `0x361`，声明对象数为 14。对象记录格式当前确认为：

| Offset | 字段 | 类型 | 说明 |
| ---: | --- | --- | --- |
| `+0x00` | typeIndex | `u16` | 指向上表 type index。 |
| `+0x02` | payloadLength | `u32` | 后续 payload 长度。 |
| `+0x06` | payload | bytes | 按 schema 编码的对象数据。 |

对象分布：

| Type | Count | 说明 |
| --- | ---: | --- |
| `stevia::Database` | 1 | 顶层资源数据库，`_texture` 和 `_image` 是引用列表。 |
| `stevia::VertexDeclaration` | 2 | 顶点声明，`_vertexElement` 是引用列表，并带 `stevia::Resource` 基类字段；Ras 中 `_name/_fullName` 为 `P` 与 `PN`。 |
| `stevia::VertexElement` | 3 | 当前按 `_semantics`、`_elementType`、`_index` 三个 `i32` 解析。 |
| `stevia::Texture` | 4 | 每个 atlas 一条 texture 对象，字段级解析出 sampler 参数、`_textureType`、`_image` 引用和 Resource 基类字段。 |
| `stevia::Image` | 4 | 每个 DDS 一条 image 对象，字段级解析出宽高、format code、file/chunk file 名和 dataSize。 |

full survey 现在会用 `YabxObjectCountMatchesDdsSkeleton` 与 `YabxObjectTypeOrderMatchesDdsSkeleton` 验证 full-survey 范围内的对象骨架。surfboard/surfboard_EN 两组各 47 个 SVO 均满足：

- object count = `6 + 2 * DDS count`，两组 expected object count 均为 844，匹配为 47/47，mismatch 为 0。
- object type order = `Database, VertexDeclaration, VertexElement, VertexDeclaration, VertexElement, VertexElement, (Texture, Image) * DDS count`，两组匹配为 47/47，mismatch 为 0。
- object type totals 两组完全一致：`Database=47`、`VertexDeclaration=94`、`VertexElement=141`、`Texture=281`、`Image=281`。

这只确认当前 full survey 的 YABX resource skeleton；不要把它写成所有 YABX 版本的通用规范。

当前 Ras 的 14 个 YABX object payload 都已被已知字段完全覆盖：`parsedBytes=1330/1330`，`unparsedBytes=0`。Chiffon 为 14/14 full coverage，Otohime 为 12/12 full coverage。`inspect-svo` 会输出每个对象的 `parsedBytes/unparsed`，用于后续样本发现新增结构。

引用列表容器当前确认格式：

| Offset | 字段 | 类型 | 说明 |
| ---: | --- | --- | --- |
| `+0x00` | byteLength | `u32` | 后续 count 与引用 ID 的字节数，等于 `4 + count * 2`。 |
| `+0x04` | count | `u32` | 引用数量。 |
| `+0x08` | references | `u16[count]` | YABX 内部对象引用 ID。 |

字符串容器当前确认格式：

| Offset | 字段 | 类型 | 说明 |
| ---: | --- | --- | --- |
| `+0x00` | capacity | `u32` | 包含 `stringLengthWithNull:u16` 与字符串 payload 的容量。 |
| `+0x04` | stringLengthWithNull | `u16` | ASCII 字符串长度，包含末尾 `NUL`。空字符串为 0。 |
| `+0x06` | text | bytes | ASCII + `NUL`；若 capacity 大于实际字符串长度，剩余为 padding。 |

对象字段 kind 分布显示同名字段在不同类型中使用不同容器。例如 `_image` 在 `stevia::Database` 中是 `ReferenceList`，在 `stevia::Texture` 中是 `ObjectReferenceId`；因此 schema descriptor 的 raw 值必须与 owning type 一起判断，不能只按字段名全局套用。

| Field | Object field kinds |
| --- | --- |
| `_image` | `ObjectReferenceId:4`、`ReferenceList:1` |
| `_vertexDeclaration` | `ReferenceList:1` |
| `_vertexElement` | `ReferenceList:2` |
| `_name` / `_fullName` / `_userParameter` | `String` |
| `_flag`、sampler 参数、image 尺寸字段 | `Int32` |

Ras 顶层 `stevia::Database` 字段：

| Field | Value |
| --- | --- |
| object ref id | `0x2711` |
| `_vertexDeclaration` | `[0x2712, 0x2714]` |
| `_texture` | `[0x2717, 0x2719, 0x271B, 0x271D]` |
| `_image` | `[0x2718, 0x271A, 0x271C, 0x271E]` |
| `_name` / `_fullName` | `MM_CH_Ras` |

survey 还会验证 resource record 与 AVTS/DDS 的绑定：

| 校验 | surfboard | surfboard_EN |
| --- | ---: | ---: |
| resource record count == DDS count | 47/47 | 47/47 |
| `Texture._image` == paired Image reference id | 281/281 | 281/281 |
| `Image._dataSize` == AVTS directory dataLength | 281/281 | 281/281 |
| Image metadata width/height == DDS width/height | 281/281 | 281/281 |

这些校验确认了 full-survey 范围内 `(Texture, Image, DDS directory entry)` 的成对关系；`Database` 内各引用列表的运行时用途、`VertexElement` 数值枚举和 `stevia::Image._format` 仍不在这里命名。

Ras/Chiffon/Otohime 样本中 YABX object reference base 均可按 `0x2711` 解析，full survey 中 JP/EN 各 47/47 个 SVO 也得到相同 base。对当前已观察的 `Database / VertexDeclaration / VertexElement / Texture / Image` resource skeleton，`referenceId - 0x2711` 可作为静态对象 index 反查规则；referenceBase 是否对其它 YABX 版本或新增对象骨架也固定仍未确认：

```text
当前已观察 SVO 中推断关系：objectReferenceId = 0x2711 + objectIndex
```

因此 object 0 是 `0x2711`，object 13 是 `0x271E`。`_file=0` 按 null reference 处理，不参与 reference base 推断。

Ras 的 texture/image 引用关系：

| Texture obj/ref | `_name` | `_textureType` | `_image` ref | Image obj/ref | Image `_fileName` | Image `_dataSize` |
| --- | --- | --- | --- | --- | --- | ---: |
| 6 / `0x2717` | `MM_CH_Ras_000` | `base` | `0x2718` | 7 / `0x2718` | `MM_CH_Ras_000.dds` | `0x240080` |
| 8 / `0x2719` | `MM_CH_Ras_001` | `base` | `0x271A` | 9 / `0x271A` | `MM_CH_Ras_001.dds` | `0x240080` |
| 10 / `0x271B` | `MM_CH_Effect_000` | `base` | `0x271C` | 11 / `0x271C` | `MM_CH_Effect_000.dds` | `0x40080` |
| 12 / `0x271D` | `MM_CH_Ras_002` | `base` | `0x271E` | 13 / `0x271E` | `MM_CH_Ras_002.dds` | `0x240080` |

并按 YABX 对象、资源名与 AVTS 目录项关联出 4 条 resource record：

| Atlas | Texture/Image obj | File | Chunk file | Directory | YABX size | Format | Data |
| --- | --- | --- | --- | ---: | --- | --- | --- |
| `MM_CH_Ras_000` | 6 / 7 | `MM_CH_Ras_000.dds` | `__HmfToSvo__MM_CH_Ras_000.dds` | 1 | 1536x1536, `0x240080` | DXT5 / code 1 | `0x1D80/0x240080` |
| `MM_CH_Ras_001` | 8 / 9 | `MM_CH_Ras_001.dds` | `__HmfToSvo__MM_CH_Ras_001.dds` | 2 | 1536x1536, `0x240080` | DXT5 / code 1 | `0x241E00/0x240080` |
| `MM_CH_Effect_000` | 10 / 11 | `MM_CH_Effect_000.dds` | `__HmfToSvo__MM_CH_Effect_000.dds` | 3 | 512x512, `0x40080` | DXT5 / code 1 | `0x481E80/0x40080` |
| `MM_CH_Ras_002` | 12 / 13 | `MM_CH_Ras_002.dds` | `__HmfToSvo__MM_CH_Ras_002.dds` | 4 | 1536x1536, `0x240080` | DXT5 / code 1 | `0x4C1F00/0x240080` |

`stevia::Image` payload 的前 8 字节是 `height:u32`、`width:u32`。surfboard/surfboard_EN full survey 中，JP/EN 各 281 条 resource record 均满足 Image width/height 与 DDS header 一致，`_dataSize` 与 AVTS `dataLength` 一致；Ras 只是该关系的最早样本锚点。full survey 还证明 `_format = 1` 覆盖 DXT5、A8R8G8B8、DXT1、A4R4G4B4、R5G6B5、R8G8B8，因此 `_format` 只能保留为 YABX format code，不能命名为 DDS pixel format 枚举。

| Atlas | DDS | 尺寸 | 格式 | PNG crops |
| --- | --- | --- | --- | ---: |
| `MM_CH_Ras_000` | DDS at `0x1D80` | 1536x1536 | DXT5 | 136 |
| `MM_CH_Ras_001` | DDS at `0x241E00` | 1536x1536 | DXT5 | 93 |
| `MM_CH_Effect_000` | DDS at `0x481E80` | 512x512 | DXT5 | 24 |
| `MM_CH_Ras_002` | DDS at `0x4C1F00` | 1536x1536 | DXT5 | 48 |

MM_CH 7 个 SVO survey 中共 22 个 DDS texture，其中 21 个为 DXT5，1 个为 A8R8G8B8。新增 gage 样本的两个 texture 如下：

| Atlas | DDS | 尺寸 | 格式 | YABX format code |
| --- | --- | --- | --- | ---: |
| `MM_CH_gage_000_ver199_0000` | DDS at `0x1D80` | 1024x1024 | A8R8G8B8 | 1 |
| `MM_CH_gage_000_ver199_0001` | DDS at `0x401E00` | 1024x1024 | DXT5 | 1 |

解析器现在支持 full survey 已观察到的 DXT5、A8R8G8B8、DXT1、A4R4G4B4、R5G6B5、R8G8B8 解码。`extract-images` 已对 gage_front 输出 2 张 atlas PNG 和 89 个 crop PNG；对 UI Announce 输出 6 张 atlas PNG / 88 个 crop PNG；对 UI ResultInfo 输出 8 张 atlas PNG / 53 个 crop PNG。

## 与 sbscene 的对应关系

`.sbscene` 尾部的 `TEXL/TEX/CROP` 描述 atlas 和裁剪表：

- `TEXL` 名称为 `MM_CH_Ras`，texture 数量为 4。
- `TEX` 名称依次为 `MM_CH_Ras_000`、`MM_CH_Ras_001`、`MM_CH_Effect_000`、`MM_CH_Ras_002`。
- `TEX.0x40/0x41` 是 width/height。
- `TEX.0x62` 是 raw packed state word；已核对 decoder/xref 只确认其 `0xF0`、`0xF00` 位段会进入 shared decoder，早期三样本中出现值为 `0` 或 `272`。full survey 中 JP/EN atlas 只见 `0x110` 与 `0x0`，分布为 JP `0x110:2625, 0x0:281`、EN `0x110:2638, 0x0:284`；具体 sampler/layout 语义仍未确认。
- `TEX.0x63` 是 crop count。
- `CROP.0x65` 是 compact int16-vector record；当前 selector `2` 表示后续 4 个 int16 crop 坐标。
- `CIMG/CREF` 将 cast image 关联到 texture/crop。

## Image Cast 映射

Ras 样本解析出：

| 项 | 数量 |
| --- | ---: |
| `CIMG` image cast | 304 |
| `CREF` block | 311 |
| `CREF` crop reference record | 350 |
| primary CREF record | 338 |
| secondary CREF record | 12 |
| 能映射到 `NODE` 名称的 image cast | 304 |
| 多 crop 引用 image cast | 35 |
| secondary CREF image cast | 12 |

`CIMG` 字段：

| 字段 | 说明 |
| --- | --- |
| `0x48` | CIMG 的 raw packed state word；与 `CNUM.0x48`、`CSLI.0x80`、`TEX.0x62`、`SLIC.0x83` 等在已核对 decoder 中共享部分 packed-state 拆分路径。 |
| `0x51` | cast/node index。 |
| `0x40` | width。 |
| `0x41` | height。 |
| `0x42` | pivot X。 |
| `0x43` | pivot Y。 |
| `0x44` | 两个 `u16` 值：primary CREF 组记录数、secondary CREF 组记录数。full survey 中 7063/7063 与 7228/7228 个 image cast 与后续 CREF 组记录数完全匹配。 |
| `0x45` | 两个 `u16` raw index 值：分别对应 primary/secondary CREF 组。full survey 中 7315/7315 与 7454/7454 个非空 CREF 组内均未越界；默认/当前引用选择语义未确认。 |

`CIMG.0x44` 分布：

| Tuple | Image casts |
| --- | ---: |
| `(1,0)` | 264 |
| `(2,0)` | 16 |
| `(3,0)` | 6 |
| `(1,1)` | 6 |
| `(0,0)` | 5 |
| `(2,1)` | 5 |
| `(4,1)` | 1 |
| `(4,0)` | 1 |

`CIMG.0x45` primary/secondary 组内 raw index 分布：

| Tuple | Image casts |
| --- | ---: |
| `(0,0)` | 296 |
| `(1,0)` | 6 |
| `(2,0)` | 2 |

`0x45` 的值作为 primary/secondary CREF group 内的 raw index 输出；默认、当前或选中引用等更高层语义仍未确认。三角色样本阶段所有 `0x45` 都落在对应非空 CREF 组范围内，没有 out-of-range；其中非零 index image cast 合计 16 个。Ras 的非零样例如下：

| Node | 0x44 | 0x45 | Primary indexed CREF | Secondary indexed CREF |
| --- | --- | --- | --- | --- |
| `koukando02_mouth` | `(2,0)` | `(1,0)` | `1 -> 0:27` |  |
| `do_mouth` | `(3,0)` | `(1,0)` | `1 -> 0:131` |  |
| `kirakira_mouth` | `(3,0)` | `(1,0)` | `1 -> 0:28` |  |
| `kirakira_eye` | `(4,1)` | `(2,0)` | `2 -> 0:47` | `0 -> 0:42` |
| `hawawa_eye` | `(2,1)` | `(1,0)` | `1 -> 0:45` | `0 -> 0:42` |
| `hawawa_mouth` | `(4,0)` | `(1,0)` | `1 -> 0:24` |  |
| `forearm_L02` | `(3,0)` | `(2,0)` | `2 -> 3:43` |  |
| `hand_L02` | `(2,0)` | `(1,0)` | `1 -> 0:115` |  |

full survey 现在输出 `Cimg45GroupIndexCounts`、`Cimg45GroupCountIndexCounts`、`Cimg45NonZeroGroupCounts` 和 `Cimg45NonZeroSamples`。JP 非零 group 为 131（primary 127、secondary 4），EN 为 124（primary 120、secondary 4）；out-of-range 与 empty group non-zero 均为 0。JP primary index 分布为 `0=6936, 1=86, 2=11, 3=14, 5=5, 6=6, 9=4, 64=1`，secondary 为 `0=7059, 1=4`；EN primary 为 `0=7108, 1=83, 2=8, 3=14, 5=5, 6=6, 9=4`，secondary 为 `0=7224, 1=4`。JP 独有的 `primary|70|64` 定位到 `MM_UI_ButtonCounter__MM_UI_Common_Switch.sbscene` 的 `text`，indexed raw CREF 为 `01000000001D00`；secondary 非零 4/4 定位到 `MM_UI_MusicSelectFinaleCourseAccept` 的 `BG_Black1/BG_Black2`，index 均为 1。这些只确认 raw index 分布和样本定位，不确认运行时选择规则。

动画侧 full survey 现在还输出 `ImageVariantGroup*` 分组范围校验。`18 primary` 在 JP/EN 为 1,791/1,792 条 track、6,986/6,988 个 key，全部落在目标 CIMG primary CREF 组范围内；`19 secondary` 在 JP/EN 均为 333 条 track、9,769 个 key，全部落在 secondary CREF 组范围内。`ImageVariantGroupCimg45FirstKey*` 进一步显示 `0x45` 与最早 key 经常一致但不是全覆盖：JP primary 1533 match / 258 mismatch、secondary 332 match / 1 mismatch；EN primary 1534 match / 257 mismatch / 1 multi-CIMG target、secondary 332 match / 1 mismatch。该结果只确认 track value、primary/secondary CREF 组和 `0x45` 静态 index 的结构关系，不确认 `CIMG.0x45` 在播放过程中的默认/当前/选中角色。

`CIMG.0x48` 已拆到 bit 分布，但它不是 CIMG 私有 flags。已核对的 loader/xref 将其作为 shared packed state word 进入 decoder；同一组低位/中位 decoder 也服务于 `CNUM`、`CSLI`、`TEX` 和 `SLIC` 的 raw state 字段。Ras 中 CIMG 只出现 bit 0、15、20、21、22、23：

| Bit | Mask | Image casts | Ras 局部观察 |
| ---: | --- | ---: | --- |
| 0 | `0x00000001` | 9 | 只出现在 `04_present_eff`、`*_add` 和 `hart_*` 节点，是 bit 22 的子集；这是节点命名/分组相关性，不确认混合或渲染语义。 |
| 15 | `0x00008000` | 304 | Ras 样本中全部 CIMG 设置；full survey 中为高覆盖候选，但 JP/EN 仍有 24/37 个 CIMG 不带该 bit，不确认 bit 语义。 |
| 20 | `0x00100000` | 121 | 117/121 个与 bit 23 共现，另有少量 tail/wing/ribbon 特例。 |
| 21 | `0x00200000` | 6 | Ras 中只覆盖少量 tail/gorgeous/plain 特例。 |
| 22 | `0x00400000` | 182 | 常见于 expression/body/effect 节点。 |
| 23 | `0x00800000` | 117 | Ras 中是 bit 20 的子集，常见于 hair/clothes/body 节点；后续 Otohime/full survey 已证明不能写成全局子集规则。 |

主 Markdown/inspect 现在同时输出 packed state bits、full-value 交叉统计和 bit 共现表；legacy JSON 字段名仍是 `imageCastFlagBits`。Ras 中 `0+22` 共现 9 次，说明 bit 0 是 bit 22 的小子集；`20+23` 共现 117 次，说明 bit 23 在 Ras 内完全落在 bit 20 内。bit 0 与 multi/secondary crop、非零 `0x45` index 没有交叉，因此它不属于资源选择索引字段；相关节点名只作为样本定位，不写成结构语义。

full survey 将 `CIMG.0x48` observed bits 扩展为 `0/1/4/5/6/7/8/9/11/12/13/15/20/21/22/23`。bit 15 仍是高覆盖率 CIMG 位，但不能写成全局必有或 image-cast marker；具体 bit 业务语义仍需渲染状态消费链验证。

survey JSON 现在还会输出 `CimgFlagBitDisplayFalseCounts`、`CimgFlagBitMultiReferenceCounts`、`CimgFlagBitSecondaryReferenceCounts`、`CimgFlagBitNonZeroReferenceIndexCounts`、`CimgFlagBitNodeFlagCounts`、`CimgFlagBitGroupCounts` 和 `CimgFlagBitPairCounts`。full survey 中 `0+22` 共现为 JP/EN 1,624/1,639，`20+23` 共现为 559/567；bit 15 覆盖 7,039/7,191 个 CIMG，但 JP/EN 仍有 24/37 个 CIMG 不带 bit 15。非零 `0x45` index 与 bit 的交叉只覆盖少量记录，因此 `CIMG.0x48` bit 不应命名为资源选择索引字段。

正式 survey 还新增统一的 `SharedPackedStateOwner*` 聚合，用同一口径覆盖 `CIMG.0x48`、`CNUM.0x48`、`CSLI.0x80`、`LAYR.0x20`、`SLIC.0x83` 和 `TEX.0x62`。JP/EN owner 总数分别为 `7063/7228`、`217/217`、`26/26`、`314/322`、`108/108`、`2906/2922`；`CNUM.0x48` 恒为 `0x8000`，`CSLI.0x80` 只见 `0x8000/0x8001/0x8002`，`SLIC.0x83` 只见 `0..3`。这些输出是 shared packed layout 的复算入口，不提升为具体渲染或采样语义。

`CIMG.0x48` full value 仍保留输出，用于以后跨样本比较组合关系；bit 拆分只由 loader/decoder xref 收窄，业务语义仍是候选。

12 个 secondary CREF cast 主要集中在 eye/hair_front 节点，例如 `kirakira_eye` 的 `0x44=(4,1)`、`0x45=(2,0)`，primary refs 为 `0:43,0:46,0:47,0:39`，secondary ref 为 `0:42`。

`CREF.0x49` 是 compact int16-vector record。当前观测 payload 为 7 字节：1 字节 component-count selector 加 3 个 int16。已核对 loader 函数 `sub_88F380` 将这 3 个 int16 保存为 `(textureListIndex, textureIndex, cropIndex)`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| componentCountSelector | `u8` | Ras/Chiffon/Otohime 中观察到的 raw value 均为 `1`（350+225+418=993 条）；full survey 中 CIMG/CNUM/CSLI 全 owner 也均为 `1`（JP 19,597、EN 19,734 条）。`1` 映射到 3 个 int16，不是业务 kind/enum。 |
| textureListIndex | `int16 raw` | texture list index；full survey 中 EN 为 `0` x19,734，JP 为 `0` x19,595，另有 2 条 raw `0xFFFF` / `65535` 特例。 |
| textureIndex | `int16 raw` | atlas index。 |
| cropIndex | `int16 raw` | atlas 内 crop index。 |

`CropReference*` full survey 现在覆盖所有 CREF owner，而不只统计 CIMG 后续 CREF。JP/EN owner 分布为：CIMG `17,069/17,206`，CNUM `2,422/2,422`，CSLI `106/106`。EN 的 19,734 条 CREF 全部 texture/crop index in range；JP 有 19,595 条 in range，另有 2 条 `textureListIndex=65535`、texture index out-of-range、crop index missing-texture 的 raw 特例。`CropReferenceOutOfRangeOwnerCounts` 显示这 2 条均为 CIMG owner；sample 定位为 `MM_UI_Common__MM_UI_Common_Ranking_ADV.sbscene` 的 `ranking_base` 和 `MM_UI_SimpleOption__Reference_TitleFrame_Small_ALL.sbscene` 的 `hard_text`，raw 均为 `01FFFFFFFFFFFF`。该 `65535` 只记录为 raw 特例，不命名 sentinel/空引用语义。

`manifest.json` 和主 `dump` 的 `surfboard.resources.imageCasts` 会输出 `nodeName`、`primaryCropReferenceCount`、`secondaryCropReferenceCount`、`primaryCropReferenceIndex`、`secondaryCropReferenceIndex`、`primaryCropReferences`、`secondaryCropReferences` 和兼容用的合并 `cropReferences`，例如 `04_present_eff -> crops/002_MM_CH_Effect_000/022.png`。

## 待实现

- YABX 对象 payload 的泛化解析边界目前限定在当前 resource skeleton；surfboard/surfboard_EN full survey 中 47 + 47 个 SVO 的 `Database`、`VertexDeclaration`、`VertexElement`、`Texture`、`Image` 已字段级完整覆盖，未剩 unparsed bytes，并且当前观察到的 object/resource skeleton 全部匹配。其它 YABX 版本或新增对象类型仍需另行验证。
- YABX 内部引用 ID 到对象 index 的规则在当前 full survey 的 resource skeleton 内已可按 `referenceBase=0x2711` 静态反查；`Texture._image` 到 paired Image reference 的绑定在当前 281+281 条 resource record 中全部匹配。待确认的是其它 YABX 版本或新增对象骨架是否继续使用同一 base/对象顺序。
- AVTS header `0x08..0x7F` 的 unknown word 语义。
- YABX 字段描述符 3 字节的准确命名；当前 `OwnerDependent`、`ObjectReference`、`Int32Like` 是候选标签，raw descriptor 会保留。
- `stevia::Image._format` 的枚举语义；已确认 code `1` 覆盖 DXT5、A8R8G8B8、DXT1、A4R4G4B4、R5G6B5、R8G8B8，不能直接等同 DDS pixel format。
- `.svo` 重新打包/写回。

## CLI 切图

```powershell
dotnet run --project src/SbScene.Cli -- extract-images `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras__Ras_00.sbscene" `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras.svo" `
  --out out\ras-svo-extract
```

检查 SVO 目录：

```powershell
dotnet run --project src/SbScene.Cli -- inspect-svo `
  "D:\maimai FiNALE (SDEY 1.99.00)\maimai\data\surfboard\MM_CH_Ras\MM_CH_Ras.svo"
```

输出结构：

```text
out/ras-svo-extract/
  manifest.json
  atlases/
    000_MM_CH_Ras_000.png
    001_MM_CH_Ras_001.png
    002_MM_CH_Effect_000.png
    003_MM_CH_Ras_002.png
  crops/
    000_MM_CH_Ras_000/*.png
    001_MM_CH_Ras_001/*.png
    002_MM_CH_Effect_000/*.png
    003_MM_CH_Ras_002/*.png
```

`CROP.0x65` 同样是 compact int16-vector record。当前观测 payload 为 9 字节：1 字节 component-count selector 加 4 个 signed int16。已核对 loader 函数 `sub_88C110` 读取 4 个坐标后按 TEX width/height 归一化为 4 个 float：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| componentCountSelector | `u8` | Ras/Chiffon/Otohime 中观察到的 raw value 均为 `2`（301+205+348=854 条）；full survey 中 JP/EN 也均为 `2`（79,801 与 80,217 条）。`2` 映射到 4 个 int16，不是业务 kind/enum。 |
| left | `i16` | 左边界。 |
| top | `i16` | 上边界。 |
| right | `i16` | 右边界，exclusive。 |
| bottom | `i16` | 下边界，exclusive。 |

full survey 中所有 TEX atlas 的 declared crop count 都等于实际 CROP 数（JP 2,906/2,906，EN 2,922/2,922），所有 crop width/height 都为正数。部分 crop 会越过 atlas 边界：JP 259 条，EN 191 条；survey 现在输出 `CropRectOutOfAtlasBoundsReasonCounts` 与逐场景 sample。原因分布为 JP `right>width=104, left<0=51, right>width+bottom>height=46, left<0+top<0+right>width+bottom>height=34, top<0=15, bottom>height=6, top<0+right>width+bottom>height=3`；EN `right>width=83, right>width+bottom>height=47, left<0+top<0+right>width+bottom>height=36, top<0=15, bottom>height=6, top<0+right>width+bottom>height=3, left<0=1`。导出器会生成完整 crop 尺寸，并用透明像素补齐越界区域。越界只确认坐标关系，不命名运行时采样或裁剪规则。
