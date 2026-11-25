using System;
using System.Collections.Generic;

namespace BattleTactics
{
    internal static class LayoutSource
    {
        internal static event Action Changed;

        internal static Dictionary<string, int> ForPlacement()
        {
            if (!OnlineSession.IsActive)
            {
                FormationStore.Reload();
                return FormationStore.Layout;
            }

            OnlineLink.EnsureAttached();
            return OnlineLink.Timeline.EffectiveFor(OnlineLink.CurrentActionIndex());
        }

        internal static Dictionary<string, int> ForDisplay()
        {
            if (!OnlineSession.IsActive)
                return FormationStore.Layout;

            OnlineLink.EnsureAttached();
            return OnlineLink.Timeline.Latest();
        }

        // online edits only take effect when they come back from the room, in the same order for every player
        internal static bool Commit(Dictionary<string, int> layout)
        {
            if (OnlineSession.IsActive)
            {
                bool sent = OnlineLink.Send(layout);
                Plugin.Note("Layout sent to room: " + sent);
                return false;
            }

            FormationStore.Save(layout);
            return true;
        }

        internal static void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
