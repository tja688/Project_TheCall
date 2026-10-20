# Unity 表现整改索引

> 2026-10-20 起，怪物拼接、骨架、部件目录和拼装工作区已从工程删除。下文若提到它们，只作当时记录。新系统从 `Assets/Notes/新怪物系统交接.md` 开始。


本索引记录实现与验收入口。最终状态以主控在最终源版本执行的截图、真实组件输入、Prefab 重载和测试结果为准；本文件本身不是完成证据。

| 项目 | 修复入口 | 确定检查入口 | 画面/交互验收 |
| --- | --- | --- | --- |
| U01 科研六项 | PresentationRemediation.MigrateResearch 使用正式 ContentBook.Techs，移除前置管线、不同图标，DrawResearch 检查余额 | Apply 保存后 ValidateSaved 检查六个唯一正式名称 | 六项点击、1点、重复研究、不足与10%文本 |
| U02 完整目标/按钮 | TheCallPresentation.DrawOperation/DrawShop，当前累计不归零、科技点/差额补充 | 操作与加班状态 Query 对比标签 | 9/50、加班累计、第7天商店结束本局 |
| U03 正式映射/consumer | M3PresentationSetup.CreateSpriteCatalog；MonsterSlotView 的 personalityIcon/raceIcon/moodIcon；FillMonster 绑定三个 Getter | ValidateSaved 对25性格/5种族/5心情逐名检查非空及槽控件引用 | 正式内容实例可见图文 |
| U04 详情 | MonsterHover 分层名称/性格机制/种族/心情/工作休息、420宽技能详情与来源；独立实例DTO | MonsterDetailsQuery 只读目录与实例来源；HoverPrefab 保存重载 | 四技能长名称、四个屏幕边缘可读 |
| U05 工具重叠 | PresentationRemediation.MigrateTools 独立 Icon 子节点；ToolArtView tooltip | ValidateSaved 确保图标组件不是布局根、18控件 | 持有1/3/6/18份均可辨与悬停 |
| U06 库存截断 | GrowInventory 按真实集合池化增长、增加 ScrollRect 内容高度，出售新按钮绑定 | 19/30只造景经实际组件首尾拖/卖 | 重排后稳定 monsterId 与全部条目可达 |
| U07 反馈/复用 | OperationFeedbackQuery 只读 LastOperationFailure；SkillTrimFeedbackQuery 只读裁切移除；工具Sprite先清理 | CommitDrop 的结构化失败toast、安装/预约/退还提示 | 失败原子性和售罄/缺映射复用 |
| U08 目录/局部迁移 | MonsterAssemblyCatalog 同名脚本；RepairAssemblyCatalog；Builder 对现有界面路由 Apply | 原始Hash受控39部件/6模板恢复，Sprite GUID/fileID及资源meta GUID保留；ValidateSaved重载 | 工作区可编辑，39/6运行Rig无退化、二次迁移无重复 |
| S04 带入播放 | InitialEnergy 在 Presentation→Stage→Tape→Show 贯通；InitialGold 与Gold Mark贯通 | ScoringTape 新结构化来源测试；自然加班/跨日 | 起播累计、工资、科技、支付、带入解释 |
| S05 来源定位 | ScoreBeat 持 SourceMonsterId/SourceSkillInstanceId/SourceToolId，ScoringShow.ActorOf按ID；工具动作从工具控件起点 | 同显示名来源测试与真实高组合播放 | 镜眼/香/耳的来源动作定位 |
| F17 解释 | MonsterDetailsQuery 传 InstanceId 至共同 SkillReadout；Tape 非超额科技0；ScoringShow不解析UI金额 | 修旧错误“普通达标科技1”断言 | 预览/逐笔/实际一致；自然播/快进/跳过状态一致 |

## 93项的表现 consumer

- 34技能：`MonsterHover._subNames/_subUses/_subRarities/_subFunctions/_subSentences`；`SkillCodexView`图鉴；`ScoringShow`读取 `SettlementLanding`。技能机制由规则测试独立覆盖。技能均以文本机制呈现，不以新造技能图标代替规则说明。
- 25性格：正式 JSON 名单→`PresentationSpriteCatalog.personalities`→`GetPersonalityIcon`→`MonsterSlotView.personalityIcon`；性格机制→只读 `MonsterDetails.PersonalityDescription`→`MonsterHover.HeaderText`。
- 18工具：正式 JSON Description/Price/Rarity→catalog工具定义→商店 `FillToolCard` 与持有 `FillOwnedTools`；`ToolArtView`悬停说明。
- 6科技：正式 JSON Techs→保存的六个 `TechNodeView`→详情文字、成本/状态与 `OnResearch`。
- 5种族/5心情：正式名单→catalog Getter→`MonsterSlotView.raceIcon/moodIcon` 与详情文字。

`PresentationRemediation.ValidateSaved()` 对上面所有图集映射和实际引用作确定检查，但不能替代93项逐条画面证据或规则条件断言。动态库存、点击安装、裁切、工具来源动作仍必须以真实组件链和Game View验证。

## 目录恢复的资产证据

`assembly-catalog-recovery.json`从失效目录只读解析，包含原始部件ID、几何、连接、模板、所有Sprite GUID/fileID和原文件SHA256。恢复经Editor JsonUtility及AssetDatabase进行，未写场景/Prefab YAML。原历史目录继续使用 `body_01`等ID；现代 `MonsterRigRuntime`仍使用自己的 `身体/身体1`等目录和挂点，未把两种ID串换。

## 主控执行入口

```csharp
return TheCall.Editor.PresentationRemediation.Apply();
return TheCall.Editor.PresentationRemediation.ValidateSaved();
```

必须在非播放状态执行，随后保存重载并复验。已由主控首次执行Apply成功，并报告当时Unity相关351/351；其后增加科技0断言、InitialGold、实例预览和纯读反馈等修正，最终测试需在这些源更改后的重编译版本重新执行。
