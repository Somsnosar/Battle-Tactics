using System.Collections.Generic;

namespace BattleTactics
{
    internal sealed class LayoutTimeline
    {
        private const int MaxEntries = 1024;

        private readonly object gate = new object();
        private readonly List<Entry> entries = new List<Entry>();
        private int lastOrderedMessageIndex = -1;

        internal bool IsEmpty
        {
            get
            {
                lock (gate)
                    return entries.Count == 0;
            }
        }

        // network callbacks and the game thread both use this, so every access takes the lock
        internal void NoteOrdered(int messageIndex)
        {
            lock (gate)
                lastOrderedMessageIndex = messageIndex;
        }

        internal void Add(Dictionary<string, int> layout, int indexOffset)
        {
            lock (gate)
            {
                entries.Add(new Entry(lastOrderedMessageIndex + indexOffset, layout));

                if (entries.Count > MaxEntries)
                    entries.RemoveRange(0, MaxEntries / 2);
            }
        }

        // an entry stamped n was sent after action n, so it governs actions after n
        internal Dictionary<string, int> EffectiveFor(int actionIndex)
        {
            lock (gate)
            {
                int governing = entries.FindLastIndex(entry => entry.Stamp < actionIndex);

                if (governing < 0)
                    return LayoutCodec.Empty();

                // later actions never need anything older than the governing entry
                entries.RemoveRange(0, governing);
                return entries[0].Layout;
            }
        }

        internal Dictionary<string, int> Latest()
        {
            lock (gate)
                return entries.Count == 0 ? LayoutCodec.Empty() : entries[entries.Count - 1].Layout;
        }

        internal void Clear()
        {
            lock (gate)
            {
                entries.Clear();
                lastOrderedMessageIndex = -1;
            }
        }

        private readonly struct Entry
        {
            internal Entry(int stamp, Dictionary<string, int> layout)
            {
                Stamp = stamp;
                Layout = layout;
            }

            internal int Stamp { get; }

            internal Dictionary<string, int> Layout { get; }
        }
    }
}
