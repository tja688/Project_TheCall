using QFramework;

namespace TheCall
{
    public sealed class TheCallApp : Architecture<TheCallApp>
    {
        public static bool IsRunning => mArchitecture != null;

        public static void Reset()
        {
            if (mArchitecture != null)
                mArchitecture.Deinit();

            OnRegisterPatch = _ => { };
        }

        protected override void Init()
        {
            var book = ContentGate.Current;
            if (book == null)
                throw new System.InvalidOperationException("先 ContentGate.Use，再访问 Interface。");

            RegisterUtility(new SkillCatalog(book));
            RegisterUtility(new SkillCopy(book));
            RegisterUtility<IDraw>(new SystemDraw());
            RegisterUtility<ILevelCatalog>(new LevelCatalog(book));
            RegisterUtility<IToolCatalog>(new ToolCatalog(book));
            RegisterUtility(new TechCatalog(book));
            RegisterModel(new RunModel());
            RegisterModel(new LevelModel());
            RegisterModel(new SettlementSight());
            RegisterSystem(new FlowSystem());
            RegisterSystem(new SettlementSystem());
            RegisterSystem(new ShopSystem());
            RegisterSystem(new BreedingSystem());
        }
    }
}
