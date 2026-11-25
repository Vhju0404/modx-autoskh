using System;
using System.Collections.Generic;

namespace Mod.ModHelper
{
    /// <summary>
    /// Hàng đợi an toàn đa luồng (có lock + giới hạn lệnh/tick) để đẩy lệnh từ Socket về Main Thread Unity.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly object SyncLock = new object();
        private static readonly Queue<Action> Queue = new Queue<Action>();
        private const int MaxActionsPerTick = 16;

        public static void dispatcher(Action action)
        {
            if (action == null) return;
            lock (SyncLock)
            {
                if (Queue.Count < 256)
                {
                    Queue.Enqueue(action);
                }
            }
        }

        public static void update()
        {
            int processed = 0;
            while (processed < MaxActionsPerTick)
            {
                Action act = null;
                lock (SyncLock)
                {
                    if (Queue.Count == 0) break;
                    act = Queue.Dequeue();
                }
                if (act != null)
                {
                    try { act.Invoke(); }
                    catch { }
                }
                processed++;
            }
        }
    }
}
