#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The ITEM FRENZY HUD on the real Game scene: the item hint is up whenever an item is ready, past the moment the
    /// stock hint would have gone, until the player uses an item, and does not come back for the next one; the same for
    /// a player who saw the stock hint before and one who did not: the mode has the hint, so the stock one, which would come
    /// down at its own moment and pop in again under the mode's, keeps off it. Once up, the hint never goes down until the
    /// item is used.
    /// </summary>
    public sealed class ItemFrenzyHudTests : ModeFixture
    {
        private const string HintShown = "hud__item-hint--shown";
        // Longer than the stock hint stays up (2.6 s).
        private const float PastStockHint = 3.2f;

        [UnityTest]
        public IEnumerator TheHintStaysUntilTheFirstItemIsUsed([Values(true, false)] bool sawStockHint)
        {
            UseSpecial(SpecialRound.ItemFrenzy);
            // The bots stand still and every car is held where it stood, so nothing takes the player's item away.
            yield return Open("sandbox", "rake");
            var round = Round;
            var pickups = Container.Resolve<ArenaPickups>();
            var mode = Container.Resolve<ItemFrenzyMode>();
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var forget = typeof(HudView).Assembly.GetType("ToyCarArena.UI.ItemFrenzyHud")
                .GetMethod("ForgetVisit", BindingFlags.Static | BindingFlags.NonPublic);
            forget.Invoke(null, null);

            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            var host = new GameObject("Frenzy HUD");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var controls = new HudControls { HasShownItemHint = sawStockHint };
            var model = new HudViewModel(null, controls);
            model.SetPayload(new HudRound(round, Container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single(),
                Tuning.Controls, false, 3, false, special: SpecialRound.ItemFrenzy, playerRound: 9, mode: mode));
            var view = new HudView(controls, Container.Resolve<IAudioService>());
            view.Bind(model);
            var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
            hud.style.position = Position.Absolute;
            hud.style.left = 0;
            hud.style.top = 0;
            hud.style.right = 0;
            hud.style.bottom = 0;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();
                yield return Frames(10);
                var hint = hud.Q<VisualElement>("item-hint");
                var button = hud.Q<VisualElement>("item-button");
                var player = round.Player;
                Assert.That(hint.ClassListContains(HintShown), Is.False, "no hint over an empty slot");
                Assert.That(button.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the empty slot shows");
                Assert.That(button.resolvedStyle.opacity, Is.LessThan(0.6f), "pale while it waits");

                Assert.That(pickups.Give(player, ItemKind.Bomb), Is.True);
                yield return Until(() => player.IsItemReady && hint.ClassListContains(HintShown), 1f);
                Assert.That(hint.ClassListContains(HintShown), Is.True, "the hint comes up with the item");

                // Past the stock hint's moment: it stays up, not down for a single frame (it would pop in again).
                var downs = 0;
                var end = Time.unscaledTime + PastStockHint;
                while (Time.unscaledTime < end) {
                    Hold();
                    yield return null;
                    if (!hint.ClassListContains(HintShown)) {
                        downs++;
                    }
                }

                Assert.That(downs, Is.Zero, "the hint stays up while the item waits");
                Assert.That(hint.ClassListContains(HintShown), Is.True);

                Assert.That(pickups.Use(player), Is.True);
                yield return Frames(3);
                Assert.That(hint.ClassListContains(HintShown), Is.False, "using the item takes the hint down");

                // The next item: no hint, neither the mode's nor the stock one.
                Assert.That(pickups.Give(player, ItemKind.Bomb), Is.True);
                var shown = 0;
                end = Time.unscaledTime + 1f;
                while (Time.unscaledTime < end) {
                    Hold();
                    yield return null;
                    if (hint.ClassListContains(HintShown)) {
                        shown++;
                    }
                }

                Assert.That(player.IsItemReady, Is.True);
                Assert.That(shown, Is.Zero, "the hint does not come back after the first use");
            }
            finally {
                view.Dispose();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(panelSettings);
                forget.Invoke(null, null);
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++) {
                yield return null;
            }
        }

        private static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            var end = Time.unscaledTime + seconds;
            while (!done() && Time.unscaledTime < end) {
                yield return null;
            }
        }
    }
}
#endif
