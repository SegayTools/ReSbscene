# game::DebugConfig 与 GrooveMaster.ini 逆向记录

本文记录当前 IDA 数据库中可见的 `GrooveMaster.ini` 读取流程、解析规则、注册项和运行时作用。结论按反编译、反汇编和交叉引用整理；仅按 key 名推断、但未看到明确读取路径的内容标为“仅注册 / 待证”。

当前仓库未找到实际的 `GrooveMaster.ini` 样本文件；以下“读取什么”按程序注册的 key 和默认值整理。

## 读取入口

| 项 | 地址 / 值 | 说明 |
| --- | --- | --- |
| 单例入口 | `sub_4054E0` | 第一次调用分配 `0x60` 字节并调用 `sub_405630` 构造 `game::DebugConfig`。参数非零时会释放旧对象并重新构造。 |
| 初始化函数 | `sub_405630` | 注册 `120` 个配置项，然后尝试读取 `GrooveMaster.ini`。 |
| 文件路径 | `0x00ABCD44` / `L"GrooveMaster.ini"` | `_wfopen_s(&Stream, aGr, L"rt")` 使用的宽字符串路径；相对当前工作目录。 |
| 读取函数 | `sub_507200` | 将 `FILE*` 包装为 `std::wifstream`，逐行 `std::getline<wchar_t>`，再调用 `util::Config` 的行解析虚函数。 |
| 行解析 | `sub_404EB0` | 提取 key/value 后调用 `sub_6A8440`，按 key 写入匹配的 `ConfigParam`。 |

打开文件失败不致命：`wfopen_s` 返回错误时不会调用 `sub_507200`，所有项保留注册默认值。

## 解析规则

`DebugConfig` 复用 `util::Config` parser，但它是独立于 `SystemConfig.txt` 的另一套配置对象。`sub_405630` 中调用 `sub_6A7C20(this, 0)`，因此本配置使用 parser mode `0`：

- 每行按宽字符串读取。
- 空行直接忽略。
- 普通有效格式是 `KEY value` 或 `KEY<TAB>value`。
- 只用首个空格或 Tab 拆分 key/value；key 和 value 会裁剪首尾空格、Tab。
- key 使用 `wcscmp` 精确匹配，大小写敏感。
- 未知 key 被忽略。
- 没有看到专门的注释语法；整行以 `#`、`;` 等开头只会因为 key 不匹配而被忽略，行内注释不会被统一剥离。
- 重复 key 会再次写入同一个 `ConfigParam`，后面的值覆盖前面的值。
- parser 中还有处理 `<...>` / `>` / `<` 的分支，但当前注册 key 本身不含这些字符；可确认的实用格式仍是空格/Tab 分隔。
- key/value 临时缓冲区各为 `256` 个 `wchar_t`，超长内容会被截断后再匹配或写入。
- 代码层面是 `_wfopen_s(..., L"rt")` 加 `std::wifstream` / `std::getline<wchar_t>`；这不能单独证明磁盘文件必须是 UTF-16。当前 key 和数值都在 ASCII 范围。

类型注册 helper 与转换规则：

| helper | 类型 | 默认值来源 / 解析 |
| --- | --- | --- |
| `sub_6A7D50` | int / bool | 当前值和默认值均为 32 位整数；解析时 `ConfigParamInt::parse` (`sub_6A6F30`) 调 `_wtoi(value)`。 |
| `sub_6A7DB0` | float | 当前值和默认值均为 float；解析时使用 `_wtof(value)`。 |
| `sub_6A7E10` | wstring | 当前值和默认值均为 `std::wstring`；缺 value 时清空。 |
| `sub_6A7EE0` | 64 位整数 | 当前值和默认值初始化为 `-1`；解析时使用 `_wtoi64(value)`。 |

读取 wrapper 与直接访问：

| 函数 | 作用 |
| --- | --- |
| `sub_6A82C0` | 取 int 值，缺项返回 `0`。 |
| `sub_6A8330` | 取 bool / truthy 值，缺项返回 `0`。 |
| `sub_6A82F0` | 取 float 值，缺项返回 `0.0`。 |
| `sub_6A8370` | 取 wstring，缺项返回空字符串。 |
| `sub_6A83C0` | 取 64 位整数。 |
| `sub_6A81B0` | 按 ID 强制写 int/bool 值，并标记该项已修改。 |

## 启动后覆盖

`GrooveMaster.ini` 读完后，`sub_405630` 还会按全局调试标志强制写入部分项：

| 条件 | 写入 |
| --- | --- |
| `byte_F416AD != 0` | `DEBUG_MENU` (`ID 50`) 写为 `1`。 |
| `byte_F416AE != 0` | `DEV` (`ID 0`) 写为 `1`。 |
| `byte_F416AF != 0` | `SET_AGING` (`ID 49`) 写为 `1`。 |
| `DEV` 与 `SET_SPECIAL_VIEWER_PLAY` 都为真 | `SET_OFFLINE_MODE` (`ID 26`) 与 `SET_VIEWER_PLAY` (`ID 111`) 写为 `1`。 |

同一函数随后会读取 `ROTATE` (`ID 11`)、交换 `SystemConfig +0x4C/+0x50` 两个固定宽高字段，再读取 `DEV` (`ID 0`) 和 `SET_SPECIAL_VIEWER_PLAY` (`ID 112`) 判断是否执行上表最后一条覆盖。

## 运行时作用概览

以下是可见代码中证据较明确的分组。未列为“确认”的 key 仍然可能被数据表驱动或旧代码路径读取，但本轮没有从 `sub_4054E0` 查询路径确认。

| 分组 | ID / key | 可确认行为 |
| --- | --- | --- |
| 启动 / 设备调试 | `0..14` 中多项 | `sub_40BE20`、`WinMain`、JVS/串口/USB/联网相关路径大量读取 `DEV`、`NO_RING`、`NO_JVS`、`NO_SERIAL`、`NO_REBOOT`、`VIRTUAL_AIME`、`USB_DL_DISABLE`、`NO_LINKCHECK` 等。 |
| 截图 / capture | `14`, `16..20` | `sub_40BE20` 读取 capture 参数，`sub_40CC30` 读取 `SCREENSHOT` 并进入截图/调试显示相关路径。`CAPTURE_X0` (`ID 15`) 本轮未看到明确读取。 |
| 运营覆盖 | `21..30`, `32`, `33`, `42` | `sub_4FF140` 读取这些项；默认 `-1` 时保留硬件/全局默认，非负值写入运行时运营状态字段。 |
| 地区 | `39 SET_REGION` | `sub_4FF140` 读取字符串；确认比较值为 `JP` 与 `EX`，用于覆盖运行时地区字段。 |
| 结果阈值 | `69..80 GO_RESULT_*` | `sub_435A90` 按结果段读取四组 float 阈值，并把所选阈值传给 `sub_435FE0`。`*_SYNC` 项通过运行时变量 ID 读取。 |
| Pandora | `81..94 PANDORA_*` | `sub_508630` 在 `DEV` 为真时按玩家 (`1P/2P`) 与版本段 `0..6` 选择 ID，读取 64 位 bit mask 并覆盖 Pandora 状态位。 |
| 调试显示 | `95..99` | `DEV_STRING`、`DISP_NETSTAT`、`PERFORMANCE_POS`、`RESOURCE_TEXT`、`ITEM_VIEW` 被 UI/渲染/调试显示路径读取。注意 `DEV_STRING` 注册类型是 int，不是字符串。 |
| CTRACK | `102 CTRACK_DAYS` | `sub_4C2840`、`sub_4C3160`、`sub_478700`、`sub_478820`、`sub_4C29F0`、`sub_4C2F40` 等读取；值在 `1..14` 时进入专门分支，否则走默认流程。 |
| 压力 / silent / viewer | `105`, `108..113` | `STRESS_CHECK` 由 `sub_432440`、`sub_433600` 读取；`PREV_FFA_FLAME` / `PREV_SKIP_FLAME` 由 `sub_4AB690` 读取；`SET_SILENT_MODE` 由 `sub_4AB900` 读取；`SET_VIEWER_PLAY` / `SET_SPECIAL_VIEWER_PLAY` / `SET_NO_SDT` 分别在 viewer 与资源路径中读取。 |
| Timing / note debug | `115..120` | `sub_4FD010` 先要求 `DEV` 为真，再按 note/动作类型 `0..5` 映射到 `TIMING_TAP`、`TIMING_HOLDON`、`TIMING_HOLDOFF`、`TIMING_SLIDE`、`ACHIEVE_SLIDE`、`DISPLAY_TAP`；对应项为真时写入一条调试事件。 |

## 配置项

`ID 114` 没有注册。表中“证据”列只写当前确认层级：`确认读取` 表示已看到 `DebugConfig` 查询路径；`仅注册` 表示当前只确认注册和默认值。

| ID | key | 类型 | 默认值 | 证据 / 主要作用 |
| ---: | --- | --- | ---: | --- |
| `0` | `DEV` | int | `0` | 确认读取。全局调试开关；启动后可被 `byte_F416AE` 强制置 `1`，并控制 Pandora、timing debug、viewer 覆盖等路径。 |
| `1` | `SET_FREEPLAY` | int | `0` | 确认读取。`sub_570F50`、`sub_571410` 读取；具体业务语义待证。 |
| `2` | `NO_RING` | int | `0` | 确认读取。多处硬件 / ring 相关路径读取。 |
| `3` | `NO_JVS` | int | `0` | 确认读取。JVS / 输入设备相关路径读取。 |
| `4` | `NO_SERIAL` | int | `0` | 确认读取。`WinMain`、`sub_522BA0`、`sub_52A4C0` 等串口/路径相关分支读取。 |
| `5` | `NO_REBOOT` | int | `0` | 确认读取。`sub_41F150` 等重启/流程分支读取。 |
| `6` | `VIRTUAL_AIME` | int | `0` | 确认读取。`sub_40BE20`、`sub_55D700` 读取。 |
| `7` | `USB_DL_DISABLE` | int | `0` | 确认读取。`sub_40BE20` 等 USB download 分支读取。 |
| `8` | `NO_LINKCHECK` | int | `0` | 确认读取。`sub_421EF0` 等联网检查分支读取。 |
| `9` | `NO_DELIVER` | int | `0` | 确认读取。投递/下载相关分支读取。 |
| `10` | `NO_RESTRICT` | int | `0` | 确认读取。限制检查相关 helper 读取。 |
| `11` | `ROTATE` | int | `0` | 确认读取。`sub_405630` 和 `sub_40BE20` 启动阶段读取。 |
| `12` | `1P_ONLY` | int | `0` | 确认读取。`sub_40BE20` 启动模式参数。 |
| `13` | `NO_WAIT` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `14` | `SCREENSHOT` | int | `0` | 确认读取。`sub_40BE20`、`sub_40CC30` 截图 / 调试显示路径读取。 |
| `15` | `CAPTURE_X0` | int | `0` | 仅注册；capture 组中本轮只确认 `16..20` 被 `sub_40BE20` 读取。 |
| `16` | `CAPTURE_X1` | int | `0` | 确认读取。`sub_40BE20` capture 参数。 |
| `17` | `CAPTURE_Y0` | int | `0` | 确认读取。`sub_40BE20` capture 参数。 |
| `18` | `CAPTURE_Y1` | int | `0` | 确认读取。`sub_40BE20` capture 参数。 |
| `19` | `CAPTURE_W` | int | `0` | 确认读取。`sub_40BE20` capture 参数。 |
| `20` | `CAPTURE_H` | int | `0` | 确认读取。`sub_40BE20` capture 参数。 |
| `21` | `SET_TOTAL_MACHINE` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `22` | `SET_LINK_ID` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `23` | `SET_TRACKS_1P` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `24` | `SET_TRACKS_MULTI` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `25` | `SET_TRACKS_EVENT` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `26` | `SET_OFFLINE_MODE` | int | `-1` | 确认读取 / 写入。`sub_4FF140` 读取；`DEV` 与 `SET_SPECIAL_VIEWER_PLAY` 同真时由 `sub_405630` 强制写 `1`。 |
| `27` | `SET_EVENT_MODE` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `28` | `SET_ADVERTISE_MODE` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `29` | `SET_ADVERTISE_SOUND` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `30` | `SET_CAMERA_POSITION` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `31` | `SET_FRIEND_TEST` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `32` | `SET_CLOSE_HOUR` | int | `-1` | 确认读取。`sub_4FF140` 读取；与 `SET_CLOSE_MINUTE` 一起覆盖关闭时间字段。 |
| `33` | `SET_CLOSE_MINUTE` | int | `-1` | 确认读取。`sub_4FF140` 读取；与 `SET_CLOSE_HOUR` 一起覆盖关闭时间字段。 |
| `34` | `SET_DEBUG_MODE` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `35` | `SET_ALL_OPEN` | int | `-1` | 确认读取。多处解锁 / 开放内容判断读取。 |
| `36` | `SET_OPEN_SECRET` | int | `-1` | 确认读取。秘密内容开放判断读取。 |
| `37` | `SET_OPEN_EVENT` | int | `-1` | 确认读取。活动内容开放判断读取。 |
| `38` | `SET_AIME_SELECT` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `39` | `SET_REGION` | wstring | `""` | 确认读取。`sub_4FF140` 比较 `JP` / `EX` 并覆盖地区字段。 |
| `40` | `SET_CLOCK_DATE` | int | `-1` | 确认读取。`sub_412120` 时钟覆盖路径读取。 |
| `41` | `SET_CLOCK_BOOST` | int | `-1` | 确认读取。`sub_412120` 时钟加速路径读取。 |
| `42` | `SET_DRESS_CODE` | int | `-1` | 确认读取。`sub_4FF140` 非负值写入运营状态字段。 |
| `43` | `GO_CAMERA_UPLOAD` | int | `0` | 确认读取。相机上传跳转 / gating 相关路径读取。 |
| `44` | `GO_COLLECTION` | int | `0` | 确认读取。Collection 跳转 / gating 相关路径读取。 |
| `45` | `SET_AUTO_PLAY` | int | `-1` | 确认读取。自动播放相关路径读取。 |
| `46` | `SET_ARAYA_SPEED` | int | `-1` | 确认读取。`sub_4F7620`、`sub_4F8870` 读取；具体语义待证。 |
| `47` | `LIVE_COMMENT` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `48` | `SPEAK_SPEAKER` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `49` | `SET_AGING` | int | `-1` | 确认读取 / 写入。多处 aging / 随机流程读取；启动后可被 `byte_F416AF` 强制写 `1`。 |
| `50` | `DEBUG_MENU` | int | `-1` | 确认读取 / 写入。debug menu 路径读取；启动后可被 `byte_F416AD` 强制写 `1`。 |
| `51` | `DEBUG_CARD` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `52` | `QUICK_BOOT` | int | `0` | 确认读取。`sub_421170` 快速启动路径读取。 |
| `53` | `VIEW_COURSE` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `54` | `SET_ICON` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `55` | `SET_TITLE` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `56` | `SET_PLATE` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `57` | `SET_FRAME` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `58` | `SET_CHALLENGE` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `59` | `SET_OP_SPEED` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `60` | `SET_OP_ANSWER` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `61` | `SET_OP_SE` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `62` | `SET_OP_BGINFO` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `63` | `SET_OP_STAR_ROT` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `64` | `SET_OP_MIRROR` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `65` | `SET_OP_MOVIEBRIGHT` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `66` | `SET_JUDGE_DISP` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `67` | `SET_AIMEID_1P` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `68` | `SET_AIMEID_2P` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `69` | `GO_RESULT_1_1PACV` | float | `50.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `70` | `GO_RESULT_1_2PACV` | float | `50.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `71` | `GO_RESULT_1_SYNC` | float | `50.0` | 确认读取。`sub_435A90` 通过变量 ID 读取的 sync 阈值。 |
| `72` | `GO_RESULT_2_1PACV` | float | `80.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `73` | `GO_RESULT_2_2PACV` | float | `80.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `74` | `GO_RESULT_2_SYNC` | float | `80.0` | 确认读取。`sub_435A90` 通过变量 ID 读取的 sync 阈值。 |
| `75` | `GO_RESULT_3_1PACV` | float | `97.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `76` | `GO_RESULT_3_2PACV` | float | `97.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `77` | `GO_RESULT_3_SYNC` | float | `99.0` | 确认读取。`sub_435A90` 通过变量 ID 读取的 sync 阈值。 |
| `78` | `GO_RESULT_4_1PACV` | float | `100.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `79` | `GO_RESULT_4_2PACV` | float | `100.0` | 确认读取。`sub_435A90` 结果阈值组。 |
| `80` | `GO_RESULT_4_SYNC` | float | `100.0` | 确认读取。`sub_435A90` 通过变量 ID 读取的 sync 阈值。 |
| `81` | `PANDORA_GREEN_1P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 1P 版本段 `0` bit mask。 |
| `82` | `PANDORA_MAIMAI_1P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 1P 版本段 `1` bit mask。 |
| `83` | `PANDORA_PINK_1P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 1P 版本段 `2` bit mask。 |
| `84` | `PANDORA_MURASAKI_1P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 1P 版本段 `3` bit mask。 |
| `85` | `PANDORA_MILK_1P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 1P 版本段 `4` bit mask。 |
| `86` | `PANDORA_ORANGE_1P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 1P 版本段 `5` bit mask。 |
| `87` | `PANDORA_FINALE_1P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 1P 版本段 `6` bit mask。 |
| `88` | `PANDORA_GREEN_2P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 2P 版本段 `0` bit mask。 |
| `89` | `PANDORA_MAIMAI_2P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 2P 版本段 `1` bit mask。 |
| `90` | `PANDORA_PINK_2P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 2P 版本段 `2` bit mask。 |
| `91` | `PANDORA_MURASAKI_2P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 2P 版本段 `3` bit mask。 |
| `92` | `PANDORA_MILK_2P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 2P 版本段 `4` bit mask。 |
| `93` | `PANDORA_ORANGE_2P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 2P 版本段 `5` bit mask。 |
| `94` | `PANDORA_FINALE_2P` | int64 | `-1` | 确认读取。`sub_508630` Pandora 2P 版本段 `6` bit mask。 |
| `95` | `DEV_STRING` | int | `-1` | 确认读取。调试文字 / UI 显示路径读取；注册类型为 int。 |
| `96` | `DISP_NETSTAT` | int | `-1` | 确认读取。网络状态显示路径读取。 |
| `97` | `PERFORMANCE_POS` | int | `-1` | 确认读取。`sub_40BE20` performance/debug 显示位置参数。 |
| `98` | `RESOURCE_TEXT` | int | `-1` | 确认读取。资源文本显示路径读取。 |
| `99` | `ITEM_VIEW` | int | `0` | 确认读取。`sub_5FED90` item view 分支读取。 |
| `100` | `HOME_RANKER_USERID` | wstring | `"0:0:0"` | 仅注册；本轮未确认运行时读取。 |
| `101` | `HOME_RANKER_3RD_RATEX100` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `102` | `CTRACK_DAYS` | int | `0` | 确认读取。值在 `1..14` 时影响 CTRACK / 课程相关分支。 |
| `103` | `DEBUG_COL_PLAYCOUNT` | int | `-1` | 仅注册；本轮未确认运行时读取。 |
| `104` | `EVENT_INFO_CHECK` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `105` | `STRESS_CHECK` | int | `0` | 确认读取。`sub_432440`、`sub_433600` 读取。 |
| `106` | `SUPERVISION` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `107` | `OLD_SERVER_TRANS_DISABLE` | int | `0` | 仅注册；本轮未确认运行时读取。 |
| `108` | `PREV_FFA_FLAME` | int | `10` | 确认读取。`sub_4AB690` 写入预览 / flame 相关字段。 |
| `109` | `PREV_SKIP_FLAME` | int | `60` | 确认读取。`sub_4AB690` 写入预览 / skip flame 相关字段。 |
| `110` | `SET_SILENT_MODE` | int | `0` | 确认读取。`sub_4AB900` silent mode 分支读取。 |
| `111` | `SET_VIEWER_PLAY` | int | `0` | 确认读取 / 写入。viewer 相关路径读取；`DEV` 与 `SET_SPECIAL_VIEWER_PLAY` 同真时强制写 `1`。 |
| `112` | `SET_SPECIAL_VIEWER_PLAY` | int | `0` | 确认读取。`sub_405630`、`sub_421560`、`sub_421EF0`、`sub_4AB4E0` 等读取。 |
| `113` | `SET_NO_SDT` | int | `0` | 确认读取。`sub_522BA0`、`sub_52A4C0` 资源 / SDT 路径读取。 |
| `114` | 未注册 | - | - | `sub_405630` 跳过该 ID。 |
| `115` | `TIMING_TAP` | int | `0` | 确认读取。`sub_4FD010` 在 `DEV` 为真且 note 类型为 `0` 时读取。 |
| `116` | `TIMING_HOLDON` | int | `0` | 确认读取。`sub_4FD010` 在 `DEV` 为真且 note 类型为 `1` 时读取。 |
| `117` | `TIMING_HOLDOFF` | int | `0` | 确认读取。`sub_4FD010` 在 `DEV` 为真且 note 类型为 `2` 时读取。 |
| `118` | `TIMING_SLIDE` | int | `0` | 确认读取。`sub_4FD010` 在 `DEV` 为真且 note 类型为 `3` 时读取。 |
| `119` | `ACHIEVE_SLIDE` | int | `0` | 确认读取。`sub_4FD010` 在 `DEV` 为真且 note 类型为 `4` 时读取。 |
| `120` | `DISPLAY_TAP` | int | `0` | 确认读取。`sub_4FD010` 在 `DEV` 为真且 note 类型为 `5` 时读取。 |

## 示例格式

```text
DEV 1
SET_REGION JP
GO_RESULT_1_1PACV 50.0
PANDORA_GREEN_1P -1
TIMING_TAP 1
```

## 待验证点

- `SET_*` 外观、自定义、option 系列中多项只确认注册，未在当前 `sub_4054E0` 查询扫描中确认读取；可能是旧项、占位项，或通过尚未归约的数据表间接访问。
- `PANDORA_*` 是 64 位项，默认值确认是 `-1`。`sub_508630` 的高/低 32 位传播在反编译中显示不够直观，若要写出精确 bit 语义，建议用断点或更细的 EDX:EAX 追踪确认。
- 代码使用宽字符串流解析，但当前证据不能单独证明实际文件编码必须是 UTF-16。
