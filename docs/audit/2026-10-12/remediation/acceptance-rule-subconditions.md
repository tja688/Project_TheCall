# A01—A28规则子条件核对

这是规则范围核对；实际玩家、资产和播放器证据必须并入主控最终矩阵。已识别的独立断言缺口明确列出，不以全绿总数关闭。

|编号|必须子条件|具体回归|结论与边界|
|---|---|---|---|
|A01|三候选/1双8单/总9/未选消失/性格种族合法|PlayerFlowM2Tests.A01；Lifecycle F12_A27边界及五族池|规则通过；真实开局由主控补证|
|A02|基础5仅一次/加3/依赖变化双倍/倍率2=22/负值截0|SettlementEngineM1Tests.A02；SettlementRemediation F17公式三例|规则通过|
|A03|香+自律6/金实例检测+电化11/目标与来源明确|SettlementEngineM1Tests.A03；F11_MonsterTargetAuraDoesNotAddToUnownedTool|规则通过|
|A04|工作2天下降/休息回升/加班一次计日/摆放取回无工作|SettlementEngineM1Tests.A04；Lifecycle Content_工作性格实际两天及隔音棉|规则通过|
|A05|固定档不改/隔音防降/严选一天快照/普通及以上收割+2|Lifecycle F09初生及改变、Content_好学加严选、隔音棉；ContentMechanism情绪收割|规则通过|
|A06|初始10/本轮25各轮/累计35与60/扣50/科技1/工资40/保留2|F17_OvertimeLedgerSeparatesInitialNetAndCumulative；原A06|规则通过；三播放器解释由主控补证|
|A07|50无科技/60有科技/两轮不足失败不商店/携带够仍确认|SettlementEngineM1Tests.A07；F17_NoExcessProducesZeroTechLogAndStableEventIds|规则通过；自然失败和玩家确认由主控补证|
|A08|首次成功开工/换位取回不重触发/空目标失败不重试/改性格不重计|原A08；Lifecycle F09笼内安装、Content_好学成功失败/严选|规则通过|
|A09|实际肌肉/移植临时取得/性格常驻取得/加班保留/过日仅清临时/成长不遗传|A11_NewActiveIsDeferredButNewAuraIsImmediate；A28_NewDeathListenerStartsOnSecondDeathOnly；Lifecycle F01成长身份及好学常驻；F07_A18三亲本成长|实际效果取得、跨加班及清理接口规则通过；完整跨日玩家轨迹由主控补证|
|A10|三个增生1/2/4/同卡复读各实例/有限结束/新轮不积存|原A10；F06_DuplicateProliferationReceivesOneGrantPerInstance；A10_ProliferationRoundGrantsResetBeforeActualOvertime|规则通过|
|A11|当前主动列表固定/实际肌肉新主动延后/新光环立即/加班可用|A11_NewActiveIsDeferredButNewAuraIsImmediate；F03_QualityEligibilityUsesOpeningInstanceCountAfterCopy|规则通过|
|A12|回响汲取每事件2/两汲取及重复同名阻断/仅匹配被动|原A12；LandingResponseTests两汲取同名、CapacityTests左右复制；F05回响收尸|规则通过|
|A13|立即换位/插入目标当前主动各1/换位者剩余/普通身份不遗漏/开场光环不追改|原A13四笔身份顺序；F03_MirrorRetainsOpeningTargetAfterSwap；SwapAndClockTests插入及秒点旧有效回归|规则通过；实际动画源定位由主控补证|
|A14|左邻不动/本人不动/无邻均拒绝/本人得当天不动/不插入目标|原A14；SwapAndClockTests目标不动及无邻；F02_SourceDeathPreservesLockedTimesButRemovesLaterGrants|已有目标不动/无邻明确断言；本人不动拒绝存在实现路径，若最终矩阵要求独立反例须主控补测，不能借上述不相关F02认领|
|A15|左死亡右监听7/随后每目标12/合计31/遗赠与监听按左右位置竞争/光环死后停止|修正原A15逐笔断言；F04_DeathDispatchCombinesLeftListenerAndRightLegacyByPosition；F04_LeftListenerReactsBeforeRightVictimLegacy|规则通过；高组合演出由主控补证|
|A16|自爆先到帐后死亡停余动作/仅实际死亡一次/数量空目标0/抽杀固定10/废弃不触发/磨机5|原A16；F02_ContentedLocksEachSkillBeforeSelfDestruct；F15_NoTargetDoesNotProducePersonalityEnergy；Lifecycle Content_研磨机失败不发钱|规则通过|
|A17|迷恋合法安装/普通拒绝/复读成功/不复读重复及满上限失败/失败原子/预约不可偷用|Lifecycle F08合法拖放、普通上限预约拒绝、材料身份；原PlayerFlow A17|规则通过；真实组件命中链由主控补证|
|A18|1亲本无子/2与3亲本保留/性格先定/基因变异候选齐备后公平裁切/留基因才消费|BreedingTests1/2亲本；Lifecycle F07_A18三亲本+基因+变异及F07同名来源裁切|规则通过；扩容三亲本真实画面由主控补证|
|A19|清临时后取样/不携成长/默认碰撞重抽及来源不足合并/复读独立来源/直接亲缘准确|原PlayerFlow A19；Lifecycle F07碰撞各来源链重抽、F07_A18成长；F10双向亲缘反例|规则通过|
|A20|预约计容量/材料单份不可复用/原格退还/满仓拒绝不丢/未遗传不消费|Lifecycle F08预约基因原格与身份；F07同名亲本基因随机裁切；原A20|规则通过；原位可见UI由主控补证|
|A21|取得全源复读许可/实例独立ID成长临时/总上限/安于现状随机合法2且含常驻|Lifecycle F01身份、F16五组合、F01商店复读、Content_复读4上限、F07来源；F06镜眼增生急急实例|规则通过；来源跨项依上述具体组合分别认领|
|A22|六科技每项1点无前置仅1次/零点拒绝/仓储3→4与加号同Owner|Lifecycle Content_六项科技逐项TestCase；原PlayerFlow A22|规则通过；真实六按钮及加号由主控补证|
|A23|已购排除/架内不重名/不足少显示/失败刷新不变/新货位即时补/合同不折扣自身|原PlayerFlow A23；Lifecycle Content_招工、验货、批发合同；ShopTests失败刷新等原有效用例|规则通过；真实库存与工具压力由主控补证|
|A24|相生两方向/中间2对/空格非相邻/五族均实际基例|原A24；ContentMechanism五条EachRaceHasActualGeneratingNeighborBonus|规则通过|
|A25|父子双向各10或2/陌生同名5/双性格源各独立/非共同技能不联动|F10_KinAffectsBothEndpointsAndExcludesStranger；F10_TwoKinPersonalitySourcesStackIndependently；PlayerFlow A25|规则基础与双向反例通过；非共同技能独立反例须最终矩阵核对，不以名字相同用例代替|
|A26|同随机输入正常/快进/跳过，全部领域状态和随机游标相等，末帧可交互|ScoringPlaybackM4Tests规则记录合同基础；真正三入口播放器由主控Unity证据|领域基础通过；实际播放器/随机游标/末帧交互仍需主控最终证据，规则重复算两次不认领实际播放|
|A27|70/20/10与50/20/20/10边界/档内均匀/功能排重空档归一/售罄及已购排除/随机次数|Lifecycle F12_A27技能五边界、人格六边界及两次消费、五族池、功能空档；原工具权重A27|规则通过，固定种子统计仅辅助|
|A28|death1实际得到收尸或移植/新监听不补death1/death2生效/笼和培育不监听|A28_NewDeathListenerStartsOnSecondDeathOnly；A28_NewTransplantInstanceStartsOnlyOnNextDeath|规则通过|
