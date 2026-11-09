using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Bugord.App.Localization;
using Bugord.Core.Storage;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The garage grid under the mouse wheel: wheeling past its top or its bottom leaves the cards still, frame after
    /// frame, instead of letting them jerk past the end and spring back.
    /// </summary>
    public sealed class GarageScrollTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Frames watched after each burst of wheel ticks: long enough for a spring back to show.</summary>
        private const int WatchedFrames = 30;

        [UnityTest]
        public IEnumerator TheWheelStopsTheGridAtItsTopAndBottom()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetInt("xp_total", 540);
            storage.SetString("cars_owned", "");
            storage.SetInt("coins", 60);
            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            panelSettings.targetTexture = target;
            var host = new GameObject("Garage scroll");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var model = new GarageViewModel(new PlayerProgress(storage, tuning, catalog), catalog,
                new MatchTests.RecordingPlatform(), null, new PlayerWallet(storage, tuning), tuning, null);
            var view = new GarageView(new SilentAudioService(library), new HudControls());
            var tabType = typeof(GarageView).GetNestedType("Tab", BindingFlags.NonPublic);
            var showTab = typeof(GarageView).GetMethod("ShowTab", Private);
            try {
                var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Garage.uxml").Instantiate();
                screen.style.position = Position.Absolute;
                screen.style.left = 0;
                screen.style.top = 0;
                screen.style.right = 0;
                screen.style.bottom = 0;
                document.rootVisualElement.Add(screen);
                view.Bind(model);
                var root = screen.hierarchy[0];
                view.Attach(root);
                root.EnableInClassList("garage--desktop", true);
                view.ShowAsync(CancellationToken.None).Forget();
                yield return Frames(20);

                var scroll = root.Q<ScrollView>(className: "garage__scroll");
                Assert.That(scroll, Is.Not.Null);
                foreach (var (tab, name) in new[] { (0, "cars"), (1, "hats") }) {
                    showTab.Invoke(view, new[] { System.Enum.ToObject(tabType, tab) });
                    yield return Frames(10);
                    var bottom = scroll.verticalScroller.highValue;
                    Assert.That(bottom, Is.GreaterThan(1f), $"The {name} grid should be taller than its panel.");

                    // Above the top: the grid starts there and the wheel has nowhere to go.
                    yield return Wheel(scroll, -3f, 6, name + " top", 0f);

                    // Down to the bottom, then on past it.
                    for (var i = 0; i < 40 && scroll.scrollOffset.y < bottom - 0.5f; i++) {
                        SendWheel(scroll, 3f);
                        yield return null;
                    }

                    yield return Frames(WatchedFrames);
                    Assert.That(scroll.scrollOffset.y, Is.EqualTo(bottom).Within(0.5f), $"The {name} grid should reach its bottom.");
                    yield return Wheel(scroll, 3f, 6, name + " bottom", scroll.scrollOffset.y);

                    // And back up past the top.
                    for (var i = 0; i < 40 && scroll.scrollOffset.y > 0.5f; i++) {
                        SendWheel(scroll, -3f);
                        yield return null;
                    }

                    yield return Frames(WatchedFrames);
                    yield return Wheel(scroll, -3f, 6, name + " top again", 0f);
                }
            }
            finally {
                view.Dispose();
                Object.Destroy(host);
                Object.DestroyImmediate(target);
                Object.Destroy(panelSettings);
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        /// <summary>
        /// Sends <paramref name="ticks"/> wheel ticks one frame apart past an end, and checks every frame through them
        /// and after them that the offset and the content's shift stay where the end is.
        /// </summary>
        private static IEnumerator Wheel(ScrollView scroll, float delta, int ticks, string where, float end)
        {
            var offsets = new List<string>();
            var shift = scroll.contentContainer.resolvedStyle.translate.y;
            for (var frame = 0; frame < ticks + WatchedFrames; frame++) {
                if (frame < ticks) {
                    SendWheel(scroll, delta);
                }

                yield return null;
                var offset = scroll.scrollOffset.y;
                var moved = scroll.contentContainer.resolvedStyle.translate.y;
                offsets.Add($"{offset:0.#}/{moved:0.#}");
                Assert.That(offset, Is.EqualTo(end).Within(0.01f),
                    $"Wheeling past the {where}, frame {frame}, offset/translate so far: {string.Join(" ", offsets)}");
                Assert.That(moved, Is.EqualTo(shift).Within(0.01f),
                    $"Wheeling past the {where}, frame {frame}, offset/translate so far: {string.Join(" ", offsets)}");
            }

            Debug.Log($"Garage wheel past the {where}: {string.Join(" ", offsets)}");
        }

        /// <summary>One wheel tick over the middle of the grid, picked like a real mouse's: by its position.</summary>
        private static void SendWheel(ScrollView scroll, float delta)
        {
            var at = scroll.contentViewport.worldBound.center;
            using var wheel = WheelEvent.GetPooled(new Event {
                type = EventType.ScrollWheel,
                delta = new Vector2(0f, delta),
                mousePosition = at,
            });
            scroll.panel.visualTree.SendEvent(wheel);
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) {
                yield return null;
            }
        }
    }
}
