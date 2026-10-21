# THE CALLED AUDIO CONFIGURATION v0.2

## 配置说明

- 音效命名遵循 `S_对象_事件_变体.wav`。
- 多变体资产按编号区分；具有明确先后关系的序列资产按编号顺序播放。
- 当前配置共 **31 个音频文件，12 个触发组**。


---

## 1. 文件路径与分类

SFX 根路径：

```text
Source code\Project_TheCall\Assets\Sound\SFX\
```

当前分类：

```text
SFX\
├── Monster\
├── Player\
├── Skill\
└── Level\
```

统一路径格式：

```text
Source code\Project_TheCall\Assets\Sound\SFX\对应分类\对应文件名
```

文档中的 `01~03`、`01~07` 为范围简写，代表多个独立文件，不是实际文件名的一部分。

---

# 2. Monster｜怪物

## 2.1 Monster Energy Gain / S_Monster_EnergyGain_01~02.wav

**触发条件：** 单只怪物自身的能量累积条发生增长时触发。  
**配置：** 2 个变体；每次触发从两个变体中选择一个播放。只负责单只怪物自身的小能量条，不与结算大条 `EnergyRise` 混用。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Monster\`

## 2.2 Monster Pick Up / S_Monster_PickUp.wav

**触发条件：** 玩家成功拿起 / 拖起怪物时播放。  
**配置：** 1 个固定样本，无变体，每次有效拿起播放一次。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Monster\S_Monster_PickUp.wav`

## 2.3 Monster Place / S_Monster_Place.wav

**触发条件：** 玩家将怪物成功放置到有效位置时播放。  
**配置：** 1 个固定样本，无变体，每次有效放置播放一次。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Monster\S_Monster_Place.wav`

## 2.4 Monster Discard / S_Monster_Discard.wav

**触发条件：** 玩家主动将怪物投入废弃 / 销毁区域，并实际销毁怪物时播放。  
**配置：** 1 个固定样本，无变体。主动销毁使用本条；技能造成怪物消灭使用 `S_Skill_Kill_01~04.wav`。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Monster\S_Monster_Discard.wav`

---

# 3. Player｜玩家

## 3.1 Obtain / S_Player_Obtain_01~03.wav

**触发条件：** 玩家实际获得普通物品 / 奖励时播放。  
**配置：** 3 个变体，作为通用“获得东西”反馈。结算页面经验累积不使用本组声音。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Player\`

---

# 4. Skill｜技能

## 4.1 Energy Skill / S_Skill_Energy_01~02.wav

**触发条件：** 产能技能实际触发并产生效果时播放。  
**配置：** 2 个变体，当前产能技能统一使用本组反馈。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Skill\`

## 4.2 Multiply / S_Skill_Multiply.wav

**触发条件：** 技能翻倍效果实际生效时播放。  
**配置：** 1 个固定样本，无变体，只在翻倍实际成立时触发。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Skill\S_Skill_Multiply.wav`

## 4.3 Kill / S_Skill_Kill_01~04.wav

**触发条件：** 消灭 / 击杀类技能实际生效，并导致怪物被消灭时播放。  
**配置：** 4 个变体；与玩家主动执行的 `Monster Discard` 区分。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Skill\`

---

# 5. Level｜结算

## 5.1 Start Settle / S_Level_Settle.wav

**触发条件：** 玩家点击开始结算，并正式进入结算流程时播放。  
**配置：** 1 个固定样本，无变体，每轮结算开始播放一次。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Level\S_Level_Settle.wav`

## 5.2 Reach Requirement / S_Level_Reach.wav

**触发条件：** 结算过程中，大能量条正式达到当前关卡要求时播放。  
**配置：** 1 个固定样本，无变体，绑定“达到要求”事件。  
**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Level\S_Level_Reach.wav`

## 5.3 Energy Rise Sequence / S_Level_EnergyRise_01~07.wav

**触发条件：** 进入结算后，随着本轮结算大能量条累积结果推进，按顺序播放对应序列。

**固定顺序：**

```text
01 → 02 → 03 → 04 → 05 → 06 → 07
```

**当前结果逻辑示意：**

```text
能量较少       → 01 → 02 → 03 → 04
获得能量更多   → 01 → 02 → 03 → 04 → 05
更高结算结果   → 01 → 02 → 03 → 04 → 05 → 06
超额 / 非常好  → 01 → 02 → 03 → 04 → 05 → 06 → 07
```

**配置要求：**
- 每轮从 `01` 开始连续播放。
- 不允许随机抽取。
- 最终结果决定序列结束位置。
- 当前没有确定各档具体数值阈值，不自行写死百分比 / 能量值。

**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Level\`

## 5.4 Experience Pickup Sequence / S_Level_ExperiencePickup_01~07.wav

**用途：** 本组不是游戏过程中拾取经验球的声音，而是进入结算页面后，根据玩家本局实际获得的经验值，让结算页面中的经验进行累积展示时使用。

**固定顺序：**

```text
01 → 02 → 03 → 04 → 05 → 06 → 07
```

**配置要求：**
- 不绑定游戏过程中的拾取事件。
- 触发发生在结算页面经验值进行累积展示时。
- 根据本局最终获得的经验量播放至对应阶段。
- 不与 `S_Player_Obtain_01~03.wav` 叠加。
- 当前未提供经验值与 01~07 各阶段的具体数值映射，因此不自行设定。

**路径：** `Source code\Project_TheCall\Assets\Sound\SFX\Level\`

---

# 6. 快速配置表

| 游戏事件 | 使用资产 | 配置 |
|---|---|---|
| 怪物自身小能量条增长 | `S_Monster_EnergyGain_01~02` | 2 变体 |
| 拿起怪物 | `S_Monster_PickUp` | 固定 |
| 放置怪物 | `S_Monster_Place` | 固定 |
| 主动丢弃 / 销毁怪物 | `S_Monster_Discard` | 固定 |
| 获得普通物品 / 奖励 | `S_Player_Obtain_01~03` | 3 变体 |
| 产能技能触发 | `S_Skill_Energy_01~02` | 2 变体 |
| 翻倍生效 | `S_Skill_Multiply` | 固定 |
| 技能消灭怪物 | `S_Skill_Kill_01~04` | 4 变体 |
| 开始结算 | `S_Level_Settle` | 固定 |
| 能量达到要求 | `S_Level_Reach` | 固定 |
| 结算大能量条增长 | `S_Level_EnergyRise_01~07` | 固定序列 |
| 结算页面经验累积 | `S_Level_ExperiencePickup_01~07` | 固定序列 |

---

# 7. 当前文件清单

```text
Monster/
├── S_Monster_EnergyGain_01.wav
├── S_Monster_EnergyGain_02.wav
├── S_Monster_PickUp.wav
├── S_Monster_Place.wav
└── S_Monster_Discard.wav

Player/
├── S_Player_Obtain_01.wav
├── S_Player_Obtain_02.wav
└── S_Player_Obtain_03.wav

Skill/
├── S_Skill_Energy_01.wav
├── S_Skill_Energy_02.wav
├── S_Skill_Multiply.wav
├── S_Skill_Kill_01.wav
├── S_Skill_Kill_02.wav
├── S_Skill_Kill_03.wav
└── S_Skill_Kill_04.wav

Level/
├── S_Level_Settle.wav
├── S_Level_Reach.wav
├── S_Level_EnergyRise_01.wav
├── S_Level_EnergyRise_02.wav
├── S_Level_EnergyRise_03.wav
├── S_Level_EnergyRise_04.wav
├── S_Level_EnergyRise_05.wav
├── S_Level_EnergyRise_06.wav
├── S_Level_EnergyRise_07.wav
├── S_Level_ExperiencePickup_01.wav
├── S_Level_ExperiencePickup_02.wav
├── S_Level_ExperiencePickup_03.wav
├── S_Level_ExperiencePickup_04.wav
├── S_Level_ExperiencePickup_05.wav
├── S_Level_ExperiencePickup_06.wav
└── S_Level_ExperiencePickup_07.wav
```

# 8. 资产统计

| 分类 | 触发组 | 文件数 |
|---|---:|---:|
| Monster | 4 | 5 |
| Player | 1 | 3 |
| Skill | 3 | 7 |
| Level | 4 | 16 |
| **合计** | **12** | **31** |

# 9. 最小接入验收

- [ ] `S_Monster_*` 全部位于 `SFX\Monster\`。
- [ ] `S_Player_Obtain_*` 位于 `SFX\Player\`。
- [ ] 怪物小能量条与结算大能量条使用两套独立资产。
- [ ] `EnergyRise_01~07` 按 `01 → 07` 顺序推进，不随机。
- [ ] EnergyRise 根据本轮能量结果决定序列终点。
- [ ] `ExperiencePickup_01~07` 只用于结算页面经验累积，不绑定游戏内拾取。
- [ ] ExperiencePickup 按编号顺序推进，不随机。
- [ ] 主动销毁怪物使用 `Monster Discard`；技能消灭怪物使用 `Skill Kill`。
- [ ] 未确认 Energy / Experience 数值映射前，程序端不自行写死阈值。
