using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tods
{
    public class Coroutine
    {
        public delegate IEnumerable<int> LogicEntryDelegate();

        private readonly List<LogicEntry> _logicEntries = new List<LogicEntry>();
        private readonly ConcurrentQueue<LogicEntryDelegate> _newLogicEntries = new ConcurrentQueue<LogicEntryDelegate>();

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private DateTime _previousTime;
        private readonly int _logicInterval;

        private const int DefaultLogicInterval = 128;

        public Coroutine()
            : this(DefaultLogicInterval)
        {
        }

        public Coroutine(int logicInterval)
        {
            _previousTime = DateTime.Now;
            _logicInterval = logicInterval;
        }

        public void AddEntry(LogicEntryDelegate entry)
        {
            _newLogicEntries.Enqueue(entry);
        }

        public void Start()
        {
            var thread = new Thread(EntryLoop) { IsBackground = true };
            thread.Start();
        }

        private void EntryLoop()
        {
            while (true)
            {
                IterateLogic();
                _resetEvent.WaitOne(_logicInterval, true);
            }
        }

        public void IterateLogic()
        {
            var now = DateTime.Now;
            var delta = (now - _previousTime).Milliseconds;

            LogicEntryDelegate newDelegator;
            while (_newLogicEntries.TryDequeue(out newDelegator))
            {
                var newEntry = new LogicEntry
                {
                    Enumerator = newDelegator().GetEnumerator(),
                    SleepTime = 0
                };
                _logicEntries.Add(newEntry);
            }

            var removals = new List<LogicEntry>();
            foreach (var each in _logicEntries)
            {
                each.SleepTime -= delta;
                if (each.SleepTime >= 0)
                    continue;

                if (!each.Enumerator.MoveNext())
                    removals.Add(each);
                else each.SleepTime = each.Enumerator.Current;
            }

            _logicEntries.RemoveAll(removals.Contains);

            _previousTime = now;
        }

        private class LogicEntry
        {
            public IEnumerator<int> Enumerator;
            public int SleepTime;
        }
    }
}
