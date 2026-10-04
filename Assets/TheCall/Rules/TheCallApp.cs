using QFramework;

namespace TheCall
{
    public sealed class TheCallApp : Architecture<TheCallApp>
    {
        public static void Reset()
        {
            if (mArchitecture != null)
                mArchitecture.Deinit();

            OnRegisterPatch = _ => { };
        }

        protected override void Init()
        {
            RegisterUtility(new SkillCatalog());
            RegisterUtility<IDraw>(new SystemDraw());
            RegisterModel(new RunModel());
            RegisterModel(new LevelModel());
            RegisterSystem(new FlowSystem());
        }
    }
}
