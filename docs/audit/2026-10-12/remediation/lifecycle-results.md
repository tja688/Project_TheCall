# 生命周期、生成、培育与操作整改记录

2026-10-12。此记录只证明下列规则工作包和隔离夹具，真实 UI / Unity 资产与自然七天由主控统一收口。正式证据：[lifecycle-final.trx](lifecycle-final.trx)为全规则运行；生命周期聚焦66/66；本次完整规则运行410/410。

## 修复与回归索引

| 审计编号 | 实现 | 确定性回归 |
| --- | --- | --- |
| F01 / F06 技能实例身份与来源 | RunModel 的 SkillInstance.Id/ContentId/SourceKind/SourceId/SourceMonsterId/PermanentQuote/GrowthOwnerId；模型顺序分配 ID；不同实例独立成长及清临时 | LifecycleRemediationTests.F01_取得实例身份来源成长及临时清理独立；F01_复读允许商店生成两份同名技能；实际技能效果生命周期由 settlement 工作包覆盖 |
| F07 培育同名来源丢失 | BreedingSystem 候选记录亲本实例/材料稳定 ID；复读保留同名独立候选，裁切选择索引；材料仅其候选保留才消费；三亲本冲突可以沿各自来源链重选 | F07_同名亲本与基因裁切按候选身份决定消费 两个结果；F07_三亲本碰撞通过各自来源链重抽而不丢合法贡献；F07_A18_三亲本加基因满四候选仍掷变异并裁切不携带成长 |
| F08 安装与容量 | OperationTransition.Install；同一重复/上限约束，先取得成功才取材料；废弃容量读取GeneCapacity含预约；材料Capture保留Id | F08_正式拖放安装遵循重复许可并只成功消费；F08_普通性格及上限与预约安装失败保持字段不变；F08_预约基因不能安装且退回保持同一材料身份与原格；F08_仓储科技第四格可废弃满仓含预约原子拒绝 |
| F09 初生心情 / 开工 | 正式开局/商店/培育生成调用同一固定性格覆盖；DayBaseMood/DayMoodBoost 分离，复制性格仅更新固定覆盖而保留基础快照和严选；实际放置才开工，笼内安装不提前记开工；LevelStart 首次操作调用开始事件一次 | F09_初生固定性格即时覆盖普通心情 四性格；F09_改变性格保留当天基础与严选提升但即时应用固定覆盖；F09_笼内安装不会提前消费开工事件 |
| F12 抽样 | StockDraw 开局与商店共用先合法稀有度后档内均匀；一般生成先性格/种族，空档仅按原权重归一；复读允许商店双同名；胚胎调用FlowSystem.GenerateSingleMonster | F12_A27_技能抽样先按七二一权重再档内均匀 五边界；F12_A27_性格五二二一选择边界及独立两次随机消费 六边界；F12_A27_五族一次等概率池选择 五位置；F12_A27_功能与排重导致空档时仅保留原权重归一 两输入 |
| F16 裁切固定首项 | Monster.ChangePersonality 枚举合法保留对并注入随机均匀选取，至少一个常驻；RunModel.LastRemovedSkills 发布准确移除实例 | F16_安于现状均匀抽合法保留对且至少一常驻 覆盖全部五个合法组合 |
| A18 条件断言跳过 / 错误变异上限 | PlayerFlowM2Tests 强制安于现状后必断言两实例与基因消费；TechTests 将满四技能不掷变异错误断言改为第五候选加入后裁切 | 上述A18及 TechTests.后代已有四个候选仍掷变异并随机裁切；Lifecycle的三亲本+基因+变异测试验证10%调用、来源、成长、父母不消耗 |
| F17 工作台状态输入 | Flow.Lay 应用实例成长/临时、怪物修正、工作休息历史、带入能量、任意格亲缘；递归拓扑创建保证真实父子MonsterId，循环/空亲本明确拒绝 | F17_工作台夹具应用带入成长临时与任意格亲缘且不产生第二份公式；F17_工作台循环亲缘明确拒绝 |

## 接口与所有权

- RunModel.GainSkill 返回 SkillAcquisitionResult(Success, Failure, Instance)。检查内容、实际性格容量和复读许可后提交。技能效果使用当天临时来源；性格、开局、购买、安装、培育使用各自常驻来源。TryGainSkill 保留兼容。
- RunModel.ChangePersonality 使用正式 IDraw，记录 LastRemovedSkills，固定心情覆盖之外保留当天基础与严选提升。直接Monster.ChangePersonality仅供隔离规则夹具；正式流程使用Model入口。
- GeneSlot.Id 为稳定材料身份，预约/退回/Capture/Restore不改变。保留原格占位；使用后移除，未留材料仅取消预约。其他材料移动不影响来源身份。
- RunModel 作为日状态唯一Owner，提供RememberDailyAdd/DailyAddsFor/RememberDailyLegacy/DailyLegacyAdds。Flow.LeaveShop在通关成长与清临时后统一ClearDailyEffects；Win/Lose也清理。Settlement重用同一日状态，无补偿全场遍历。
- RunModel.LastOperationFailure 是操作失败结果状态；OperationFeedbackQuery向UI只读公开。成功操作清空，满仓/预约/普通性格/重复/上限给明确原因。
- Flow.GenerateSingleMonster 按性格→种族→非增幅单技能（先稀有度后档内）生成，供外接胚胎调用。该入口不注入额外玩家资源。
- Flow.Lay 和 ExtractionBench 使用明确隔离规则夹具，不属于自然七天验收。接口随机种子在Bench.Prepare独立注册，不消费Unity对局随机。

旧测试随机夹具ScriptedDraw/RecordingDraw/PinnedDraw/BreedingDraw已显式兼容新增稀有度抽样步骤；保留原具体输入和有效断言。BreedingDraw明确在LeaveShop前启用继承选择，避免小档内商品池误消费亲本选择队列。旧孤独心已退役，涉及继承重抽的该输入改为正式回响嗓；不使用未知技能作为正确性夹具。

运行命令：

```powershell
dotnet test kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj --filter FullyQualifiedName~LifecycleRemediationTests --logger "trx;LogFileName=lifecycle-final.trx" --results-directory docs/audit/2026-10-12/remediation
dotnet test kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj
```

不在本记录宣称：Unity动态容量/投放命中/真实图标、完整42种组合画面、Page使用体验、自然七天、正常快进跳过全状态比较。其结果必须由对应正式证据另行证明。

后续完整基础机制映射：[非结算内容规则索引](lifecycle-content-index.md)。

安全窗口来源补强：好学直接抽取目标SkillInstance，SourceId记录实际复制的实例ID；`Content_好学复读来源记录实际抽中的第二实例且不继承其成长`明确抽第二同名实例、成长100不继承、复制基础报价5。固定心情复制三个输入同时断言来源实例ID。Assets已重新冻结。
