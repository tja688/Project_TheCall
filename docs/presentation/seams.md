# 表现层的职责与接缝

当前整理保留现有 QFramework、规则源码、场景和结算主体。美术最常修改的内容从总控迁入各界面 module。

| module | interface 与 seam | implementation 的职责 |
| --- | --- | --- |
| `TheCallPresentation` | `Perform`、`Refresh` | 玩家动作、命令、界面流程、当前界面对应的音乐 |
| `ScreenAssembly`、`AssemblySurface` | `Prepare`、`Sample`、`Restore` | 已有分层的临时姿态、透明度和底图分块；总控拥有交接与输入生命周期 |
| 各 `ScreenView` | `Render` | 查询投影、控件文字与显隐、当前界面的素材 |
| `PresentationReadout` | 怪物绘制 | 开局、怪物笼、提取槽、培育槽共用的画像与徽标 |
| `MonsterInventory` | 怪物笼绘制 | 扩容、内容排列、选中态；新出售槽回调 |
| `SkillChipView` | `RenderGene`、`RenderBreeding`、`HasPayload` | 基因显示及其实际存在状态 |
| `PresentationActionButton` | 动作枚举 | 新美术按钮 adapter；与原按钮共用总控入口 |
| `SettlementPlayback` | `TryStart`、`Cancel` | 规则交接、悬停快照、完成与取消的生命周期 |
| `ScoringTape` | `Arrange` | 已确认的结算记录转成演出 cue |
| `ScoringShow` | tape、进展通知、`Cancel`、`Advance` | cue 的 Unity 演出 adapter |

怪物画像由 `MonsterPortrait.Show(MonsterView)` 接收状态快照。`MonsterVisualTraits` 将性格与真实心情投影为眼型和动作状态；`MonsterComposition` 按 `MonsterArtCatalog` 的 12 套成品模板、整套色卡和兼容饰品绘制。身体、嘴型、眼位及主要装饰出生后固定，技能不改变造型。画像、拖动虚影与结算共享同一外观，画像不查询正在变化的玩法状态。维护入口见[成品模板系统](monster-system.md)与[美术规则](monster-art-direction.md)。已删除的旧拼接系统不恢复。

`Render` 只查询规则。原有命令保留 QFramework 路由职责，Model 仍是对局状态的主人。总控控制界面显隐和音乐，界面 module 不独立改变流程。

depth 来自一次调用能完成多少行为。怪物绘制把画像、徽标和标签收进一个 implementation，删除它会让同一知识再次散入多个界面。它提供实际 leverage。各界面保持自己的绘制入口，提升美术修改的 locality。

结算规则在演出开始前已完成。播放速度、中断或界面刷新不会再次确认结算。规则快照只在 `SettlementPlayback` 中更新，`ScoringShow` 不持有架构引用。取消时先退休回调，再销毁播放对象；延期刷新使用会话编号避免影响新一局。

本次选择不替代 ADR 0003、0004、0006、0007。规则仍与 Unity 分离，并继续复用现有结算记录和动画能力。

换图与动画的操作步骤见 [给现有游戏接入美术](art-integration.md)。
