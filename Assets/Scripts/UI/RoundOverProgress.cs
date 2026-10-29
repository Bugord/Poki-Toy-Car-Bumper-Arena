using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Replays an already saved reward. Its lifetime belongs to the result view, so PLAY AGAIN can leave at any point,
    /// including halfway through several unlocks, without holding up the next round or touching a detached screen.
    /// </summary>
    internal sealed class RoundOverProgress : IDisposable
    {
        private const float FillDuration = 0.8f;
        private const float PopDuration = 0.35f;
        private const int AfterCardDelay = 300;
        private const float RevealDuration = 0.3f;
        // How long the goal badge celebrates before showing the next target; the unlock sticker stays afterwards.
        private const int UnlockPreviewHold = 900;

        private readonly RoundOverViewModel viewModel;
        private readonly VisualElement levelBadge;
        private readonly Label levelLabel;
        private readonly VisualElement fill;
        private readonly VisualElement gain;
        private readonly Label nextCar;
        private readonly VisualElement newCar;
        private readonly Label newCarName;
        private readonly VisualElement newCarPortrait;
        private readonly VisualElement carBadge;
        private readonly VisualElement portrait;
        private readonly Label ready;
        private CancellationTokenSource animation;

        public RoundOverProgress(VisualElement root, RoundOverViewModel viewModel)
        {
            this.viewModel = viewModel;
            this.levelBadge = root.Q<VisualElement>("xp-level");
            this.levelLabel = root.Q<Label>("xp-level-value");
            this.fill = root.Q<VisualElement>("xp-fill");
            this.gain = root.Q<VisualElement>("xp-gain");
            this.nextCar = root.Q<Label>("xp-next");
            this.newCar = root.Q<VisualElement>("xp-new-car");
            this.newCarName = root.Q<Label>("xp-new-car-name");
            this.newCarPortrait = root.Q<VisualElement>("xp-new-car-portrait");
            this.carBadge = root.Q<VisualElement>("xp-car-badge");
            this.portrait = root.Q<VisualElement>("xp-car-portrait");
            this.ready = root.Q<Label>("xp-ready");

            var before = viewModel.XpGain.Before;
            SetLevel(before.Level);
            SetNextCar(before.Level);
            SetFill((float)before.Xp / before.Cost);
            foreach (var label in gain.Query<Label>().ToList()) {
                label.text = $"+{viewModel.XpGain.Total} XP";
            }

            SetScale(gain, 0f);
            SetScale(newCar, 0f);
            ready.style.visibility = Visibility.Hidden;
        }

        public void Play(CancellationToken token)
        {
            Stop();
            animation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Animate(animation.Token).Forget();
        }

        public void Stop()
        {
            animation?.Cancel();
            animation?.Dispose();
            animation = null;
        }

        public void Dispose() => Stop();

        private async UniTask Animate(CancellationToken token)
        {
            // ShowAsync already faded in for 0.2s; the card finishes its delayed entrance before the reward starts.
            await UniTask.Delay(AfterCardDelay, ignoreTimeScale: true, cancellationToken: token);
            await UniTask.WhenAll(Pop(gain, token), FillLevels(token));
        }

        private async UniTask FillLevels(CancellationToken token)
        {
            var reward = viewModel.XpGain;
            var level = reward.Before.Level;
            var from = (float)reward.Before.Xp / reward.Before.Cost;
            while (level < reward.After.Level) {
                await Fill(from, 1f, token);
                level++;
                SetLevel(level);
                // Keep the full bar while the level celebrates, then start the new level empty on the next frame.
                await UniTask.WhenAll(Bounce(levelBadge, token), ShowUnlocks(level, token));
                SetNextCar(level);
                SetFill(0f);
                from = 0f;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            var target = (float)reward.After.Xp / reward.After.Cost;
            if (!Mathf.Approximately(from, target)) {
                await Fill(from, target, token);
            }
        }

        private void SetLevel(int level)
        {
            levelLabel.text = $"LVL {level}";
        }

        private void SetNextCar(int level)
        {
            nextCar.text = viewModel.NextCarText(level);
            var car = viewModel.NextCar(level);
            carBadge.style.display = car == null ? DisplayStyle.None : DisplayStyle.Flex;
            portrait.style.backgroundImage = new StyleBackground(car?.Portrait);
            portrait.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            portrait.AddToClassList("car-portrait--locked");
        }

        private async UniTask ShowUnlocks(int level, CancellationToken token)
        {
            foreach (var car in viewModel.NewCarsAt(level)) {
                carBadge.style.display = DisplayStyle.Flex;
                portrait.style.backgroundImage = new StyleBackground(car.Portrait);
                portrait.style.unityBackgroundImageTintColor = StyleKeyword.Null;
                portrait.AddToClassList("car-portrait--locked");
                // Let the palette's silhouette tint resolve before interpolating it to the original portrait.
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                // Keep the latest unlock in its own sticker, so the goal badge can advance independently.
                newCarName.text = $"NEW CAR! {car.DisplayName}";
                newCarPortrait.style.backgroundImage = new StyleBackground(car.Portrait);
                ready.style.visibility = Visibility.Visible;
                await UniTask.WhenAll(Pop(newCar, token), Reveal(token), Bounce(carBadge, token));
                await UniTask.Delay(UnlockPreviewHold, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        private async UniTask Reveal(CancellationToken token)
        {
            var from = portrait.resolvedStyle.unityBackgroundImageTintColor;
            for (var time = 0f; time < RevealDuration; time += Time.unscaledDeltaTime) {
                portrait.style.unityBackgroundImageTintColor = Color.Lerp(from, Color.white, time / RevealDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            portrait.RemoveFromClassList("car-portrait--locked");
            portrait.style.unityBackgroundImageTintColor = Color.white;
        }

        private async UniTask Fill(float from, float to, CancellationToken token)
        {
            for (var time = 0f; time < FillDuration; time += Time.unscaledDeltaTime) {
                var t = time / FillDuration;
                var ease = 1f - Mathf.Pow(1f - t, 3f);
                SetFill(Mathf.Lerp(from, to, ease));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetFill(to);
        }

        private static async UniTask Bounce(VisualElement element, CancellationToken token)
        {
            for (var time = 0f; time < PopDuration; time += Time.unscaledDeltaTime) {
                SetScale(element, 1f + 0.22f * Mathf.Sin(Mathf.PI * time / PopDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(element, 1f);
        }

        /// <summary>The same 0.35s ease-out-back spring as NEW BEST!, on unscaled time and without its entrance delay.</summary>
        private static async UniTask Pop(VisualElement sticker, CancellationToken token)
        {
            SetScale(sticker, 0f);
            for (var time = 0f; time < PopDuration; time += Time.unscaledDeltaTime) {
                var t = time / PopDuration - 1f;
                var scale = 1f + 2.70158f * t * t * t + 1.70158f * t * t;
                SetScale(sticker, scale);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(sticker, 1f);
        }

        private void SetFill(float value) => fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);

        private static void SetScale(VisualElement element, float value) =>
            element.style.scale = new Scale(new Vector3(value, value, 1f));
    }
}
