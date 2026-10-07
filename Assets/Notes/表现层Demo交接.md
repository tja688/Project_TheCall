# The Call 表现层

界面放在 `Assets/Scenes/Main.unity` 里，运行时不再生成控件。换图、挪位置、改字号都在场景里做。

## 场景里有什么

`UICanvas` 下有这些界面，同一时间只显示一个：

- `OpeningScreen`：开局，留下第一只怪物
- `LevelStartScreen`：离开商店后的下一关开场
- `OperationScreen`：主界面，对应 `Assets/示意图/主界面示意图.jpg`
- `ResearchScreen`：科研，对应 `Assets/示意图/科研界面示意图.jpg`
- `ShopScreen`：商店，对应 `Assets/示意图/商店示意图.jpg`
- `ResultScreen`：胜利或失败
- `Toast`：短提示

选中场景里的 `TheCallPresentation`，检视面板下方可以只显示其中一个界面，方便编辑。

`Assets/Scripts/Presentation/TheCallPresentation.cs` 只负责把查询结果写进这些控件，并把点击发给命令。它不创建物体，也不改 RectTransform。

## 换自己的图

- 背景、面板、按钮、科研图标、博士立绘：选中对应物体上的 `Image`，把 Sprite 换成自己的图。物体名里带 `BackgroundImage`、`PanelImage`、`IconImage`、`PortraitImage`、`BeltImage`、`RecycleIcon`、`TrashIcon` 的都是预留位。
- 怪物拼接：编辑预制体 `Assets/Prefabs/UI/MonsterPortrait.prefab`。`Body`、`Tail`、`Foot`、`Hand`、`Head`、`Eye`、`Mouth`、`Hat`、`Accessory` 各是一层，画在同一张 142×102 画布上，所以默认铺满同一个矩形。要微调，拖这些层，不要改代码。运行时只替换 Sprite，位置会保留。
- 部件图放在 `Assets/Resources/MonsterParts` 各文件夹。同一只怪物用稳定组合，在收容笼、生产区、培育室和商店里长得一样。
- 工具图和说明在商店界面的 `工具图标` 上。这个物体默认隐藏，在层级里选中后给 `急急装置`、`上级员工证`、`独孤装置` 指定 Sprite 和说明。

菜单 `The Call/重建表现层界面` 会按脚本重搭整套界面，并覆盖已经改过的位置和图片。日常改界面不要用它。

## 和核心的关系

表现层不重写规则。读取用查询，改状态用命令，都走 `TheCallApp`。

开局点「留下它」后直接进入第 1 关操作。离开商店后，若还没到第七关，会先进入下一关开场，再点「进入操作台」。

操作阶段可以：从收容笼点选怪物，再点生产区或培育室的空位放入；点已占用的格子放回；点废弃回收丢掉选中的怪物；点技能槽再点「装入」；点「投入技能」放进培育方案；点「科研」解锁科技；点「进入第 N 天」结算。

商店在结算发工资之后才进入。购入商品是货架上的 4 只怪物和最多 2 件工具，出售库存卖掉收容笼里、且不属于已锁定培育方案的怪物。

科技只能在操作阶段研究。培育位默认 2 个；「大乱炖」之后变 3 个，「槽位扩容」之后再增加一组。场景里多出来的位子会在未开放时隐藏。生产区默认 5 格，持有「上级员工证」后出现第 6 格。收容笼一次能看见 6 只，更多的往下滚。
