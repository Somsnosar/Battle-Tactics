using System.Collections.Generic;
using System.Linq;

namespace BattleTactics
{
    internal static class PartyGrid
    {
        internal const int LanesPerRow = 4;
        internal const int SlotCount = LanesPerRow * 2;

        internal static bool TryGetSlotTiles(int group, List<Entity> allTiles, out Entity[] slotTiles)
        {
            List<Entity> back = LaneTiles(group, allTiles, eTileRowPositions.BACK);
            List<Entity> front = LaneTiles(group, allTiles, eTileRowPositions.FRONT);

            slotTiles = back.Concat(front).ToArray();
            return back.Count == LanesPerRow && front.Count == LanesPerRow && LanesAlign(back, front);
        }

        private static List<Entity> LaneTiles(int group, List<Entity> allTiles, eTileRowPositions row)
        {
            // lowest lane first, so the portrait screen runs in the same direction as the map
            return allTiles
                .Where(tile => IsTileOf(tile, group, row))
                .OrderBy(LaneOf)
                .ToList();
        }

        private static bool IsTileOf(Entity tile, int group, eTileRowPositions row)
        {
            return tile.TryGet<VenueTileComponent>(out var venueTile)
                && venueTile.GroupIndex == group
                && venueTile.RowPositionsType == row;
        }

        private static bool LanesAlign(List<Entity> back, List<Entity> front)
        {
            return back.Select(LaneOf).SequenceEqual(front.Select(LaneOf));
        }

        private static int LaneOf(Entity tile)
        {
            return tile.Get<VenueComponent>().TilePosition.Item2;
        }
    }
}
