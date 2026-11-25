using System.Collections.Generic;
using System.Linq;

namespace BattleTactics
{
    internal static class SlotResolver
    {
        internal static Dictionary<string, int> Resolve(List<Entity> units, Dictionary<string, int> layout)
        {
            if (units.Count > PartyGrid.SlotCount)
                return Decline("the party has more than " + PartyGrid.SlotCount + " units");

            if (units.Any(unit => CharacterHelper.ActorIsLarge(unit)))
                return Decline("the party has an oversized unit");

            var slotByUnit = new Dictionary<string, int>();
            var takenSlots = new HashSet<int>();

            foreach (Entity unit in units)
            {
                if (TryGetPinnedSlot(unit, units, layout, out int slot) && IsFreeSlot(slot, takenSlots))
                    Take(unit, slot, slotByUnit, takenSlots);
            }

            foreach (Entity unit in units.Where(unit => !slotByUnit.ContainsKey(unit.Guid)))
                Take(unit, LowestFreeSlot(takenSlots), slotByUnit, takenSlots);

            return slotByUnit;
        }

        private static Dictionary<string, int> Decline(string reason)
        {
            Plugin.Note("slots not resolved: " + reason);
            return null;
        }

        private static bool TryGetPinnedSlot(Entity unit, List<Entity> units, Dictionary<string, int> layout, out int slot)
        {
            string key = UnitKeys.Of(unit, units);
            slot = -1;
            return key != null && layout.TryGetValue(key, out slot);
        }

        private static bool IsFreeSlot(int slot, HashSet<int> takenSlots)
        {
            return slot >= 0 && slot < PartyGrid.SlotCount && !takenSlots.Contains(slot);
        }

        private static int LowestFreeSlot(HashSet<int> takenSlots)
        {
            return Enumerable.Range(0, PartyGrid.SlotCount).First(slot => !takenSlots.Contains(slot));
        }

        private static void Take(Entity unit, int slot, Dictionary<string, int> slotByUnit, HashSet<int> takenSlots)
        {
            slotByUnit[unit.Guid] = slot;
            takenSlots.Add(slot);
        }
    }
}
