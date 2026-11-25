using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleTactics
{
    internal static class UnitKeys
    {
        internal static string NameOf(Entity unit)
        {
            CharacterComponent character = unit.Get<CharacterComponent>();
            string name = (character.DisplayName ?? string.Empty).Trim();
            return name.Length > 0 ? name : (character.ConfigName ?? string.Empty).Trim();
        }

        // guids are random per client, so online play pins units by name, numbering units that share one in party order
        internal static string Of(Entity unit, List<Entity> party)
        {
            string name = NameOf(unit);

            if (!LayoutCodec.IsStorable(name))
                return OnlineSession.IsActive ? null : unit.Guid;

            List<Entity> sameName = party
                .Where(other => string.Equals(NameOf(other), name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sameName.Count == 1)
                return name;

            return OnlineSession.IsActive ? name + "#" + (sameName.IndexOf(unit) + 1) : unit.Guid;
        }
    }
}
