# game::SystemConfig 与 SystemConfig.txt 逆向记录

本文记录 `SystemConfig.txt` 在当前 IDA 数据库中可见的读取流程、配置项、默认值和主要用途。结论只按反编译、反汇编和交叉引用整理；无法完全从代码直接证明的语义标为“待验证”。

当前仓库未找到实际的 `SystemConfig.txt` 样本文件；以下“读取什么”按程序注册的 key 和默认值整理。

## 读取入口

| 项 | 地址 / 值 | 说明 |
| --- | --- | --- |
| 单例入口 | `sub_40FAA0` | 第一次调用分配 `0x70` 字节并调用 `sub_40FBF0` 初始化 `game::SystemConfig`。传入非零参数时会释放旧对象并重新构造。 |
| 初始化函数 | `sub_40FBF0` | 注册 `28` 个配置项及默认值，然后尝试读取配置文件。 |
| 文件路径 | `0x00ABD808` / `L"SystemConfig.txt"` | `_wfopen_s(&Stream, aSy, L"rt")` 使用的宽字符串路径；相对当前工作目录。 |
| 读取函数 | `sub_507200` | 将 `FILE*` 包装为 `std::wifstream`，逐行 `std::getline<wchar_t>`，再调用 `util::Config` 虚函数解析每行。 |
| 行解析 | `sub_404EB0` | 提取 key/value 后调用 `sub_6A8440` 按 key 写入匹配的 `ConfigParam`。 |

读取失败时没有看到错误传播：打开失败或流状态异常会保留注册时的默认值。

## 解析规则

`SystemConfig` 继承/复用 `util::Config`。`sub_40FBF0` 里调用 `sub_6A7C20(this, 0)`，因此本配置使用 parser mode `0`：

- 每行按宽字符读取。
- 空行直接忽略。
- 普通有效格式是 `KEY value` 或 `KEY<TAB>value`。
- key 和 value 会去掉首尾空格/Tab。
- key 使用 `wcscmp` 精确匹配，大小写敏感。
- 未知 key 被忽略。没有看到专门的注释语法；整行以 `#`、`;` 等开头只会因为 key 不匹配而被忽略，行内注释不会被统一剥离。
- 重复 key 会再次写入同一个 `ConfigParam`，因此后面的值覆盖前面的值。
- `int` 项由 `ConfigParamInt::parse` (`sub_6A6F30`) 使用 `_wtoi(value)` 写入当前值；没有范围校验。非数字开头的文本会按 `_wtoi` 行为变成 `0`，但仍会被标记为已设置。`int` 项缺 value 时会把空指针传给 `_wtoi`，不应当作“清零”格式使用。
- `wstring` 项由 `ConfigParamString::parse` (`sub_6A7520`) 写入；缺 value 时清空。
- 泛用 parser 中还有查找 `>`/`<` 的分支，但当前注册 key 不包含 `>`；对 `SystemConfig.txt` 可确认的实用格式仍是空格/Tab 分隔。
- key/value 临时缓冲区各为 `256` 个 `wchar_t`，超长内容会被截断后再匹配/写入。
- 代码层面是 `_wfopen_s(..., L"rt")` 加 `std::wifstream` / `std::getline<wchar_t>`，解析器内部处理宽字符串；这不能单独证明磁盘文件必须是 UTF-16。当前 key 和数值均为 ASCII 范围，按普通文本保存通常可被读取。

写入位置不是 `SystemConfig` 固定字段本身，而是 `util::ConfigParam*` 参数对象：

| 对象 | 字段 | 说明 |
| --- | --- | --- |
| `ConfigParamInt` | `+0x04` | key 的宽字符串指针。 |
| `ConfigParamInt` | `+0x08` | 是否被写入/修改标志；成功解析后置 `1`。 |
| `ConfigParamInt` | `+0x0C` | 配置 ID。 |
| `ConfigParamInt` | `+0x10` | 当前值。 |
| `ConfigParamInt` | `+0x14` | 默认值。 |
| `ConfigParamString` | `+0x10` | 当前 `std::wstring`。 |
| `ConfigParamString` | `+0x2C` | 默认 `std::wstring`。 |

`SystemConfig` 对象自身还有运行时字段，例如启动模式标志和固定分辨率字段。这些字段与 `SystemConfig.txt` 的 `ConfigParam` 表是两层结构，不能把配置 ID 直接当作对象成员偏移。

## 配置项

| ID | key | 类型 | 默认值 | 主要用途 / 生效位置 |
| ---: | --- | --- | ---: | --- |
| `0` | `GAME_VER_A` | int | `0` | 版本号主段。`sub_40FF70` 格式化为 `A.BB.CC`；`sub_410040` 格式化为 `A.BB`；`sub_4100F0` 计算 `A * 100 + B`。 |
| `1` | `GAME_VER_B` | int | `0` | 版本号次段。见 `sub_40FF70` / `sub_410040` / `sub_4100F0`。 |
| `2` | `GAME_VER_C` | int | `0` | 版本号补丁段。见 `sub_40FF70`。 |
| `3` | `GAME_SHOW_VER` | wstring | 空字符串 | 显示用版本字符串。`sub_410170` 读取 ID `3`；`sub_41E960` 会把它拼进显示文本。 |
| `4` | `MASSPRO` | int/bool | `1` | 量产/产品模式开关 candidate。`WinMain` 命中后调用 `sub_960D30(..., 2)`；`sub_563C20`、`sub_522BA0`、`sub_52A4C0`、`sub_626C90`、`sub_692360`、`sub_6AA470` 等处用它选择路径、容量或流程。 |
| `5` | `FULLSCREEN` | int/bool | `1` | `WinMain` 读取后传给 `sub_6CEC60`，用于应用/窗口全屏设置。 |
| `6` | `WINDOW_W` | int | `1920` | `WinMain` 读取后作为宽度传给 `sub_6CEDD0(app, W, H)`。`sub_40BE20` 启动阶段也读取该项放入初始化结构。 |
| `7` | `WINDOW_H` | int | `2160` | `WinMain` 读取后作为高度传给 `sub_6CEDD0(app, W, H)`。`sub_40BE20` 启动阶段也读取该项放入初始化结构。 |
| `8` | `SOUND_CH_SRC` | int | `2` | `sub_5855B0` 读取后作为 `sub_6965D0(SoundCore, ..., src, dst)` 的源声道/源通道参数。 |
| `9` | `SOUND_CH_DST` | int | `6` | `sub_5855B0` 读取后作为 `sub_6965D0(SoundCore, ..., src, dst)` 的目标声道/目标通道参数。 |
| `10` | `DIST_S3_A1S_A1O` | int | `450` | `sub_531340` 缓存到 `dword_F5E8D8`，用于构建 `S3` 距离/时序表。 |
| `11` | `DIST_S3_A1O_X2I` | int | `220` | `sub_531340` 缓存到 `dword_F5E8D4`，用于构建 `S3` 距离/时序表。 |
| `12` | `DIST_S3_X2I_MID` | int | `288` | `sub_531340` 缓存到 `dword_F5E8D0`，用于构建 `S3` 距离/时序表。 |
| `13` | `DIST_S4_A1S_A1O` | int | `527` | `sub_531340` 缓存到 `dword_F5E8C8`，用于构建 `S4` 距离/时序表。 |
| `14` | `DIST_S4_A1O_B2I` | int | `332` | `sub_531340` 缓存到 `dword_F5E8C4`，用于构建 `S4` 距离/时序表。 |
| `15` | `DIST_S4_B2I_B2O` | int | `322` | `sub_531340` 缓存到 `dword_F5E8C0`，用于构建 `S4` 距离/时序表。 |
| `16` | `DIST_S4_B2O_MID` | int | `70` | `sub_531340` 缓存到 `dword_F5E8BC`，用于构建 `S4` 距离/时序表。 |
| `17` | `DIST_S5_A1S_A1O` | int | `522` | `sub_531340` 缓存到 `dword_F5E8B4`，用于构建 `S5` 距离/时序表。 |
| `18` | `DIST_S5_A1O_B1I` | int | `121` | `sub_531340` 缓存到 `dword_F5E8B0`，用于构建 `S5` 距离/时序表。 |
| `19` | `DIST_S5_B1I_B1O` | int | `344` | `sub_531340` 缓存到 `dword_F5E8AC`，用于构建 `S5` 距离/时序表。 |
| `20` | `DIST_S5_B1O_CI` | int | `129` | `sub_531340` 缓存到 `dword_F5E8A8`，用于构建 `S5` 距离/时序表。 |
| `21` | `DIST_S5_CI_MID` | int | `250` | `sub_531340` 缓存到 `dword_F5E8A4`，并派生 `dword_F5E8A0 = 2 * value`。 |
| `22` | `DIST_CA_AI_MID` | int | `434` | `sub_531340` 缓存到 `dword_F5E89C`，并派生多个 `CA` 距离值。 |
| `23` | `DIST_CA_AO_MID` | int | `0` | `sub_531340` 缓存到 `dword_F5E898`，并派生多个 `CA` 距离值。 |
| `24` | `DIST_CA_EXTRA` | int | `54` | `sub_531340` 缓存到 `dword_F5E888`，派生 `dword_F5E884 = DIST_CA_AI_MID + DIST_CA_EXTRA`。 |
| `25` | `DIST_CB_BI_MID` | int | `152` | `sub_531340` 缓存到 `dword_F5E880`，并派生多个 `CB` 距离值。 |
| `26` | `DIST_CB_BO_MID` | int | `70` | `sub_531340` 缓存到 `dword_F5E87C`，并派生多个 `CB` 距离值。 |
| `27` | `SET_SUDDEN_SLIDE` | int | `100` | `sub_5923A0` 读取，用于把输入进度 `a1` 按 `(100 - value)` 阈值和 `a1 * 100 / value` 公式换算；默认 `100` 基本等价于直接返回输入。 |

## 距离参数的集中生效

`sub_531340` 是 `DIST_*` 系列最集中的使用点：

- ID `10..26` 被懒加载到一组全局缓存 `dword_F5E8D8` 到 `dword_F5E87C`。
- 后续用这些缓存调用 `sub_5310D0` 写入 `dword_F48300`、`dword_F48334` 等表。
- 部分配置会派生出二倍值或组合值，例如 `DIST_S5_CI_MID * 2`、`DIST_CA_AI_MID + DIST_CA_EXTRA`。

因此这些配置不是在读取文件时直接改运行时表，而是在 `sub_531340` 第一次构建相关距离/时序表时才生效。

## 固定字段与启动参数

`SystemConfig` 固定字段中有一组运行时启动标志，它们不是 `SystemConfig.txt` 配置项：

| Offset | 含义 candidate | 设置位置 | 主要用途 |
| ---: | --- | --- | --- |
| `+0x28` (`40`) | `gametest` 启动标志 | `WinMain`：命令行精确等于 `"gametest"` 时置 `1`。`sub_40FBF0` 读取文件后先清零。 | 跳过/改写部分启动流程、资源检测和联网流程；`sub_40BE20` 根据它创建对应模式。 |
| `+0x29` (`41`) | 直接打开谱面/资源文件标志 candidate | `WinMain`：命令行后缀为 `.sdt`、`.sct`、`.srt`、`.szt` 时，`sub_401460` 置 `1` 并写入 `+0x2C` 宽字符串路径。 | 多处流程判断中禁用常规资源或演出路径；`sub_40BE20` 根据它创建专用模式。 |
| `+0x2C` (`44`) | 命令行文件路径 `std::wstring` | `sub_401460` 写入；`sub_40FBF0` 读取文件后清空。 | `sub_4AF670` 复制该路径并提取文件名。 |
| `+0x48` (`72`) | `designviewer` 标志 | `WinMain`：命令行精确等于 `"designviewer"` 时置 `1`。 | `sub_40BE20` 创建 `ModeDesignViewer`。 |
| `+0x49` (`73`) | `spriteviewer` 标志 | `WinMain`：命令行精确等于 `"spriteviewer"` 时置 `1`。 | `sub_40BE20` 创建 `ModeSpriteViewer`。 |
| `+0x4A` (`74`) | `noisetest` 标志 | `WinMain`：命令行精确等于 `"noisetest"` 时置 `1`。 | `sub_41E960`、`sub_41F150`、`sub_41FCE0` 等噪声测试相关路径引用。 |
| `+0x4C` / `+0x50` (`76` / `80`) | 固定渲染/坐标基准宽高 candidate | `sub_40FBF0` 在读取文件后写 `1920` / `2160`；`DebugConfig` 初始化 `sub_405630` 末尾会交换这两个字段。 | 大量渲染、坐标换算函数直接读取这些固定字段，例如 `sub_432440`、`sub_459510`、`sub_4DCB80`、`sub_6ACDD0`。这不是 `WINDOW_W/H` 参数对象本身。 |

命令行比较是精确字符串比较；当前可见值包括：

```text
game.exe gametest
game.exe designviewer
game.exe spriteviewer
game.exe noisetest
game.exe <path ending .sdt/.sct/.srt/.szt>
```

其中 `.sdt/.sct/.srt/.szt` 路径先经 `sub_401230` 使用 Windows code page `932` 转成宽字符串，再由 `sub_401460` 写入 `SystemConfig + 0x2C`。

## 与 ModeDesignViewer 的关系

`designviewer` 不是 `SystemConfig.txt` 的 key。进入链路是：

1. `sub_40FBF0` 构造 `SystemConfig`，读取 `SystemConfig.txt`，随后清零 `+0x28/+0x29/+0x48/+0x49/+0x4A`。
2. `WinMain` 将 `lpCmdLine` 转成 `std::string`。
3. 若命令行精确等于 `"designviewer"`，`WinMain` 在 `0x402290` 写 `SystemConfig + 0x48 = 1`。
4. `sub_40BE20` 检查 `SystemConfig + 0x48`，分配 `0x138` 字节并调用 `sub_40A300` 构造 `ModeDesignViewer`。

因此启动方式 candidate 是：

```text
game.exe designviewer
```

## 待验证点

- `SystemConfig +0x4C/+0x50` 固定宽高在 `sub_40FBF0` 中写为 `1920/2160`，但 `DebugConfig` 初始化 `sub_405630` 末尾会交换这两个字段。需要结合完整启动顺序或运行时断点确认最终稳定值。
- `MASSPRO` 的高层业务语义仍按候选命名理解；当前证据只能说明它控制量产/常规路径、容量和若干启动流程分支。
- parser 中 `>`/`<` 分支的历史用途未确认；对当前 `SystemConfig.txt` 的可用格式建议仍按空格/Tab 分隔。
