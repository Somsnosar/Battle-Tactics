using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace BattleTactics
{
    internal sealed class PartyPortraits
    {
        private const string ContainerName = "left-portraits-container";
        private const string PortraitName = "mini-portrait";

        private readonly List<Entity> party;
        private readonly List<VisualElement> portraits;
        private readonly Dictionary<VisualElement, Entity> unitByPortrait;

        private Dictionary<string, int> slotByUnit;

        private PartyPortraits(
            VisualElement container, List<VisualElement> portraits,
            List<Entity> party, Dictionary<string, int> slotByUnit)
        {
            Container = container;
            this.portraits = portraits;
            this.party = party;
            this.slotByUnit = slotByUnit;

            // the game shows ally i on the i-th portrait, so the pairing is read from the current order
            unitByPortrait = party.Select((unit, index) => (unit, index))
                .ToDictionary(pair => portraits[pair.index], pair => pair.unit);
        }

        internal VisualElement Container { get; }

        internal bool IsAttached => Container.panel != null;

        internal IEnumerable<VisualElement> Portraits => portraits;

        internal static PartyPortraits Create(VisualElement menu, List<Entity> party)
        {
            VisualElement container = menu.Q<VisualElement>(ContainerName);

            if (container == null)
            {
                Plugin.Note("Portraits: '" + ContainerName + "' was not found in the menu");
                return null;
            }

            List<VisualElement> portraits = FindPortraits(container);

            if (portraits.Count != PartyGrid.SlotCount || container.childCount != portraits.Count)
            {
                Plugin.Note("Portraits: expected " + PartyGrid.SlotCount + " portraits directly in the container, found "
                    + portraits.Count + " portraits and " + container.childCount + " children");
                return null;
            }

            Dictionary<string, int> slotByUnit = SlotResolver.Resolve(party, LayoutSource.ForDisplay());

            return slotByUnit == null ? null : new PartyPortraits(container, portraits, party, slotByUnit);
        }

        internal bool IsOccupied(VisualElement portrait)
        {
            return unitByPortrait.ContainsKey(portrait);
        }

        internal List<Rect> SlotRects()
        {
            return Container.Children().Select(portrait => portrait.worldBound).ToList();
        }

        internal void Refresh()
        {
            Dictionary<string, int> resolved = SlotResolver.Resolve(party, LayoutSource.ForDisplay());

            if (resolved == null)
                return;

            slotByUnit = resolved;
            Arrange();
        }

        internal void Arrange()
        {
            Dictionary<int, VisualElement> portraitBySlot =
                unitByPortrait.ToDictionary(pair => slotByUnit[pair.Value.Guid], pair => pair.Key);
            var emptyPortraits = new Queue<VisualElement>(portraits.Where(portrait => !IsOccupied(portrait)));

            for (int slot = 0; slot < PartyGrid.SlotCount; slot++)
            {
                bool isOccupied = portraitBySlot.TryGetValue(slot, out VisualElement portrait);
                Container.Insert(slot, isOccupied ? portrait : emptyPortraits.Dequeue());
            }
        }

        internal void MoveTo(VisualElement portrait, int targetSlot)
        {
            Entity mover = unitByPortrait[portrait];
            int originSlot = slotByUnit[mover.Guid];

            if (originSlot != targetSlot)
                Commit(SwappedSlots(mover, originSlot, targetSlot));

            Arrange();
        }

        // the portrait name may be an element name or a style class depending on the game's layout
        private static List<VisualElement> FindPortraits(VisualElement container)
        {
            List<VisualElement> found = container.Query<VisualElement>(name: PortraitName).ToList();

            if (found.Count == 0)
                found = container.Query<VisualElement>(className: PortraitName).ToList();

            if (found.Count != PartyGrid.SlotCount && container.childCount == PartyGrid.SlotCount)
                found = container.Children().ToList();

            return found;
        }

        private Dictionary<string, int> SwappedSlots(Entity mover, int originSlot, int targetSlot)
        {
            var swapped = new Dictionary<string, int>(slotByUnit);
            Entity occupant = party.FirstOrDefault(unit => unit != mover && slotByUnit[unit.Guid] == targetSlot);

            swapped[mover.Guid] = targetSlot;
            if (occupant != null)
                swapped[occupant.Guid] = originSlot;

            return swapped;
        }

        private void Commit(Dictionary<string, int> proposedSlots)
        {
            Dictionary<string, int> layout = ToLayout(proposedSlots);
            Plugin.Note("Layout proposed for " + party.Count + " units: " + LayoutCodec.Format(layout));

            if (LayoutSource.Commit(layout))
                slotByUnit = proposedSlots;
        }

        private Dictionary<string, int> ToLayout(Dictionary<string, int> slots)
        {
            Dictionary<string, int> layout = LayoutCodec.Empty();

            foreach (Entity unit in party)
            {
                string key = UnitKeys.Of(unit, party);

                if (key != null)
                    layout[key] = slots[unit.Guid];
            }

            return layout;
        }
    }
}
