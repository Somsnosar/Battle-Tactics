using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BattleTactics
{
    internal sealed class PortraitDragger
    {
        private const float DragThreshold = 8f;

        private readonly PartyPortraits party;
        private readonly Dictionary<VisualElement, PickingMode> pickingModeByPortrait =
            new Dictionary<VisualElement, PickingMode>();

        private VisualElement grabbed;
        private VisualElement placeholder;
        private List<Rect> slotRects;
        private Vector2 pointerStart;
        private Vector2 grabbedStart;
        private int pointerId;
        private bool isDragging;
        private bool isRefreshPending;

        internal PortraitDragger(PartyPortraits party)
        {
            this.party = party;
        }

        internal void OnLayoutChanged()
        {
            if (isDragging)
                isRefreshPending = true;
            else
                party.Refresh();
        }

        internal void Attach()
        {
            foreach (VisualElement portrait in party.Portraits)
            {
                pickingModeByPortrait[portrait] = portrait.pickingMode;
                portrait.pickingMode = PickingMode.Position;

                // trickle-down sees events before the game's own handlers can consume them
                portrait.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                portrait.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
                portrait.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                portrait.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut, TrickleDown.TrickleDown);
            }
        }

        internal void Detach()
        {
            Cancel();

            foreach (VisualElement portrait in party.Portraits)
            {
                portrait.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                portrait.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
                portrait.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                portrait.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut, TrickleDown.TrickleDown);

                if (pickingModeByPortrait.TryGetValue(portrait, out PickingMode original))
                    portrait.pickingMode = original;
            }

            pickingModeByPortrait.Clear();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            try
            {
                Grab((VisualElement)evt.currentTarget, evt);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            try
            {
                Drag(evt);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            try
            {
                Release(evt);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if (grabbed != null)
                Cancel();
        }

        private void Grab(VisualElement portrait, PointerDownEvent evt)
        {
            if (evt.button != 0 || grabbed != null || !party.IsOccupied(portrait))
                return;

            Plugin.Note("Portrait grabbed");

            grabbed = portrait;
            pointerId = evt.pointerId;
            pointerStart = evt.position;
            slotRects = party.SlotRects();
            portrait.CapturePointer(pointerId);
        }

        private void Drag(PointerMoveEvent evt)
        {
            if (grabbed == null || evt.pointerId != pointerId)
                return;

            Vector2 offset = (Vector2)evt.position - pointerStart;

            if (!isDragging && offset.magnitude < DragThreshold)
                return;

            if (!isDragging)
                Lift();

            grabbed.style.left = grabbedStart.x + offset.x;
            grabbed.style.top = grabbedStart.y + offset.y;
        }

        private void Release(PointerUpEvent evt)
        {
            if (grabbed == null || evt.pointerId != pointerId)
                return;

            VisualElement portrait = grabbed;
            grabbed = null;
            portrait.ReleasePointer(pointerId);

            if (!isDragging)
                return;

            Drop(portrait, SlotAt(evt.position));
            evt.StopPropagation();
        }

        private void Fail(Exception exception)
        {
            Plugin.Log.LogError(exception);

            try
            {
                Cancel();
            }
            catch (Exception cancelException)
            {
                Plugin.Log.LogError(cancelException);
            }
        }

        private void Cancel()
        {
            VisualElement portrait = grabbed;
            grabbed = null;

            if (portrait != null && isDragging)
                Drop(portrait, -1);
        }

        // the placeholder keeps the portrait's cell from collapsing while it floats above the grid
        private void Lift()
        {
            isDragging = true;

            Rect bounds = grabbed.layout;
            grabbedStart = bounds.position;

            placeholder = new VisualElement();
            placeholder.style.width = bounds.width;
            placeholder.style.height = bounds.height;
            placeholder.style.flexShrink = 0;
            party.Container.Insert(party.Container.IndexOf(grabbed), placeholder);

            grabbed.style.position = Position.Absolute;
            grabbed.style.left = grabbedStart.x;
            grabbed.style.top = grabbedStart.y;
            grabbed.style.width = bounds.width;
            grabbed.style.height = bounds.height;
            grabbed.BringToFront();
        }

        private void Drop(VisualElement portrait, int targetSlot)
        {
            isDragging = false;
            ReturnToGrid(portrait);
            Plugin.Note("Portrait dropped on slot " + targetSlot);

            if (targetSlot >= 0)
                party.MoveTo(portrait, targetSlot);
            else
                party.Arrange();

            if (!isRefreshPending)
                return;

            isRefreshPending = false;
            party.Refresh();
        }

        private void ReturnToGrid(VisualElement portrait)
        {
            placeholder?.RemoveFromHierarchy();
            placeholder = null;

            portrait.style.position = StyleKeyword.Null;
            portrait.style.left = StyleKeyword.Null;
            portrait.style.top = StyleKeyword.Null;
            portrait.style.width = StyleKeyword.Null;
            portrait.style.height = StyleKeyword.Null;
        }

        private int SlotAt(Vector2 position)
        {
            return slotRects.FindIndex(rect => rect.Contains(position));
        }
    }
}
