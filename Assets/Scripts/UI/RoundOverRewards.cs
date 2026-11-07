using System;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The round's reward at the top of the level card, as in the design: two equal stickers, the XP (a green star) and
    /// the coins (a gold coin), each the round's own amount, missions aside, and beside them CLAIM ×2, a purple button
    /// with a video sign and an AD tag that watches a rewarded ad for the same once more. Doubled, both numbers count up
    /// to twice, each sticker gets a ×2 stamp and the button gives way to DOUBLED!. Built in code into the screen's tree;
    /// the animation runs on unscaled time.
    /// </summary>
    internal sealed class RoundOverRewards : IDisposable
    {
        private const int EnterDelay = 200;
        private const int EnterStep = 90;
        private const float CountDuration = 0.7f;
        private const float PopDuration = 0.35f;

        private readonly RoundOverViewModel viewModel;
        private readonly VisualElement xp;
        private readonly VisualElement coins;
        private readonly Label[] xpValue;
        private readonly Label[] coinValue;
        private readonly VisualElement[] stamps = new VisualElement[2];
        private readonly VisualElement doubled;
        private readonly LiveText text = new();
        private CancellationTokenSource animation;

        public RoundOverRewards(VisualElement root, RoundOverViewModel viewModel, bool isTouch)
        {
            this.viewModel = viewModel;
            var row = new VisualElement { name = "rewards", pickingMode = PickingMode.Ignore };
            row.AddToClassList("rewards");
            xp = Sticker("reward--xp", HudView.CreateStar("ko-star"), "result.reward_xp", viewModel.RoundXp, 0,
                out xpValue);
            // The XP boost bought in the menu paid for this round: its XP already counts it.
            if (viewModel.XpGain.BoostXp > 0) {
                var boost = new VisualElement { pickingMode = PickingMode.Ignore };
                boost.AddToClassList("reward__boost");
                var boostLabel = RoundOverProgress.Outlined("", "reward__boost-label", out var boostLabels);
                foreach (var part in boostLabels) {
                    text.Set(part, () => Strings.Format("result.boost_stamp", viewModel.BoostMultiplier));
                }

                boost.Add(boostLabel);
                xp.Add(boost);
            }

            coins = Sticker("reward--coins", RoundOverMeta.Coin(), "result.reward_coins", viewModel.RoundCoins, 1,
                out coinValue);
            row.Add(xp);
            row.Add(coins);

            Claim = new Button { name = "double-button" };
            Claim.RemoveFromClassList(Button.ussClassName);
            Claim.AddToClassList("button");
            Claim.AddToClassList("button--purple");
            Claim.AddToClassList("double");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("button__face");
            Claim.Add(face);
            Claim.Add(PlaySign());
            var label = RoundOverProgress.Outlined("", "double__label", out var labels);
            foreach (var part in labels) {
                part.AddToClassList("button__label");
                text.Set(part, () => Strings.Get("result.claim_x2"));
            }
            FitText.Fit(label, labels);
            Claim.Add(label);
            if (!isTouch) {
                var key = new VisualElement { pickingMode = PickingMode.Ignore };
                key.AddToClassList("key");
                key.AddToClassList("double__key");
                key.Add(Text("X", "key__text"));
                Claim.Add(key);
            }

            var tag = Text("", "double__tag");
            text.Set(tag, () => Strings.Get("result.ad"));
            Claim.Add(tag);
            Claim.style.display = viewModel.CanDouble ? DisplayStyle.Flex : DisplayStyle.None;
            row.Add(Claim);

            doubled = new VisualElement { pickingMode = PickingMode.Ignore };
            doubled.AddToClassList("doubled");
            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.AddToClassList("doubled__tick");
            var mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.AddToClassList("track__tick-mark");
            tick.Add(mark);
            doubled.Add(tick);
            var doneLabel = Text("", "doubled__label");
            text.Set(doneLabel, () => Strings.Get("result.doubled"));
            doubled.Add(doneLabel);
            doubled.style.display = DisplayStyle.None;
            row.Add(doubled);

            var card = root.Q<VisualElement>("level-card");
            card.Insert(0, row);
            SetScale(xp, 0f);
            SetScale(coins, 0f);
            SetScale(Claim, 0f);
        }

        /// <summary>CLAIM ×2; the view presses it.</summary>
        public Button Claim { get; }

        public void Play(CancellationToken token)
        {
            Stop();
            animation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Enter(animation.Token).Forget();
        }

        public void Stop()
        {
            animation?.Cancel();
            animation?.Dispose();
            animation = null;
            SetScale(xp, 1f);
            SetScale(coins, 1f);
            SetScale(Claim, 1f);
        }

        public void Dispose()
        {
            Stop();
            text.Dispose();
        }

        /// <summary>While the ad plays the button stays, greyed; without its reward it goes for this result.</summary>
        public void SetWaiting(bool isWaiting) => Claim.SetEnabled(!isWaiting);

        public void HideClaim() => Claim.style.display = DisplayStyle.None;

        /// <summary>The ad paid: the numbers count up to twice, the stamps land and DOUBLED! takes the button's place.</summary>
        public void ShowDoubled(CancellationToken token)
        {
            Stop();
            animation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Double(animation.Token).Forget();
        }

        private async UniTaskVoid Enter(CancellationToken token)
        {
            SetScale(xp, 0f);
            SetScale(coins, 0f);
            SetScale(Claim, 0f);
            if (await UniTask.Delay(EnterDelay, ignoreTimeScale: true, cancellationToken: token)
                    .SuppressCancellationThrow()) {
                return;
            }

            foreach (var part in new[] { xp, coins, Claim }) {
                Pop(part, token).Forget();
                if (await UniTask.Delay(EnterStep, ignoreTimeScale: true, cancellationToken: token)
                        .SuppressCancellationThrow()) {
                    return;
                }
            }

            if (viewModel.CanDouble) {
                viewModel.DoubleShown();
            }
        }

        private async UniTaskVoid Double(CancellationToken token)
        {
            Claim.style.display = DisplayStyle.None;
            doubled.style.display = DisplayStyle.Flex;
            Pop(doubled, token).Forget();
            var start = Time.unscaledTime;
            var xpFrom = viewModel.RoundXp;
            var coinsFrom = viewModel.RoundCoins;
            var xpTo = xpFrom + viewModel.BonusXp.Total;
            var coinsTo = coinsFrom + viewModel.BonusCoins.Total;
            try {
                while (true) {
                    var share = Mathf.Clamp01((Time.unscaledTime - start) / CountDuration);
                    SetValue(xpValue, Mathf.RoundToInt(Mathf.Lerp(xpFrom, xpTo, share)));
                    SetValue(coinValue, Mathf.RoundToInt(Mathf.Lerp(coinsFrom, coinsTo, share)));
                    if (share >= 1f) {
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                foreach (var stamp in stamps) {
                    stamp.style.display = DisplayStyle.Flex;
                    Pop(stamp, token).Forget();
                }

                await UniTask.WhenAll(Bounce(xp, token), Bounce(coins, token));
            }
            catch (OperationCanceledException) {
                SetValue(xpValue, xpTo);
                SetValue(coinValue, coinsTo);
                foreach (var stamp in stamps) {
                    stamp.style.display = DisplayStyle.Flex;
                    SetScale(stamp, 1f);
                }

                SetScale(doubled, 1f);
            }
        }

        private VisualElement Sticker(string className, VisualElement icon, string nameKey, int value, int index,
            out Label[] valueLabels)
        {
            var sticker = new VisualElement { pickingMode = PickingMode.Ignore };
            sticker.AddToClassList("reward");
            sticker.AddToClassList(className);
            var what = new VisualElement { pickingMode = PickingMode.Ignore };
            what.AddToClassList("reward__what");
            icon.AddToClassList("reward__icon");
            what.Add(icon);
            var name = Text("", "reward__name");
            text.Set(name, () => Strings.Get(nameKey));
            what.Add(name);
            sticker.Add(what);
            sticker.Add(RoundOverProgress.Outlined($"+{value}", "reward__value", out valueLabels));

            var stamp = RoundOverProgress.Outlined("×2", "reward__stamp", out _);
            stamp.style.display = DisplayStyle.None;
            sticker.Add(stamp);
            stamps[index] = stamp;
            return sticker;
        }

        // The video sign: a white disc with a navy ring and a purple triangle, as video ads are marked.
        internal static VisualElement PlaySign()
        {
            var sign = new VisualElement { pickingMode = PickingMode.Ignore };
            sign.AddToClassList("double__play");
            sign.generateVisualContent += context => {
                var rect = sign.contentRect;
                var painter = context.painter2D;
                var size = Mathf.Min(rect.width, rect.height);
                var centre = rect.center;
                painter.fillColor = new Color(0.114f, 0.169f, 0.31f);
                painter.BeginPath();
                painter.Arc(centre, size * 0.5f, 0f, 360f);
                painter.Fill();
                painter.fillColor = Color.white;
                painter.BeginPath();
                painter.Arc(centre, size * 0.5f - size * 0.09f, 0f, 360f);
                painter.Fill();
                painter.fillColor = new Color(0.114f, 0.169f, 0.31f);
                painter.BeginPath();
                var r = size * 0.22f;
                painter.MoveTo(centre + new Vector2(-r * 0.7f, -r));
                painter.LineTo(centre + new Vector2(r * 1.1f, 0f));
                painter.LineTo(centre + new Vector2(-r * 0.7f, r));
                painter.ClosePath();
                painter.Fill();
            };
            return sign;
        }

        private static void SetValue(Label[] labels, int value)
        {
            foreach (var label in labels) {
                label.text = $"+{value}";
            }
        }

        private static Label Text(string value, string className)
        {
            var label = new Label(value) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private static async UniTask Pop(VisualElement sticker, CancellationToken token)
        {
            SetScale(sticker, 0f);
            for (var time = 0f; time < PopDuration; time += Time.unscaledDeltaTime) {
                var t = time / PopDuration - 1f;
                SetScale(sticker, 1f + 2.70158f * t * t * t + 1.70158f * t * t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(sticker, 1f);
        }

        private static async UniTask Bounce(VisualElement element, CancellationToken token)
        {
            for (var time = 0f; time < PopDuration; time += Time.unscaledDeltaTime) {
                SetScale(element, 1f + 0.12f * Mathf.Sin(Mathf.PI * time / PopDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(element, 1f);
        }

        private static void SetScale(VisualElement element, float value) =>
            element.style.scale = new Scale(new Vector3(value, value, 1f));
    }
}
