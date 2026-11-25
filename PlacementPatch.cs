using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace BattleTactics
{
    internal static class PlacementPatch
    {
        private static readonly WeakReference<List<Entity>> currentBatch = new WeakReference<List<Entity>>(null);

        private static Dictionary<string, int> batchLayout = LayoutCodec.Empty();

        private static readonly Dictionary<int, Dictionary<string, Entity>> planByGroup =
            new Dictionary<int, Dictionary<string, Entity>>();

        internal static void Install(Harmony harmony)
        {
            MethodInfo original = AccessTools.Method(typeof(VenueViewHelper), "_setTilePositionOfCharacter");

            if (original == null)
            {
                Plugin.Log.LogError("Placement method not found, the game may have been updated. Mod is inactive.");
                return;
            }

            var prefix = new HarmonyMethod(typeof(PlacementPatch), nameof(Prefix)) { priority = Priority.Last };
            harmony.Patch(original, prefix: prefix);
            Plugin.Note("Placement patch installed");
        }

        // parameter names must match the game's, and returning false skips the base placement
        private static bool Prefix(
            Entity pEntity, List<Entity> pAllTiles, eLoadVenueGridRules pLoadRule,
            List<Entity> pVacantTiles, List<Entity> pAllCharacters)
        {
            if (!Plugin.Enabled.Value || pLoadRule != eLoadVenueGridRules.PREFERRED)
                return true;

            try
            {
                StartBatchIfNew(pAllCharacters);
                Dictionary<string, Entity> plan = PlanFor(pEntity, pAllTiles, pVacantTiles, pAllCharacters);

                if (plan == null || !plan.TryGetValue(pEntity.Guid, out Entity tile) || !pVacantTiles.Contains(tile))
                    return true;

                VenueHelper.SetTilePosition(pEntity, tile.Get<VenueComponent>().TilePosition);
                pVacantTiles.Remove(tile);
                return false;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
                return true;
            }
        }

        // the layout is resolved once per fight, at the action that starts it, so every client agrees
        // the game builds a fresh character list for every fight
        private static void StartBatchIfNew(List<Entity> allCharacters)
        {
            if (currentBatch.TryGetTarget(out List<Entity> batch) && ReferenceEquals(batch, allCharacters))
                return;

            currentBatch.SetTarget(allCharacters);
            planByGroup.Clear();
            batchLayout = LayoutSource.ForPlacement();
        }

        private static Dictionary<string, Entity> PlanFor(
            Entity unit, List<Entity> allTiles, List<Entity> vacantTiles, List<Entity> allCharacters)
        {
            int group = unit.Get<CharacterComponent>().GroupIndex;

            if (planByGroup.TryGetValue(group, out var plan) && IsCurrent(plan, allTiles))
                return plan;

            plan = PlacementPlanner.Plan(group, allTiles, vacantTiles, allCharacters, batchLayout);
            planByGroup[group] = plan;
            return plan;
        }

        private static bool IsCurrent(Dictionary<string, Entity> plan, List<Entity> allTiles)
        {
            return plan == null || plan.Values.All(allTiles.Contains);
        }
    }
}
