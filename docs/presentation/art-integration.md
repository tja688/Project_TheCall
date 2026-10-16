# 给现有游戏接入美术

保留各界面的 `ScreenView` 和已接好的槽位脚本。图片、分层、按钮位置和动画可以在这些对象下替换。玩法规则继续由同一个 QFramework 架构持有。

首批实验室 PSD 的图层对应、临时素材和替换入口见[实验室原画接棒笔记](lab-art-handoff.md)。

## 替换界面的图片与布局

1. 在对应界面的 `ScreenView.Render` 中修改文字、显隐和素材绑定。
2. 将新控件接到现有序列化字段。移除按钮或标签时，清空对应引用。
3. 给怪物画像换图时，保留 `MonsterPortrait`。给怪物卡片换布局时，保留 `MonsterSlotView` 的怪物标识。
4. 在操作界面的 Inspector 中设置 `cageColumns`、`cageRowSpacing` 和 `cagePadding`。
5. 在商店界面的 Inspector 中设置 `sellColumns`、`sellRowSpacing` 和 `sellPadding`。

如果怪物笼的父对象启用了 `LayoutGroup`，由该对象安排卡片位置。原场景继续使用原来的两列与行距。

各界面的按钮、标签、槽位数组可以缺省。保留界面根对象和正在使用的玩法槽位，以便玩家仍能执行所需操作。运行时扩容的卡片继承第一张卡片的美术，新增出售卡片由总控绑定出售动作。

## 接入拆好图层的怪物

1. 在 `The Call/怪物拼装工作区` 中接入部件 Sprite、挂点和运动支点。
2. 将骨架与姿势保存到 `Assets/Resources/MonsterRig.json`。
3. 在 `MonsterMotionProfile` 中调整抓取、回弹和计分反应的参数。
4. 检查开局、怪物笼、商店、拖动虚影和结算中的同一只怪物。

当前 `MonsterPortrait.Show` 优先从 `MonsterRigRuntime` 组装骨架。这份 implementation 共用部件与姿势，图层接入不需要修改怪物技能或结算规则。没有可用骨架时才使用 `MonsterAssemblyCatalog` 的回退模板。

## 接入新的按钮

1. 给新的 `Button` 加上 `PresentationActionButton`。
2. 在 Inspector 中选择 `Action`。
3. 将美术动画、音效回调接到 `Button.onClick`。

当前 Main 场景中，总控在 Canvas 旁的独立根对象上。按钮会在所属场景寻找唯一总控；有多个总控时，显式设置 `_presentation` 引用。

原有按钮和新按钮使用同一个 `TheCallPresentation.Perform` interface。给原有按钮添加此 adapter 后，声明的动作接管原绑定。停用 adapter 或选择 `None` 会恢复原绑定。总控只移除自己添加的监听器，保留美术回调。

常用动作有 `Settle`、`OpenResearch`、`ReturnToTitle`、`ToggleSettings`、`OpenHandbook`、`OpenSettingsLog`、`RefreshShelf` 和 `LeaveShop`。完整动作以 [PresentationActionButton.cs](../../Assets/Scripts/Presentation/PresentationActionButton.cs) 中的枚举为准。

增加动作时使用新的枚举数值。保留现有数值，以免已经保存的按钮改变用途。

## 替换基因槽的画面

1. 保留 `SkillChipView` 和 `OperationPayloadDrag`。
2. 在 `RenderGene` 或 `RenderBreeding` 中调整图标、标签和预约标记。
3. 保留 `SkillName` 与 `HasPayload` 的查询投影。

拖动读取 `HasPayload`，不读取按钮状态或标签文字。删除文字、替换“空”字样或隐藏按钮不会改变基因是否存在。拖动指针来自所属 `OperationScreenView.pointer`。

## 编排结算演出

1. 在 `ScoringTape.Arrange` 查看结算记录如何转成 cue。
2. 在 `ScoringShow` 的 `Walk`、`Pop`、`Swap`、`Remove`、`Pay` 和 `Mark` 中调整相应表演。
3. 将新的 Tween 交给 `Live`，使取消操作能清理它。
4. 给可变播放时长使用 `Span` 和 `Wait`，让快进与跳过仍能工作。
5. 保留换位时的 `Moved`、消灭时的 `Removed` 和结束时的 `Completed` 通知。

`SettlementPlayback` module 负责确认结算、读取结算记录和维护演出中的悬停快照。`ScoringShow` adapter 只消费 tape，使用上述通知跨过 seam。动画不发送规则命令。

取消演出会清理 Tween、恢复画像、停用阻挡层并释放交互。返回标题会先取消演出，再重置架构。播放期间调用 `Refresh` 会等待演出结束，避免提前显示规则已经进入的商店或失败界面。

## 验证改动

在 Unity 6000.6.0f1 中打开 Main 场景，然后退出 Play Mode。使用 PowerShell 7 运行：

```powershell
pwsh -NoProfile -File scripts/verify-presentation-architecture.ps1
```

脚本检查新按钮与原按钮、手册、拖动、加班失败、播放中刷新、重启和停用，并完成同种子的七天玩家操作。它比较正常、快进、跳过三种模式的最终规则状态与生产记录。截图和检查结果位于 `docs/audit/2026-10-13/architecture/`。

如果只改了按钮或标签，先运行较短的检查：

```powershell
pwsh -NoProfile -File scripts/verify-presentation-architecture.ps1 -SkipNaturalRuns
```

需要查看失败时的现场时，加上 `-KeepPlaying`。演出播放仍需检查真实 Game View，单独通过编译不能证明动画和交互正确。

需要检查基因安装、预约和大容量显示时，运行 `scripts/verify-presentation-fixtures.ps1`。这些局部场景使用注入资源，仅用于验证界面接入，不作为七天通关证据。
