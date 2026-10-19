using System.Threading;
using Cysharp.Threading.Tasks;
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

        protected virtual float FadeDuration => 0.2f;

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
            SetOpacity(0f);
            await base.ShowAsync(token);
            await Fade(1f, token);
            IsShown = true;
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            IsShown = false;
            await Fade(0f, token);
            await base.HideAsync(token);
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
}
