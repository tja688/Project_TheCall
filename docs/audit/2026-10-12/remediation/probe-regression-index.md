# 36条原规则探针到正式断言的迁移

原probe只打印，不能称为36/36测试通过。这里保存每条修前观察及期望，并指向正式NUnit；`settlement-final.trx`是实际断言执行证据。gene-provenance-trim原输出没有候选来源身份，仅数字无矛盾；新的身份裁切反例才证明基因消费正确。

|原探针|修前分类|规格期望/增强边界|正式回归|
|---|---|---|---|
|baseline|正确基线|5|SkillProductionHasIndependentBasicFixture(能量体,5)|
|new-active-exec|错误反例|当前肌肉取得新主动后本次不执行，下一轮有效|A11_NewActiveIsDeferredButNewAuraIsImmediate|
|muscle-after-clear|错误反例|新得技能实际临时，日清后只留原常驻|F01_取得实例身份来源成长及临时清理独立; SkillSupportAndReactionHasIndependentBasicFixture(无穷肌肉)|
|corpse-echo|错误反例|自爆10，收尸3、3|F05_EchoRepeatsCorpseResponseForOneDeath|
|honey-gap|正确基线|3、8|蜜能量体落地三点且只给空档后的下一只后续产能量计分加三|
|mind-negative|错误反例|自私-3经仪-2，目标4|F14_MindControlCorrectsNegativePersonalityAmount|
|kin-positive|错误反例|父子各10，陌生5|F10_KinAffectsBothEndpointsAndExcludesStranger(重视血脉,10)|
|kin-negative|错误反例|父子各2，陌生5|F10_KinAffectsBothEndpointsAndExcludesStranger(仇恨血脉,2)|
|single-tool|正确基线|10、10|SingleSkillDeviceAddsOneTriggerAndDoublesEachOutput|
|breeding-duplicate|错误反例|复读两个亲本同名保留独立候选|F07_同名亲本与基因裁切按候选身份决定消费|
|initial-fixed-mood|错误反例|初生即热情|F09_初生固定性格即时覆盖普通心情(乐天派,热情)|
|preview-isolated|错误反例|预览与执行10|F17_PreviewAndExecutionSharePersonalityAndMoodFormula(孤僻,普通,10)|
|per-skill-lock|错误反例|5、5、10共20|F02_ContentedLocksEachSkillBeforeSelfDestruct|
|expanded-discard-drag|错误反例|容量4第四次拖放成功；满含预约时拒绝不改变|F08_仓储科技第四格可废弃满仓含预约原子拒绝|
|expanded-discard-direct|正确基线|直接废弃有空间成功|F08_仓储科技第四格可废弃满仓含预约原子拒绝|
|install-drag|错误反例|迷恋死亡合法拖放成功，普通拒绝|F08_正式拖放安装遵循重复许可并只成功消费|
|install-command|正确基线|已合法的Command保持|A17_基因安装只有迷恋死亡合法成功才消耗普通性格拒绝失败状态与材料不变|
|gene-provenance-trim|正确基线|原基线仅打印名字/数量不能分辨同名来源；新回归强制分别保留亲本与基因，并按实例决定退还/消费|F07_同名亲本与基因裁切按候选身份决定消费|
|opening-fixed-mood|错误反例|固定性格立即热情|F09_初生固定性格即时覆盖普通心情; Content_好学经真实开工获得常驻技能并即时复制固定性格心情|
|preview-impatient|错误反例|预览与执行8|F17_PreviewAndExecutionSharePersonalityAndMoodFormula(急躁,普通,8)|
|preview-mood|错误反例|预览与执行7|F17_PreviewAndExecutionSharePersonalityAndMoodFormula(稳定,热爱,7)|
|sacrifice-per-target|错误反例|收尸7，血祭12、12共31|A15_左祭品中央血祭右收尸按序死亡且遗赠即时生效且光环撤销|
|devour-no-target-buff|错误反例|无目标即0，急躁不能凭空加3|F15_NoTargetDoesNotProducePersonalityEnergy|
|honey-round1|正确基线|首轮3、8|蜜能量体的加三不写进技能报价且加班当天保留|
|honey-round2|错误反例|收回蜜后加班仍8|蜜能量体的加三不写进技能报价且加班当天保留|
|echo-active-order|错误反例|蜜、蜜、能量、能量|F02_EchoExecutesEachActiveInstanceCompletely|
|duplicate-mirror|错误反例|两源实例×2×2=20|F06_DuplicateMirrorInstancesMultiplyIndependently|
|duplicate-extra|错误反例|每增生实例两次，总四次|F06_DuplicateProliferationReceivesOneGrantPerInstance|
|opening-aura-target|错误反例|镜眼保留开场目标，不追改换位后邻居|F03_MirrorRetainsOpeningTargetAfterSwap|
|opening-skillcount|错误反例|第四技能取得后开场三技能资格保持，原能量两次|F03_QualityEligibilityUsesOpeningInstanceCountAfterCopy|
|death-listener-position|错误反例|左收尸先3，右遗赠后抽杀14|F04_DeathDispatchCombinesLeftListenerAndRightLegacyByPosition|
|log-no-excess|错误反例|50科技0，日志0|F17_NoExcessProducesZeroTechLogAndStableEventIds|
|log-cumulative|错误反例|每轮25，带入10/35，累计35/60，支付50，工资40，带2|F17_OvertimeLedgerSeparatesInitialNetAndCumulative|
|monster-scope-global-tool|错误反例|香不加无归属电化，仍10|F11_MonsterTargetAuraDoesNotAddToUnownedTool|
|legacy-first-event-global-tool|正确基线|电化先10；血祭灭祭品后12；原基线此输入已正确，不能据它证明后续无归属工具范围|MachineProducesUnownedEnergyInItsOwnEntry; SkillSupportAndReactionHasIndependentBasicFixture(祭品腺)|
|copy-with-duplicate-tool|错误反例|实际复制路径已使用复读许可与临时取得；不同反例分别覆盖独立实例、取得上限和来源|F06_DuplicateProliferationReceivesOneGrantPerInstance; F01_取得实例身份来源成长及临时清理独立; F07_同名亲本与基因裁切按候选身份决定消费|
