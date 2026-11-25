using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BattleTactics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "com.example.ftk2.battletactics";
        private const string PluginName = "Battle Tactics";
        private const string PluginVersion = "0.6.9";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }
        internal static ConfigEntry<bool> Enabled { get; private set; }
        internal static ConfigEntry<bool> LogPlacement { get; private set; }
        internal static ConfigEntry<string> Layout { get; private set; }
        internal static ConfigEntry<string> HostedLayout { get; private set; }

        internal static void Note(string message)
        {
            if (LogPlacement == null || LogPlacement.Value)
                Log.LogInfo("[Battle Tactics] " + message);
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            FormationStore.Reload();

            // the patches must outlive this component, which the game may destroy with its scene objects
            var harmony = new Harmony(PluginGuid);
            Install(() => PlacementPatch.Install(harmony));
            Install(() => EncounterMenuPatch.Install(harmony));
            Install(() => OnlineLink.Install(harmony));
        }

        private static void Install(Action installPatch)
        {
            try
            {
                installPatch();
            }
            catch (Exception exception)
            {
                Log.LogError(exception);
            }
        }

        private void BindConfig()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "When false, the base game places units and the portrait grid is left alone.");

            LogPlacement = Config.Bind("General", "LogPlacement", true,
                "Log each fight's applied layout to the BepInEx log.");

            Layout = Config.Bind("General", "Layout", "",
                "Used offline and in local co-op. Online sessions share a live layout instead and leave this alone. " +
                "Written by the drag and swap screen, and can be edited by hand. " +
                "Name=Slot pairs separated by semicolons, for example: Boggart=0;Sif=4. " +
                "Slots 0-3 and 4-7 are the two portrait rows, each from left to right.");

            HostedLayout = Config.Bind("General", "HostedLayout", "",
                "The last online layout seen while hosting. It is offered to the room again when you host a new session.");
        }
    }
}
