# 独立UI夹具分步验收

> 2026-10-20 起，怪物拼接、骨架、部件目录和拼装工作区已从工程删除。下文若提到它们，只作当时记录。新系统从 `Assets/Notes/新怪物系统交接.md` 开始。


所有调用由主控通过Unity CLI `eval` 执行。禁止与自然七天并发；先完成自然轨迹归档，再启用本工具。此处资源注入只检查局部UI和原子拒绝，不能计为自然七天资源来源。

`PresentationFixtureAcceptance`将结果写至 `docs/audit/2026-10-12/remediation/evidence/ui-fixtures/<scenario>-report.json`，每条包含输入、frame、实际文本/Sprite/ID状态和断言判定。断言失败会保存失败记录并抛异常。`Capture()`保存含Overlay的截图，调用后等待frame推进再读取图，不能把capture返回成功当作图片验收。

## 非播放状态

编译最新代码后，执行并保存返回值：

```csharp
return TheCall.Editor.PresentationRemediation.Apply();
return TheCall.Editor.PresentationFixtureAcceptance.Idempotency();
return TheCall.Editor.PresentationFixtureAcceptance.Assets();
```

Idempotency连续迁移两次，确认根对象ID、对象数量及无关画像位置人工调参均保留，并在finally恢复哨兵位置保存正式Main。Assets重载UI与MonsterSlotCard目录全部Prefab，查missing script和真实消费者引用；读取保存的39部件/6模板并逐模板查enabled部件Sprite。

## 播放状态：工具压力

对count分别1、3、6、18重复下面步骤，每次分开eval，等待下一帧：

```csharp
return TheCall.Editor.PresentationFixtureAcceptance.Begin("tools", 18);
return TheCall.Editor.PresentationFixtureAcceptance.CheckTools(18);
return TheCall.Editor.PresentationFixtureAcceptance.HoverTool(17);
return TheCall.Editor.PresentationFixtureAcceptance.Capture("tools-18.png");
```

检查包括实际enabled Image.Sprite、名称、正式description、位置不重叠与screen bounds；HoverTool调用实际ToolArtView pointer consumer并检查显示的详情文本。

## 19/30只库存首尾真实操作

count=19与30各复跑；拖最后一只时索引为count-1。拖一只后count暂减一，Return后回到原count。

```csharp
return TheCall.Editor.PresentationFixtureAcceptance.Begin("inventory", 30);
return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(30);
return TheCall.Editor.PresentationFixtureAcceptance.DragInventory(29, 0);
return TheCall.Editor.PresentationFixtureAcceptance.ReturnExtraction(0);
return TheCall.Editor.PresentationFixtureAcceptance.DragInventory(0, 1);
return TheCall.Editor.PresentationFixtureAcceptance.ReturnExtraction(1);
return TheCall.Editor.PresentationFixtureAcceptance.Capture("inventory-30.png");
return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(30, true);
return TheCall.Editor.PresentationFixtureAcceptance.SellInventory(29);
return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(29, true);
return TheCall.Editor.PresentationFixtureAcceptance.SellInventory(0);
return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(28, true);
```

ScrollTo将具体首尾卡滚入可见viewport再发真实指针事件；出售经过真实Button回调。校验集合排序、唯一MonsterId、实际图层可见及对象正确。

## 科研/6-2-3-4容量/预约满仓原子失败/第四格废弃

```csharp
return TheCall.Editor.PresentationFixtureAcceptance.Begin("capacity", 12);
```

依次对正式六个名称调用 `Research(name)`，每调用单独eval并等待帧；研究后以 `Research(name,false)`检查重复研究不扣费。六点耗尽时，另开无点`Begin("reject",1)`再`Research("基因实验",false)`检查余额不足。容量场景研究完成后：

```csharp
return TheCall.Editor.PresentationFixtureAcceptance.BackResearch();
return TheCall.Editor.PresentationFixtureAcceptance.CheckCapacity();
return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,0);
return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,1);
return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,2);
return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,3);
return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,4);
return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,5);
return TheCall.Editor.PresentationFixtureAcceptance.PrepareGenes(3);
return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"breeding",0,true);
return TheCall.Editor.PresentationFixtureAcceptance.Discard(0,true);
return TheCall.Editor.PresentationFixtureAcceptance.Discard(0,false);
return TheCall.Editor.PresentationFixtureAcceptance.ReturnGene(0);
return TheCall.Editor.PresentationFixtureAcceptance.Capture("capacity-reservation.png");
```

第四格废弃真实成功使总量3→4；包含已预约的满仓再废弃必须逐字段不变；退回校验同GeneId原索引与预约解除。两槽/三亲本的实际摆放可调用 `DragInventory`提供的底层同一真实drag链，参见后续 `DragParent` 接口。

## 真实基因安装与拒绝

每个场景都从Begin重新生成局部夹具，以免失败比较夹杂前一次操作：

```csharp
return TheCall.Editor.PresentationFixtureAcceptance.Begin("install",1);
return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"monster",0,true);
return TheCall.Editor.PresentationFixtureAcceptance.Begin("reject",1);
return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"monster",0,false);
return TheCall.Editor.PresentationFixtureAcceptance.Begin("full",1);
return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"monster",0,false);
return TheCall.Editor.PresentationFixtureAcceptance.Begin("duplicate",1);
return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"monster",0,false);
return TheCall.Editor.PresentationFixtureAcceptance.EnableDuplicateFixture();
return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"monster",0,true);
```

成功只消费对应基因；普通性格、满4及未复读重复失败比较金币/科技、完整Monster+SkillInstance身份与来源、全部基因ID与预约和区域位置，必须不变且有明确domain反馈。

## 25性格/5种族/5心情逐条实际consumer

分别 `Consumer("personality",0..24)`、`Consumer("race",0..4)`、`Consumer("mood",0..4)`，每项单独eval，下一帧 `Capture("consumer-personality-00.png")` 等保存对应图片。

每次用正式怪物 DTO经过实际FillMonster绑定名称/Sprite，然后实际MonsterHover显示机制与历史；检查不是仅GetIcon非空，报告记录实际Image的Sprite名和Header文字。这里固定心情性格以实际生成心情核对；心情单项使用稳定性格显式造景覆盖5档。

## 34技能逐条实际详情consumer

分别 `SkillConsumer(0..33)`，每项单独eval，等待帧后保存对应 `Capture("consumer-skill-00.png")`。正式 JSON 技能经实际 MonsterDetailsQuery / SkillReadout，进入 MonsterHover；断言Name/Function/DisplaySentence实际TMP字段显示且无overflow，记录稀有度/主动被动/说明，不把内容计数当机制证明。

## 长名四技能与四角

```csharp
return TheCall.Editor.PresentationFixtureAcceptance.Begin("details",1);
return TheCall.Editor.PresentationFixtureAcceptance.DetailsAt("top-left");
return TheCall.Editor.PresentationFixtureAcceptance.Capture("details-top-left.png");
```

依次 top-left、top-right、bottom-left、bottom-right 复验；每项抓图前等待frame推进。调用真正MonsterHover.Show与HoverLayout，夹具仅延长显示名；断言四份实际TMP消费者无overflow且位于屏幕内、Header长名完整。为截图暂时冻结hover采样（不改游戏规则或播放器）；最后 `HideDetails()`恢复。

验收结束退出Play Mode，确认没有将夹具Model持久化。上述任何失败都须返修并复跑相关步骤；本工具与通过映射测试不代替截图人工视觉QA。
