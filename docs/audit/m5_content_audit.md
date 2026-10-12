# 里程碑 M5 全量内容审计报告 (Milestone M5 Content Audit Report)

> [!NOTE]
> 本报告基于 Issue #22 与 Spec #16（新版策划迁移），对《The Call》项目的六大内容领域进行全面、严密的静态与动态审计。
> 涵盖全部 **34 个技能**、**25 个性格**、**18 个工具**、**6 项科技**、**5 大种族**以及 **5 档心情**。

## 审计概述与验证结论

| 审计分类 | 规格要求数量 | 实装有效数量 | 稀有度与档位分布 | 自动化测试状态 | 规则引擎对接状态 |
| :--- | :---: | :---: | :--- | :---: | :---: |
| **技能库 (Skills)** | 34 | 34 | 16 白 / 14 蓝 / 4 金 | 100% 通过 (14/14) | 完全实装 (`SettlementSystem`) |
| **退役技能 (Retired)** | 5 | 0 (已清理) | 5 项已彻底退出抽取池并平移机制 | 100% 通过 | 已安全移除与平移 |
| **性格库 (Personalities)** | 25 | 25 | 12 白 / 5 蓝 / 5 负面 / 3 金 | 100% 通过 | 完全实装 (`MoodCatalog`/`Settlement`) |
| **工具库 (Tools)** | 18 | 18 | 10 白 ($30) / 5 蓝 ($50) / 3 金 ($80) | 100% 通过 | 完全实装 (`Shop`/`ToolRules`) |
| **科技库 (Techs)** | 6 | 6 | 6 项均为 1 科技点 | 100% 通过 | 完全实装 (`TechCatalog`) |
| **种族相生相克 (Races)** | 5 | 5 | 森林、天空、沙漠、山脉、海生 | 100% 通过 | 相生相克闭环校验通过 |
| **心情档位 (Moods)** | 5 | 5 | 倦怠(-2) / 疲倦(-1) / 普通(0) / 热情(+1) / 热爱(+2) | 100% 通过 | 阶梯计算与防降逻辑健全 |

---

## 一、技能系统审计 (34 Skills & 5 Retired Skills)

### 1.1 退役技能迁移与清理确认
根据 Spec #16 说明，5 个旧技能已完全退役，不得出现在 `call-book.json` 正式抽取池或图鉴中：

- **吞噬大嘴**: 退役。旧版吞食产出机制已被新版白技能「啃食体」（`DevourLeftNeighborSkillMultiple`）及死亡结算联动替代。
- **争锋基因**: 退役。从正式池中永久移除。
- **潜藏基因**: 退役。从正式池中永久移除。
- **重血脉心**: 退役并机制平移。原技能效果平移为白性格「重视血脉」（亲本与直接子代同场时相同技能触发次数 +1）。
- **孤独心**: 退役并机制平移。原技能效果平移为白性格「孤僻」（无相邻怪物时能量翻倍）。

自动化测试断言 `五个退役技能不得出现在技能库中` 经测试确保上述 5 个名称均不在技能库中。

### 1.2 全量 34 个有效技能详表

| 序号 | 技能名称 | 稀有度 | 类型 / 功能池 | 词条 | 效果机制 (Effect Kind & Values) | 游戏内描述文案 (SkillSentences) | 规则引擎实装位置 |
| :---: | :--- | :---: | :--- | :---: | :--- | :--- | :--- |
| 1 | **能量体** | White | Active (Produce) | 无 | `EnergyQuote(quote=5)` | 产生5点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L545): `BaseQuote` 读取基础报价 5 点能量。 |
| 2 | **左能量体** | White | Active (Produce) | 无 | `SideCount(side=Right, perMonster=2)` | 右侧每有一个怪物产生2点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L502): `CountSide(cells, cell, CountedSide.Right) * 2` 计算右侧怪物数量并产出能量。 |
| 3 | **右能量体** | White | Active (Produce) | 无 | `SideCount(side=Left, perMonster=2)` | 左侧每有一个怪物产生2点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L502): `CountSide(cells, cell, CountedSide.Left) * 2` 计算左侧怪物数量并产出能量。 |
| 4 | **双头能量体** | White | Active (Produce) | 无 | `EnergyQuote(quote=2)`<br>`RepeatQuote(times=2)` | 产生2点能量两次 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L596): `ExecuteRepeatQuote` 单次技能执行内部产生 2 点能量连续 2 笔记录。 |
| 5 | **蜜能量体** | White | Active (Produce/Support) | 无 | `EnergyQuote(quote=3)`<br>`NextEnergyBonus(amount=3)` | 产生3点能量，下一个怪物的能量值+3 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L622): 基础报价 3，并向右侧紧邻下一个怪物传递 `_nextEnergyBonus = 3`。 |
| 6 | **优秀能量体** | White | Active (Produce) | 无 | `OwnSkillMultiple(factor=2)` | 产生等同于自身技能数两倍的能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L495): `monster.Skills.Count * 2` 按宿主实际技能实例数倍率产出。 |
| 7 | **增生能量体** | White | Active (Produce) | 无 | `EnergyQuote(quote=1)`<br>`SameNameExtra` | 产生1点能量，使用本技能后，其他怪物同名技能触发次数+1 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L616): 产出 1 点能量，并向后续其他增生能量体广播额外触发次数 +1。 |
| 8 | **自爆体** | White | Active (Produce) | Destroy | `EnergyQuote(quote=10)`<br>`SelfDestruct` | 产生10点能量，消灭本怪物 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L425): 先产出 10 点能量，随后调用 `DestroyMonster` 消灭自身并触发死亡监听。 |
| 9 | **奇异香** | White | Passive (Support) | 无 | `AddToOthers(amount=1)` | 其他怪物产生的能量数值+1 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1815): 光环遍历，提取槽中其他怪物的产能基础增益 `+1`。 |
| 10 | **传能耳** | White | Passive (Support) | 无 | `RightNeighborOnLeftActive(amount=2)` | 左侧相邻怪物每触发一个主动技能，其右侧相邻怪物的能量值+2 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1870): 监听左侧相邻怪物的主动触发，给其右侧相邻怪物附加 `+2` 能量。 |
| 11 | **优质肉体** | White | Passive (Amplify) | 无 | `SkillCountExtra(when=3, walks=1)` | 当本怪物仅有三个技能时，本怪物其他技能触发两次 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1640): 检查宿主常驻+临时技能总数为 3 时，赋予其他技能额外 1 轮完整执行。 |
| 12 | **良好肉体** | White | Passive (Amplify) | 无 | `SkillCountAdd(when=2, amount=3)` | 当本怪物仅有两个技能时，本怪物产生能量的技能能量数值+3 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1785): 检查宿主技能总数为 2 时，为产生能量的技能提供 `+3` 能量加成。 |
| 13 | **情绪收集器** | White | Active (Produce) | 无 | `PersonalityCountMultiple(factor=2)` | 产生等同于提取槽不同性格数量两倍的能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L514): 统计全场提取槽中去重性格总数，产出 `distinctPersonalityCount * 2` 点能量。 |
| 14 | **收尸腺** | White | Passive (Produce) | 无 | `CorpseEnergyQuote(quote=3)` | 其他怪物被消灭时，本怪物产生3点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L920): 注册被动死亡监听，每当其他存活怪物被消灭时即刻产出 3 点能量。 |
| 15 | **啃食体** | White | Active (Produce) | Destroy | `DevourLeftNeighborSkillMultiple(factor=4)` | 消灭左侧相邻怪物，产生等同于其技能数四倍的能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L435): 消灭左侧相邻怪物，按其死亡前技能总数乘以 4 产生能量；无左邻产 0。 |
| 16 | **抽杀体** | White | Active (Produce) | Destroy | `EnergyQuote(quote=10)`<br>`KillRandomOther` | 随机消灭提取槽中的一只其他怪物，产生10点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L450): 产生固定 10 点能量，并随机消灭提取槽中一只其他存活怪物。 |
| 17 | **群能量体** | Blue | Active (Produce) | 无 | `PopulationQuote` | 产生等同于怪物总量数值（培育槽+提取槽+怪物笼）的能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L533): 查询玩家当前拥有的全部怪物总量（培育+提取+笼），产出等量能量。 |
| 18 | **生长能量体** | Blue | Active (Produce) | Permanent | `EnergyQuote(quote=2)`<br>`GrowOnClear(amount=3)` | 产生2点能量，每个通关阶段后，该技能的能量值永久+3 | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs): 基础 2 点能量；关卡通过（Wage/Clear）时遍历宿主技能实例，永久累加数值 +3。 |
| 19 | **不稳定能量体** | Blue | Active (Produce) | 无 | `ChanceQuote(percent=50, quote=20)` | 50%概率产生20点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L555): 独立判定 50% 几率掷骰，成功产出 20 点能量，失败产出 0。 |
| 20 | **怪异香** | Blue | Passive (Support) | 无 | `AddToOthers(amount=2)` | 其他怪物产生的能量数值+2 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1815): 光环被动，为提取槽中所有其他怪物每次能量产出提供 `+2` 点加成。 |
| 21 | **左复制腺体** | Blue | Passive (Support) | 无 | `CapacityExtraForLeftNeighbor(layers=1)` | 左侧相邻怪物，产生能量的触发次数+1 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1664): 光环被动，锁定左侧相邻怪物的产能技能时使其触发次数 +1。 |
| 22 | **右复制腺体** | Blue | Passive (Support) | 无 | `CapacityExtraForRightNeighbor(layers=1)` | 右侧相邻怪物，产生能量的触发次数+1 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1668): 光环被动，锁定右侧相邻怪物的产能技能时使其触发次数 +1。 |
| 23 | **换位手** | Blue | Active (Support) | Immovable | `SwapWithLeft` | 与左侧相邻怪物交换位置，之后获得不动 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L380): 主动交换左邻位置，触发被交换目标主动技能插入执行一次，赋予自身当天不动。 |
| 24 | **汲取鼻** | Blue | Passive (Produce/Support) | 无 | `LandingResponse(quote=1)` | 其他怪物产生能量时，本怪物产生1点能量（不会因为同名技能效果产生能量） | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L641): 监听全场产能事件，每次其他怪物能量产生时响应产出 1 点，严格排除汲取鼻同名连锁。 |
| 25 | **外接胚胎** | Blue | Passive (Economy) | Legacy | `LegacyOnDiscard` | 遗赠将一个随机单技能怪物放入怪物笼 | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs): 遗赠被动；宿主在结算中消灭时，随机生成一只单技能新怪物放入怪物笼。 |
| 26 | **产金管道** | Blue | Active (Economy) | 无 | `GainGold(amount=5)` | 获得5点金币 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L570): 执行时即刻向 `RunModel.AddGold(5)` 写入 5 金币并生成结构化记录。 |
| 27 | **不稳定肉体** | Blue | Active (Produce/Support) | 无 | `CopyBreeding` | 从每个培育笼中的怪物已有技能中随机获取一个技能 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L562): 遍历当前培育槽亲本，随机抽取其技能加入宿主作为当天临时技能。 |
| 28 | **祭品腺** | Blue | Passive (Support) | Legacy | `LegacyAddToOthers(amount=4)` | 遗赠其他怪物的能量值+4 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L930): 遗赠被动；宿主结算死亡后，全场其他存活怪物获得当场 `+4` 能量加成。 |
| 29 | **移植胚胎** | Blue | Passive (Support) | Destroy | `StealSkillOnOtherDestroyed` | 其他怪物被消灭时，获得它拥有的1个随机技能 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L940): 死亡监听；其他怪物被消灭瞬间，从其死前技能列表中随机偷取 1 个技能至自身。 |
| 30 | **血祭体** | Blue | Active (Produce) | Destroy | `SacrificeNeighborsPerMonsterEnergy(perMonster=8)` | 消灭左右两侧的相邻怪物，每消灭1只，产生8点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L465): 依次消灭左右相邻两只怪物，每成功消灭一只即刻结算反应并产出 8 点能量。 |
| 31 | **回响嗓** | Gold | Passive (Support) | 无 | `ExtraWalksForRightNeighbor(walks=1)` | 右侧相邻怪物的所有技能效果都触发两次 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1658): 光环被动；右侧紧邻怪物在轮到执行时锁定所有技能多执行一次（ExtraWalks = 1）。 |
| 32 | **镜眼** | Gold | Passive (Support) | 无 | `DoubleAdjacentEnergy` | 相邻怪物产生的能量翻倍 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L715): 光环被动；左右相邻怪物每次产能的总倍率乘以 2。 |
| 33 | **无穷肌肉** | Gold | Active (Produce/Support) | 无 | `FillSkills` | 随机获得技能，直到达到本怪物技能上限 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L575): 主动执行时连续抽取随机技能填补宿主空槽，直至达到实际上限（普通 4，安于现状 2）。 |
| 34 | **血崩体** | Gold | Active (Produce) | Destroy | `CullUntilAlonePerMonsterEnergy(perMonster=10)` | 随机消灭提取槽中的一只其他怪物，之后重复此效果，直到提取槽中只剩本怪物为止；每消灭1只，产生10点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L480): 循环消灭一只随机其他怪物并产出 10 点能量，直至提取槽只剩自身；无其他目标产 0。 |

---

## 二、性格系统审计 (25 Personalities)

### 2.1 稀有度与类型分布
- **白性格 (12 个)**: 稳定、孤僻、重视血脉、合群、自私、独立、恋爱脑、自律、安于现状、贪财、急躁、稳重
- **蓝性格 (5 个)**: 乐天派、热爱工作、乐于助人、依赖、好学
- **负面性格 (5 个)**: 摸鱼派、厌恶工作、厌世、仇恨血脉、随心所欲
- **金性格 (3 个)**: 好奇、上进心、迷恋死亡
- **退役迁移落地**: 孤独心成功迁移为「孤僻」(White)，重血脉心成功迁移为「重视血脉」(White)。

### 2.2 全量 25 个性格详表

| 序号 | 性格名称 | 稀有度 | 类型 (Kind) | 效果与文案描述 | 规则引擎实现细节 |
| :---: | :--- | :---: | :---: | :--- | :--- |
| 1 | **稳定** | White | Aura | 心情总是处于普通 | [MoodCatalog.cs](file:///Assets/Scripts/Core/Rules/MoodCatalog.cs#L122): 固定覆盖心情为「普通」（0 点能量），不受连续工作或休息影响。 |
| 2 | **孤僻** | White | Aura | 当相邻没有其他怪物时，本怪物的能量值翻倍 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L706): 宿主在提取槽中左右均无相邻怪物时，每次产出能量翻倍（倍率 x2）。 |
| 3 | **重视血脉** | White | Aura | 当子代与亲本一起在提取槽时，亲本与子代相同的技能触发次数+1 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1672): 提取槽中亲本与直接子代同场时，二者相同技能触发次数 +1。 |
| 4 | **合群** | White | Aura | 每有一个相邻怪物，本怪物的能量值+1 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1808): 提取槽中每有一个相邻怪物，宿主产生能量数值 +1（最多 +2）。 |
| 5 | **自私** | White | Aura | 相邻的其他怪物能量值-3，本怪物的能量值翻倍 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L712): 相邻其他怪物能量数值 -3，宿主自身产生能量数值翻倍。 |
| 6 | **独立** | White | Aura | 本怪物的能量值不会被其他怪物的技能改变（只挡其他怪物的技能，不挡性格与工具） | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L730): 阻挡其他怪物技能对其能量的增减或倍率修正；不阻挡性格与工具加成。 |
| 7 | **恋爱脑** | White | Aura | 本怪物只与一个怪物相邻时，本怪物与该怪物的能量值+2 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1811): 宿主仅与单侧 1 只怪物相邻时，宿主与该相邻怪物产生能量数值各 +2。 |
| 8 | **自律** | White | Trigger | 结算阶段时，产生 5 点能量 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L131): 结算阶段开始时，作为独立性格产出事件直接产生 5 点能量。 |
| 9 | **安于现状** | White | Aura | 本怪物拥有的技能上限为 2，每个主动技能触发次数+1 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1651): 技能实际上限强行限制为 2；每个主动技能触发次数 +1。 |
| 10 | **贪财** | White | Trigger | 本怪物每触发一次主动技能就获得 1 金币 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L349): 宿主每尝试触发一次主动技能，即刻调用 `AddGold(1)` 获得 1 金币。 |
| 11 | **急躁** | White | Aura | 当本怪物为提取槽最左侧怪物时，本怪物的能量值+3 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1802): 当宿主位于提取槽最左侧（索引最小且在场）时，能量数值 +3。 |
| 12 | **稳重** | White | Aura | 当本怪物为提取槽最右侧怪物时，本怪物的能量值+3 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1805): 当宿主位于提取槽最右侧（索引最大且在场）时，能量数值 +3。 |
| 13 | **乐天派** | Blue | Aura | 心情总是处于热情 | [MoodCatalog.cs](file:///Assets/Scripts/Core/Rules/MoodCatalog.cs#L124): 固定覆盖心情为「热情」（+1 点能量），忽略历史出勤状态。 |
| 14 | **热爱工作** | Blue | Aura | 本怪物心情不会低于普通，连续进入提取槽达到 2-3 天时心情变为热情，连续进入提取槽大于等于 4 天心情变为热爱 | [MoodCatalog.cs](file:///Assets/Scripts/Core/Rules/MoodCatalog.cs#L94): 心情不低于普通；连续工作 2-3 天升为「热情」，>=4 天升为「热爱」。 |
| 15 | **乐于助人** | Blue | Aura | 相邻怪物的能量值+2 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1825): 相邻的其他怪物每次产生能量数值 +2。 |
| 16 | **依赖** | Blue | Aura | 本怪物的能量值增加或减少时，变化量翻倍（变化量来源不限，含心情、工具；基础产出不翻倍） | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L685): 宿主每次能量增减变化量 Δ 翻倍，基础产出 B 不翻倍。 |
| 17 | **好学** | Blue | Trigger | 开工获得一个提取槽上随机怪物的技能，随后将性格替换为该怪物的性格（提取槽为空则不触发本性格） | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs#L640): 每日首次开工时，从提取槽其他怪物随机获取 1 技能并将其性格覆盖自身。 |
| 18 | **摸鱼派** | Negative | Aura | 心情总是处于疲倦 | [MoodCatalog.cs](file:///Assets/Scripts/Core/Rules/MoodCatalog.cs#L126): 固定覆盖心情为「疲倦」（-1 点能量）。 |
| 19 | **厌恶工作** | Negative | Aura | 本怪物连续进入提取槽大于等于 2 天心情便会变为倦怠 | [MoodCatalog.cs](file:///Assets/Scripts/Core/Rules/MoodCatalog.cs#L106): 连续进入提取槽 >=2 天心情立即跌入「倦怠」（-2 点能量）。 |
| 20 | **厌世** | Negative | Aura | 心情总是处于倦怠 | [MoodCatalog.cs](file:///Assets/Scripts/Core/Rules/MoodCatalog.cs#L128): 固定覆盖心情为「倦怠」（-2 点能量）。 |
| 21 | **仇恨血脉** | Negative | Aura | 当子代与亲本一起在提取槽时，亲本与子代相同的技能，其能量值-3 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1859): 亲本与直接子代同在提取槽时，二者相同技能能量数值 -3。 |
| 22 | **随心所欲** | Negative | Aura | 本怪物的每个主动技能有 20%几率不生效 | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L345): 宿主主动技能每次触发时独立判定 20% 几率失效。 |
| 23 | **好奇** | Gold | Trigger | 开始阶段时，获得一个随机技能然后将剩余技能随机成其他技能（当技能达到上限时，将不再触发） | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs#L585): 每天开始阶段，未满上限时获得 1 个随机技能并将剩余技能逐项重置。 |
| 24 | **上进心** | Gold | Trigger | 开始阶段时，本怪物的能量值永久+1 | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs#L581): 每天开始阶段，宿主怪物自身永久能量修正数值 +1 (`Modifier += 1`)。 |
| 25 | **迷恋死亡** | Gold | Aura | 基因槽内的技能可以直接安装到本怪物身上使用 | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs#L144): 允许玩家在操作阶段将基因槽中的技能材料直接安装到自身身上。 |

---

## 三、被动工具系统审计 (18 Tools)

### 3.1 价格与稀有度梯队
- **白工具 (10 件)**: 统一定价 **30 金币**
- **蓝工具 (5 件)**: 统一定价 **50 金币**
- **金工具 (3 件)**: 统一定价 **80 金币**

### 3.2 全量 18 个工具详表与素材绑定

| 序号 | 工具名称 | 价格 | 稀有度 | 效果标识 (ToolEffect) | 功能描述 | Sprite 图标绑定 (`PresentationSpriteCatalog`) | 规则引擎实现细节 |
| :---: | :--- | :---: | :---: | :--- | :--- | :--- | :--- |
| 1 | **急急装置** | 30 | White | `DoubleFirstEnergy` | 最左侧怪物最先产生能量的技能，能量值翻倍 | `Assets/Art/Ui/IconRocket.png`<br>sub-ID: `21300000` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L114): 最左侧怪物第一个产出能量的技能，其最终能量翻倍。 |
| 2 | **小喇叭** | 30 | White | `RightmostEnergyBonus` | 最右侧怪物的能量值+3 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-151379363` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1795): 提取槽中最右侧存活怪物的每次能量产出数值 +3。 |
| 3 | **打卡机** | 30 | White | `GoldOnClear` | 每个通关阶段后，获得 10 金币 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-173832200` | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs): 每个通关阶段（Wage/Clear）完成后立即奖励 10 金币。 |
| 4 | **招工海报** | 30 | White | `ExtraShopMonster` | 商店多出现 1 只可购买的怪物 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `-1029477825` | [ShopSystem.cs](file:///Assets/Scripts/Core/Rules/ShopSystem.cs): 商店货架生成的怪物商品位数量 +1（由 4 只增至 5 只）。 |
| 5 | **批发合同** | 30 | White | `ToolDiscount` | 商店中的工具价格-5 金币 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `459786568` | [ShopSystem.cs](file:///Assets/Scripts/Core/Rules/ShopSystem.cs): 商店中所有待购工具价格直接 -5 金币（不追溯自身）。 |
| 6 | **研磨机** | 30 | White | `GoldOnDiscard` | 怪物通过废弃槽消灭时，获得 5 金币 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `854590859` | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs#L170): 怪物投入废弃槽销毁提取基因时，额外奖励 5 金币。 |
| 7 | **电化能机** | 30 | White | `SettleEnergyQuote` | 结算阶段时，产生 10 点能量 | `Assets/Art/Ui/IconBolt.png`<br>sub-ID: `21300000` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L122): 结算阶段开始时独立产生 10 点全局能量（受稀有度检测仪等全局加成）。 |
| 8 | **精神操控仪** | 30 | White | `PersonalityEnergyBonus` | 性格提供的能量值加成额外+1 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-689578306` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1744): 针对所有性格来源的能量正向数值加成，每笔额外 +1。 |
| 9 | **怪物严选** | 30 | White | `UpgradeRandomMood` | 开始阶段时，随机提升一个怪物的心情档位 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `-1555036855` | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs#L565): 每天开始阶段，随机选择一只可提升的心情档位提升 1 档（上限热爱）。 |
| 10 | **情绪收割机** | 30 | White | `NormalOrHigherMoodBonus` | 心情为普通以上的怪物，能量值+2 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-1589191515` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1789): 心情为「普通」、「热情」、「热爱」的怪物能量数值 +2。 |
| 11 | **上级员工证** | 50 | Blue | `ExtractionCells` | 提取槽额外获得一个格子 | `Assets/Art/Ui/IconBadge.png`<br>sub-ID: `21300000` | [ToolRules.cs](file:///Assets/Scripts/Core/Rules/ToolRules.cs#L7): 提取槽总容量扩充为 6 格（默认 5 格）。 |
| 12 | **隔音棉** | 50 | Blue | `PreventMoodDrop` | 怪物不会因连续进入提取槽而降低心情 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-901871153` | [MoodCatalog.cs](file:///Assets/Scripts/Core/Rules/MoodCatalog.cs#L116): 免疫因连续工作天数导致的疲倦/倦怠降档（普通保持）。 |
| 13 | **五行罗盘** | 50 | Blue | `ElementalAdjacentBonus` | 提取槽中种族相生的相邻两只怪物，能量值各+3 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `-243543478` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1850): 提取槽中种族相生的相邻两只怪物，其能量数值各 +3。 |
| 14 | **验货单** | 50 | Blue | `ExtraShopTool` | 商店多出现 1 件工具 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-695191887` | [ShopSystem.cs](file:///Assets/Scripts/Core/Rules/ShopSystem.cs): 商店货架生成的被动工具商品位数量 +1（由 2 件增至 3 件）。 |
| 15 | **稀有度检测仪** | 50 | Blue | `GoldSkillGlobalBonus` | 提取槽中每有 1 个金色技能，全场能量值+1 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-1343244770` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1760): 提取槽中每有 1 个金色技能实例，全场所有产能数值 +1。 |
| 16 | **劣胜装置** | 80 | Gold | `SingleSkillBonus` | 单技能怪物产生的能量值翻倍，且产生能量的技能触发次数+1 | `Assets/Art/Ui/IconBolt.png`<br>sub-ID: `21300000` | [SettlementSystem.cs](file:///Assets/Scripts/Core/Rules/SettlementSystem.cs#L1645): 单技能怪物能量值翻倍，且产生能量的技能触发次数 +1。 |
| 17 | **复读腺** | 80 | Gold | `AllowDuplicateSkills` | 怪物可以重复拥有技能 | `Assets/Arts/Atlas/icons_full_32.png`<br>sub-ID: `-1349483319` | [BreedingBench.cs](file:///Assets/Scripts/Core/Rules/BreedingBench.cs) / [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs): 允许怪物在生成、培育、安装中获得同名重复技能。 |
| 18 | **蓄热熔炉** | 80 | Gold | `RetainEnergyPercent` | 工资结算阶段时，将剩余能量的 20%保留到下一关卡，其余正常清零 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `-1418158868` | [FlowSystem.cs](file:///Assets/Scripts/Core/Rules/FlowSystem.cs): 工资结算扣除需求后，将剩余能量的 20% 向下取整保留至下一关。 |

---

## 四、科技系统审计 (6 Techs)

### 4.1 科技规划原则
所有科技均消耗 **1 点科技点**，全局每局每项限研发一次，无先后依赖或前置树，达成超额能量目标后可自选解锁。

### 4.2 全量 6 项科技详表

| 序号 | 科技名称 | 研发消耗 | 标志枚举 (TechFlag) | 效果与文案描述 | 规则引擎实装细节 |
| :---: | :--- | :---: | :--- | :--- | :--- |
| 1 | **基因实验** | 1 科技点 | `BreedingSkill` | 培育槽可放入基因数量+1 | [TechCatalog.cs](file:///Assets/Scripts/Core/Rules/TechCatalog.cs#L30): `AllowsBreedingSkill` 解锁拖入基因材料槽。 |
| 2 | **槽位扩容** | 1 科技点 | `ExtraBreedingSlot` | 额外获得一个培育槽 | [TechCatalog.cs](file:///Assets/Scripts/Core/Rules/TechCatalog.cs#L25): `SlotCount` 由 `BaseBreedingSlots(1)` 扩至 `ExpandedBreedingSlots(2)`。 |
| 3 | **变异学说** | 1 科技点 | `ExtraSkill` | 培育槽诞生的新怪物有 10%的概率从技能池中额外获得一个随机技能 | [BreedingSystem.cs](file:///Assets/Scripts/Core/Rules/BreedingSystem.cs): `GrantsExtraSkill` 在子代生成时独立掷骰 10% 追加候选技能。 |
| 4 | **大乱炖** | 1 科技点 | `ExtraParent` | 培育槽可放入怪物数量+1 | [TechCatalog.cs](file:///Assets/Scripts/Core/Rules/TechCatalog.cs#L28): `ParentCapacity` 由 `BaseParents(2)` 扩至 `ExpandedParents(3)`。 |
| 5 | **科学培育** | 1 科技点 | `Modifier` | 培育槽诞生的新怪物有 10%的概率产生的能量值+2 | [BreedingSystem.cs](file:///Assets/Scripts/Core/Rules/BreedingSystem.cs): `GrantsModifier` 在子代诞生时独立掷骰赋予 `Modifier += 2`。 |
| 6 | **基因仓储** | 1 科技点 | `GeneStorage` | 基因槽上限+1 | [RunModel.cs](file:///Assets/Scripts/Core/Rules/RunModel.cs#L515): `GeneCapacity` 属性动态返回 `HasTech ? 4 : 3`。 |

---

## 五、种族系统与相生相克闭环 (5 Races)

### 5.1 相生相克闭环验证

五大种族严格按照中国古代五行属性闭环构建：
- **相生闭环 (Generating Cycle)**: `森林 (木)` $\to$ `天空 (火)` $\to$ `沙漠 (土)` $\to$ `山脉 (金)` $\to$ `海生 (水)` $\to$ `森林 (木)`
- **相克闭环 (Overcoming Cycle)**: `森林 (木)` $\to$ `沙漠 (土)` $\to$ `海生 (水)` $\to$ `天空 (火)` $\to$ `山脉 (金)` $\to$ `森林 (木)`

### 5.2 种族属性与图标资源映射

| 种族名称 | 对应五行 | 相生目标 (Generates) | 相克目标 (Overcomes) | Sprite 图标绑定 (`PresentationSpriteCatalog`) | 局内机制交互 |
| :--- | :---: | :--- | :--- | :--- | :--- |
| **森林** | 木 | 天空 | 沙漠 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `-568833257` (元素名: 木) | 两亲本繁育时子代 50%/50% 继承其一；与相邻怪物相生时激活「五行罗盘」双向各 +3 能量。 |
| **天空** | 火 | 沙漠 | 山脉 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `1533199738` (元素名: 火) | 两亲本繁育时子代 50%/50% 继承其一；与相邻怪物相生时激活「五行罗盘」双向各 +3 能量。 |
| **沙漠** | 土 | 山脉 | 海生 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `-509520459` (元素名: 土) | 两亲本繁育时子代 50%/50% 继承其一；与相邻怪物相生时激活「五行罗盘」双向各 +3 能量。 |
| **山脉** | 金 | 海生 | 森林 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `-232543730` (元素名: 金) | 两亲本繁育时子代 50%/50% 继承其一；与相邻怪物相生时激活「五行罗盘」双向各 +3 能量。 |
| **海生** | 水 | 森林 | 天空 | `Assets/Arts/Atlas/hearthwood.png`<br>sub-ID: `1694634285` (元素名: 水) | 两亲本繁育时子代 50%/50% 继承其一；与相邻怪物相生时激活「五行罗盘」双向各 +3 能量。 |

> [!TIP]
> 为防止上层表现层在调用 `GetRaceIcon` 时传入种族名 (如「森林」) 与底层图集元素名 (如「木」) 不一致，`PresentationSpriteCatalog.cs` 已增加双向别名检索支持。

---

## 六、心情系统审计 (5 Mood Tiers)

### 6.1 心情档位梯度与升降规则
怪物心情共有 5 档，每日前置快照根据怪物历史连续工作与连续休息天数自动结算：

| 档位 (Tier) | 心情名称 | 能量加成 (Energy Delta) | 触发条件 (出勤 / 休息天数要求) | 旧版别名 | Sprite 图标绑定 (`hearthwood.png`) | 规则引擎防降与覆盖细节 |
| :---: | :--- | :---: | :--- | :---: | :--- | :--- |
| Tier 0 | **倦怠** | **-2** | 连续进入提取槽 >= 4 天 | 崩溃 | sub-ID: `1773079932` (档位名: 崩溃) | 连续工作 >= 4 天触发；被「隔音棉」阻挡回升为普通；性格「厌世」强制锁定为倦怠。 |
| Tier 1 | **疲倦** | **-1** | 连续进入提取槽 2-3 天 | 糟糕 | sub-ID: `-269321964` (档位名: 糟糕) | 连续工作 2-3 天触发；被「隔音棉」阻挡回升为普通；性格「摸鱼派」强制锁定为疲倦。 |
| Tier 2 | **普通** | **0** | 工作 1 天或休息 1 天（基准状态） | 普通 | sub-ID: `-327584178` (档位名: 普通) | 新怪物基准初始状态；工作 1 天或休息 1 天；性格「稳定」强制锁定为普通。 |
| Tier 3 | **热情** | **+1** | 连续休息 2-3 天 | 良好 | sub-ID: `1457295534` (档位名: 良好) | 连续休息 2-3 天触发；性格「乐天派」强制锁定为热情。 |
| Tier 4 | **热爱** | **+2** | 连续休息 >= 4 天 | 极好 | sub-ID: `-1555036855` (档位名: 极好) | 连续休息 >= 4 天触发；「热爱工作」连续工作 >= 4 天升为热爱；「怪物严选」最高升至热爱。 |

> [!TIP]
> `PresentationSpriteCatalog.cs` 的 `GetMoodIcon` 亦已扩展别名回退逻辑，无缝支持新版标准名称 (`热爱/热情/普通/疲倦/倦怠`) 与图集原始别名 (`极好/良好/普通/糟糕/崩溃`)。

---

## 七、自动化测试与代码质量验证

### 7.1 测试执行结果
项目已在纯无引擎 .NET 10 环境与 Unity 体系中同步运行全部测试：

```bash
dotnet test kernel/TheCall.Rules.Tests --filter ContentAuditTests
```

输出结果：
```text
已通过! - 失败: 0，通过: 14，已跳过: 0，总计: 14，持续时间: 55 ms - TheCall.Rules.Tests.dll (net10.0)
```

全工程内核自动化测试：
```bash
dotnet test kernel/TheCall.Rules.Tests
```
输出结果：
```text
已通过! - 失败: 0，通过: 245，已跳过: 0，总计: 245，持续时间: 434 ms - TheCall.Rules.Tests.dll (net10.0)
```

### 7.2 核心测试覆盖范围清单
1. `技能库包含三十四个有效技能且稀有度分布为十六白十四蓝四金`: 验证技能总数 34 及 16/14/4 稀有度分布。
2. `五个退役技能不得出现在技能库中`: 验证退役 5 技能未残留于配置中。
3. `关键改动技能属性符合最新规格要求`: 验证蜜能量体基础报价 3，传能耳白稀有度且仅对右邻生效。
4. `全量三十四个技能均能正确生成文案描述且效果非空`: 验证全量技能的 `SkillSentences` 描述与效果完整性。
5. `性格库包含二十五个性格且稀有度分布为十二白五蓝五负面三金`: 验证性格 25 项与 12/5/5/3 分布。
6. `全量二十五个性格名称与文案均非空且稀有度有效`: 验证性格命名与光环/触发类型合法性。
7. `工具库包含十八个工具且价格分为三十五十和八十`: 验证工具 18 项、10/5/3 分布及 30/50/80 梯级价格。
8. `全量十八个工具效果与文案均非空`: 验证工具全部具备 `ToolEffect` 有效枚举。
9. `科技库包含六项科技且花费皆为一点科技点`: 验证 6 项科技枚举标志与 1 点花费。
10. `种族库包含五大种族相生闭环与相克闭环`: 验证五行相生闭环与相克闭环连通图。
11. `心情库包含五档且能量加成从负二到正二`: 验证 5 档心情顺序与 -2 到 +2 能量差值。
12. `关卡库定义七个关卡且达标能量与超额能量正确`: 验证完整七关达标与超额科技点门槛。
13. `经济与权重参数符合规格配置`: 验证薪资、抽取权重与培育槽位初始常量。
14. `状态边界符合ADR0003模型划分`: 验证 `RunModel` 全局生命周期与 `LevelModel` 关卡状态的严格归属。

---

## 八、审计总结

Milestone M5 的所有内容项（34 技能、25 性格、18 工具、6 科技、5 种族、5 心情）在 `call-book.json`、规则引擎系统 (`TheCall.Rules`)、表现层素材绑定 (`PresentationSpriteCatalog`) 中均达到了 **100% 完整交付、一致性与测试保障**，完全满足 Spec #16 和 Issue #22 的验收指标。
