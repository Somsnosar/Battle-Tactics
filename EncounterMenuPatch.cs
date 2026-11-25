using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UIElements;

namespace BattleTactics
{
    internal static class EncounterMenuPatch
    {
        private const long RetryDelayMilliseconds = 150;
        private const long RefreshPollMilliseconds = 100;

        private static PartyPortraits activePortraits;
        private static PortraitDragger activeDragger;
        private static int menuGeneration;
        private static volatile bool isLayoutDirty;
        private static IVisualElementScheduledItem refreshPoll;
        private static int containerIndex;
        private static int alliesIndex;
        private static int viewOnlyIndex;

        internal static void Install(Harmony harmony)
        {
            Type menu = AccessTools.TypeByName("EncounterMenuViewHelper2");
            MethodInfo show = menu == null ? null : AccessTools.Method(menu, "Show");

            if (show == null)
            {
                Plugin.Log.LogError("Encounter menu method not found, the game may have been updated. Portrait editing is inactive.");
                return;
            }

            containerIndex = ParameterIndex(show, "pTemplateContainer");
            alliesIndex = ParameterIndex(show, "pAllies");
            viewOnlyIndex = ParameterIndex(show, "pIsViewOnly");

            if (containerIndex < 0 || alliesIndex < 0 || viewOnlyIndex < 0)
            {
                Plugin.Log.LogError("Encounter menu parameters changed, the game may have been updated. Portrait editing is inactive.");
                return;
            }

            var postfix = new HarmonyMethod(typeof(EncounterMenuPatch), nameof(Postfix)) { priority = Priority.Last };
            var probe = new HarmonyMethod(typeof(EncounterMenuPatch), nameof(NoteCalled)) { priority = Priority.First };
            harmony.Patch(show, prefix: probe, postfix: postfix);
            Plugin.Note("Encounter menu patch installed on " + show.DeclaringType.FullName + "." + show
                + ", postfixes: " + Harmony.GetPatchInfo(show)?.Postfixes.Count);
            LayoutSource.Changed += OnLayoutChanged;
        }

        private static void NoteCalled()
        {
            Plugin.Note("Encounter menu: Show was called");
        }

        private static int ParameterIndex(MethodInfo method, string name)
        {
            return Array.FindIndex(method.GetParameters(), parameter => parameter.Name == name);
        }

        // reading the arguments by position keeps this mod from linking to the game's tuple type
        private static void Postfix(object[] __args)
        {
            try
            {
                OnShown((VisualElement)__args[containerIndex], __args[alliesIndex] as IEnumerable, (bool)__args[viewOnlyIndex]);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
            }
        }

        private static void OnShown(VisualElement menu, IEnumerable allyEntries, bool isViewOnly)
        {
            DetachActiveDragger();
            menuGeneration++;

            List<Entity> allies = EntitiesOf(allyEntries);
            Plugin.Note("Encounter menu shown: " + allies.Count + " allies, viewOnly=" + isViewOnly
                + ", online=" + OnlineSession.IsActive);

            if (!Plugin.Enabled.Value || allies.Count == 0)
                return;

            if (TrySetUp(menu, allies, isViewOnly))
                return;

            int generation = menuGeneration;
            menu.schedule.Execute(() => RetryIfCurrent(generation, menu, allies, isViewOnly))
                .ExecuteLater(RetryDelayMilliseconds);
        }

        // each ally entry is a tuple whose first field is the entity
        private static List<Entity> EntitiesOf(IEnumerable allyEntries)
        {
            if (allyEntries == null)
                return new List<Entity>();

            return allyEntries.Cast<object>()
                .Select(entry => entry.GetType().GetField("Item1").GetValue(entry) as Entity)
                .Where(entity => entity != null)
                .ToList();
        }

        // the game may fill the menu after show returns
        private static void RetryIfCurrent(
            int generation, VisualElement menu, List<Entity> allies, bool isViewOnly)
        {
            if (generation == menuGeneration)
                TrySetUp(menu, allies, isViewOnly);
        }

        private static bool TrySetUp(
            VisualElement menu, List<Entity> allies, bool isViewOnly)
        {
            try
            {
                PartyPortraits portraits = PartyPortraits.Create(menu, allies);

                if (portraits == null)
                    return false;

                if (LayoutSource.ForDisplay().Count > 0)
                    portraits.Arrange();

                if (!isViewOnly)
                    AttachDragger(portraits);

                activePortraits = portraits;
                isLayoutDirty = false;
                refreshPoll = menu.schedule.Execute(ApplyLayoutIfDirty).Every(RefreshPollMilliseconds);

                Plugin.Note(isViewOnly ? "Portraits shown, view only" : "Portrait dragging attached");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
                return false;
            }
        }

        private static void AttachDragger(PartyPortraits portraits)
        {
            activeDragger = new PortraitDragger(portraits);
            activeDragger.Attach();
        }

        // network callbacks may not run on the main thread, so they only flag and the ui thread applies
        private static void OnLayoutChanged()
        {
            isLayoutDirty = true;
        }

        private static void ApplyLayoutIfDirty()
        {
            if (!isLayoutDirty)
                return;

            isLayoutDirty = false;

            try
            {
                if (activePortraits == null || !activePortraits.IsAttached)
                    return;

                if (activeDragger != null)
                    activeDragger.OnLayoutChanged();
                else
                    activePortraits.Refresh();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(exception);
            }
        }

        // the game reuses the same portrait elements on every menu, so old callbacks must go first
        private static void DetachActiveDragger()
        {
            refreshPoll?.Pause();
            refreshPoll = null;
            activeDragger?.Detach();
            activeDragger = null;
            activePortraits = null;
        }
    }
}
