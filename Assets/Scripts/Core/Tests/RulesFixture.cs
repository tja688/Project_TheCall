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
                    "能量吐息",
                    "左能量体",
                    "右能量体",
                    "增量小手",
                    "增量大手",
                    "残留提取腺体",
                    "孤独心",
                    "吞噬大嘴",
                    "双重吐息"));
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

        protected void UseDraw(params string[] names)
        {
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(BookFile())));
            TheCallApp.OnRegisterPatch = app =>
                app.RegisterUtility<IDraw>(new ScriptedDraw(names));
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
