# 横向设施正式美术接入

运行入口仍是 `Assets/Scenes/Main.unity`。源文件 `Assets/Arts/Psds/横向地图分层拆解.psd` 保持原样，三张功能说明图对应宿舍、生产培育区和商店。旧版原画接入工具 `LabArtIntegration` 留给旧布局；当前布局使用 `FacilityArtIntegration`。

## 画面与玩法

| 原画区域 | 当前行为 | 入口 |
| --- | --- | --- |
| 左侧窄面板 | 全部持有怪物的状态监控，可滚轮查看；显示心情、去向、连续工作/休息天数与心情能量修正 | `FacilityStatusQuery`、`FacilitySceneView` |
| 两层宿舍 | 展示当前未上工的怪物，每页六只；保留悬停详情与拖放，可把工作区怪物拖回来休息 | `DormResidents`、原 `ReturnMonsterCommand` 路径 |
| 右上培养舱 | 两个亲本；研究“大乱炖”后显示三个亲本。技能加号仍是培育基因预约落点 | 原 `breedingSlots`、`breedingSkills` |
| 右上卷帘门 | “槽位扩容”研究入口；解锁后卷帘向上收起，露出第二培育室 | `FacilityRoomLock`、`Shutter` |
| 下层基座 | 五格均匀铺开；员工证开放第六格后重新等距排列。独立分柱蓄能，离开当前格时压缩传能 | 原 `extractionSlots`、`FacilityEnergyColumn` |
| 表盘和收集罐 | 分柱传能的同一进度驱动液面和指针；接收过程中触发小幅灯光变化 | `FacilitySceneView.ReceiveEnergy` |
| 右侧收容柜 | 一怪一笼，独立铁栏前景与开口裁切，滚轮/上下按钮按整排升降 | `LabCageLift`，新尺寸为 344×308、每排高 154 |
| 回收口 | 悬停张开独立上下颚；有效废弃后吞咽，并由原规则提取一个基因 | 原 `OperationZone`、`LabRecycler` |
| 右侧药剂柜 | 三个基因格及扩容后的第四格，保留技能安装和预约规则 | 原 `SkillChipView` |
| 底部实体按钮 | 触发原结算流程 | 原 `nextDayButton` |
| 商店 | 怪物/工具分类、购买、出售、付费刷新和下一天；摇杆随成功刷新动作，博士屏幕可交谈 | `FacilityShopView`、原 `ShopScreenView` 与 Commands |

宿舍是休息状态的可视化入口。工作与休息天数、次日心情继续由原回合规则计算，拖回宿舍不会立即加心情。监控按“怪物笼 + 提取槽 + 培育槽”的当前归属汇总，商品和开局候选不计入全员。

## 怪物结算的文字与传能

`ScoringShow.Energy.cs` 在当前怪物头顶保留技能名和能量数字。辅助效果在实际来源怪物上方出现，然后把 TMP 字形的两个三角形拆开，汇入当前数字。数字单独膨胀，按结算记录的逐次结果变化。侧向计数、负加成、倍率、工具和永久写回使用同一条表现路径。

辅助飘字避让当前主飘字和表盘，并限制在画面内。工具提示从工具栏下方出现。较大的能量数字自动缩小字号。进入支付前先收齐尾柱；跳过不会补播尾柱的传能动画。

一格内的多次技能产出先进入本格分柱。结算轴离开时，分柱向下压缩，碎屑沿底部进入主柱；下一格可以同时开始出字。第一批碎屑到达后，主柱、指针和累计数字才开始增长。主柱和指针使用相同的连续进度，不再各自追赶最终整数。换位、消灭和支付等待已蓄能量传完。零能量只上浮淡出。

`FacilityEnergyColumn` 控制液面和对应 `FacilityLamp.Energy`。底座与液面使用 `FacilityLit.mat`，真实 `Light2D` 照亮分柱附近。`ScoringGlyphShard` 保留字形的 UV、颜色和字体材质，兼容中文与备用字体。自然加速、点击快进、再次点击跳过沿用 `ScoringShow` 的节奏控制。取消时统一清理碎屑、液面和灯光。

表盘外壳独立为 `GaugeHousing`，生产界面使用移除固定表盘的 `ProductionWorkShell`。开局、科研和结果页继续使用原 `ProductionShell`。场景升级入口为 `FacilityArtIntegration.ApplySettlementLayout()`；完整重建入口仍为 `Apply()`。两者均通过 Editor API 保存场景。

局部演出验收运行 `scripts/verify-settlement-visuals.ps1`，逐帧与灯光像素检查运行 `scripts/audit-settlement-visuals.py`。证据位于 `docs/audit/2026-10-18/settlement/`，其中注入怪物的 fixture 与七天自然回放分别记录。

## 两区切换

拖动空白处可以切区，松手后自动完成或取消滑动，最终只停在宿舍或生产区。底部按钮也可切换。拖着怪物在左右屏幕边缘停留约 0.35 秒，会带着怪物跨区；虚影在独立拖动层中保持同一张剪影。点选怪物再选择落点的原操作继续保留。

商店仍是结算之后的独立阶段。切区本身不推进天数、不触发结算。

## 灯光、配色与素材

`Assets/Arts/Facility/manifest.json` 保存 PSD 的 SHA-256、图层来源、像素位置和导出 PNG 的哈希。背景按两个 1920×1080 区域导出，商店为独立的 1920×1080。35 张导出 PNG 使用无压缩、无 mipmap、双线性过滤。

原画的色罩没有固化到导出图。底材保留明暗，以 `FacilitySprite.tint` 控制暖红、冷蓝和商店绿色。`FacilitySprite` 把同一套 RectTransform 坐标投影为 `SpriteRenderer`，实际使用 `FacilityLit.mat` 的 `Sprite-Lit-Default` 材质。语义槽位和文字继续留在 uGUI，保证拖放、遮罩和悬停一致。

每盏灯的编辑入口在 `FacilityContent/FacilityLamp*` 上的 `FacilityLamp`，可改 `color`、`intensity`、`phase` 和 `flicker`。它对应 `FacilityLighting` 下真实的 `Light2D`；运行时控制器会写回该灯，所以应调整 `FacilityLamp` 字段。`GlobalLight2D` 控制全局基础亮度。这样接入符合 [Unity 的 2D 光照材质要求](https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-index/2d-light-properties-explained)。

五张怪物素材只把黑色填充归一为可染色白色，透明轮廓逐像素保留。`Assets/Resources/ShadowMonsterCatalog.asset` 保存列表，同一外观种子在开局、宿舍、工作区、收容柜、商店、结算和拖动虚影中使用同一张剪影，允许重复。剪影保留整体呼吸和计分反应，不使用部件拼接或抓取弹簧。

旧拼接、骨架和工作区已删除。画像只有剪影。新系统见 [新怪物系统交接](../../Assets/Notes/新怪物系统交接.md)。

## 后续设计接入

| 已准备的摆件 | 预留对象 | 当前状态 |
| --- | --- | --- |
| 神秘几何体 | `DormGeometryUnlock` | 默认隐藏，开放后悬浮、轻转 |
| 电视机 | `DormTelevisionUnlock` | 默认隐藏，开放后轻微扫描闪动 |
| 糖果机器 | `DormCandyUnlock` | 默认隐藏，开放后轻微呼吸 |
| 嘴唇盆栽 | `DormPlantUnlock` | 默认隐藏，开放后摆动 |

每个对象有 `FacilityUpgradeView`。设计确定后，将 `toolName` 或 `techName` 填成内容目录的准确名称，拥有工具或完成研究后便会显现。空名称默认不开放。`previewUnlocked` 仅用于美术预览，不添加工具、科技或生产效果。改造的价格、具体效果和正式内容 ID 尚未定义，未向规则中加入虚构数值。

博士区域用《游玩主界面》图层「图层 6」的立绘站在供应站屏幕里，名牌下是值班对话。交谈会换一句，并随货架、回购、刷新和金币改口。基因图标仍使用可读名称。开局、关卡提示、科研和结果页已统一使用这次正式背景；它们继续保留现有界面流程。

## 重建与验证

导出素材：

```powershell
uv run --with psd-tools python -X utf8 scripts/extract-facility-art.py
```

退出 Play Mode，通过就绪 Editor 的 Unity CLI 执行 `TheCall.Editor.FacilityArtIntegration.Apply()`。这会通过 Editor API 重摆实际对象并保存 Main，首次备份位于 `Temp/FacilityArt/rollback/`。没有手写场景 YAML。

界面验收：

```powershell
pwsh -NoProfile -File scripts/verify-facility-art.ps1
pwsh -NoProfile -File scripts/verify-facility-aspects.ps1
uv run --with psd-tools python -X utf8 scripts/audit-facility-art.py
```

证据位于 `docs/audit/2026-10-17/facility/`。`acceptance.json` 记录局部 UI fixture，明确含注入资源；`02-dorm-upgrade-art-preview.png` 只展示美术预览。`art-integrity.json` 独立检查源文件、五张透明轮廓和真实灯光开关的像素变化。七天玩家操作回放与状态/生产记录对照另外保存，不能用 fixture 代替。

本次验收通过 516 项 EditMode 测试、52 项界面检查、三个画幅，以及三轮各 125 次输入的七天回放。九项回放状态/生产记录均与接入前基线一致。完整结果与截图见 [验收记录](../audit/2026-10-17/facility/review.md)。
