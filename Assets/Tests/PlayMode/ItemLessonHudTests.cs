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
    /// The item lesson's hint in a plain round with boxes, on the real Game scene: until the player has used an item, the
    /// HUD's TAP TO USE is up whenever an item is ready, past the 2.6 s it used to stay, goes down on the use, and does not
    /// come back once the lesson is over.
    /// </summary>
    public sealed class ItemLessonHudTests : ObjectsBFixture
    {
        private const string HintShown = "hud__item-hint--shown";
        // Longer than the old one-time hint stayed up (2.6 s).
        private const float PastOldHint = 3.2f;

        [UnityTest]
        public IEnumerator TheHintStaysUntilTheFirstItemIsUsed()
        {
            HasUsedItem = false;
            yield return Open("sandbox", "rake", round: 2);
            var pickups = Container.Resolve<ArenaPickups>();
            Assert.That(pickups.IsEnabled, Is.True);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));

            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            var host = new GameObject("Lesson HUD");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            // The HUD's own controls, apart from the scope's lesson: the test marks the use on them as the app would.
            var controls = new HudControls { HasUsedItem = false };
            var model = new HudViewModel(null, controls);
            model.SetPayload(new HudRound(Round, Container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single(),
                Tuning.Controls, false, 2, false, playerRound: 2));
            var view = new HudView(controls, Container.Resolve<IAudioService>());
            view.Bind(model);
            var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
            hud.style.position = Position.Absolute;
            hud.style.left = 0;
            hud.style.top = 0;
            hud.style.right = 0;
            hud.style.bottom = 0;
            var player = Round.Player;
            void OnUsed(ItemKind kind) => controls.MarkItemUsed();
            player.ItemUsed += OnUsed;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();
                yield return Frames(10);
                var hint = hud.Q<VisualElement>("item-hint");
                Assert.That(hint.ClassListContains(HintShown), Is.False, "no hint over an empty slot");

                Assert.That(pickups.Give(player, ItemKind.Bomb), Is.True);
                yield return Until(() => hint.ClassListContains(HintShown), 1f);
                Assert.That(hint.ClassListContains(HintShown), Is.True, "the hint comes up with the item");

                var downs = 0;
                var end = Time.unscaledTime + PastOldHint;
                while (Time.unscaledTime < end) {
                    Hold();
                    yield return null;
                    if (!hint.ClassListContains(HintShown)) {
                        downs++;
                    }
                }

                Assert.That(downs, Is.Zero, "the hint stays up while the item waits");

                Assert.That(pickups.Use(player), Is.True);
                yield return Frames(3);
                Assert.That(hint.ClassListContains(HintShown), Is.False, "using the item takes the hint down");
                Assert.That(controls.HasUsedItem, Is.True);
                Assert.That(((ItemLessonMemory)Container.Resolve<IItemLesson>()).HasUsedItem, Is.True,
                    "the scene ended the lesson");

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
                Assert.That(shown, Is.Zero, "the hint does not come back once the lesson is over");
            }
            finally {
                player.ItemUsed -= OnUsed;
                view.Dispose();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(panelSettings);
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
