# 结算规则整改索引

此文件只报告结算规则工作包。Unity、Page真实操作和无作弊七天结项由主控最终证据包判定。

## 修复及正式反例

实现均在 `Assets/Scripts/Core/Rules/SettlementSystem.cs` 增量修改，新增回归在 `Assets/Scripts/Core/Tests/SettlementRemediationTests.cs`。

|问题|改动与验证|
|---|---|
|F02|WalkSkills开始固定主动实例列表、逐实例锁次数并完整重复；新主动延后。`F02_ContentedLocksEachSkillBeforeSelfDestruct`断言5/5/10=20；`F02_EchoExecutesEachActiveInstanceCompletely`断言蜜/蜜/能量/能量；`A11_NewActiveIsDeferredButNewAuraIsImmediate`实际肌肉取得临时主动及奇异香，当前主动不补、光环立即给右目标6、加班新主动出现。|
|F03|开场格子及每怪物实例数独立冻结；镜眼读取开场目标并查来源存活；优质/良好/劣胜按开场数。`F03_MirrorRetainsOpeningTargetAfterSwap`被交换者两笔10、换位者5；`F03_QualityEligibilityUsesOpeningInstanceCountAfterCopy`取得第四实例仍给原能量体两次，临时新主动不补。|
|F04|统一死亡清单在发出事件时一次冻结所有监听实例；遗赠与存活监听按原位置/卡内順序；血祭/血崩逐只死亡反应后产能。修正原A15错误20断言，逐笔7/12/12=31；`F04_DeathDispatchCombinesLeftListenerAndRightLegacyByPosition`左监听先3、后右遗赠作用抽杀14；反向`F04_LeftListenerReactsBeforeRightVictimLegacy`血祭8/12。|
|F05|死亡与能量被动复用TriggerGrants，传能耳匹配主动事件也锁额外次数。`F05_EchoRepeatsCorpseResponseForOneDeath`收尸3/3；原A12两汲取同名阻断回归保留。|
|F06|赠次数使用SkillInstance.Id，镜眼逐实例倍率、执行记录实例ID、急急只绑定第一实际实例；复制遵复读许可。`F06_DuplicateMirrorInstancesMultiplyIndependently`20且两个源实例ID；`F06_DuplicateProliferationReceivesOneGrantPerInstance`每实例两次共四次；`F06_FirstEnergyToolBindsOnlyFirstDuplicateInstance`预览及执行10/5；原A10保留1/2/4。|
|F10|直接亲本关系双向、每个性格持有源独立追加；陌生同名排除。`F10_KinAffectsBothEndpointsAndExcludesStranger`重视父子各10/陌生5，仇恨父子各2/陌生5；`F10_TwoKinPersonalitySourcesStackIndependently`双源父子各15。|
|F11|其他怪物能量光环需目标有怪物归属，工具全场加成仍合法。`F11_MonsterTargetAuraDoesNotAddToUnownedTool`奇异香+电化10；原A03奇异香+自律6、检测仪+电化11保留。|
|F13|已发蜜/传能耳/遗赠放入RunModel日效果唯一Owner，来源回笼/死亡后已发金额保留，加班不清；Flow过日清。修正LandingResponseTests原错误当轮失效断言为加班保留8，不改技能Quote。|
|F14|精神操控每笔性格正负加减+1；合群按策划“每有一个相邻怪物+1”逐邻记笔。`F14_MindControlCorrectsNegativePersonalityAmount`自私-2、最终4；`F14_SociableEachNeighborIsOnePersonalityContribution`两笔2、最终10。|
|F15|无合法目标啃食显式0且不应用加成，血崩无目标未中；抽杀固定10保留。`F15_NoTargetDoesNotProducePersonalityEnergy`急躁+空啃食0；原A16零目标与抽杀10保留。|
|F17|Land仅提交共享EvaluateLanding纯计算结果；Preview使用隔离System上下文，不写Models、不抽随机；SkillReadout旧独立平加/倍率计算已删除。`F17_PreviewAndExecutionSharePersonalityAndMoodFormula`急躁8/热爱7/孤僻10；`F17_PreviewDoesNotConsumeRandomOrChangeRuleState`无随机消费与模型修改。日账本Produced净增、InitialEnergy带入、AccumulatedEnergy支付前累计，日志实际0科技。`F17_OvertimeLedgerSeparatesInitialNetAndCumulative`25/25、10/35、35/60、支付50、工资40、保留2、科技1；`F17_NoExcessProducesZeroTechLogAndStableEventIds`50无科技且事件ID/序号连续。|
|A28|`A28_NewDeathListenerStartsOnSecondDeathOnly`真实移植从death1取得临时收尸，death1不补，death2仅一笔3；笼和培育监听0。|

## 共享合同

- `SettlementSystem.Preview(monsterId, skillName, quote, SettlementSight sight = null, string skillInstanceId = null)`纯只读，返回`SettlementLanding`。预览代表成功分支，不掷骰；`ProbabilityPercent`、`MinimumEnergy`、`MaximumEnergy`表达概率范围，随心所欲20%失效与技能概率相乘。
- `SettlementSystem.EvaluateLanding`由执行及预览共用，技能增长报价、Mood、目标范围、独立过滤、依赖、人格、工具、倍率共享。
- `SettlementEntry.EventId/Sequence`在最终Seal后统一赋值；ID含天、轮类、顺序。`SettlementLanding.SkillInstanceId/LockedTimes/OpeningCell/OpeningSkillCount`标明执行实例与本轮资格；`SourceKind/SourceToolId`识别无怪物工具。
- `LandingAdd/Factor.SourceMonsterId/SourceSkillInstanceId/SourceToolId/SourceKind`供表现定位；保留兼容`SourceId/SourceSkillIndex`。可选显式sourceKind用于Growth等非性格修正。显示名只供文本。
- `SettlementRemoval.Cell/SkillInstanceIds/Personality/SourceId/SourceSkillInstanceId`保存死亡与因果快照。
- `ProductionSubmission.InitialEnergy/Produced/AccumulatedEnergy`分别带入、本轮新增、支付前累计；Payment继续提供扣需求、工资、科技标记与保留。
- `SettlementSight.Capture`复制技能实例/成长/临时性及怪物属性，避免模型终局写入污染开場悬停。
- RunModel的`RememberDailyAdd/DailyAddsFor/RememberDailyLegacy/DailyLegacyAdds/ClearDailyEffects`由生命周期工作包提供；结算使用该Owner，无第二份日状态。

## 验证证据

执行命令：`dotnet test kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj --no-restore --logger "trx;LogFileName=settlement-final.trx" --results-directory docs/audit/2026-10-12/remediation --verbosity quiet`。

修前行为证据：`../evidence/rule-probe-baseline.jsonl`与质检报告。整改中正式测试红阶段诊断：`../evidence/remediation/settlement.xml`（旧A15单笔断言及旧蜜当轮清除等失败），后续`remaining.xml/latest.xml`保留中间集成诊断。最终通过输出以本目录最新`settlement-final.trx`为准；主控仍会在全部改动完成后统一重新执行，因此不把子代理较早测试总数当最终结项数量。

合群粒度已据正式策划性格表逐邻断言。好学/严选基础心情判定由生命周期工作包处理，不在这里代替其证据。

最终结算工作包源码冻结后，全规则412/412通过（同目录settlement-final.trx）。补齐34技能逐项机制、5族和5心情及结算性格/工具基例后，93项基础规则映射见content-rule-index.md/json。A子条件核对见acceptance-rule-subconditions.md；其中本人不动、非共同亲缘技能独立反例及实际播放器证据需主控收口，未伪称完整UI验收。
