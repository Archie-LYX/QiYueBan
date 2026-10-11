# 七月半 · QiYueBan

《七月半》是一款开发中的 2D 叙事经营游戏，采用固定的第一人称柜台视角，以中国街边纸扎店的日常劳动与亲子关系为主题。

父亲在中元节前去世。葬礼结束后，已在外工作多年的儿子应母亲请求，回到纸扎店，替父亲守完最后七个营业日。在备货、制作与接待来客的过程中，他逐渐看见父亲平凡的生活。故事以克制的日常与告别为方向，主角不会被强制安排辞职继承店铺。

## 开发环境

- Unity **6000.3.26f1**（Unity 6.3 LTS），准确版本见 `ProjectSettings/ProjectVersion.txt`。
- Universal 2D / URP **17.3.0**，C#。
- Input System **1.20.0**、Unity UI；依赖及锁定版本保存在 `Packages/manifest.json` 和 `Packages/packages-lock.json`。
- 当前原型以 Windows 桌面端、鼠标左键操作为主。

## 核心玩法与当前进度

整体设计围绕「采购备货 → 手作制作 → 接待订单与报价 → 打烊结算」展开，并以七天叙事串联。经营、七天剧情和其他手作小游戏属于后续开发内容。

目前完成的是独立可玩的 **金元宝记忆连线小游戏 V0.2**：

1. 选择难度，观看金纸上的路线逐段展示，记忆正确折点顺序。
2. 7 秒记忆阶段结束后连线隐藏，候选点及绿起点、红终点保持可见。正确点与干扰点外观相同。
3. 从绿色起点附近按住鼠标左键，一笔按顺序连接所有正确折点，避开干扰点，再连接红色终点。
4. 点间可以自由弯曲移动；靠近点时线端平滑吸附，连接后圆点有轻微放大反馈。
5. 完整连接终点后锁定成功，松手播放金纸淡出、元宝弹出动画并累计奖励；失败可重试并生成新布局。

| 难度 | 正确折点（不含起终点） | 干扰点 | 成功奖励 |
| --- | ---: | ---: | ---: |
| 简单 | 6 | 2 | 1 扎 |
| 普通 | 8 | 3 | 2 扎 |
| 困难 | 10 | 4 | 3 扎 |

所有难度直接开放。连接干扰点、跳点、乱序、未完成时离开纸面或提前松手会失败；已经连齐终点后再移出纸面不影响成功。失败不发奖，重复松手不重复结算。

奖励目前仅为本次运行的会话计数，未接入全局库存、原料消耗、经营经济或存档。元宝图形与界面仍为原型表现；完整剧情、正式美术、声音及发布版本尚未完成。旧折纸模拟脚本与场景继续保留。

## 打开与运行

1. 克隆仓库后，用 Unity Hub 打开仓库根目录，使用上述 Unity 版本。首次打开会重新生成 Library 并恢复 Packages。
2. 打开 `Assets/_Project/Scences/GoldIngot_V01.unity`，进入 Play，在 Game 视图操作。推荐 16:9，关闭 Game 视图 Gizmos，并将 Scale 调至适合窗口。
3. 选择简单、普通或困难开始游戏。运行时自动生成标题、按钮、点与连线，所以编辑模式下没有完整游戏界面属于预期行为。

`Scences` 是现有目录的实际拼写；场景名仍为 `GoldIngot_V01`，其现有脚本已经更新为 V0.2 玩法。

当前 `EditorBuildSettings` 仍只包含 `Assets/Scenes/SampleScene.unity`。导出前需在对应 Build Profile 的场景列表中加入小游戏场景并设为入口；本次仓库整理未修改构建配置，也未生成安装包。

当前 UI 默认尝试使用 Windows 的 Microsoft YaHei / SimHei 字体。跨平台发布前，应通过组件的 `Ui Font` 指定含中文字符的项目字体。

## 验证与开发记录

- 编辑器菜单 `Tools > Gold Ingot > Test Sequence Rules`：验证随机布局、间距、顺序判定及异常输入。
- 在小游戏场景 Play 模式下运行 `Tools > Gold Ingot > Verify Sequence Game in Play Mode`：通过虚拟鼠标验证 UI 与 Input System 交互。测试会自动操作小游戏，请勿同时手动画线。
- 现有测试工具将截图、报告和请求文件输出到仓库旁的 `QiYueBan/review/ingot-sequence` 与 `QiYueBan/review/first-fold`。这些目录在本机位于 Unity 仓库之外；同时忽略仓库根目录的 `review/`，避免手动复制的验收输出进入提交。
- 迭代过程与已存在的验收记录摘要见 [首次开发日志](Docs/Devlog.md)。本次整理只检查仓库与文档，没有重新运行 Unity 玩法测试。

## 项目结构

```text
Assets/
  _Project/
    Scences/         # Shop_Prototype、GoldIngot_V01
    Scripts/         # 折纸原型、记忆连线、图形与 Editor 验证工具
    Art/ Audio/ Data/ Prefabs/ UI/  # 现有资源目录
  Scenes/            # 模板 SampleScene
  Settings/          # URP、输入配置与场景模板
Packages/            # 包清单与锁文件
ProjectSettings/     # Unity 项目配置
Docs/Devlog.md       # 开发日志
```

版本管理保留 `Assets` 及必要的 `.meta`、`Packages`、`ProjectSettings`；排除 Library、Temp、Logs、UserSettings、IDE 文件、构建产物和本地验收输出。仓库未附加开源许可证；素材与代码的授权范围需在正式发布前明确。
