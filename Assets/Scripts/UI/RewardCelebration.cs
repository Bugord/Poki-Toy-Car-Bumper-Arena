using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// What a reward popup does when the player takes its reward (BUY &amp; EQUIP on a new car, EQUIP on a gift): a
    /// white flash, the card jumping, the reward itself springing up with a wiggle, an EQUIPPED! stamp slammed over it,
    /// confetti out of the card and, for a bought car, a stream of coins arcing from the price into the coin counter.
    /// Everything is built in the popup's overlay, goes with it, and runs on unscaled time; the popup closes after
    /// <see cref="Duration"/>. A press without the coins shakes the card and says how many are missing.
    /// </summary>
    internal static class RewardCelebration
    {
        /// <summary>Real seconds the celebration takes before the popup may close.</summary>
        public const float Duration = 1.7f;

        private const int CoinCount = 10;
        private const float CoinFlight = 0.55f;
        private const float CoinStagger = 0.045f;
        private const float CoinArc = 260f;
        private const float FlashTime = 0.3f;
        private const float StampTime = 0.18f;
        private const float StampTilt = -10f;
        private const float JumpTime = 0.4f;
        private const float ShakeTime = 0.4f;
        private const float ToastTime = 1.1f;

        public static void Play(VisualElement overlay, VisualElement card, VisualElement art, string stampText,
            VisualElement coinsFrom, VisualElement coinsTo, CancellationToken token)
        {
            Flash(overlay, token).Forget();
            Jump(card, 1.1f, token).Forget();
            Wiggle(art, token).Forget();
            Stamp(art, stampText, token).Forget();
            new ConfettiBurst(overlay).Play(card, token).Forget();
            if (coinsFrom != null && coinsTo != null) {
                Coins(overlay, coinsFrom, coinsTo, token).Forget();
            }
        }

        /// <summary>The coins are short: the card shakes and a red NEED N MORE floats up over the button.</summary>
        public static void Refuse(VisualElement overlay, VisualElement card, VisualElement button, string text,
            CancellationToken token)
        {
            Shake(card, token).Forget();
            Toast(overlay, button, text, token).Forget();
        }

        private static async UniTaskVoid Flash(VisualElement overlay, CancellationToken token)
        {
            var flash = new VisualElement { pickingMode = PickingMode.Ignore };
            flash.AddToClassList("reward-flash");
            overlay.Add(flash);
            try {
                for (var time = 0f; time < FlashTime; time += Time.unscaledDeltaTime) {
                    flash.style.opacity = 0.85f * (1f - time / FlashTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally {
                flash.RemoveFromHierarchy();
            }
        }

        private static async UniTaskVoid Jump(VisualElement element, float peak, CancellationToken token)
        {
            for (var time = 0f; time < JumpTime; time += Time.unscaledDeltaTime) {
                SetScale(element, 1f + (peak - 1f) * Mathf.Sin(Mathf.PI * time / JumpTime));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(element, 1f);
        }

        // The reward springs up big, wobbles side to side twice and settles.
        private static async UniTaskVoid Wiggle(VisualElement art, CancellationToken token)
        {
            const float duration = 0.6f;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime) {
                var t = time / duration;
                var fade = 1f - t;
                SetScale(art, 1f + 0.28f * fade * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)));
                art.style.rotate = new Rotate(8f * fade * Mathf.Sin(t * Mathf.PI * 4f));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(art, 1f);
            art.style.rotate = new Rotate(0f);
        }

        // EQUIPPED! drops from big onto the reward, tilted, as a rubber stamp.
        private static async UniTaskVoid Stamp(VisualElement art, string text, CancellationToken token)
        {
            var stamp = new VisualElement { pickingMode = PickingMode.Ignore };
            stamp.AddToClassList("reward-stamp");
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("reward-stamp__pill");
            pill.Add(StrokeText.Outlined(text, "reward-stamp__label", out _));
            stamp.Add(pill);
            pill.style.rotate = new Rotate(StampTilt);
            art.parent.Add(stamp);
            stamp.style.opacity = 0f;
            await UniTask.Delay(250, true, cancellationToken: token);
            stamp.style.opacity = 1f;
            for (var time = 0f; time < StampTime; time += Time.unscaledDeltaTime) {
                var t = time / StampTime;
                SetScale(stamp, Mathf.Lerp(2.4f, 1f, t * t));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(stamp, 1f);
            Jump(art.parent, 1.06f, token).Forget();
        }

        // Coins leave the price one after another and arc into the counter, which bumps as they land.
        private static async UniTaskVoid Coins(VisualElement overlay, VisualElement from, VisualElement to,
            CancellationToken token)
        {
            var start = overlay.WorldToLocal(from.worldBound.center);
            var end = overlay.WorldToLocal(to.worldBound.center);
            var coins = new VisualElement[CoinCount];
            for (var i = 0; i < CoinCount; i++) {
                var coin = RoundOverMeta.Coin();
                coin.AddToClassList("reward-coin");
                coin.pickingMode = PickingMode.Ignore;
                coin.style.translate = new Translate(start.x, start.y);
                SetScale(coin, 0f);
                overlay.Add(coin);
                coins[i] = coin;
            }

            var began = Time.unscaledTime;
            var landed = 0;
            try {
                while (landed < CoinCount) {
                    var now = Time.unscaledTime - began;
                    landed = 0;
                    for (var i = 0; i < CoinCount; i++) {
                        var t = Mathf.Clamp01((now - i * CoinStagger) / CoinFlight);
                        if (t >= 1f) {
                            if (coins[i].style.display != DisplayStyle.None) {
                                coins[i].style.display = DisplayStyle.None;
                                Jump(to, 1.18f, token).Forget();
                            }

                            landed++;
                            continue;
                        }

                        // Out to the side a little, then up and over into the counter, eased in.
                        var eased = t * t * (3f - 2f * t);
                        var side = (i % 2 == 0 ? -1f : 1f) * (40f + 12f * i) * Mathf.Sin(t * Mathf.PI);
                        var point = Vector2.Lerp(start, end, eased) + new Vector2(side, -CoinArc * Mathf.Sin(t * Mathf.PI));
                        coins[i].style.translate = new Translate(point.x, point.y);
                        SetScale(coins[i], t <= 0f ? 0f : Mathf.Lerp(1.3f, 0.7f, t));
                        coins[i].style.rotate = new Rotate(t * 540f);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally {
                foreach (var coin in coins) {
                    coin.RemoveFromHierarchy();
                }
            }
        }

        private static async UniTaskVoid Shake(VisualElement card, CancellationToken token)
        {
            for (var time = 0f; time < ShakeTime; time += Time.unscaledDeltaTime) {
                var t = time / ShakeTime;
                card.style.translate = new Translate(28f * (1f - t) * Mathf.Sin(t * Mathf.PI * 8f), 0f);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            card.style.translate = new Translate(0f, 0f);
        }

        private static async UniTaskVoid Toast(VisualElement overlay, VisualElement over, string text,
            CancellationToken token)
        {
            var toast = StrokeText.Outlined(text, "reward-toast", out _);
            toast.pickingMode = PickingMode.Ignore;
            overlay.Add(toast);
            var origin = overlay.WorldToLocal(new Vector2(over.worldBound.center.x, over.worldBound.yMin));
            try {
                for (var time = 0f; time < ToastTime; time += Time.unscaledDeltaTime) {
                    var t = time / ToastTime;
                    var width = toast.layout.width;
                    toast.style.translate = new Translate(origin.x - (float.IsNaN(width) ? 0f : width * 0.5f),
                        origin.y - 90f - 70f * t);
                    toast.style.opacity = t < 0.75f ? 1f : (1f - t) / 0.25f;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally {
                toast.RemoveFromHierarchy();
            }
        }

        private static void SetScale(VisualElement element, float value) =>
            element.style.scale = new Scale(new Vector3(value, value, 1f));
    }
}
