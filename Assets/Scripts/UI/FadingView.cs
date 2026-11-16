using System.Collections.Generic;
using System.Threading;
using Bugord.App.UI;
using Cysharp.Threading.Tasks;
using ToyCarArena.Rendering;
using UIKit.Runtime.Abstractions;
using UIKit.Runtime.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A view that fades its root in when shown and out when hidden.
    /// </summary>
    public abstract class FadingView<TViewModel> : View<TViewModel> where TViewModel : ViewModel
    {
        private float opacity = 1f;
        // This screen's entry in ShownScreens while it is shown.
        private object shown;
        // The layer of screens this popup moved away while it covers them.
        private VisualElement hiddenScreens;
        private int visits;

        protected virtual float FadeDuration => 0.2f;

        /// <summary>Which screen this is, for <see cref="ShownScreens"/>; None leaves it out.</summary>
        protected virtual ShownScreen ScreenKind => ShownScreen.None;

        /// <summary>
        /// Whether the screen is opaque over the whole arena once faded in, so the arena's camera stops drawing the 3D
        /// under it (<see cref="ShownScreens.IsArenaCovered"/>).
        /// </summary>
        protected virtual bool CoversArena => false;

        /// <summary>
        /// True once the fade-in is complete, false again as soon as the fade-out starts. Screens ignore input until
        /// then, so a held key or a stray tap from the game cannot press a button that is still fading in.
        /// </summary>
        protected bool IsShown { get; private set; }

        protected sealed override void OnAttach()
        {
            // The navigator attaches a screen before the one under it has faded out, so it stays hidden until shown;
            // else it would pop up at full opacity over the old one, then vanish and fade in.
            Root.style.display = DisplayStyle.None;
            SetOpacity(0f);
            OnAttached();
            PillShape.Keep(Root);
        }

        /// <summary>
        /// Called once the view has its root, hidden until <see cref="ShowAsync"/>.
        /// </summary>
        protected virtual void OnAttached()
        {
        }

        public override async UniTask ShowAsync(CancellationToken token)
        {
            IsShown = false;
            var visit = ++visits;
            SetOpacity(0f);
            await base.ShowAsync(token);
            await Fade(1f, token);
            // A hide that began during the fade-in (the curtain, opened and closed at once) wins: the screen must not
            // note itself shown, or hide the screens under it, after it has started to go.
            if (visit != visits) {
                return;
            }

            IsShown = true;
            if (ScreenKind != ShownScreen.None) {
                ShownScreens.Remove(shown);
                shown = ShownScreens.Add(ScreenKind, CoversArena);
            }

            HideScreensUnder();
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            IsShown = false;
            visits++;
            // From the first frame of the fade-out, so the arena is drawn again as soon as it can show.
            ForgetShown();
            await Fade(0f, token);
            await base.HideAsync(token);
        }

        public override void Dispose()
        {
            // A screen torn down without hiding must not leave the arena undrawn.
            ForgetShown();
            base.Dispose();
        }

        private void ForgetShown()
        {
            ShownScreens.Remove(shown);
            shown = null;
            ShowScreensUnder();
        }

        // An opaque popup (the garage over the menu or the result, the curtain over the result or the HUD) hides the
        // screens under it: UIKit keeps drawing them, and on a phone the whole menu under the garage filled the screen
        // once more every frame for nothing. They are moved out of the viewport rather than hidden, so their layout,
        // animations and geometry stay as they were and nothing measures a hidden element; the GPU drops triangles
        // outside the viewport before filling them.
        private void HideScreensUnder()
        {
            if (!CoversArena || hiddenScreens != null || !TryFindScreensUnder(out var screens)) {
                return;
            }

            hiddenScreens = screens;
            CoveredScreens.Hide(this, screens);
        }

        private void ShowScreensUnder()
        {
            if (hiddenScreens == null) {
                return;
            }

            CoveredScreens.Show(this);
            hiddenScreens = null;
        }

        // Only a popup has screens under it: its root sits beside UIKit's layer of screens, not in it. Siblings only:
        // a query would match the layer itself as the parent of a screen in it, and the menu would hide its own layer.
        private bool TryFindScreensUnder(out VisualElement screens)
        {
            screens = null;
            for (var element = Root; element?.parent != null; element = element.parent) {
                var parent = element.parent;
                if (parent.name == CoveredScreens.LayerName) {
                    return false;
                }

                foreach (var sibling in parent.Children()) {
                    if (sibling != element && sibling.name == CoveredScreens.LayerName) {
                        screens = sibling;
                        return true;
                    }
                }
            }

            return false;
        }


        private async UniTask Fade(float targetOpacity, CancellationToken token)
        {
            var fromOpacity = opacity;

            // Unscaled, so a screen still fades while the game is paused.
            for (var time = 0f; time < FadeDuration; time += Time.unscaledDeltaTime) {
                SetOpacity(Mathf.Lerp(fromOpacity, targetOpacity, time / FadeDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetOpacity(targetOpacity);
        }

        private void SetOpacity(float value)
        {
            opacity = value;
            Root.style.opacity = value;
        }
    }

    /// <summary>
    /// UIKit's layer of screens while opaque popups cover it: moved out of the viewport by the first, back when none is left.
    /// Not in the generic view, whose statics would be one per screen type.
    /// </summary>
    internal static class CoveredScreens
    {
        /// <summary>UIKit's layer of screens; popups (the curtain, the garage) are drawn over it.</summary>
        public const string LayerName = "ScreenRoot";

        // Far enough to leave any panel: the moved screens' triangles all fall outside the viewport.
        private const float AwayDistance = 100000f;

        // Which layer each covering popup moved: a layer comes back when no popup covers it any more. Kept by layer, so
        // an app torn down without hiding its popups (tests) cannot keep the next app's new layer from moving.
        private static readonly Dictionary<object, VisualElement> covering = new();

        public static void Hide(object popup, VisualElement screens)
        {
            covering[popup] = screens;
            screens.style.translate = new Translate(AwayDistance, 0f);
        }

        public static void Show(object popup)
        {
            if (!covering.Remove(popup, out var screens) || covering.ContainsValue(screens)) {
                return;
            }

            screens.style.translate = StyleKeyword.Null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlay() => covering.Clear();
    }
}
