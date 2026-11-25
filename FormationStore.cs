using System;
using System.Collections.Generic;

namespace BattleTactics
{
    internal static class FormationStore
    {
        internal static Dictionary<string, int> Layout { get; private set; } = LayoutCodec.Empty();

        internal static void Reload()
        {
            ReloadConfigFile();
            Layout = LayoutCodec.Parse(Plugin.Layout.Value);
        }

        internal static void Save(Dictionary<string, int> layout)
        {
            Layout = new Dictionary<string, int>(layout, StringComparer.OrdinalIgnoreCase);
            Plugin.Layout.Value = LayoutCodec.Format(Layout);
        }

        internal static Dictionary<string, int> LoadHosted()
        {
            return LayoutCodec.Parse(Plugin.HostedLayout.Value);
        }

        internal static void SaveHosted(Dictionary<string, int> layout)
        {
            string text = LayoutCodec.Format(layout);

            if (Plugin.HostedLayout.Value != text)
                Plugin.HostedLayout.Value = text;
        }

        private static void ReloadConfigFile()
        {
            try
            {
                Plugin.Instance.Config.Reload();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("Config reload failed, keeping the current layout: " + exception.Message);
            }
        }
    }
}
