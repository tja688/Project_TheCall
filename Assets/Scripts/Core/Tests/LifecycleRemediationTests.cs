using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QFramework;
namespace TheCall.Tests
{
    public sealed class LifecycleRemediationTests : RulesFixture
    {
        RunModel Run => App.GetModel<RunModel>();
        LevelModel Level => App.GetModel<LevelModel>();
        [TestCase("乐天派", "热情")]
        [TestCase("摸鱼派", "疲倦")]
        [TestCase("厌世", "倦怠")]
        [TestCase("稳定", "普通")]
        public void F09_初生固定性格即时覆盖普通心情(string personality, string mood)
        {
            var monster = Run.CreateMonster(new[] { "能量体" }, personality: personality);
            Assert.That(monster.Mood, Is.EqualTo(mood));
            Assert.That(monster.DayBaseMood, Is.EqualTo("普通"));
            Assert.That(monster.ConsecutiveWorkDays, Is.Zero);
        }
        [Test]
        public void F09_改变性格保留当天基础与严选提升但即时应用固定覆盖()
        {
            var monster = Run.CreateMonster(new[] { "能量体" }, personality: "好学");
            monster.BoostDailyMood("热情");
            Run.ChangePersonality(monster.Id, "摸鱼派");
            Assert.That(monster.Mood, Is.EqualTo("疲倦"));
            Run.ChangePersonality(monster.Id, "急躁");
            Assert.That(monster.Mood, Is.EqualTo("热情"));
            Assert.That(monster.ConsecutiveWorkDays, Is.Zero);
            Assert.That(monster.DayMoodBoost, Is.EqualTo(1));
        }
        [Test]
        public void F01_取得实例身份来源成长及临时清理独立()
        {
            Run.AddTool("复读腺");
            var monster = Run.CreateMonster(new[] { "能量体" });
            var acquired = Run.GainSkill(monster.Id, "能量体", true, SkillSourceKind.SkillEffect, "source-instance", "source-monster");
            Assert.That(acquired.Success, Is.True);
            Assert.That(acquired.Instance.Id, Is.Not.EqualTo(monster.Skills[0].Id));
            Assert.That(acquired.Instance.SourceId, Is.EqualTo("source-instance"));
            Assert.That(acquired.Instance.SourceMonsterId, Is.EqualTo("source-monster"));
            acquired.Instance.Add(9, true);
            Run.ClearTemporarySkills();
            Assert.That(monster.Skills.Count, Is.EqualTo(1));
            Assert.That(monster.Skills[0].Quote, Is.EqualTo(5));
            Assert.That(monster.Skills[0].GrowthOwnerId, Is.EqualTo(monster.Skills[0].Id));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void F16_安于现状均匀抽合法保留对且至少一常驻(int pairIndex)
        {
            var monster = Run.CreateMonster(new[] { "能量体", "左能量体" });
            Run.GainSkill(monster.Id, "右能量体", true);
            Run.GainSkill(monster.Id, "奇异香", true);
            var original = monster.Skills.ToArray();
            var draw = new IndexDraw(pairIndex);
            var removed = monster.ChangePersonality("安于现状", draw);
            Assert.That(draw.LastCount, Is.EqualTo(5));
            Assert.That(monster.Skills.Count, Is.EqualTo(2));
            Assert.That(monster.Skills.Any(x => !x.IsTemporary), Is.True);
            Assert.That(removed.Count, Is.EqualTo(2));
            Assert.That(monster.Skills.Concat(removed), Is.EquivalentTo(original));
            monster.ClearTemporarySkills();
            Assert.That(monster.Skills.Count, Is.GreaterThanOrEqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void F08_正式拖放安装遵循重复许可并只成功消费(bool duplicateAllowed)
        {
            Run.EnterOperation(1);
            var host = Run.CreateMonster(new[] { "能量体" }, personality: "迷恋死亡");
            Run.AddToCage(host);
            Run.PutSkillInSlot("能量体");
            var geneId = Run.GeneSlots[0].Id;
            if (duplicateAllowed) Run.AddTool("复读腺");
            var result = App.GetSystem<FlowSystem>().Commit(new OperationDrop(DropPayload.SkillChip(0), new DropLanding(LandingPlace.Cage, 0, host.Id)));
            Assert.That(result, Is.EqualTo(duplicateAllowed));
            Assert.That(host.Skills.Count, Is.EqualTo(duplicateAllowed ? 2 : 1));
            Assert.That(Run.GeneSlots.Count, Is.EqualTo(duplicateAllowed ? 0 : 1));
            Assert.That(Run.Gold, Is.Zero);
            if (duplicateAllowed) Assert.That(host.Skills[1].SourceId, Is.EqualTo(geneId));
            else Assert.That(Run.LastOperationFailure, Does.Contain("重复"));
        }
        [Test]
        public void F08_普通性格及上限与预约安装失败保持字段不变()
        {
            Run.EnterOperation(1);
            foreach (var personality in new[] { "稳定", "迷恋死亡" })
            {
                var host = Run.CreateMonster(personality == "稳定" ? new[] { "能量体" } : new[] { "能量体", "左能量体", "右能量体", "奇异香" }, personality: personality);
                Run.AddToCage(host);
                Run.PutSkillInSlot("回响嗓");
                var before = host.Skills.Select(x => x.Id).ToArray();
                var genes = Run.GeneSlots.Select(x => x.Id).ToArray();
                Assert.That(App.GetSystem<FlowSystem>().Commit(new OperationDrop(DropPayload.SkillChip(Run.GeneSlots.Count - 1), new DropLanding(LandingPlace.Cage, 0, host.Id))), Is.False);
                Assert.That(host.Skills.Select(x => x.Id), Is.EqualTo(before));
                Assert.That(Run.GeneSlots.Select(x => x.Id), Is.EqualTo(genes));
                Assert.That(Run.Gold, Is.Zero);
            }
        }
        [Test]
        public void F08_仓储科技第四格可废弃满仓含预约原子拒绝()
        {
            Run.EnterOperation(1);
            Run.AddUnlockedTech("基因仓储");
            for (var i = 0; i < 3; i++) Run.PutSkillInSlot("能量体");
            Run.TryReserveGeneSlot(0, 0);
            var victim = Run.CreateMonster(new[] { "能量体" }); Run.AddToCage(victim);
            var flow = App.GetSystem<FlowSystem>();
            Assert.That(flow.Commit(new OperationDrop(DropPayload.Monster(victim.Id), new DropLanding(LandingPlace.Discard, 0, null))), Is.True);
            Assert.That(Run.GeneSlots.Count, Is.EqualTo(4));
            Assert.That(Run.GeneSlots[0].IsReserved, Is.True);
            var next = Run.CreateMonster(new[] { "能量体" }); Run.AddToCage(next);
            Assert.That(flow.Commit(new OperationDrop(DropPayload.Monster(next.Id), new DropLanding(LandingPlace.Discard, 0, null))), Is.False);
            Assert.That(Run.Find(next.Id), Is.SameAs(next));
            Assert.That(Run.GeneSlots.Count, Is.EqualTo(4));
            Assert.That(Run.Gold, Is.Zero);
        }
        [TestCase(0, false)] [TestCase(1, true)]
        public void F07_同名亲本与基因裁切按候选身份决定消费(int removeIndex, bool consumesGene)
        {
            Run.EnterOperation(1); Run.AddTool("复读腺");
            var first = Run.CreateMonster(new[] { "能量体" });
            var second = Run.CreateMonster(new[] { "能量体" });
            Level.Put(OperationArea.Breeding, 0, first.Id); Level.Put(OperationArea.Breeding, 1, second.Id);
            Run.PutSkillInSlot("能量体"); Run.TryReserveGeneSlot(0, 0); Level.TryPutSkill(0, "能量体");
            var geneId = Run.GeneSlots[0].Id;
            Level.LockBreeding();
            App.RegisterUtility<IDraw>(new BreedIndexDraw(removeIndex));
            App.GetSystem<BreedingSystem>().Spawn();
            var child = Run.Cage.Single();
            Assert.That(child.Personality, Is.EqualTo("安于现状"));
            Assert.That(child.Skills.Count, Is.EqualTo(2));
            Assert.That(child.Skills.Select(x => x.Name), Is.EqualTo(new[] { "能量体", "能量体" }));
            Assert.That(child.Skills.Select(x => x.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(child.Skills.Any(x => x.SourceId == geneId), Is.EqualTo(consumesGene));
            Assert.That(Run.GeneSlots.Count, Is.EqualTo(consumesGene ? 0 : 1));
            if (!consumesGene) Assert.That(Run.GeneSlots[0].IsReserved, Is.False);
            Assert.That(Run.Find(first.Id), Is.SameAs(first));
            Assert.That(Run.Find(second.Id), Is.SameAs(second));
        }
        [TestCase(0, Rarity.White)] [TestCase(6, Rarity.White)]
        [TestCase(7, Rarity.Blue)] [TestCase(8, Rarity.Blue)] [TestCase(9, Rarity.Gold)]
        public void F12_A27_技能抽样先按七二一权重再档内均匀(int boundary, Rarity rarity)
        {
            var draw = new SamplingDraw(boundary);
            var name = StockDraw.One(App.GetUtility<SkillCatalog>(), draw, DrawnPool.NoAmplifyBit, null);
            Assert.That(App.GetUtility<SkillCatalog>().RarityOf(name), Is.EqualTo(rarity));
            Assert.That(draw.Counts, Is.EqualTo(new[] { 10, App.GetUtility<SkillCatalog>().Names(DrawnPool.NoAmplifyBit, rarity, null).Count }));
            Assert.That(draw.Words.Count(x => x == "白"), Is.EqualTo(7));
            Assert.That(draw.Words.Count(x => x == "蓝"), Is.EqualTo(2));
            Assert.That(draw.Words.Count(x => x == "金"), Is.EqualTo(1));
        }
        [TestCase(49, Rarity.White)] [TestCase(50, Rarity.Blue)] [TestCase(69, Rarity.Blue)]
        [TestCase(70, Rarity.Negative)] [TestCase(89, Rarity.Negative)] [TestCase(90, Rarity.Gold)]
        public void F12_A27_性格五二二一选择边界及独立两次随机消费(int boundary, Rarity rarity)
        {
            var draw = new SamplingDraw(boundary);
            var name = App.GetUtility<IPersonalityCatalog>().Draw(draw);
            Assert.That(App.GetUtility<IPersonalityCatalog>().Find(name).Rarity, Is.EqualTo(rarity));
            Assert.That(draw.Counts.Count, Is.EqualTo(2));
            Assert.That(draw.Counts[0], Is.EqualTo(100));
            Assert.That(draw.Counts[1], Is.EqualTo(App.GetUtility<IPersonalityCatalog>().NamesOf(rarity).Count));
        }
        [Test]
        public void F07_A18_三亲本加基因满四候选仍掷变异并裁切不携带成长()
        {
            Run.EnterOperation(1); Run.AddUnlockedTech("变异学说");
            Level.ApplyShape(1, 3);
            var names = new[] { "能量体", "左能量体", "右能量体" };
            var parents = new List<Monster>();
            for (var i = 0; i < names.Length; i++)
            {
                var parent = Run.CreateMonster(new[] { names[i] }); parent.Skills[0].Add(100, true);
                parents.Add(parent); Level.Put(OperationArea.Breeding, i, parent.Id);
            }
            Run.PutSkillInSlot("奇异香"); Run.TryReserveGeneSlot(0, 0); Level.TryPutSkill(0, "奇异香");
            Level.LockBreeding();
            var draw = new MutationDraw(); App.RegisterUtility<IDraw>(draw);
            App.GetSystem<BreedingSystem>().Spawn();
            var child = Run.Cage.Single();
            Assert.That(draw.PercentCalls, Is.EqualTo(new[] { 40, 10 }));
            Assert.That(child.Skills.Count, Is.EqualTo(4));
            Assert.That(child.Skills.Select(x => x.Name), Is.EqualTo(new[] { "左能量体", "右能量体", "奇异香", "收尸腺" }));
            Assert.That(child.Skills.Last().SourceKind, Is.EqualTo(SkillSourceKind.Mutation));
            Assert.That(child.Skills[0].SourceId, Is.EqualTo(parents[1].Skills[0].Id));
            Assert.That(child.Skills[0].Quote, Is.EqualTo(App.GetUtility<SkillCatalog>().StartingQuote("左能量体")));
            Assert.That(Run.GeneSlots, Is.Empty);
            Assert.That(parents.All(x => Run.Find(x.Id) == x), Is.True);
        }
        [Test]
        public void F01_复读允许商店生成两份同名技能()
        {
            var names = StockDraw.ShopOffer(App.GetUtility<SkillCatalog>(), ScriptedDraw.Exact("双", "白", "能量体", "白", "能量体"), true);
            Assert.That(names, Is.EqualTo(new[] { "能量体", "能量体" }));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void F12_A27_五族一次等概率池选择(int index)
        {
            var draw = new IndexDraw(index);
            var names = App.GetUtility<IRaceCatalog>().Names();
            Assert.That(App.GetUtility<IRaceCatalog>().Draw(draw), Is.EqualTo(names[index]));
            Assert.That(draw.LastCount, Is.EqualTo(5));
        }
        [TestCase(null, 7, 0)] [TestCase("良好肉体", 7, 0)]
        public void F12_A27_功能与排重导致空档时仅保留原权重归一(string taken, int whiteCount, int blueCount)
        {
            var draw = new SamplingDraw(6);
            var name = StockDraw.One(App.GetUtility<SkillCatalog>(), draw, DrawnPool.AmplifyOnly, taken);
            Assert.That(App.GetUtility<SkillCatalog>().RarityOf(name), Is.EqualTo(Rarity.White));
            Assert.That(draw.Words.Count(x => x == "白"), Is.EqualTo(whiteCount));
            Assert.That(draw.Words.Count(x => x == "蓝"), Is.EqualTo(blueCount));
            Assert.That(draw.Words.Any(x => x == "金"), Is.False);
            Assert.That(draw.Counts.Count, Is.EqualTo(2));
            Assert.That(name, Is.Not.EqualTo(taken));
        }
        [Test]
        public void F08_预约基因不能安装且退回保持同一材料身份与原格()
        {
            Run.EnterOperation(1);
            var host = Run.CreateMonster(new[] { "能量体" }, personality: "迷恋死亡"); Run.AddToCage(host);
            Run.PutSkillInSlot("左能量体"); Run.PutSkillInSlot("右能量体");
            var id = Run.GeneSlots[1].Id;
            Run.TryReserveGeneSlot(1, 0); Level.TryPutSkill(0, "右能量体");
            Assert.That(App.GetSystem<FlowSystem>().InstallGene(host.Id, 1), Is.False);
            Assert.That(Run.GeneSlots.Count, Is.EqualTo(2));
            Assert.That(host.Skills.Count, Is.EqualTo(1));
            App.GetSystem<FlowSystem>().ReturnBreedingSkill(0);
            Assert.That(Run.GeneSlots[1].Id, Is.EqualTo(id));
            Assert.That(Run.GeneSlots[1].IsReserved, Is.False);
            Assert.That(Run.GeneSlots[1].SkillName, Is.EqualTo("右能量体"));
        }
        [Test]
        public void F07_三亲本碰撞通过各自来源链重抽而不丢合法贡献()
        {
            Run.EnterOperation(1); Level.ApplyShape(1, 3);
            var parents = new[] {
                Run.CreateMonster(new[] { "能量体", "左能量体" }),
                Run.CreateMonster(new[] { "左能量体", "右能量体" }),
                Run.CreateMonster(new[] { "能量体" }) };
            for (var i = 0; i < parents.Length; i++) Level.Put(OperationArea.Breeding, i, parents[i].Id);
            Level.LockBreeding(); App.RegisterUtility<IDraw>(ScriptedDraw.Exact());
            App.GetSystem<BreedingSystem>().Spawn();
            var child = Run.Cage.Single();
            Assert.That(child.Skills.Select(x => x.Name), Is.EqualTo(new[] { "左能量体", "右能量体", "能量体" }));
            Assert.That(child.Skills.Select(x => x.SourceMonsterId), Is.EqualTo(parents.Select(x => x.Id)));
        }
        [Test]
        public void F09_笼内安装不会提前消费开工事件()
        {
            Run.EnterOperation(1); Level.BeginLevel(50, 60);
            var host = Run.CreateMonster(new[] { "能量体" }, personality: "迷恋死亡"); Run.AddToCage(host);
            Run.PutSkillInSlot("左能量体");
            Assert.That(App.GetSystem<FlowSystem>().Commit(new OperationDrop(DropPayload.SkillChip(0), new DropLanding(LandingPlace.Cage, 0, host.Id))), Is.True);
            Assert.That(host.HasClockedInToday, Is.False);
            App.GetSystem<FlowSystem>().Place(host.Id, OperationArea.Extraction, 0);
            Assert.That(host.HasClockedInToday, Is.True);
        }
        [Test]
        public void F17_工作台夹具应用带入成长临时与任意格亲缘且不产生第二份公式()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量体" }, personality: "重视血脉", instances: new[] { new BenchSkill("能量体", 2, true) }, modifier: 1, workDays: 2, parentCells: new[] { 4 });
            row[4] = new BenchMonster(new[] { "能量体" });
            var board = new BenchBoard(1, new string[0], row, 10, 17);
            var details = ExtractionBench.Preview(board);
            var app = TheCallApp.Interface;
            var run = app.GetModel<RunModel>();
            var cells = app.GetModel<LevelModel>().Extraction;
            var child = run.Find(cells[0]); var parent = run.Find(cells[4]);
            Assert.That(child.ParentIds, Is.EqualTo(new[] { parent.Id }));
            Assert.That(child.Skills[0].Quote, Is.EqualTo(7));
            Assert.That(child.Skills[0].IsTemporary, Is.True);
            Assert.That(child.Modifier, Is.EqualTo(1));
            Assert.That(child.ConsecutiveWorkDays, Is.EqualTo(2));
            Assert.That(app.GetModel<LevelModel>().Energy, Is.EqualTo(10));
            Assert.That(details[0].Skills[0].InstanceId, Is.EqualTo(child.Skills[0].Id));
            Assert.That(run.ProductionLog, Is.Empty);
        }
        [Test]
        public void F17_工作台循环亲缘明确拒绝()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量体" }, parentCells: new[] { 1 });
            row[1] = new BenchMonster(new[] { "能量体" }, parentCells: new[] { 0 });
            Assert.Throws<BenchRejectedException>(() => ExtractionBench.Preview(new BenchBoard(1, new string[0], row)));
        }
        void ClearForStart()
        {
            foreach (var monster in Run.LivingMonsters.ToArray()) Run.TryDestroy(monster.Id);
            App.RegisterUtility<IDraw>(ScriptedDraw.Exact());
            App.RegisterUtility<ILevelCatalog>(new ScriptedLevelCatalog(0));
            Run.EnterLevelStart(2); Level.BeginLevel(0, 60);
        }
        [Test]
        public void Content_好奇开始新增及改写常驻实例清原成长保留怪物修正()
        {
            ClearForStart();
            var host = Run.CreateMonster(new[] { "能量体" }, modifier: 7, personality: "好奇");
            var originalId = host.Skills[0].Id; host.Skills[0].Add(100, true);
            App.RegisterUtility<IDraw>(new NamedDraw("右能量体", "左能量体"));
            App.GetSystem<FlowSystem>().BeginLevel();
            Assert.That(host.Skills.Select(x => x.Name), Is.EqualTo(new[] { "左能量体", "右能量体" }));
            Assert.That(host.Skills.All(x => !x.IsTemporary && x.SourceKind == SkillSourceKind.Personality), Is.True);
            Assert.That(host.Skills.Any(x => x.Id == originalId), Is.False);
            Assert.That(host.Skills[0].Quote, Is.EqualTo(2));
            Assert.That(host.Modifier, Is.EqualTo(7));
            App.GetSystem<FlowSystem>().BeginLevel();
            Assert.That(host.Skills.Count, Is.EqualTo(2));
        }
        [Test]
        public void Content_上进心每日开始只加一次常驻修正()
        {
            ClearForStart(); var host = Run.CreateMonster(new[] { "能量体" }, personality: "上进心");
            var flow = App.GetSystem<FlowSystem>(); flow.BeginLevel(); flow.BeginLevel();
            Assert.That(host.Modifier, Is.EqualTo(1));
            Run.EnterLevelStart(3); flow.BeginLevel();
            Assert.That(host.Modifier, Is.EqualTo(2));
        }
        [TestCase("乐天派", "热情")] [TestCase("摸鱼派", "疲倦")] [TestCase("厌世", "倦怠")]
        public void Content_好学经真实开工获得常驻技能并即时复制固定性格心情(string personality, string mood)
        {
            ClearForStart(); Run.EnterOperation(2);
            var host = Run.CreateMonster(new[] { "能量体" }, personality: "好学"); Run.AddToCage(host);
            var target = Run.CreateMonster(new[] { "左能量体" }, personality: personality); Level.Put(OperationArea.Extraction, 0, target.Id);
            App.GetSystem<FlowSystem>().Place(host.Id, OperationArea.Extraction, 1);
            Assert.That(host.Personality, Is.EqualTo(personality)); Assert.That(host.Mood, Is.EqualTo(mood));
            Assert.That(host.Skills.Select(x => x.Name), Is.EqualTo(new[] { "能量体", "左能量体" }));
            Assert.That(host.Skills.Last().SourceKind, Is.EqualTo(SkillSourceKind.Personality));
            Assert.That(host.Skills.Last().SourceMonsterId, Is.EqualTo(target.Id));
            Assert.That(host.Skills.Last().SourceId, Is.EqualTo(target.Skills[0].Id));
            Assert.That(host.Skills.Last().IsTemporary, Is.False);
            Assert.That(host.ConsecutiveWorkDays, Is.Zero);
        }
        [Test]
        public void Content_好学加严选真实开始与开工保留当天提升不重记历史()
        {
            ClearForStart(); Run.AddTool("怪物严选");
            var host = Run.CreateMonster(new[] { "能量体" }, personality: "好学"); Run.AddToCage(host);
            var target = Run.CreateMonster(new[] { "左能量体" }, personality: "急躁"); Level.Put(OperationArea.Extraction, 0, target.Id);
            App.RegisterUtility<IDraw>(new NamedDraw(host.Id));
            var flow = App.GetSystem<FlowSystem>(); flow.BeginLevel();
            Assert.That(host.Mood, Is.EqualTo("热情"));
            flow.Place(host.Id, OperationArea.Extraction, 1);
            Assert.That(host.Personality, Is.EqualTo("急躁")); Assert.That(host.Mood, Is.EqualTo("热情"));
            Assert.That(host.DayBaseMood, Is.EqualTo("普通")); Assert.That(host.DayMoodBoost, Is.EqualTo(1));
            Assert.That(host.ConsecutiveWorkDays, Is.Zero);
        }
        [Test]
        public void Content_好学重复或上限失败不改变性格心情材料()
        {
            ClearForStart(); Run.EnterOperation(2);
            var host = Run.CreateMonster(new[] { "能量体" }, personality: "好学"); Run.AddToCage(host);
            var target = Run.CreateMonster(new[] { "能量体" }, personality: "厌世"); Level.Put(OperationArea.Extraction, 0, target.Id);
            App.GetSystem<FlowSystem>().Place(host.Id, OperationArea.Extraction, 1);
            Assert.That(host.Personality, Is.EqualTo("好学")); Assert.That(host.Mood, Is.EqualTo("普通"));
            Assert.That(host.Skills.Count, Is.EqualTo(1));
        }
        [TestCase("热爱工作", "热情")] [TestCase("厌恶工作", "倦怠")]
        public void Content_工作性格通过两天实际结算影响次日心情且加班不双记(string personality, string mood)
        {
            ClearForStart(); Run.EnterOperation(1);
            var host = Run.CreateMonster(new[] { "能量体" }, personality: personality); Run.AddToCage(host);
            var flow = App.GetSystem<FlowSystem>();
            for (var day = 1; day <= 2; day++)
            {
                flow.Place(host.Id, OperationArea.Extraction, 0); flow.Confirm(); flow.LeaveShop();
                Assert.That(host.ConsecutiveWorkDays, Is.EqualTo(day));
            }
            Assert.That(host.Mood, Is.EqualTo(mood));
        }
        [Test]
        public void Content_隔音棉阻止厌恶工作两天后的下降()
        {
            ClearForStart(); Run.EnterOperation(1); Run.AddTool("隔音棉");
            var host = Run.CreateMonster(new[] { "能量体" }, personality: "厌恶工作"); Run.AddToCage(host);
            var flow = App.GetSystem<FlowSystem>();
            for (var day = 1; day <= 2; day++) { flow.Place(host.Id, OperationArea.Extraction, 0); flow.Confirm(); flow.LeaveShop(); }
            Assert.That(host.ConsecutiveWorkDays, Is.EqualTo(2)); Assert.That(host.Mood, Is.EqualTo("普通"));
        }
        [Test]
        public void Content_打卡机仅离店生效一次额外十金币()
        {
            ClearForStart(); Run.EnterOperation(1); Run.AddTool("打卡机");
            var flow = App.GetSystem<FlowSystem>(); flow.Confirm(); var before = Run.Gold;
            flow.LeaveShop(); Assert.That(Run.Gold, Is.EqualTo(before + 10));
            flow.LeaveShop(); Assert.That(Run.Gold, Is.EqualTo(before + 10));
        }
        [TestCase("基因实验")] [TestCase("槽位扩容")] [TestCase("大乱炖")]
        [TestCase("变异学说")] [TestCase("科学培育")] [TestCase("基因仓储")]
        public void Content_六项科技逐项无前置一点仅一次且零余额拒绝(string name)
        {
            ClearForStart(); Run.EnterOperation(1); var flow = App.GetSystem<FlowSystem>();
            flow.Unlock(name); Assert.That(Run.HasTech(name), Is.False);
            Run.AddTechPoint(); flow.Unlock(name); Assert.That(Run.HasTech(name), Is.True); Assert.That(Run.TechPoints, Is.Zero);
            Run.AddTechPoint(); flow.Unlock(name); Assert.That(Run.TechPoints, Is.EqualTo(1));
            var tech = App.GetUtility<TechCatalog>();
            switch (name)
            {
                case "基因实验": Assert.That(tech.AllowsBreedingSkill(Run.UnlockedTech), Is.True); break;
                case "槽位扩容": Assert.That(Level.BreedingSlotCount, Is.EqualTo(2)); break;
                case "大乱炖": Assert.That(tech.ParentCapacity(Run.UnlockedTech), Is.EqualTo(3)); break;
                case "变异学说": Assert.That(tech.GrantsExtraSkill(Run.UnlockedTech), Is.True); break;
                case "科学培育": Assert.That(tech.GrantsModifier(Run.UnlockedTech), Is.True); break;
                case "基因仓储": Assert.That(Run.GeneCapacity, Is.EqualTo(4)); break;
                default: Assert.Fail("未覆盖科技"); break;
            }
        }
        void PrepareShop(string tool)
        {
            ClearForStart(); Run.EnterOperation(1); App.GetSystem<FlowSystem>().Confirm(); Run.AddGold(1000);
            if (!Run.ShelfToolNames.Contains(tool)) Run.AddShelfTool(tool);
        }
        [Test]
        public void Content_招工海报购买即补第五怪物且下次刷新仍五个()
        {
            PrepareShop("招工海报"); var shop = App.GetSystem<ShopSystem>();
            Assert.That(Run.ShelfMonsterIds.Count, Is.EqualTo(4)); var before = Run.Gold;
            shop.BuyTool("招工海报"); Assert.That(Run.ShelfMonsterIds.Count, Is.EqualTo(5));
            Assert.That(Run.Gold, Is.EqualTo(before - 30)); Assert.That(Run.HasTool("招工海报"), Is.True);
            shop.Refresh(); Assert.That(Run.ShelfMonsterIds.Count, Is.EqualTo(5));
            Assert.That(Run.ShelfToolNames, Does.Not.Contain("招工海报"));
        }
        [Test]
        public void Content_验货单购买即补第三工具且不再刷新已购工具()
        {
            PrepareShop("验货单"); var shop = App.GetSystem<ShopSystem>(); var before = Run.Gold;
            shop.BuyTool("验货单"); Assert.That(Run.Gold, Is.EqualTo(before - 50));
            Assert.That(Run.ShelfToolNames.Count, Is.EqualTo(3)); Assert.That(Run.ShelfToolNames.Distinct().Count(), Is.EqualTo(3));
            Assert.That(Run.ShelfToolNames, Does.Not.Contain("验货单"));
            shop.Refresh(); Assert.That(Run.ShelfToolNames.Count, Is.EqualTo(3)); Assert.That(Run.ShelfToolNames, Does.Not.Contain("验货单"));
        }
        [Test]
        public void Content_批发合同不折扣自身但后买每个工具减五金币()
        {
            PrepareShop("批发合同"); var shop = App.GetSystem<ShopSystem>(); var before = Run.Gold;
            shop.BuyTool("批发合同"); Assert.That(Run.Gold, Is.EqualTo(before - 30));
            if (!Run.ShelfToolNames.Contains("隔音棉")) Run.AddShelfTool("隔音棉");
            before = Run.Gold; shop.BuyTool("隔音棉"); Assert.That(Run.Gold, Is.EqualTo(before - 45));
        }
        [Test]
        public void Content_上级员工证购买后下一天可放第六格()
        {
            PrepareShop("上级员工证"); App.GetSystem<ShopSystem>().BuyTool("上级员工证");
            App.GetSystem<FlowSystem>().LeaveShop();
            Assert.That(Level.Extraction.Count, Is.EqualTo(6));
            var host = Run.CreateMonster(new[] { "能量体" }); Run.AddToCage(host);
            App.GetSystem<FlowSystem>().Place(host.Id, OperationArea.Extraction, 5);
            Assert.That(Level.Extraction[5], Is.EqualTo(host.Id));
        }
        [Test]
        public void Content_研磨机仅成功废弃加五金币满仓拒绝不发钱()
        {
            ClearForStart(); Run.EnterOperation(1); Run.AddTool("研磨机");
            var host = Run.CreateMonster(new[] { "能量体" }); Run.AddToCage(host);
            App.GetSystem<FlowSystem>().Discard(host.Id);
            Assert.That(Run.Gold, Is.EqualTo(5)); Assert.That(Run.Find(host.Id), Is.Null);
            Assert.That(Run.SkillSlots, Is.EqualTo(new[] { "能量体" }));
            Run.PutSkillInSlot("能量体"); Run.PutSkillInSlot("能量体");
            var blocked = Run.CreateMonster(new[] { "能量体" }); Run.AddToCage(blocked);
            App.GetSystem<FlowSystem>().Discard(blocked.Id);
            Assert.That(Run.Gold, Is.EqualTo(5)); Assert.That(Run.Find(blocked.Id), Is.SameAs(blocked));
        }
        [Test]
        public void Content_复读腺不增加四技能上限()
        {
            ClearForStart(); Run.AddTool("复读腺");
            var host = Run.CreateMonster(new[] { "能量体" });
            for (var i = 0; i < 3; i++) Assert.That(Run.GainSkill(host.Id, "能量体").Success, Is.True);
            var before = host.Skills.Select(x => x.Id).ToArray();
            var result = Run.GainSkill(host.Id, "能量体"); Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Does.Contain("上限")); Assert.That(host.Skills.Select(x => x.Id), Is.EqualTo(before));
        }
        [Test]
        public void Content_好学复读来源记录实际抽中的第二实例且不继承其成长()
        {
            ClearForStart(); Run.EnterOperation(2); Run.AddTool("复读腺");
            var host = Run.CreateMonster(new[] { "左能量体" }, personality: "好学"); Run.AddToCage(host);
            var target = Run.CreateMonster(new[] { "能量体", "能量体" }); target.Skills[1].Add(100, true);
            Level.Put(OperationArea.Extraction, 0, target.Id);
            App.RegisterUtility<IDraw>(new SecondInstanceDraw());
            App.GetSystem<FlowSystem>().Place(host.Id, OperationArea.Extraction, 1);
            Assert.That(host.Skills.Last().SourceId, Is.EqualTo(target.Skills[1].Id));
            Assert.That(host.Skills.Last().SourceId, Is.Not.EqualTo(target.Skills[0].Id));
            Assert.That(host.Skills.Last().Quote, Is.EqualTo(5));
            Assert.That(host.Skills.Last().IsTemporary, Is.False);
        }
        sealed class SecondInstanceDraw : IDraw
        {
            public T Choose<T>(IReadOnlyList<T> options) => options[options[0] is SkillInstance ? 1 : 0];
            public bool Chance(int percent) => false;
        }
        sealed class NamedDraw : IDraw
        {
            readonly string[] _names; public NamedDraw(params string[] names) { _names = names; }
            public T Choose<T>(IReadOnlyList<T> options)
            {
                foreach (var name in _names)
                    foreach (var option in options)
                        if ((option is string s && s == name) || (option is Monster m && m.Id == name)) return option;
                return options[0];
            }
            public bool Chance(int percent) => false;
        }
        sealed class MutationDraw : IDraw
        {
            public readonly List<int> PercentCalls = new List<int>();
            public T Choose<T>(IReadOnlyList<T> options)
            {
                foreach (var item in options) if (item is string s && s == "稳定") return item;
                foreach (var item in options) if (item is string s && s == "收尸腺") return item;
                return options[0];
            }
            public bool Chance(int percent) { PercentCalls.Add(percent); return percent == 10; }
        }
        sealed class SamplingDraw : IDraw
        {
            readonly int _boundary; public List<int> Counts = new List<int>(); public List<string> Words = new List<string>();
            public SamplingDraw(int boundary) { _boundary = boundary; }
            public T Choose<T>(IReadOnlyList<T> options)
            {
                Counts.Add(options.Count);
                if (Counts.Count == 1) { foreach (var item in options) Words.Add(item.ToString()); return options[_boundary]; }
                return options[0];
            }
            public bool Chance(int percent) => false;
        }
        sealed class IndexDraw : IDraw
        {
            readonly int _index; public int LastCount;
            public IndexDraw(int index) { _index = index; }
            public T Choose<T>(IReadOnlyList<T> options) { LastCount = options.Count; return options[_index % options.Count]; }
            public bool Chance(int percent) => false;
        }
        sealed class BreedIndexDraw : IDraw
        {
            readonly int _remove;
            public BreedIndexDraw(int remove) { _remove = remove; }
            public T Choose<T>(IReadOnlyList<T> options)
            {
                foreach (var item in options) if (item is string s && s == "安于现状") return item;
                if (options[0] is Rarity) foreach (var item in options) if (item is Rarity r && r == Rarity.White) return item;
                if (options[0] is int) return options[_remove == 0 ? 2 : 0];
                return options[0];
            }
            public bool Chance(int percent) => false;
        }
    }
}
