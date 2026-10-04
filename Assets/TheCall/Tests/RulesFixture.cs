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

        [TearDown]
        public void TearDown() => TheCallApp.Reset();
    }
}
