using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QFramework;

namespace TheCall.Tests
{
    public abstract class RulesFixture
    {
        protected IArchitecture App { get; private set; }

        [SetUp]
        public void SetUp()
        {
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(BookFile())));
            TheCallApp.OnRegisterPatch = app =>
                app.RegisterUtility<IDraw>(new ScriptedDraw(
                    "能量体",
                    "左能量体",
                    "右能量体",
                    "奇异香",
                    "怪异香",
                    "汲取鼻",
                    "吞噬大嘴",
                    "孤独心",
                    "双头能量体"));
            App = TheCallApp.Interface;
        }

        static string BookFile()
        {
#if UNITY_5_3_OR_NEWER
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "call-book.json");
#else
            return Path.Combine(TestContext.CurrentContext.TestDirectory, "call-book.json");
#endif
        }

        [TearDown]
        public void TearDown() => TheCallApp.Reset();

        protected string KeepOpened()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));
            DrawAdapt.Restore(App);
            var cage = App.SendQuery(new MonsterCageQuery()).ToArray();
            return cage.Length == 0 ? keptId : cage[0].Id;
        }

        protected void InstallFromSlot(string hostId, int slot = 0)
        {
            var run = App.GetModel<RunModel>();
            if (!run.TryTakeSkillAt(slot, out var skill))
                return;

            run.TryGainSkill(hostId, skill);
        }

        protected void UseDraw(params string[] names) => UseDraw(true, names);

        protected void UseExactDraw(params string[] names) => UseDraw(false, names);

        void UseDraw(bool adapt, string[] names)
        {
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(BookFile())));
            TheCallApp.OnRegisterPatch = app =>
                app.RegisterUtility<IDraw>(adapt ? new ScriptedDraw(names) : ScriptedDraw.Exact(names));
            App = TheCallApp.Interface;
        }

        protected void UseLevel(ILevelCatalog levels, params string[] names)
        {
            UseRules(new ScriptedDraw(names), levels);
        }

        protected void UseRules(
            IDraw draw,
            ILevelCatalog levels,
            IToolCatalog tools = null,
            ClockIntents intents = null)
        {
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(BookFile())));
            TheCallApp.OnRegisterPatch = app =>
            {
                app.RegisterUtility(draw);
                app.RegisterUtility(levels);
                if (tools != null)
                    app.RegisterUtility(tools);
                if (intents != null)
                    app.RegisterUtility(intents);
            };
            App = TheCallApp.Interface;
        }

        protected static bool Holds(MonsterView monster, string skillName)
        {
            if (monster == null || skillName == null)
                return false;

            var names = monster.SkillNames;
            if (names.Count == 1)
                return names[0] == skillName;

            if (names.Count == 0 || names[0] != skillName)
                return false;

            return skillName == "吞噬大嘴" || skillName == "良好肉体" || skillName == "优质肉体" ||
                   skillName == "争锋基因" || skillName == "潜藏基因" || skillName == "重血脉心" ||
                   skillName == "孤独心";
        }

        protected IReadOnlyList<SettlementLanding> Landings() =>
            App.SendQuery(new SettlementRecordQuery()).OfType<SettlementLanding>().ToArray();

        protected SettlementPayment Payment() =>
            App.SendQuery(new SettlementRecordQuery()).OfType<SettlementPayment>().Single();

        protected IReadOnlyList<string> RemovedIds() =>
            App.SendQuery(new SettlementRecordQuery())
                .OfType<SettlementRemoval>()
                .Where(item => item.Happened)
                .Select(item => item.MonsterId)
                .ToArray();
    }
}
