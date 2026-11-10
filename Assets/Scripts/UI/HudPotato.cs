using System;
using Bugord.App.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The HOT POTATO mode on the HUD: the seconds left on the bomb's fuse in a sticker over the bomb, wherever it rides,
    /// red and beating in its last seconds; and, while the player holds it, a tilted orange PASS IT! under the bar,
    /// pulsing faster as the fuse runs short. The bomb itself is in the arena; this only tells how long it has and that
    /// the player has to get rid of it.
    /// <para>
    /// Made only in the mode (<see cref="Create"/> gives null otherwise). Built once; a frame only sets styles, on real
    /// time, and nothing here catches input.
    /// </para>
    /// </summary>
    internal sealed class HudPotato : IDisposable
    {
        // Reference pixels right of the bomb's middle the fuse sticker's middle sits: beside the bomb, clear of the
        // holder's name pill over it.
        private const float FuseSide = 120f;
        private const float FuseMargin = 24f;
        // The beat of the fuse's last seconds: up to this scale on each second, easing back.
        private const float BeatScale = 1.35f;
        private const float BeatSeconds = 0.25f;

        // Reference pixels under the top of the safe area the banner sits at: under the HUD bar and the kill feed's three
        // rows at its left in portrait.
        private const float PassTop = 480f;
        private const float PassPulse = 0.07f;
        private const float PassRate = 5f;
        private const float PassHurryRate = 10f;

        private const string FuseShownClass = "hud__fuse--shown";
        private const string FuseShortClass = "hud__fuse--short";
        private const string PassShownClass = "hud__pass--shown";

        private readonly HudViewModel model;
        private readonly VisualElement layer;
        private readonly VisualElement fuse;
        private readonly VisualElement fuseSticker;
        private readonly Label[] fuseLabels;
        private readonly VisualElement pass;
        private readonly VisualElement passSticker;
        private readonly Action onLanguage;
        private int shownSeconds = -1;

        public static HudPotato Create(VisualElement layer, VisualElement root, HudViewModel model)
        {
            return model.IsPotato && layer != null ? new HudPotato(layer, root, model) : null;
        }

        private HudPotato(VisualElement layer, VisualElement root, HudViewModel model)
        {
            this.model = model;
            this.layer = layer;

            fuse = new VisualElement { name = "fuse", pickingMode = PickingMode.Ignore };
            fuse.AddToClassList("hud__fuse");
            fuseSticker = new VisualElement { pickingMode = PickingMode.Ignore };
            fuseSticker.AddToClassList("hud__fuse-sticker");
            var fuseText = Outlined("", "hud__fuse-text");
            fuseLabels = fuseText.Query<Label>().ToList().ToArray();
            fuseSticker.Add(fuseText);
            fuse.Add(fuseSticker);
            layer.Add(fuse);

            pass = new VisualElement { name = "pass", pickingMode = PickingMode.Ignore };
            pass.AddToClassList("hud__pass");
            passSticker = new VisualElement { pickingMode = PickingMode.Ignore };
            passSticker.AddToClassList("hud__pass-sticker");
            var passText = Outlined(Strings.Get("hud.pass_it"), "hud__pass-text");
            passSticker.Add(passText);
            pass.Add(passSticker);
            root.Add(pass);

            onLanguage = () => passText.Query<Label>().ForEach(label => label.text = Strings.Get("hud.pass_it"));
            Strings.Changed += onLanguage;
        }

        /// <summary>Follows the bomb with the fuse and shows or hides PASS IT!; <paramref name="top"/> is the safe top.</summary>
        public void Tick(Camera camera, float top, float left, float right)
        {
            var time = Time.unscaledTime;
            var isHeld = model.HasBomb && camera != null && fuse.panel != null && !model.IsFinished;
            fuse.EnableInClassList(FuseShownClass, isHeld);
            var isPlayerHolding = isHeld && model.IsPlayerHoldingBomb;
            pass.EnableInClassList(PassShownClass, isPlayerHolding);
            pass.style.top = top + PassTop;

            if (isPlayerHolding) {
                var rate = model.IsFuseShort ? PassHurryRate : PassRate;
                var pulse = 1f + PassPulse * (0.5f + 0.5f * Mathf.Sin(time * rate));
                passSticker.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
            }

            if (!isHeld) {
                shownSeconds = -1;
                return;
            }

            var fuseLeft = model.FuseLeft;
            var seconds = Mathf.Max(0, Mathf.CeilToInt(fuseLeft));
            if (seconds != shownSeconds) {
                shownSeconds = seconds;
                foreach (var label in fuseLabels) {
                    label.text = seconds.ToString();
                }
            }

            var isShort = model.IsFuseShort;
            fuse.EnableInClassList(FuseShortClass, isShort);
            var scale = 1f;
            if (isShort) {
                // Beats as each second starts: the fuse's whole seconds run down, so the beat is just past each.
                var sinceSecond = Mathf.Ceil(fuseLeft) - fuseLeft;
                var beat = Mathf.Clamp01(1f - sinceSecond / BeatSeconds);
                scale = 1f + (BeatScale - 1f) * beat * beat;
            }

            fuseSticker.style.scale = new Scale(new Vector3(scale, scale, 1f));

            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(fuse.panel, model.BombPosition, camera);
            var point = layer.WorldToLocal(panelPoint);
            point.x += FuseSide;
            var size = layer.layout.size;
            var halfWidth = fuseSticker.layout.width * 0.5f + FuseMargin;
            var halfHeight = fuseSticker.layout.height * 0.5f + FuseMargin;
            var minX = halfWidth + left;
            point.x = Mathf.Clamp(point.x, minX, Mathf.Max(minX, size.x - halfWidth - right));
            point.y = Mathf.Clamp(point.y, halfHeight + top, Mathf.Max(halfHeight + top, size.y - halfHeight));
            fuse.style.left = point.x;
            fuse.style.top = point.y;
        }

        public void Dispose()
        {
            Strings.Changed -= onLanguage;
            fuse.RemoveFromHierarchy();
            pass.RemoveFromHierarchy();
        }

        private static VisualElement Outlined(string value, string textClass)
        {
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("stroke");
            foreach (var isFace in new[] { false, true }) {
                var label = new Label(value) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("display");
                label.AddToClassList(textClass);
                if (isFace) {
                    label.AddToClassList("stroke__face");
                }

                text.Add(label);
            }

            return text;
        }
    }
}
