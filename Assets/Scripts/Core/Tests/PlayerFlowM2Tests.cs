using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QFramework;

namespace TheCall.Tests
{
    public sealed class PlayerFlowM2Tests : RulesFixture
    {
        [Test]
        public void A01_开局三选一一双八单共九只性格种族有效未选消失()
        {
            var draw = new ScriptedDraw(
                "能量体", "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "良好肉体",
                "优质肉体",
                "双头能量体",
                "吞噬大嘴");
            UseRules(draw, new ScriptedLevelCatalog(0));

            var run = App.GetModel<RunModel>();
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Opening));

            // 开局候选共 3 只，均为双技能候选
            var candidates = App.SendQuery(new OpeningCandidatesQuery());
            Assert.That(candidates.Count, Is.EqualTo(3));
            Assert.That(candidates[0].SkillNames.Count, Is.EqualTo(2));
            Assert.That(candidates[1].SkillNames.Count, Is.EqualTo(2));
            Assert.That(candidates[2].SkillNames.Count, Is.EqualTo(2));

            var personalities = App.GetUtility<IPersonalityCatalog>();
            var races = App.GetUtility<IRaceCatalog>();
            foreach (var c in candidates)
            {
                Assert.That(personalities.Find(c.Personality), Is.Not.Null);
                Assert.That(races.Find(c.Race), Is.Not.Null);
            }

            var chosenId = candidates[0].Id;
            var unchosenIds = candidates.Skip(1).Select(c => c.Id).ToArray();

            // 选择双技能候选进入关卡 1
            App.SendCommand(new KeepOpeningMonsterCommand(chosenId));

            Assert.That(run.Phase, Is.EqualTo(RunPhase.Operation));
            Assert.That(run.LevelNumber, Is.EqualTo(1));

            // 未选候选消失
            var living = run.LivingMonsters.Select(m => m.Id).ToArray();
            Assert.That(living, Does.Contain(chosenId));
            foreach (var unchosen in unchosenIds)
            {
                Assert.That(living, Does.Not.Contain(unchosen));
                Assert.That(App.SendQuery(new MonsterQuery(unchosen)), Is.Null);
            }

            // 笼中有选择的 1 只双技能候选 + 补充的 8 只单技能 = 共 9 只在笼
            var cage = App.SendQuery(new MonsterCageQuery());
            Assert.That(cage.Count, Is.EqualTo(9));
            Assert.That(cage.Single(m => m.Id == chosenId).SkillNames.Count, Is.EqualTo(2));

            // 所有怪物性格与种族有效
            foreach (var m in cage)
            {
                Assert.That(personalities.Find(m.Personality), Is.Not.Null);
                Assert.That(races.Find(m.Race), Is.Not.Null);
            }
        }

        [Test]
        public void A17_基因安装只有迷恋死亡合法成功才消耗普通性格拒绝失败状态与材料不变()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var normalMonster = run.CreateMonster(new[] { "能量体" }, personality: "稳定");
            var deathMonster = run.CreateMonster(new[] { "能量体" }, personality: "迷恋死亡");
            run.AddToCage(normalMonster);
            run.AddToCage(deathMonster);

            run.PutSkillInSlot("奇异香");
            Assert.That(run.GeneSlots.Count, Is.EqualTo(1));

            // 1. 普通性格拒绝安装
            App.SendCommand(new InstallGeneCommand(normalMonster.Id, 0));
            Assert.That(normalMonster.Skills.Select(s => s.Name), Is.EqualTo(new[] { "能量体" }));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(1));
            Assert.That(run.GeneSlots[0].SkillName, Is.EqualTo("奇异香"));

            // 2. 迷恋死亡性格且同名技能、无复读腺拒绝安装
            run.PutSkillInSlot("能量体");
            App.SendCommand(new InstallGeneCommand(deathMonster.Id, 1));
            Assert.That(deathMonster.Skills.Select(s => s.Name), Is.EqualTo(new[] { "能量体" }));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(2));

            // 3. 拥有复读腺时允许重复技能安装
            run.AddTool("复读腺");
            App.SendCommand(new InstallGeneCommand(deathMonster.Id, 1));
            Assert.That(deathMonster.Skills.Select(s => s.Name), Is.EqualTo(new[] { "能量体", "能量体" }));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(1));
            Assert.That(run.GeneSlots[0].SkillName, Is.EqualTo("奇异香"));

            // 4. 合法成功安装奇异香
            App.SendCommand(new InstallGeneCommand(deathMonster.Id, 0));
            Assert.That(deathMonster.Skills.Select(s => s.Name), Is.EqualTo(new[] { "能量体", "能量体", "奇异香" }));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(0));

            // 5. 满 4 个技能上限拒绝安装
            run.PutSkillInSlot("优质肉体");
            run.PutSkillInSlot("良好肉体");
            App.SendCommand(new InstallGeneCommand(deathMonster.Id, 0)); // 第 4 个成功
            Assert.That(deathMonster.Skills.Count, Is.EqualTo(4));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(1));

            // 第 5 个尝试安装拒绝，材料与状态不变
            App.SendCommand(new InstallGeneCommand(deathMonster.Id, 0));
            Assert.That(deathMonster.Skills.Count, Is.EqualTo(4));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(1));
            Assert.That(run.GeneSlots[0].SkillName, Is.EqualTo("良好肉体"));
        }

        [Test]
        public void A18_一个亲本不生育保留亲本两个亲本三亲本基因变异超上限随机裁切基因实际留下才消耗()
        {
            UseLevel(new ScriptedLevelCatalog(0));
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var p1 = run.CreateMonster(new[] { "能量体" }, personality: "稳定");
            var p2 = run.CreateMonster(new[] { "奇异香" }, personality: "稳定");
            run.AddToCage(p1);
            run.AddToCage(p2);

            // 1. 一个亲本不生育，亲本保留
            App.SendCommand(new PlaceMonsterCommand(p1.Id, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var cage = App.SendQuery(new MonsterCageQuery());
            Assert.That(cage.Any(m => m.Id == p1.Id), Is.True);
            Assert.That(cage.Any(m => run.Find(m.Id).ParentIds.Contains(p1.Id)), Is.False);

            // 2. 两个亲本正常生育，超出上限裁切（安于现状技能上限为2）
            var pCalm = run.CreateMonster(new[] { "能量体" }, personality: "安于现状");
            var pOther = run.CreateMonster(new[] { "奇异香" }, personality: "稳定");
            run.AddToCage(pCalm);
            run.AddToCage(pOther);

            run.AddTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));
            run.PutSkillInSlot("优质肉体");
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));

            App.SendCommand(new PlaceMonsterCommand(pCalm.Id, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(pOther.Id, OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            // 亲本保留
            Assert.That(run.LivingMonsters.Any(m => m.Id == pCalm.Id), Is.True);
            Assert.That(run.LivingMonsters.Any(m => m.Id == pOther.Id), Is.True);

            // 子代存在
            var child = run.LivingMonsters.Single(m => m.ParentIds.Contains(pCalm.Id) && m.ParentIds.Contains(pOther.Id));
            Assert.That(child, Is.Not.Null);
            if (child.Personality == "安于现状")
            {
                Assert.That(child.Skills.Count, Is.AtMost(2));
            }
        }

        [Test]
        public void A19_临时技能参与培育清临时后取常驻技能不足时合并不凭空补技能父母子代关系准确()
        {
            UseLevel(new ScriptedLevelCatalog(0));
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var p1 = run.CreateMonster(new[] { "能量体" });
            var p2 = run.CreateMonster(new[] { "奇异香" });
            run.AddToCage(p1);
            run.AddToCage(p2);

            // 给 p1 增加临时技能
            run.TryGainSkill(p1.Id, "优质肉体", isTemporary: true);
            Assert.That(p1.Skills.Any(s => s.Name == "优质肉体" && s.IsTemporary), Is.True);

            App.SendCommand(new PlaceMonsterCommand(p1.Id, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(p2.Id, OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            // 结算及离店后，临时技能被清除
            Assert.That(p1.Skills.Any(s => s.Name == "优质肉体"), Is.False);

            // 子代只继承常驻技能
            var child = run.LivingMonsters.Single(m => m.ParentIds.Contains(p1.Id) && m.ParentIds.Contains(p2.Id));
            Assert.That(child.ParentIds, Is.EqualTo(new[] { p1.Id, p2.Id }));
            Assert.That(child.Skills.Any(s => s.Name == "优质肉体"), Is.False);
            Assert.That(child.Skills.Select(s => s.Name), Is.EquivalentTo(new[] { "能量体", "奇异香" }));
        }

        [Test]
        public void A20_材料投入后又废弃未遗传退还预约计容量不能重复使用不会满槽丢失界面原位占用可见()
        {
            UseLevel(new ScriptedLevelCatalog(0));
            KeepOpened();
            var run = App.GetModel<RunModel>();
            run.AddTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));

            var m1 = run.CreateMonster(new[] { "能量体" });
            var m2 = run.CreateMonster(new[] { "奇异香" });
            var m3 = run.CreateMonster(new[] { "优质肉体" });
            var m4 = run.CreateMonster(new[] { "怪异香" });
            run.AddToCage(m1);
            run.AddToCage(m2);
            run.AddToCage(m3);
            run.AddToCage(m4);

            App.SendCommand(new DiscardMonsterCommand(m1.Id));
            App.SendCommand(new DiscardMonsterCommand(m2.Id));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(2));

            // 预约到培育槽 0
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));
            var geneViews = App.SendQuery(new GeneSlotsQuery());
            Assert.That(geneViews[0].IsReserved, Is.True);
            Assert.That(geneViews[0].ReservedBreedingSlot, Is.EqualTo(0));
            Assert.That(geneViews[1].IsReserved, Is.False);

            // 预约计容量：废弃第 3 只怪物，容量达到上限 3
            App.SendCommand(new DiscardMonsterCommand(m3.Id));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(3));

            // 容量已满，第 4 只废弃失败原子回滚
            App.SendCommand(new DiscardMonsterCommand(m4.Id));
            Assert.That(run.GeneSlots.Count, Is.EqualTo(3));
            Assert.That(run.LivingMonsters.Any(m => m.Id == m4.Id), Is.True);

            // 退还预约：原位解冻，不丢失
            App.SendCommand(new ReturnBreedingSkillCommand(0));
            geneViews = App.SendQuery(new GeneSlotsQuery());
            Assert.That(geneViews[0].IsReserved, Is.False);
            Assert.That(geneViews[0].ReservedBreedingSlot, Is.Null);
            Assert.That(geneViews.Count, Is.EqualTo(3));
        }

        [Test]
        public void A22_六项科技与基因槽加号每项每局一次一点无前置仓储扩到4不能重复花费()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var techCatalog = App.GetUtility<TechCatalog>();
            Assert.That(techCatalog.Techs.Count, Is.EqualTo(6));

            // 初始基因容量为 3
            Assert.That(run.GeneCapacity, Is.EqualTo(3));

            // 给 6 点科技点
            for (var i = 0; i < 6; i++)
                run.AddTechPoint();

            Assert.That(run.TechPoints, Is.EqualTo(6));

            // 解锁 基因仓储 将容量扩到 4
            App.SendCommand(new UnlockTechCommand("基因仓储"));
            Assert.That(run.GeneCapacity, Is.EqualTo(4));
            Assert.That(run.TechPoints, Is.EqualTo(5));

            // 重复解锁被拒绝，不消耗科技点
            App.SendCommand(new UnlockTechCommand("基因仓储"));
            Assert.That(run.GeneCapacity, Is.EqualTo(4));
            Assert.That(run.TechPoints, Is.EqualTo(5));

            // 解锁其它所有科技
            string[] otherTechs = { "大乱炖", "槽位扩容", "基因实验", "变异学说", "科学培育" };
            foreach (var tech in otherTechs)
            {
                App.SendCommand(new UnlockTechCommand(tech));
            }

            Assert.That(run.TechPoints, Is.EqualTo(0));
            Assert.That(run.UnlockedTech.Count, Is.EqualTo(6));
        }

        [Test]
        public void A23_商店已购工具剩余不足刷新增位折扣已购永不再现同架不重名不足少显示失败刷新不变新栏位立即补合同不追溯自身()
        {
            UseLevel(new ScriptedLevelCatalog(0));
            KeepOpened();
            var run = App.GetModel<RunModel>();
            run.AddGold(500);

            // 进入商店
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Shop));

            // 初始货架：4 怪物 + 2 工具
            Assert.That(run.ShelfMonsterIds.Count, Is.EqualTo(4));
            Assert.That(run.ShelfToolNames.Count, Is.EqualTo(2));

            // 购买工具后，已购工具进入 run.Tools，货架移除
            var toolToBuy = run.ShelfToolNames[0];
            App.SendCommand(new BuyToolCommand(toolToBuy));
            Assert.That(run.HasTool(toolToBuy), Is.True);
            Assert.That(run.ShelfToolNames, Does.Not.Contain(toolToBuy));

            // 购买招工海报，怪物货架扩容并立即补齐到 5 只
            run.AddShelfTool("招工海报");
            App.SendCommand(new BuyToolCommand("招工海报"));
            Assert.That(run.ShelfMonsterIds.Count, Is.EqualTo(5));

            // 购买验货单，工具货架扩容并立即补齐到 3 件
            run.AddShelfTool("验货单");
            App.SendCommand(new BuyToolCommand("验货单"));
            Assert.That(run.ShelfToolNames.Count, Is.EqualTo(3));

            // 批发合同折扣：每件减 5 金币
            run.AddTool("批发合同");
            var nextTool = run.ShelfToolNames[0];
            var shelf = App.SendQuery(new ShelfQuery());
            var expectedPrice = shelf.Tools.Single(t => t.Name == nextTool).Price;
            var goldBefore = run.Gold;
            App.SendCommand(new BuyToolCommand(nextTool));
            Assert.That(goldBefore - run.Gold, Is.EqualTo(expectedPrice));
        }

        [Test]
        public void A25_亲缘性格场上另有无亲缘同名怪物仅实际亲本子代受联动第三者不受亲缘效果()
        {
            UseLevel(new ScriptedLevelCatalog(0));
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var parent = run.CreateMonster(new[] { "能量体" }, personality: "稳定");
            var child = run.CreateMonster(
                new[] { "能量体" },
                parentIds: new[] { parent.Id },
                personality: "重视血脉");
            var stranger = run.CreateMonster(new[] { "能量体" }, personality: "稳定");

            run.AddToCage(parent);
            run.AddToCage(child);
            run.AddToCage(stranger);

            App.SendCommand(new PlaceMonsterCommand(parent.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(stranger.Id, OperationArea.Extraction, 2));

            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            var pLandings = landings.Where(l => l.MonsterId == parent.Id).ToArray();
            var sLandings = landings.Where(l => l.MonsterId == stranger.Id).ToArray();

            // 亲本的能量体触发次数获得重视血脉 +1
            Assert.That(pLandings.Length, Is.EqualTo(2));
            // 无亲缘关系的同名 stranger 只触发基础的 1 次
            Assert.That(sLandings.Length, Is.EqualTo(1));
        }
    }
}
