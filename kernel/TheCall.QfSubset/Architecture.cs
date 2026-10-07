using System;
using System.Linq;

namespace QFramework
{
    public interface IArchitecture
    {
        void RegisterSystem<T>(T system) where T : ISystem;

        void RegisterModel<T>(T model) where T : IModel;

        void RegisterUtility<T>(T utility) where T : IUtility;

        T GetSystem<T>() where T : class, ISystem;

        T GetModel<T>() where T : class, IModel;

        T GetUtility<T>() where T : class, IUtility;

        void SendCommand<T>(T command) where T : ICommand;

        TResult SendCommand<TResult>(ICommand<TResult> command);

        TResult SendQuery<TResult>(IQuery<TResult> query);

        void Deinit();
    }

    public abstract class Architecture<T> : IArchitecture where T : Architecture<T>, new()
    {
        bool mInited;
        readonly IOCContainer mContainer = new IOCContainer();

        public static Action<T> OnRegisterPatch = architecture => { };

        protected static T mArchitecture;

        public static IArchitecture Interface
        {
            get
            {
                if (mArchitecture == null)
                    InitArchitecture();

                return mArchitecture;
            }
        }

        public static void InitArchitecture()
        {
            if (mArchitecture != null)
                return;

            mArchitecture = new T();
            mArchitecture.Init();
            OnRegisterPatch?.Invoke(mArchitecture);

            foreach (var model in mArchitecture.mContainer.GetInstancesByType<IModel>().Where(m => !m.Initialized))
            {
                model.Init();
                model.Initialized = true;
            }

            foreach (var system in mArchitecture.mContainer.GetInstancesByType<ISystem>().Where(m => !m.Initialized))
            {
                system.Init();
                system.Initialized = true;
            }

            mArchitecture.mInited = true;
        }

        protected abstract void Init();

        public void Deinit()
        {
            OnDeinit();
            foreach (var system in mContainer.GetInstancesByType<ISystem>().Where(s => s.Initialized))
                system.Deinit();

            foreach (var model in mContainer.GetInstancesByType<IModel>().Where(m => m.Initialized))
                model.Deinit();

            mContainer.Clear();
            mArchitecture = null;
        }

        protected virtual void OnDeinit()
        {
        }

        public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
        {
            system.SetArchitecture(this);
            mContainer.Register(system);
            if (!mInited)
                return;

            system.Init();
            system.Initialized = true;
        }

        public void RegisterModel<TModel>(TModel model) where TModel : IModel
        {
            model.SetArchitecture(this);
            mContainer.Register(model);
            if (!mInited)
                return;

            model.Init();
            model.Initialized = true;
        }

        public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility =>
            mContainer.Register(utility);

        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => mContainer.Get<TSystem>();

        public TModel GetModel<TModel>() where TModel : class, IModel => mContainer.Get<TModel>();

        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => mContainer.Get<TUtility>();

        public TResult SendCommand<TResult>(ICommand<TResult> command) => ExecuteCommand(command);

        public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand => ExecuteCommand(command);

        protected virtual TResult ExecuteCommand<TResult>(ICommand<TResult> command)
        {
            command.SetArchitecture(this);
            return command.Execute();
        }

        protected virtual void ExecuteCommand(ICommand command)
        {
            command.SetArchitecture(this);
            command.Execute();
        }

        public TResult SendQuery<TResult>(IQuery<TResult> query) => DoQuery(query);

        protected virtual TResult DoQuery<TResult>(IQuery<TResult> query)
        {
            query.SetArchitecture(this);
            return query.Do();
        }
    }

    public interface IBelongToArchitecture
    {
        IArchitecture GetArchitecture();
    }

    public interface ICanSetArchitecture
    {
        void SetArchitecture(IArchitecture architecture);
    }

    public interface ICanInit
    {
        bool Initialized { get; set; }

        void Init();

        void Deinit();
    }

    public interface ICanGetModel : IBelongToArchitecture
    {
    }

    public static class CanGetModelExtension
    {
        public static T GetModel<T>(this ICanGetModel self) where T : class, IModel =>
            self.GetArchitecture().GetModel<T>();
    }

    public interface ICanGetSystem : IBelongToArchitecture
    {
    }

    public static class CanGetSystemExtension
    {
        public static T GetSystem<T>(this ICanGetSystem self) where T : class, ISystem =>
            self.GetArchitecture().GetSystem<T>();
    }

    public interface ICanGetUtility : IBelongToArchitecture
    {
    }

    public static class CanGetUtilityExtension
    {
        public static T GetUtility<T>(this ICanGetUtility self) where T : class, IUtility =>
            self.GetArchitecture().GetUtility<T>();
    }

    public interface IUtility
    {
    }

    public interface ISystem : IBelongToArchitecture, ICanSetArchitecture, ICanGetModel, ICanGetUtility, ICanGetSystem, ICanInit
    {
    }

    public abstract class AbstractSystem : ISystem
    {
        IArchitecture mArchitecture;

        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecture;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;

        public bool Initialized { get; set; }

        void ICanInit.Init() => OnInit();

        public void Deinit() => OnDeinit();

        protected virtual void OnDeinit()
        {
        }

        protected abstract void OnInit();
    }

    public interface IModel : IBelongToArchitecture, ICanSetArchitecture, ICanGetUtility, ICanInit
    {
    }

    public abstract class AbstractModel : IModel
    {
        IArchitecture mArchitecture;

        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecture;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;

        public bool Initialized { get; set; }

        void ICanInit.Init() => OnInit();

        public void Deinit() => OnDeinit();

        protected virtual void OnDeinit()
        {
        }

        protected abstract void OnInit();
    }

    public interface ICommand : IBelongToArchitecture, ICanSetArchitecture, ICanGetSystem, ICanGetModel, ICanGetUtility
    {
        void Execute();
    }

    public interface ICommand<TResult> : IBelongToArchitecture, ICanSetArchitecture, ICanGetSystem, ICanGetModel, ICanGetUtility
    {
        TResult Execute();
    }

    public abstract class AbstractCommand : ICommand
    {
        IArchitecture mArchitecture;

        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecture;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;

        void ICommand.Execute() => OnExecute();

        protected abstract void OnExecute();
    }

    public abstract class AbstractCommand<TResult> : ICommand<TResult>
    {
        IArchitecture mArchitecture;

        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecture;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;

        TResult ICommand<TResult>.Execute() => OnExecute();

        protected abstract TResult OnExecute();
    }

    public interface IQuery<TResult> : IBelongToArchitecture, ICanSetArchitecture, ICanGetModel, ICanGetSystem
    {
        TResult Do();
    }

    public abstract class AbstractQuery<T> : IQuery<T>
    {
        IArchitecture mArchitecture;

        public T Do() => OnDo();

        protected abstract T OnDo();

        public IArchitecture GetArchitecture() => mArchitecture;

        public void SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;
    }
}
