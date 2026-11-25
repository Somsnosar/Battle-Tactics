using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleTactics
{
    internal static class LayoutCodec
    {
        private const char EntrySeparator = ';';
        private const char KeySlotSeparator = '=';
        private const int MaxTextLength = 4096;

        internal static Dictionary<string, int> Empty()
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        internal static Dictionary<string, int> Parse(string text)
        {
            Dictionary<string, int> layout = Empty();

            if (string.IsNullOrEmpty(text) || text.Length > MaxTextLength)
                return layout;

            foreach (string entry in text.Split(EntrySeparator))
                AddEntry(layout, entry);

            return layout;
        }

        internal static string Format(Dictionary<string, int> layout)
        {
            return string.Join(
                EntrySeparator.ToString(),
                layout.Select(entry => entry.Key + KeySlotSeparator + entry.Value));
        }

        internal static bool IsStorable(string key)
        {
            return key.Length > 0 && key.IndexOf(EntrySeparator) < 0 && key.IndexOf(KeySlotSeparator) < 0;
        }

        private static void AddEntry(Dictionary<string, int> layout, string entry)
        {
            string[] keyAndSlot = entry.Split(KeySlotSeparator);

            if (keyAndSlot.Length != 2 || !int.TryParse(keyAndSlot[1], out int slot))
                return;

            string key = keyAndSlot[0].Trim();

            if (key.Length > 0 && slot >= 0 && slot < PartyGrid.SlotCount)
                layout[key] = slot;
        }
    }
}
