# game::sequence::ModeDesignViewer 运行时代码记录

本文记录通过 IDA MCP 读取到的 `game::sequence::ModeDesignViewer` 运行时代码结构。结论只按当前 IDA 数据库可见的符号、RTTI、虚表、反编译和交叉引用整理；未能由代码直接证明的命名保留为 candidate。

## 定位

| 项 | 地址 / 值 | 说明 |
| --- | --- | --- |
| 虚表符号 | `0x00ABD4C4` / `??_7ModeDesignViewer@sequence@game@@6B@` | IDA 已识别为 `game::sequence::ModeDesignViewer::vftable`。 |
| RTTI complete object locator | `0x00BB8414` | `??_R4ModeDesignViewer@sequence@game@@6B@`。 |
| Type descriptor | `.?AVModeDesignViewer@sequence@game@@` | RTTI 中的 C++ 类型名。 |
| 构造函数 | `sub_40A300` | 唯一直接调用点在 `sub_40BE20`。 |2
| 析构函数主体 | `sub_40A480` | 由 scalar deleting destructor `sub_40B5C0` 调用。 |
| 创建入口 | `sub_40BE20` at `0x40C8F8..0x40C924` | 当 `sub_40FAA0(0)+0x48` 标志为真时，分配 `0x138` 字节并以宽字符串 `DesignViewer` 构造本类。 |

RTTI 继承链为：

```text
game::sequence::ModeDesignViewer
  -> game::sequence::ModeSpriteViewer
    -> game::sequence::ModeSequenceBase
      -> util::ModeObj
```

`ModeDesignViewer` 是 `ModeSpriteViewer` 的专用派生类：基础 viewer 负责 DDS/sprite 浏览、平移和 overlay；派生类增加 `.sbscene` / `.svo` 扫描、解析、layer/animation 选择与 reload 状态机。

## 虚表

`ModeDesignViewer` 虚表共有 6 个函数入口，后面紧跟其它只读数据。

| Slot | 地址 | 角色 | 证据 |
| ---: | --- | --- | --- |
| 0 | `sub_40B5C0` | scalar deleting destructor | 调用 `sub_40A480(this)`，按 `a2 & 1` 决定是否 `aligned_free(this)`。 |
| 1 | `sub_40A5A0` | enter/init candidate | 只做一件事：`this+0xD8 = sub_40AB00`，把派生状态机置为扫描 `.sbscene`。 |
| 2 | `sub_40A5B0` | leave/reset candidate | 直接调用 `sub_40A5C0(this)` 清理已加载资源。 |
| 3 | `sub_415A90` | inherited frame wrapper | `ModeSequenceBase` 共有实现；先调用 slot 4，再在需要时调用 slot 5 做绘制/提交。 |
| 4 | `sub_40A760` | per-frame update | 先执行继承自 `ModeSpriteViewer` 的 `this+0x3C` 回调，再执行本类 `this+0xD8` 状态回调，返回 `1`。 |
| 5 | `sub_40A780` | draw/render | 调用 base render，再在加载完成状态绘制 sbscene/svo 内容和文字 overlay。 |

## 成员布局

`ModeSpriteViewer` 基类占用到 `0x84` 前。`ModeDesignViewer` 构造函数从 `0x84` 开始初始化派生成员，对象总大小在创建点为 `0x138`。

| Offset | 类型 / 角色 | 读写函数 | 说明 |
| ---: | --- | --- | --- |
| `0x84` | `sbsceneResource*` candidate | `sub_40ADE0`, `sub_40A5C0` | `sub_6D2600(path)` 取得的 `.sbscene` 资源指针；清理时 `sub_791840` 后释放。 |
| `0x88` | parsed sbscene/model object | `sub_40ADE0`, `sub_40A5C0` | 由 `sub_89CD10` 创建，`sub_89CC40` 用资源数据初始化；清理时走虚析构。 |
| `0x8C` | scene/player controller | `sub_40ADE0`, `sub_40A780`, `sub_40A5C0` | 由 `sub_899CC0(..., 1024)` 创建；负责接收 SVO wrapper、选择 layer/animation 并绘制。 |
| `0x90` | `vector<string>` | ctor, `sub_40ADE0`, dtor | 每项是 `basePath + resourceName + ".svo"` 的 SVO 路径。 |
| `0xA8` | `vector<object*>` | `sub_40B090`, `sub_40A5C0`, dtor | 每项由 `sub_89D100(loadedSvo)` 创建，随后交给 scene/player controller。 |
| `0xC0` | `vector<resource*>` | `sub_40B090`, `sub_40A5C0`, dtor | 每项是资源管理器查到的 loaded SVO 指针。 |
| `0xD8` | state callback | 多数本类函数 | 状态机函数指针，值见下一节。 |
| `0xDC` | current layer index | `sub_40B4A0`, `sub_40B260` | 当前 layer 选择，动画选择时作为 layer 参数。 |
| `0xE0` | `std::string currentSbscenePath` | `sub_40AB50`, `sub_40AB00`, `sub_40ADE0` | 保存扫描到的第一个 `.sbscene` 完整路径。 |
| `0xFC` | repeat flag | ctor, `sub_40B260`, `sub_40B520`, `sub_40A780` | `R` keycode `82` 切换；用于 `sub_896680`，overlay 显示 `ON/OFF`。 |
| `0xFD` | scene pair toggle | ctor, `sub_40B260`, `sub_40A780` | `T` keycode `84` 切换；渲染时在 `SCENE_SBF1L/SBF2L` 与 `SCENE_SBF1H/SBF2H` 两组资源 ID 间切换。高层语义未确认。 |
| `0x100` | `std::string currentLayerName` | `sub_40B4A0`, `sub_40A780` | overlay 的 `Layer : %s`。 |
| `0x11C` | `std::string currentAnimationName` | `sub_40B4A0`, `sub_40B520`, `sub_40A780` | overlay 的 `Animation : %s`；切 layer 时清空。 |

## 硬编码资源和文本

| 地址 / 名称 | 值 | 用途 |
| --- | --- | --- |
| `aDataTestImageS` | `./data/TEST_IMAGE/sprite/` | `ModeSpriteViewer` 默认路径。 |
| `aDataTestImageS_0` | `./data/TEST_IMAGE/surfboard/` | `ModeDesignViewer` 构造后覆盖到基类路径 `this+0x5C`。 |
| `aSbscene` | `.*\\.sbscene` | `.sbscene` 扫描正则。 |
| `aDds` | `.*\\.dds` | 基类 DDS 扫描正则。 |
| `aSvo` | `.svo` | SVO 路径拼接后缀。 |
| `aLayerSAnimatio` | `Layer : %s\nAnimation : %s\nrepeat : %s` | overlay 文本格式。 |
| `aOn_0` / `off_ABD06C` | `ON` / `OFF` | repeat flag 显示文本。 |
| `off_ABD228` | `DesignViewer` | 创建本类时传入的 mode 名称。 |
| `off_ABD244` | `SpriteViewer` | 相邻的基类 viewer 创建分支名称。 |

`sub_40A780` 用到的 scene resource key：

| Index | 字符串 |
| ---: | --- |
| 9 | `SCENE_SBF1L` |
| 10 | `SCENE_SBF2L` |
| 11 | `SCENE_SBF1H` |
| 12 | `SCENE_SBF2H` |

`0xFD == 0` 时使用 9/10，`0xFD != 0` 时使用 11/12。

## 状态机

`this+0xD8` 是本类自己的状态回调，`sub_40A760` 每帧调用一次。

| 状态函数 | 进入来源 | 行为 | 下一状态 |
| --- | --- | --- | --- |
| `sub_40AB00` | slot 1 `sub_40A5A0` 或 reload 后 | 调用 `sub_40AB50` 扫描 `.sbscene`；若找到路径则向资源管理器请求加载。 | 找到 `.sbscene` 后切到 `sub_40ADE0`；未找到则切到 `sub_40B260`。 |
| `sub_40ADE0` | `sub_40AB00` | 等待/取得 `.sbscene` 资源，创建 parsed model 和 scene/player controller；枚举 model 中的资源名，生成 SVO 路径并请求加载。 | 初始化成功后切到 `sub_40B090`。 |
| `sub_40B090` | `sub_40ADE0` | 等待所有 SVO 资源可取；为每个 SVO 建 wrapper 并送入 scene/player controller；ready 后默认选择 layer 0。 | `sub_895AB0(controller)` ready 后切到 `sub_40B260`。 |
| `sub_40B260` | loaded/interactive 状态 | 处理 layer、animation、repeat、scene pair、reload 等输入。 | `E` keycode `69` 时切到 `sub_40B480`。 |
| `sub_40B480` | interactive reload | 调用 `sub_40A5C0` 清资源。 | 回到 `sub_40AB00` 重新扫描/加载。 |

### `.sbscene` 扫描

`sub_40AB50` 会先确保 `.*\\.sbscene` 正则全局初始化，然后以 `this+0x5C` 的基类路径为目录扫描。当前构造默认该路径是：

```text
./data/TEST_IMAGE/surfboard/
```

扫描命中后，它把 `basePath + matchedName` 写入 `0xE0 currentSbscenePath`。函数只返回成功/失败，不解析文件内容。

### `.svo` 请求和绑定

`sub_40ADE0` 通过 `sub_6D2600(currentSbscenePath)` 取已加载的 `.sbscene` 资源。成功后：

1. `sub_89CD10` 创建 parsed model，`sub_89CC40(model, data, size)` 初始化。
2. `sub_899CC0(..., 1024)` 创建 scene/player controller。
3. 对 `model+8` 的 count resize 三个容器。
4. 对每个 index 调用 `sub_89CCC0(model, index)` 取资源名。
5. 生成 `basePath + resourceName + ".svo"`，写入 `0x90 vector<string>`。
6. 对每个 SVO 路径调用 `sub_6D25A0(path)` 请求资源加载。

`sub_40B090` 后续通过 `sub_6D2F80(resourceManager, path)` 取每个 loaded SVO，成功后：

1. 保存 resource 指针到 `0xC0 vector<resource*>`。
2. `sub_89D100(loadedSvo)` 创建 wrapper，保存到 `0xA8 vector<object*>`。
3. 调用 controller 虚函数 `+0x28` 把 wrapper 加入 controller。
4. `sub_895AB0(controller)` ready 后执行 `sub_40B4A0(this, 0)` 选择第 0 个 layer。

## 输入处理

`sub_40B260` 是 loaded 状态下的输入处理函数。它先调用基类 `sub_40A080`，再处理本类 layer/animation。

| 输入证据 | 行为 | 说明 |
| --- | --- | --- |
| keycode `0x80..0x89` | 生成 `10,20,...,100` 的 modifier 值 | `sub_40A080` 返回该值；同时用于平移步长和 layer index 偏移。具体物理键未从本函数确认。 |
| keycode `155` / `156` | `this+0x7C` 左右平移 | 每次变化量为 `2 + modifier`。 |
| keycode `153` / `154` | `this+0x80` 上下平移 | 每次变化量为 `2 + modifier`。 |
| `Q` keycode `81` | 平移归零 | 清 `this+0x7C` 与 `this+0x80`。 |
| `I` keycode `73` | 切换基类 overlay 显示 | 改写 `this+0x78`。 |
| `'1'..'9'` keycode `49..57` | 选择 layer | 调用 `sub_40B4A0(this, digitIndex + modifier)`。 |
| keycode `159..167` | 选择 animation 或基类 DDS | 无 modifier 时调用 `sub_40B520(this, currentLayer, index)`；有 modifier 时走 `ModeSpriteViewer` 的 DDS 选择路径。具体物理键未从本函数确认。 |
| `R` keycode `82` | 切换 repeat flag | 改写 `0xFC`，并在 `sub_40B520` 中传给 `SbPlayerMO_SetAnimationRepeat` (`sub_896680`)。 |
| `T` keycode `84` | 切换 scene pair flag | 改写 `0xFD`，影响 `sub_40A780` 使用哪组 `SCENE_SBF*` resource key。 |
| `E` keycode `69` | reload | 把状态回调设为 `sub_40B480`。 |

`sub_40B4A0` 选择 layer：遍历 controller 的 layer 数，只有目标 layer 调用 `SbPlayerMO_SetLayerEnabled` (`sub_896560`, value `1`)，其它 layer 调用同一 API 关闭。它同时把 selected layer index 写到 `0xDC`，把 `SbPlayerMO_GetLayerName` (`sub_8969A0`) 返回的名称写到 `0x100 currentLayerName`，并清空 `0x11C currentAnimationName`。

`sub_40B520` 选择 animation：先用 `SbPlayerMO_GetAnimationCount` (`sub_896920`) 取得 animation 数，再对目标 animation 调用：

- `SbPlayerMO_SetAnimationEnabled` (`sub_8967A0`, value `1`) 设为选中；
- `SbPlayerMO_SetAnimationRepeat` (`sub_896680`) 传入 `0xFC`；
- `SbPlayerMO_SetAnimationTime` (`sub_896710`, `0.0`) 把当前 animation time seek 到起点；
- `SbPlayerMO_GetAnimationData` (`sub_896A10`) 取动画名写到 `0x11C currentAnimationName`。

其它 animation 只调用 `sub_8967A0(..., 0)` 取消选择。

## 渲染路径

`sub_40A780` 是本类 slot 5 渲染函数。

1. 先调用 `sub_409E40(this)`，也就是 `ModeSpriteViewer` 的基础渲染/overlay 前处理。
2. 只有当 `this+0xD8 == sub_40B260` 且 `0x8C controller != 0` 时，才绘制 design viewer 内容。
3. 构造矩阵并调用 `sub_895670(controller, matrix)`：
   - 单位矩阵；
   - 调用 `sub_408BC0(matrix, flt_C5B79C, 0, 0)`；
   - 调用 `sub_408C30(matrix, flt_B96D38)`；
   - 再按基类平移 `this+0x7C / this+0x80` 调用 `sub_408BC0`。
4. 根据 `0xFD` 选择两组 resource key：
   - `0xFD == 0`：`SCENE_SBF1L` 与 `SCENE_SBF2L`；
   - `0xFD != 0`：`SCENE_SBF1H` 与 `SCENE_SBF2H`。
5. 对 controller 连续调用虚函数 `+0x50` 提交两个 resource ID，再调用虚函数 `+0x48`，参数为 `1.0`。
6. 若基类 `this+0x78` overlay flag 为真，则绘制黑色文字层，并显示：

```text
Layer : <currentLayerName>
Animation : <currentAnimationName>
repeat : ON/OFF
```

函数最后会对全局渲染对象 `dword_F4071C` 的 pending buffer 做一次 `sub_684570` / `sub_7971D0` 提交并清 pending 标志。

## 清理

`sub_40A5C0` 是派生类资源清理核心，析构、slot 2 和 reload 都会调用。

清理顺序：

1. 若 `0x8C controller` 非空，走其虚析构并清 0。
2. 遍历 `0xA8 vector<object*>`，每项非空则虚析构并清 0。
3. 遍历 `0xC0 vector<resource*>`，每项非空则虚析构并清 0。
4. 若 `0x88 parsed model` 非空，虚析构并清 0。
5. 若 `0x84 sbsceneResource` 非空，调用 `sub_791840` 后释放并清 0。

`sub_40A480` 析构主体会先把 vtable 恢复为 `ModeDesignViewer`，调用 `sub_40A5C0`，再析构三个字符串，释放两个 pointer vector 的 backing storage，析构 string vector，最后调用 `ModeSpriteViewer` 析构 `sub_4097B0`。

## 和 ModeSpriteViewer 的关系

`ModeDesignViewer` 复用 `ModeSpriteViewer` 的以下机制：

- mode name / base mode object：由 `ModeSequenceBase` 构造，保存到 `util::ModeObj` / `ModeSequenceBase` 基类区。
- 基类路径 `this+0x5C`：基类默认是 `./data/TEST_IMAGE/sprite/`，本类构造后改为 `./data/TEST_IMAGE/surfboard/`。
- `this+0x3C` 基类回调：用于 DDS/sprite viewer 的异步加载状态，本类每帧仍先调用它。
- `this+0x7C / 0x80` 平移偏移：由 `sub_40A080` 修改，并被 `sub_40A780` 应用到 sbscene controller 矩阵。
- `this+0x78` overlay flag：`I` 切换，本类 overlay 也复用该开关。

本类真正新增的是 `.sbscene` 到 `.svo` 的资源链、controller 绑定、layer/animation 选择和 reload 状态机。

## 未确认项

以下结论仍应保持 candidate，不应写成正式业务语义：

| 项 | 当前证据 | 未确认原因 |
| --- | --- | --- |
| `0xFD` scene pair toggle | 只确认切换 `SCENE_SBF1L/SBF2L` 与 `SCENE_SBF1H/SBF2H` 两组 key。 | 不能仅凭 key 名确认它是 high/low、front/back、单双画面或其它业务模式。 |
| keycode `159..167` | 只确认它们在 `sub_40B260` 中选择 animation 或触发基类 DDS 选择。 | 具体物理按键由输入层映射决定，本函数没有给出名称。 |
| `sub_89CD10` / `sub_899CC0` 等类型名 | 代码显示它们是 parsed model 和 controller/wrapper 类。 | IDA 当前符号未给出真实 C++ 类型名。 |
| `.sbscene` 扫描策略 | `sub_40AB50` 取第一个匹配项并保存路径。 | 文件枚举顺序由 helper 实现决定，本文不推断排序规则。 |
| controller 虚函数 `+0x48/+0x50` | `sub_40A780` 用它们提交 resource key 和 `1.0`。 | 具体渲染语义仍需继续沿 controller 类型反查。 |
