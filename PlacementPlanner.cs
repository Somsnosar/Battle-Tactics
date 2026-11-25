using System.Collections.Generic;
using System.Linq;

namespace BattleTactics
{
    internal static class PlacementPlanner
    {
        internal static Dictionary<string, Entity> Plan(
            int group, List<Entity> allTiles, List<Entity> vacantTiles, List<Entity> allCharacters,
            Dictionary<string, int> layout)
        {
            List<Entity> units = allCharacters.Where(unit => GroupOf(unit) == group).ToList();

            if (!units.Any(unit => unit.Has<PlayerComponent>()))
                return null;

            if (!PartyGrid.TryGetSlotTiles(group, allTiles, out Entity[] slotTiles))
                return Decline("the grid is not the standard 2x4");

            if (layout.Count == 0)
                return Decline("no layout is in use");

            Dictionary<string, int> slotByUnit = SlotResolver.Resolve(units, layout);

            if (slotByUnit == null)
                return null;

            return TilesFor(units, slotByUnit, slotTiles, vacantTiles);
        }

        private static Dictionary<string, Entity> TilesFor(
            List<Entity> units, Dictionary<string, int> slotByUnit, Entity[] slotTiles, List<Entity> vacantTiles)
        {
            var tileByUnit = new Dictionary<string, Entity>();

            foreach (Entity unit in units)
            {
                int slot = slotByUnit[unit.Guid];
                Entity tile = slotTiles[slot];

                if (!vacantTiles.Contains(tile))
                    return Decline("slot " + slot + " is occupied");

                tileByUnit[unit.Guid] = tile;
                Plugin.Note(UnitKeys.NameOf(unit) + " goes to slot " + slot);
            }

            return tileByUnit;
        }

        private static Dictionary<string, Entity> Decline(string reason)
        {
            Plugin.Note("base game placement used: " + reason);
            return null;
        }

        private static int GroupOf(Entity unit)
        {
            return unit.TryGet<CharacterComponent>(out var character) ? character.GroupIndex : -1;
        }
    }
}
