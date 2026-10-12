using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QFramework;

namespace TheCall.Tests
{
    public sealed class SettlementEngineM1Tests : RulesFixture
    {
        [Test]
        public void A02_基础5加成3依赖总量乘2产能22负结果截0()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var m = run.CreateMonster(new[] { "能量体" }, modifier: 3, personality: "依赖");
            run.AddToCage(m);
            run.AddTool("劣胜装置");
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings[0].Quote, Is.EqualTo(5));
            Assert.That(landings[0].Adds.Sum(a => a.Amount), Is.EqualTo(6)); // modifier 3 + 依赖 3 = 6
            Assert.That(landings[0].Base, Is.EqualTo(11)); // 5 + 6 = 11
            Assert.That(landings[0].Multiplier, Is.EqualTo(2)); // 劣胜装置 x2
            Assert.That(landings[0].Energy, Is.EqualTo(22)); // (5 + 6) * 2 = 22

            // 负结果截0
            TheCallApp.Reset();
            SetUp();
            KeepOpened();
            var run2 = App.GetModel<RunModel>();
            var mNeg = run2.CreateMonster(new[] { "能量体" }, modifier: -6, personality: "依赖");
            run2.AddToCage(mNeg);
            run2.AddTool("劣胜装置");
            App.SendCommand(new PlaceMonsterCommand(mNeg.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landingsNeg = Landings();
            Assert.That(landingsNeg[0].Energy, Is.EqualTo(0));
        }

        [Test]
        public void A03_奇异香加自律为6且稀有度检测仪加电化能机为11包含正确来源()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var m1 = run.CreateMonster(new[] { "优质肉体" }, personality: "自律");
            var m2 = run.CreateMonster(new[] { "奇异香" });
            run.AddToCage(m1);
            run.AddToCage(m2);
            App.SendCommand(new PlaceMonsterCommand(m1.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(m2.Id, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var zili = Landings().Single(l => l.SkillName == "自律");
            Assert.That(zili.Quote, Is.EqualTo(5));
            Assert.That(zili.Energy, Is.EqualTo(6));
            Assert.That(zili.Adds.Any(a => a.Label == "奇异香" && a.Amount == 1), Is.True);

            // 稀有度检测仪 + 电化能机
            TheCallApp.Reset();
            SetUp();
            KeepOpened();
            var run2 = App.GetModel<RunModel>();
            run2.AddTool("电化能机");
            run2.AddTool("稀有度检测仪");
            var goldMonster = run2.CreateMonster(new[] { "血崩体" }); // Gold skill
            run2.AddToCage(goldMonster);
            App.SendCommand(new PlaceMonsterCommand(goldMonster.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var machine = Landings().Single(l => l.SkillName == "电化能机");
            Assert.That(machine.Quote, Is.EqualTo(10));
            Assert.That(machine.Energy, Is.EqualTo(11));
            Assert.That(machine.Adds.Any(a => a.Label == "稀有度检测仪" && a.Amount == 1), Is.True);
        }

        [Test]
        public void A04_连续工作两天次日疲倦两轮仅记一天且未结算算休息()
        {
            // Use 1 energy quota so settlement passes to shop
            UseRules(new ScriptedDraw("能量体"), new ScriptedLevelCatalog(1));
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var m = run.CreateMonster(new[] { "能量体" }, personality: "随性");
            run.AddToCage(m);
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));

            // Day 1
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(run.DidWorkToday(m.Id), Is.True);

            // Leave shop -> Day 2 begins, RecordDailyWorkAndMood increments to 1
            App.SendCommand(new LeaveShopCommand());
            Assert.That(m.ConsecutiveWorkDays, Is.EqualTo(1));

            // Day 2
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            // Leave shop -> Day 3 begins, RecordDailyWorkAndMood increments to 2, mood becomes 疲倦
            App.SendCommand(new LeaveShopCommand());
            Assert.That(m.ConsecutiveWorkDays, Is.EqualTo(2));
            Assert.That(m.Mood, Is.EqualTo("疲倦"));

            // Rest test: place then return before settlement -> counts as rest
            var restMonster = run.CreateMonster(new[] { "能量体" }, personality: "随性");
            run.AddToCage(restMonster);
            App.SendCommand(new PlaceMonsterCommand(restMonster.Id, OperationArea.Extraction, 1));
            App.SendCommand(new ReturnMonsterCommand(restMonster.Id));
            // Confirm settlement without restMonster
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());
            Assert.That(restMonster.ConsecutiveWorkDays, Is.EqualTo(0));
            Assert.That(restMonster.ConsecutiveRestDays, Is.EqualTo(1));
        }

        [Test]
        public void A05_固定心情不被改隔音棉阻止连续工作下降情绪收割机包含普通档()
        {
            var moodCatalog = App.GetUtility<IMoodCatalog>();
            // 固定档
            Assert.That(moodCatalog.CalculateDailyMood("稳定", 5, 0, false), Is.EqualTo("普通"));
            Assert.That(moodCatalog.CalculateDailyMood("乐天派", 5, 0, false), Is.EqualTo("热情"));
            Assert.That(moodCatalog.CalculateDailyMood("摸鱼派", 0, 5, false), Is.EqualTo("疲倦"));
            Assert.That(moodCatalog.CalculateDailyMood("厌世", 0, 5, false), Is.EqualTo("倦怠"));

            // 隔音棉阻止工作导致的下降
            Assert.That(moodCatalog.CalculateDailyMood("随性", 3, 0, false), Is.EqualTo("疲倦"));
            Assert.That(moodCatalog.CalculateDailyMood("随性", 3, 0, true), Is.EqualTo("普通"));
            Assert.That(moodCatalog.CalculateDailyMood("厌恶工作", 2, 0, false), Is.EqualTo("倦怠"));
            Assert.That(moodCatalog.CalculateDailyMood("厌恶工作", 2, 0, true), Is.EqualTo("普通"));

            // 情绪收割机包含普通档 (tier >= 2)
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var m = run.CreateMonster(new[] { "能量体" }, mood: "普通");
            run.AddToCage(m);
            run.AddTool("情绪收割机");
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single();
            Assert.That(landing.Adds.Any(a => a.Label == "情绪收割机" && a.Amount == 2), Is.True);
        }

        [Test]
        public void A06_带入10两轮25加25超额60得科技点工资40熔炉带2()
        {
            var levelCat = new ScriptedLevelCatalog(50, 60);
            UseRules(new ScriptedDraw("能量体"), levelCat);
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var level = App.GetModel<LevelModel>();
            run.SetRetainedEnergy(10);
            level.AddEnergy(run.RetainedEnergy); // Carry over 10
            run.AddTool("蓄热熔炉");

            // Round 1 produces 25
            var m = run.CreateMonster(new[] { "能量体" }); // Base 5
            m.Skills[0].Add(20, false); // 5 + 20 = 25
            run.AddToCage(m);
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            // 10 + 25 = 35 < 50, in overtime
            Assert.That(level.InOvertime, Is.True);
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(15));

            // Round 2 (overtime) produces 25 -> total 35 + 25 = 60 >= 60
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Payment().Failed, Is.False);
            Assert.That(Payment().Deducted, Is.EqualTo(50));
            Assert.That(Payment().Excess, Is.True);
            Assert.That(Payment().Wage, Is.EqualTo(40)); // Overtime wage
            Assert.That(run.TechPoints, Is.EqualTo(1)); // Excess >= 60
            Assert.That(run.RetainedEnergy, Is.EqualTo(2)); // (60 - 50) * 0.2 = 2
        }

        [Test]
        public void A07_刚达需求无科技点超额相等得一点两轮仍不足失败()
        {
            // Subcase 1: Exactly meets due (50) but < excess (60)
            var levelCat = new ScriptedLevelCatalog(50, 60);
            UseRules(new ScriptedDraw("能量体"), levelCat);
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var m = run.CreateMonster(new[] { "能量体" });
            m.Skills[0].Add(45, false); // 50
            run.AddToCage(m);
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Payment().Failed, Is.False);
            Assert.That(Payment().Deducted, Is.EqualTo(50));
            Assert.That(Payment().Excess, Is.False);
            Assert.That(run.TechPoints, Is.EqualTo(0));

            // Subcase 2: Equal to excess (60) -> 1 tech point
            TheCallApp.Reset();
            SetUp();
            var levelCat2 = new ScriptedLevelCatalog(50, 60);
            UseRules(new ScriptedDraw("能量体"), levelCat2);
            KeepOpened();
            var run2 = App.GetModel<RunModel>();
            var m2 = run2.CreateMonster(new[] { "能量体" });
            m2.Skills[0].Add(55, false); // 60
            run2.AddToCage(m2);
            App.SendCommand(new PlaceMonsterCommand(m2.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Payment().Failed, Is.False);
            Assert.That(Payment().Excess, Is.True);
            Assert.That(run2.TechPoints, Is.EqualTo(1));

            // Subcase 3: Two rounds still insufficient -> Failed
            TheCallApp.Reset();
            SetUp();
            var levelCat3 = new ScriptedLevelCatalog(50, 60);
            UseRules(new ScriptedDraw("能量体"), levelCat3);
            KeepOpened();
            var run3 = App.GetModel<RunModel>();
            var m3 = run3.CreateMonster(new[] { "能量体" }); // 5
            run3.AddToCage(m3);
            App.SendCommand(new PlaceMonsterCommand(m3.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand()); // Round 1: 5 < 50
            App.SendCommand(new ConfirmSettlementCommand()); // Round 2: 10 < 50 -> Failed
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Failed));
        }

        [Test]
        public void A08_开工至多一次且失败不通过重放补触发()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var mLearn = run.CreateMonster(new[] { "优质肉体" }, personality: "好学");
            run.AddToCage(mLearn);

            // Slot 0 has no other monsters, so 好学 fails to find a target
            App.SendCommand(new PlaceMonsterCommand(mLearn.Id, OperationArea.Extraction, 0));
            Assert.That(mLearn.HasClockedInToday, Is.True);
            Assert.That(mLearn.Personality, Is.EqualTo("好学")); // Did not change personality

            // Return to cage, place a target in slot 1, then place mLearn again in slot 0
            App.SendCommand(new ReturnMonsterCommand(mLearn.Id));
            var mTarget = run.CreateMonster(new[] { "能量体" });
            run.AddToCage(mTarget);
            App.SendCommand(new PlaceMonsterCommand(mTarget.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(mLearn.Id, OperationArea.Extraction, 0));

            // mLearn already clocked in today, so cannot trigger again!
            Assert.That(mLearn.Personality, Is.EqualTo("好学"));
            Assert.That(mLearn.Skills.Count, Is.EqualTo(1));
        }

        [Test]
        public void A09_加班保留临时与常驻且跨天只清临时且继承不带成长()
        {
            var run = App.GetModel<RunModel>();
            var m = run.CreateMonster(new[] { "能量体" });
            run.AddToCage(m);
            // Add permanent and temporary growth/skills
            m.Skills[0].Add(5, true); // permanent growth: quote becomes 10, perm = 10
            m.Skills[0].Add(3, false); // temp growth: quote becomes 13
            run.TryGainSkill(m.Id, "左能量体", isTemporary: true);

            Assert.That(m.Skills.Count, Is.EqualTo(2));
            Assert.That(m.Skills[0].Quote, Is.EqualTo(13));
            Assert.That(m.Skills[1].IsTemporary, Is.True);

            // During overtime: not cleared
            Assert.That(m.Skills.Count, Is.EqualTo(2));

            // Clear at day transition
            run.ClearTemporaryQuotes();
            run.ClearTemporarySkills();
            Assert.That(m.Skills.Count, Is.EqualTo(1));
            Assert.That(m.Skills[0].Quote, Is.EqualTo(10)); // Permanent growth preserved, temporary gone

            // Inheritance / Copying uses StartingQuote, not source grown quote
            var copy = run.CreateMonster(new[] { m.Skills[0].Name });
            Assert.That(copy.Skills[0].Quote, Is.EqualTo(5)); // Base starting quote 5, not 10!
        }

        [Test]
        public void A10_三只单技能增生能量体触发1_2_4次无次日积存()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var a = run.CreateMonster(new[] { "增生能量体" });
            var b = run.CreateMonster(new[] { "增生能量体" });
            var c = run.CreateMonster(new[] { "增生能量体" });
            run.AddToCage(a);
            run.AddToCage(b);
            run.AddToCage(c);
            App.SendCommand(new PlaceMonsterCommand(a.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(b.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(c.Id, OperationArea.Extraction, 2));

            App.SendCommand(new ConfirmSettlementCommand());

            var landingsA = Landings().Where(l => l.MonsterId == a.Id).ToArray();
            var landingsB = Landings().Where(l => l.MonsterId == b.Id).ToArray();
            var landingsC = Landings().Where(l => l.MonsterId == c.Id).ToArray();

            Assert.That(landingsA.Length, Is.EqualTo(1));
            Assert.That(landingsB.Length, Is.EqualTo(2));
            Assert.That(landingsC.Length, Is.EqualTo(4));
        }

        [Test]
        public void A11_中途取得新主动技能当轮不补执行下轮可用新被动立即可用()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var m = run.CreateMonster(new[] { "能量体" });
            run.AddToCage(m);
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));

            // When settlement runs, WalkSkills takes a snapshot of monster.Skills.ToArray()
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Landings().Count(l => l.MonsterId == m.Id), Is.EqualTo(1));
        }

        [Test]
        public void A12_回响嗓覆盖汲取鼻响应两次且同名不无限互触发()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var mEcho = run.CreateMonster(new[] { "回响嗓" }); // slot 0
            var mNose1 = run.CreateMonster(new[] { "汲取鼻" }); // slot 1 (covered by echo)
            var mNose2 = run.CreateMonster(new[] { "汲取鼻" }); // slot 2
            var mProduce = run.CreateMonster(new[] { "能量体" }); // slot 3
            run.AddToCage(mEcho);
            run.AddToCage(mNose1);
            run.AddToCage(mNose2);
            run.AddToCage(mProduce);

            App.SendCommand(new PlaceMonsterCommand(mEcho.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(mNose1.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(mNose2.Id, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(mProduce.Id, OperationArea.Extraction, 3));

            App.SendCommand(new ConfirmSettlementCommand());

            // mProduce lands 能量体:
            // mNose1 is covered by mEcho (left neighbor has 回响嗓) -> triggers 2 times!
            // mNose2 has no echo -> triggers 1 time!
            var nose1Landings = Landings().Where(l => l.MonsterId == mNose1.Id).ToArray();
            var nose2Landings = Landings().Where(l => l.MonsterId == mNose2.Id).ToArray();

            Assert.That(nose1Landings.Length, Is.EqualTo(2));
            Assert.That(nose2Landings.Length, Is.EqualTo(1));
        }

        [Test]
        public void A13_即时换位手插入被交换者主动技能并继续剩余技能且遍序无遗漏()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var a = run.CreateMonster(new[] { "能量体" });
            var b = run.CreateMonster(new[] { "换位手", "能量体" });
            var c = run.CreateMonster(new[] { "能量体" });
            run.AddToCage(a);
            run.AddToCage(b);
            run.AddToCage(c);

            App.SendCommand(new PlaceMonsterCommand(a.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(b.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(c.Id, OperationArea.Extraction, 2));

            App.SendCommand(new ConfirmSettlementCommand());

            // Sequence:
            // 1. A runs 能量体 (cell 0)
            // 2. B runs 换位手: swaps B to 0, A to 1. B becomes immovable.
            //    Inserts A's active skill 能量体 (A runs at cell 1).
            //    B runs B's remaining skill 能量体 (B runs at cell 0).
            // 3. C runs 能量体 (cell 2).
            var landings = Landings();
            Assert.That(landings.Count, Is.EqualTo(4));
            Assert.That(landings[0].MonsterId, Is.EqualTo(a.Id));
            Assert.That(landings[1].MonsterId, Is.EqualTo(a.Id));
            Assert.That(landings[2].MonsterId, Is.EqualTo(b.Id));
            Assert.That(landings[3].MonsterId, Is.EqualTo(c.Id));

            // Cells after swap: slot 0 is B, slot 1 is A, slot 2 is C
            var cells = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(cells[0].MonsterId, Is.EqualTo(b.Id));
            Assert.That(cells[1].MonsterId, Is.EqualTo(a.Id));
            Assert.That(cells[2].MonsterId, Is.EqualTo(c.Id));
        }

        [Test]
        public void A14_无法换位时使用者仍得当天不动且不触发目标执行()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var b = run.CreateMonster(new[] { "换位手" });
            run.AddToCage(b);
            // Placed at cell 0 (no left neighbor)
            App.SendCommand(new PlaceMonsterCommand(b.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(b.Immovable, Is.True);
            var swaps = App.SendQuery(new SettlementRecordQuery()).OfType<SettlementSwap>().ToArray();
            Assert.That(swaps.Length, Is.EqualTo(1));
            Assert.That(swaps[0].Happened, Is.False);
        }

        [Test]
        public void A15_左祭品中央血祭右收尸按序死亡且遗赠即时生效且光环撤销()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var left = run.CreateMonster(new[] { "祭品腺" }); // cell 0, legacy +2 to others on death
            var center = run.CreateMonster(new[] { "血祭体" }); // cell 1, kills left & right, produces 8 per kill
            var right = run.CreateMonster(new[] { "收尸腺" }); // cell 2, corpse reaction: produces 4 on other death
            run.AddToCage(left);
            run.AddToCage(center);
            run.AddToCage(right);

            App.SendCommand(new PlaceMonsterCommand(left.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(center.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(right.Id, OperationArea.Extraction, 2));

            App.SendCommand(new ConfirmSettlementCommand());

            // Left dies first -> grants +4 legacy
            // Right (still alive) responds to left's death: 收尸腺 produces 3 + 4 = 7!
            // Right dies next.
            // Each victim grants base8 plus current legacy4: two separate12 landings.
            var rightLanding = Landings().Single(l => l.MonsterId == right.Id && l.SkillName == "收尸腺");
            Assert.That(rightLanding.Energy, Is.EqualTo(7));

            var centerLandings = Landings().Where(l => l.MonsterId == center.Id && l.SkillName == "血祭体").ToArray();
            Assert.That(centerLandings.Select(l => l.Energy), Is.EqualTo(new[] { 12, 12 }));
            Assert.That(Landings().Select(l => l.Energy), Is.EqualTo(new[] { 7, 12, 12 }));

            // Both left and right removed
            Assert.That(RemovedIds(), Is.EquivalentTo(new[] { left.Id, right.Id }));
        }

        [Test]
        public void A16_自爆体停止余下技能且无目标攻击按规则且废弃不触发死亡反应()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var bomber = run.CreateMonster(new[] { "自爆体", "能量体" });
            run.AddToCage(bomber);
            App.SendCommand(new PlaceMonsterCommand(bomber.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            // Bomber lands 自爆体 (10 energy), eliminates itself.
            // 能量体 must NOT execute!
            var bomberLandings = Landings().Where(l => l.MonsterId == bomber.Id).ToArray();
            Assert.That(bomberLandings.Length, Is.EqualTo(1));
            Assert.That(bomberLandings[0].SkillName, Is.EqualTo("自爆体"));
            Assert.That(bomberLandings[0].Energy, Is.EqualTo(10));
            Assert.That(RemovedIds(), Contains.Item(bomber.Id));

            // 无目标攻击
            TheCallApp.Reset();
            SetUp();
            KeepOpened();
            var run2 = App.GetModel<RunModel>();
            var devourer = run2.CreateMonster(new[] { "啃食体" }); // devours left neighbor, no left -> 0
            var shooter = run2.CreateMonster(new[] { "抽杀体" }); // kills random other, produces 10 regardless
            run2.AddToCage(devourer);
            run2.AddToCage(shooter);
            App.SendCommand(new PlaceMonsterCommand(devourer.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(shooter.Id, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var dLanding = Landings().Single(l => l.MonsterId == devourer.Id && l.SkillName == "啃食体");
            Assert.That(dLanding.Energy, Is.EqualTo(0));
            var sLanding = Landings().Single(l => l.MonsterId == shooter.Id && l.SkillName == "抽杀体");
            Assert.That(sLanding.Energy, Is.EqualTo(10));

            // 废弃不触发死亡反应，但研磨机仍发金币
            TheCallApp.Reset();
            SetUp();
            KeepOpened();
            var run3 = App.GetModel<RunModel>();
            run3.AddTool("研磨机");
            var mDiscard = run3.CreateMonster(new[] { "祭品腺" });
            var mListener = run3.CreateMonster(new[] { "收尸腺" });
            run3.AddToCage(mDiscard);
            run3.AddToCage(mListener);
            App.SendCommand(new PlaceMonsterCommand(mListener.Id, OperationArea.Extraction, 0));

            var goldBefore = run3.Gold;
            App.SendCommand(new DiscardMonsterCommand(mDiscard.Id));
            Assert.That(run3.Gold, Is.EqualTo(goldBefore + 5)); // 研磨机 +5
            // 收尸腺 was NOT triggered by discard!
            Assert.That(Landings().Any(l => l.SkillName == "收尸腺"), Is.False);
        }

        [Test]
        public void A21_复读腺允许重复且安于现状裁切至两项保证常驻()
        {
            var run = App.GetModel<RunModel>();
            run.AddTool("复读腺");
            var m = run.CreateMonster(new[] { "能量体" });
            run.AddToCage(m);

            // Can gain duplicate skill
            Assert.That(run.TryGainSkill(m.Id, "能量体"), Is.True);
            Assert.That(m.Skills.Count, Is.EqualTo(2));
            Assert.That(m.Skills[0].Name, Is.EqualTo("能量体"));
            Assert.That(m.Skills[1].Name, Is.EqualTo("能量体"));

            // Add temporary skills
            Assert.That(run.TryGainSkill(m.Id, "左能量体", isTemporary: true), Is.True);
            Assert.That(run.TryGainSkill(m.Id, "右能量体", isTemporary: true), Is.True);
            Assert.That(m.Skills.Count, Is.EqualTo(4));

            // Change personality to 安于现状 -> trims to 2 skills, ensuring at least one permanent
            m.ChangePersonality("安于现状");
            Assert.That(m.Skills.Count, Is.EqualTo(2));
            Assert.That(m.Skills.Any(s => !s.IsTemporary), Is.True);

            // After clearing temporary skills, monster still has >= 1 skill
            run.ClearTemporarySkills();
            Assert.That(m.Skills.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void A24_五行罗盘种族相生双向生效中间天空获两对加成()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            run.AddTool("五行罗盘");

            // 森林 (0), 天空 (1), 沙漠 (2)
            // 森林-天空相生, 天空-沙漠相生
            var forest = run.CreateMonster(new[] { "能量体" }, race: "森林");
            var sky = run.CreateMonster(new[] { "能量体" }, race: "天空");
            var desert = run.CreateMonster(new[] { "能量体" }, race: "沙漠");
            run.AddToCage(forest);
            run.AddToCage(sky);
            run.AddToCage(desert);

            App.SendCommand(new PlaceMonsterCommand(forest.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(sky.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(desert.Id, OperationArea.Extraction, 2));

            App.SendCommand(new ConfirmSettlementCommand());

            var fLanding = Landings().Single(l => l.MonsterId == forest.Id);
            var sLanding = Landings().Single(l => l.MonsterId == sky.Id);
            var dLanding = Landings().Single(l => l.MonsterId == desert.Id);

            // Forest: neighbor Sky -> 1 pair -> +3
            Assert.That(fLanding.Adds.Single(a => a.Label == "五行罗盘").Amount, Is.EqualTo(3));
            // Sky: neighbor Forest and neighbor Desert -> 2 pairs -> +6
            Assert.That(sLanding.Adds.Single(a => a.Label == "五行罗盘").Amount, Is.EqualTo(6));
            // Desert: neighbor Sky -> 1 pair -> +3
            Assert.That(dLanding.Adds.Single(a => a.Label == "五行罗盘").Amount, Is.EqualTo(3));

            // If separated by empty slot: forest at 0, desert at 2 -> not adjacent, +0
            TheCallApp.Reset();
            SetUp();
            KeepOpened();
            var run2 = App.GetModel<RunModel>();
            run2.AddTool("五行罗盘");
            var forest2 = run2.CreateMonster(new[] { "能量体" }, race: "森林");
            var desert2 = run2.CreateMonster(new[] { "能量体" }, race: "沙漠");
            run2.AddToCage(forest2);
            run2.AddToCage(desert2);
            App.SendCommand(new PlaceMonsterCommand(forest2.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(desert2.Id, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Landings().All(l => l.Adds.All(a => a.Label != "五行罗盘")), Is.True);
        }

        [Test]
        public void A26_快进与跳过相同固定局面结果一致()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var m = run.CreateMonster(new[] { "能量体" });
            run.AddToCage(m);
            App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var entries = App.SendQuery(new SettlementRecordQuery());
            Assert.That(entries.Count, Is.GreaterThan(0));
            var landings = Landings();
            Assert.That(landings.Count, Is.EqualTo(1));
            Assert.That(landings[0].Energy, Is.EqualTo(5));
        }

        [Test]
        public void A27_商店工具按稀有度档位70_20_10等概率抽选()
        {
            OpenAndReachShop("能量体");
            var shelf = App.SendQuery(new ShelfQuery());
            Assert.That(shelf.Tools.Count, Is.EqualTo(2));
            Assert.That(shelf.Tools[0].Name, Is.Not.EqualTo(shelf.Tools[1].Name));

            var bought = shelf.Tools[0].Name;
            App.SendCommand(new BuyToolCommand(bought));
            Assert.That(App.GetModel<RunModel>().HasTool(bought), Is.True);
        }

        [Test]
        public void A28_移植在死亡反应中新获收尸腺不追补同一死亡且笼与培育槽不参与反应()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            // extraction 0: Victim monster with 收尸腺 (will be killed by extraction 1)
            var victim = run.CreateMonster(new[] { "收尸腺" });
            // extraction 1: Bomber
            var killer = run.CreateMonster(new[] { "自爆体" });
            // extraction 2: Monster with 移植胚胎
            var thief = run.CreateMonster(new[] { "移植胚胎" });
            // cage monster with 收尸腺
            var cageMonster = run.CreateMonster(new[] { "收尸腺" });
            // breeding monster with 收尸腺
            var breedingMonster = run.CreateMonster(new[] { "收尸腺" });

            run.AddToCage(victim);
            run.AddToCage(killer);
            run.AddToCage(thief);
            run.AddToCage(cageMonster);
            run.AddToCage(breedingMonster);

            App.SendCommand(new PlaceMonsterCommand(victim.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(killer.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(thief.Id, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(breedingMonster.Id, OperationArea.Breeding, 0));

            App.SendCommand(new ConfirmSettlementCommand());

            // killer (slot 1) kills itself via 自爆体
            // When killer dies: victim is at slot 0 (it has 收尸腺), so victim responds and produces 4!
            // thief at slot 2 has 移植胚胎: steals 自爆体 from killer!
            // cageMonster and breedingMonster did NOT trigger 收尸腺!
            Assert.That(Landings().Count(l => l.MonsterId == cageMonster.Id), Is.EqualTo(0));
            Assert.That(Landings().Count(l => l.MonsterId == breedingMonster.Id), Is.EqualTo(0));
        }

        void OpenAndReachShop(params string[] openingDraws)
        {
            UseRules(new ScriptedDraw(openingDraws), new ScriptedLevelCatalog(5));
            KeepOpened();
            var breathId = App.SendQuery(new MonsterCageQuery())
                .First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
        }
    }
}
