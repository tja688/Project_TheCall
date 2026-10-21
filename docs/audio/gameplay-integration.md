# 玩法音效接入

资源来自 `eed3ed53cf5a59657743a3b2b67267f103220b4f`。本批实际交付 24 个 WAV，17 个接入现有玩法，7 个经验音按编号保留在目录。已有 UI 音效和 BGM 沿用原接入。

| 资产组 | 实际触发时刻 |
| --- | --- |
| Monster PickUp | 怪物拖动虚影成功生成，或点选怪物进入待放置状态。一次手势只响一次。 |
| Monster Place | `CommitOperationDropCommand` 返回成功后，怪物放到提取槽、培育槽或回到怪物笼。 |
| Monster Discard | 主动废弃操作成功销毁怪物后。不会同时播放技能消灭。 |
| Monster EnergyGain | 结算中文字碎片开始充入单只怪物的小能量条。零产能保持安静。 |
| Player Obtain | 开局成功收下怪物、研究解锁、基因安装、结算获得金币或技能、工资和超额奖励。商店购买仍使用已有专用购买音。 |
| Skill Energy | 正产能的技能演出开始。 |
| Skill Multiply | 倍增效果合入当前技能数字，且实际把正能量提高后。 |
| Skill Kill | 结算记录中的消灭实际成立、怪物开始退场时。空目标和未成立的消灭不响。 |
| Level Settle | 成功进入结算演出后，一轮一次。普通结算与加班均适用。 |
| Level Reach | 大能量条显示值第一次跨过本关真实需求值，提前确认的规则结果不会提前发声。 |

同组变体轮换，不消耗玩法随机数。短音保持原音高，走既有 AudioKit 和 SFX 混音组，受原设置页的总音量、音效音量与开关控制。快进保留演出触发点，并合并 90 毫秒内同类声音；跳过省略中间技能、充能与消灭音，保留最终大条达标和工资反馈。返回标题、取消演出或停用总控会停止该演出的活动声音。自然结束保留音效尾部。

## 待补的资源与规则

- 配置单列出的 `S_Level_EnergyRise_01~07.wav` 未在项目中交付。当前大能量条不会借用小条或经验音，未自行设置能量档位阈值。
- `S_Level_ExperiencePickup_01~07.wav` 已按顺序绑定到 `GameAudioCatalog.experiencePickup`。当前玩法没有经验值、经验条或经验结算展示，也没有各音阶的数值映射，因此保持待用，不绑定金币、科技点或普通获得。

## 维护与验证

`Assets/Resources/GameAudioCatalog.asset` 持有原文件引用，不复制 WAV。通过 Editor 菜单 `TheCall/Audio/更新玩法音效目录`，或 Unity CLI 调用 `TheCall.Editor.GameplayAudioIntegration.Apply()`，可重新绑定本批资源，并设置短音 PCM、预加载和保留采样率。

运行 `pwsh -NoProfile -File scripts/verify-gameplay-audio.ps1` 可重复验证真实拖放、拒绝、废弃、普通结算、快进、跳过、零产能、技能消灭、取消、静音、自然结束和池化句柄回收。它使用明确标注的局部起始状态，随后走正式命令与播放流程，不等同自然通关。每条声音记录实际播放的 AudioSource、混音组、帧号和大条值，结果在 `docs/audit/2026-10-21/audio/`。

Editor 测试 `SfxCatalogTests` 检查本批 24 个剪辑的直接引用、可解码的非静音数据、导入设置、经验序列编号与既有 UI 音效。完整七天玩法回归可另用既有自然回放脚本。

2026-10-21 在 Unity 6000.6.0f1 的 Main 场景完成验证。34 项运行检查通过，记录 60 次实际 AudioSource 播放，全部为 SFX 混音组的一次性播放。普通、快进、跳过的规则状态与生产记录逐字一致，验证期间无新增 Unity 错误。3 项音效 Editor 测试通过。24 个 WAV 的 SHA-256 均与原提交的 LFS 对象一致。

证据分别为 `runtime.json`、`settlement-0/1/2-state.txt`、`settlement-0/1/2-log.txt`、`console.json`、`editor-tests.json` 和 `source-integrity.json`。这些结果证明本批接入和局部结算流程，未在本轮重跑完整七天通关。
