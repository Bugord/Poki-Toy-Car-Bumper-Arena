using System;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Pickups;
using Bugord.App.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The ITEM FRENZY mode's HUD, over the shared item button (<see cref="HudView"/>): the round is about the items, so
    /// <list type="bullet">
    /// <item>the slot shows from the first second, an empty one pale with its "?" (in every other round an empty slot is
    /// hidden), so the player sees where the items go before the first box;</item>
    /// <item>the usual hint, TAP TO USE over the button or RIGHT CLICK with its E key beside it, stays up with the button
    /// pulsing whenever an item is ready, until the player has used an item in the mode once this visit, even if they
    /// saw the hint in another round or visit (it shows once for good there); the HUD's own one-time hint keeps off it
    /// (<see cref="OwnsItemHint"/>), so it shows once and stays.</item>
    /// </list>
    /// It only borrows the shared elements (the button, the hint and its texts) and leaves them as it found them.
    /// </summary>
    internal sealed class ItemFrenzyHud : IModeHud
    {
        private const float EmptyOpacity = 0.45f;
        private const float PulsePeriod = 0.65f;
        private const long PulseDuration = 120;
        // The widest the hint's words may be, in reference pixels: the hint is 400 wide less the pill's padding, and on a
        // desktop the E key cap beside them; longer words (Russian) shrink, down to half, instead of leaving the screen.
        private const float TouchTextWidth = 330f;
        private const float DesktopTextWidth = 270f;
        private const float MinTextShare = 0.5f;

        private const string HintShownClass = "hud__item-hint--shown";
        private const string HintPulseClass = "hud__item--hint-pulse";
        private const string EmptyClass = "hud__item--empty";

        // Whether the player has used an item in the mode this visit: from then on the hint is not needed.
        private static bool hasUsed;

        private readonly HudViewModel model;
        private readonly VisualElement button;
        private readonly VisualElement hint;
        private readonly Label[] hintLabels;
        private bool isHintShown;
        private bool isPale;
        private bool isFitDue;
        private float nextPulse;

        /// <summary>The mode's HUD, made by its look (<see cref="ItemFrenzyLook"/>) in an item frenzy round only.</summary>
        public static ItemFrenzyHud Create(VisualElement layer, VisualElement root, HudViewModel model)
        {
            if (model.Mode is not ItemFrenzyMode { IsActive: true } || root == null || !model.HasItems) {
                return null;
            }

            var button = root.Q<VisualElement>("item-button");
            var hint = root.Q<VisualElement>("item-hint");
            return button != null && hint != null ? new ItemFrenzyHud(model, button, hint) : null;
        }

        /// <summary>Forgets that the player used an item this visit, for the tests.</summary>
        internal static void ForgetVisit() => hasUsed = false;

        /// <summary>Whether the player has used an item in the mode this visit.</summary>
        internal static bool HasUsed => hasUsed;

        private ItemFrenzyHud(HudViewModel model, VisualElement button, VisualElement hint)
        {
            this.model = model;
            this.button = button;
            this.hint = hint;
            var text = hint.Q<VisualElement>(className: "stroke");
            hintLabels = text != null ? StrokeText.LabelsOf(text) : Array.Empty<Label>();
            model.ItemUsed += OnItemUsed;
        }

        /// <summary>The whole round: the HUD's one-time hint would come down at its moment, and show after a first use.</summary>
        public bool OwnsItemHint => true;

        public void Tick(Camera camera, float top, float left, float right)
        {
            RefreshSlot();
            RefreshHint();
        }

        public void Dispose()
        {
            model.ItemUsed -= OnItemUsed;
            SetPale(false);
            HideHint();
            SetHintSize(StyleKeyword.Null);
        }

        private void RefreshSlot()
        {
            // Pale while it waits, as long as the round is placed and the player is in it; the usual look once it fills.
            SetPale(button.ClassListContains(EmptyClass) && model.PlayerCar != null && !model.PlayerCar.IsOut);
        }

        private void SetPale(bool isOn)
        {
            if (isPale == isOn) {
                return;
            }

            isPale = isOn;
            button.style.opacity = isOn ? EmptyOpacity : new StyleFloat(StyleKeyword.Null);
            button.style.scale = isOn ? new StyleScale(new Scale(Vector3.one)) : new StyleScale(StyleKeyword.Null);
        }

        private void RefreshHint()
        {
            if (hasUsed || !model.IsItemReady) {
                HideHint();
                return;
            }

            var now = Time.unscaledTime;
            if (!isHintShown || !hint.ClassListContains(HintShownClass)) {
                isHintShown = true;
                var isDesktop = hint.ClassListContains("hud__item-hint--desktop");
                StrokeText.SetText(hintLabels, Strings.Get(isDesktop ? "hud.right_click" : "hud.tap_to_use"));
                SetHintSize(StyleKeyword.Null);
                isFitDue = true;
                hint.AddToClassList(HintShownClass);
                nextPulse = now;
            }
            else if (isFitDue) {
                // A frame on, the labels have the style sheet's size for this screen; the hint is still popping in.
                isFitDue = false;
                FitHint(hint.ClassListContains("hud__item-hint--desktop") ? DesktopTextWidth : TouchTextWidth);
            }

            if (now >= nextPulse) {
                nextPulse = now + PulsePeriod;
                button.AddToClassList(HintPulseClass);
                button.schedule.Execute(() => button.RemoveFromClassList(HintPulseClass)).StartingIn(PulseDuration);
            }
        }

        private void FitHint(float room)
        {
            if (hintLabels.Length == 0) {
                return;
            }

            var label = hintLabels[0];
            var size = label.resolvedStyle.fontSize;
            var width = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f,
                VisualElement.MeasureMode.Undefined).x;
            if (size > 0f && width > room) {
                SetHintSize(Mathf.Max(size * room / width, size * MinTextShare));
            }
        }

        private void SetHintSize(StyleLength size)
        {
            foreach (var label in hintLabels) {
                label.style.fontSize = size;
            }
        }

        private void HideHint()
        {
            if (!isHintShown) {
                return;
            }

            isHintShown = false;
            hint.RemoveFromClassList(HintShownClass);
        }

        private void OnItemUsed(ItemKind kind)
        {
            hasUsed = true;
            HideHint();
        }
    }
}
