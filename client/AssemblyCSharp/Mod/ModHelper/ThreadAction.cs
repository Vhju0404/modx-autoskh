using System;
using System.Threading;

namespace Mod.ModHelper
{
    public abstract class ThreadAction<T> where T : ThreadAction<T>, new()
    {
        public static T gI { get; } = new T();

        public bool IsActing => threadAction != null && threadAction.IsAlive;

        protected Thread threadAction;

        protected abstract void action();

        public void performAction()
        {
            if (IsActing) return;
            executeAction();
        }

        public void toggle(bool? isActing = null)
        {
            if (isActing == null) isActing = !IsActing;
            if (isActing.Value)
            {
                if (!IsActing) executeAction();
            }
            else if (IsActing)
            {
                try { threadAction.Abort(); } catch { }
            }
        }

        protected void executeAction()
        {
            threadAction = new Thread(action) { IsBackground = true };
            threadAction.Start();
        }
    }

    public abstract class ThreadActionUpdate<T> : ThreadAction<T> where T : ThreadActionUpdate<T>, new()
    {
        public abstract int Interval { get; }

        protected override void action()
        {
            while (true)
            {
                try { update(); }
                catch { }
                Thread.Sleep(Interval);
            }
        }

        protected abstract void update();
    }
}
