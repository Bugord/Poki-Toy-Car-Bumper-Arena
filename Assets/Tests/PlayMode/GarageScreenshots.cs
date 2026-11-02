using System.Collections;
using System.IO;
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
    /// Pictures of the garage on its CARS and TOPPERS tabs, in portrait and landscape, with coins for some toppers and
    /// one owned and worn: <c>Logs/garage/&lt;tab&gt;_&lt;w&gt;x&lt;h&gt;.png</c>. Explicit: it is for looking at, and
    /// needs graphics.
    /// </summary>
    [Explicit]
    public sealed class GarageScreenshots
    {
        [UnityTest]
        public IEnumerator CaptureTheGarageTabs()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "garage");
            Directory.CreateDirectory(directory);
            var showTab = typeof(GarageView).GetMethod("ShowTab", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var (width, height) in new[] { (1080, 1920), (1920, 1080) }) {
                var storage = new RoundOverScreenshots.MemoryStorage();
                // Level 7: every car open, the speedster and the monster truck still to buy.
                storage.SetInt("xp_total", 540);
                storage.SetString("cars_owned", "");
                storage.SetInt("coins", 260);
                storage.SetString("toppers_owned", "party-hat");
                storage.SetString("topper_selected", "party-hat");
                var panelSettings = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                panelSettings.targetTexture = target;
                var host = new GameObject("Garage screenshot");
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                var model = new GarageViewModel(new PlayerProgress(storage, tuning, catalog), catalog,
                    new MatchTests.RecordingPlatform(), null, new PlayerWallet(storage, tuning), tuning);
                var view = new GarageView(new SilentAudioService(library));
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
                    view.Attach(screen);
                    view.ShowAsync(CancellationToken.None).Forget();
                    foreach (var isToppers in new[] { false, true }) {
                        showTab.Invoke(view, new object[] { isToppers });
                        var start = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - start < 0.8f) {
                            yield return null;
                        }

                        Save(target, Path.Combine(directory, $"{(isToppers ? "toppers" : "cars")}_{width}x{height}.png"));
                    }
                }
                finally {
                    view.Dispose();
                    Object.Destroy(host);
                    Object.DestroyImmediate(target);
                    Object.Destroy(panelSettings);
                }
            }

            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        private static void Save(RenderTexture source, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Garage screenshot: {path}");
        }
    }
}
