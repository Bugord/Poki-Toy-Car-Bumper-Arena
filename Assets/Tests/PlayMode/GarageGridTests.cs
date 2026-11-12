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
    /// The garage grids keep three cards a row at any window size. The layout rounds every card to the screen's
    /// pixels, and on a small window a pixel is several reference pixels, so cards sized to fill the row exactly
    /// came out too wide together and the third wrapped (seen in Poki's preview at about 1040x580).
    /// </summary>
    public sealed class GarageGridTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly Vector2Int[] Sizes = {
            new Vector2Int(1040, 580), new Vector2Int(1060, 590), new Vector2Int(1100, 619), new Vector2Int(1280, 720),
            new Vector2Int(1289, 725), new Vector2Int(1366, 768), new Vector2Int(1536, 864), new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2048, 1536), new Vector2Int(900, 506),
            new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(540, 960), new Vector2Int(2340, 1080),
        };

        [UnityTest]
        public IEnumerator TheGridsKeepThreeCardsARowAtAnySize()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            var tabType = typeof(GarageView).GetNestedType("Tab", BindingFlags.NonPublic);
            var showTab = typeof(GarageView).GetMethod("ShowTab", Private);
            var failures = new List<string>();
            try {
                foreach (var size in Sizes) {
                    var storage = new RoundOverScreenshots.MemoryStorage();
                    storage.SetInt("xp_total", 540);
                    storage.SetInt("coins", 60);
                    var panelSettings = Object.Instantiate(
                        AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                    var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                    panelSettings.targetTexture = target;
                    var host = new GameObject("Garage grid");
                    var document = host.AddComponent<UIDocument>();
                    document.panelSettings = panelSettings;
                    var model = new GarageViewModel(new PlayerProgress(storage, tuning, catalog), catalog,
                        new MatchTests.RecordingPlatform(), null, new PlayerWallet(storage, tuning), tuning, null);
                    var view = new GarageView(new SilentAudioService(library), new HudControls());
                    var showing = new CancellationTokenSource();
                    try {
                        var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Garage.uxml")
                            .Instantiate();
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
                        view.ShowAsync(showing.Token).SuppressCancellationThrow().Forget();
                        yield return Frames(15);

                        foreach (var (tab, name) in new[] { (0, "cars"), (1, "hats") }) {
                            showTab.Invoke(view, new[] { System.Enum.ToObject(tabType, tab) });
                            yield return Frames(8);
                            var rows = Rows(root.Q<ScrollView>(className: "garage__scroll").contentContainer);
                            Debug.Log($"Garage {size.x}x{size.y} {name}: rows {string.Join(",", rows)}");
                            for (var i = 0; i < rows.Count - 1; i++) {
                                if (rows[i] != 3) {
                                    failures.Add($"{size.x}x{size.y} {name}: rows {string.Join(",", rows)}");
                                    break;
                                }
                            }
                        }
                    }
                    finally {
                        showing.Cancel();
                        showing.Dispose();
                        view.Dispose();
                        Object.Destroy(host);
                        Object.DestroyImmediate(target);
                        Object.Destroy(panelSettings);
                    }

                    yield return null;
                }
            }
            finally {
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>Cards a row of the visible grid inside <paramref name="content"/>, top to bottom.</summary>
        private static List<int> Rows(VisualElement content)
        {
            var rows = new List<int>();
            VisualElement grid = null;
            foreach (var child in content.Children()) {
                if (child.resolvedStyle.display != DisplayStyle.None && child.childCount > 0) {
                    grid = child.ClassListContains("garage__grid") ? child : grid ?? child;
                }
            }

            if (grid == null) {
                return rows;
            }

            var lastY = float.NaN;
            foreach (var card in grid.Children()) {
                if (card.resolvedStyle.display == DisplayStyle.None) {
                    continue;
                }

                var y = card.layout.y;
                if (float.IsNaN(lastY) || Mathf.Abs(y - lastY) > 1f) {
                    rows.Add(0);
                    lastY = y;
                }

                rows[rows.Count - 1]++;
            }

            return rows;
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) {
                yield return null;
            }
        }
    }
}
