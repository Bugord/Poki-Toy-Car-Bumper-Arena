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
    /// Pictures of the garage on its CARS and TOPPERS tabs, upright and wide, with a finger (phones) and with a mouse
    /// (desktops), for a player well on (every car open, coins for the speedster but not the monster truck, a topper worn)
    /// and a new one (cars still locked, toppers too dear), plus a pass in Russian, the line over a too dear car and a
    /// hat tried on before it is bought:
    /// <c>Logs/garage/&lt;save&gt;_&lt;tab&gt;_&lt;w&gt;x&lt;h&gt;.png</c>. Explicit: it is for looking at, and needs graphics.
    /// </summary>
    [Explicit]
    public sealed class GarageScreenshots
    {
        private static readonly (int Width, int Height, bool IsTouch)[] Sizes = {
            (1080, 1920, true), (1080, 2340, true), (2340, 1080, true),
            (1920, 1080, false), (1920, 1200, false), (2048, 1536, false),
        };

        [UnityTest]
        public IEnumerator CaptureTheGarageTabs()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "garage");
            Directory.CreateDirectory(directory);
            foreach (var size in Sizes) {
                // Level 7: every car open, the speedster and the monster truck still to buy, 260 coins.
                yield return Capture(directory, "rich", size, 540, 260, "party-hat", true);
                // Level 2, 30 coins: most cars locked and every topper too dear.
                yield return Capture(directory, "new", size, 60, 30, "", false);
            }

            Strings.SetLanguage("ru");
            yield return Capture(directory, "ru", (1080, 1920, true), 540, 260, "party-hat", false);
            yield return Capture(directory, "ru", (1920, 1080, false), 540, 260, "party-hat", false);
            Strings.SetLanguage("en");
            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        private static IEnumerator Capture(string directory, string save, (int Width, int Height, bool IsTouch) size,
            int xp, int coins, string topper, bool tapTooDear)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            var showTab = typeof(GarageView).GetMethod("ShowTab", BindingFlags.Instance | BindingFlags.NonPublic);
            var select = typeof(GarageView).GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic);
            var pickTopper = typeof(GarageView).GetMethod("PickTopper", BindingFlags.Instance | BindingFlags.NonPublic);
            var (width, height, isTouch) = size;
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetInt("xp_total", xp);
            storage.SetString("cars_owned", "");
            storage.SetInt("coins", coins);
            storage.SetString("toppers_owned", topper);
            storage.SetString("topper_selected", topper);
            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            panelSettings.targetTexture = target;
            var host = new GameObject("Garage screenshot");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var model = new GarageViewModel(new PlayerProgress(storage, tuning, catalog), catalog,
                new MatchTests.RecordingPlatform(), null, new PlayerWallet(storage, tuning), tuning, null);
            // The 3D car and toppers as the arena scene would give them, under a light like its sun.
            var light = new GameObject("Garage screenshot light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var stage = new GarageStage(AssetDatabase.LoadAssetAtPath<Car>("Assets/Prefabs/Cars/Car.prefab"), host.transform);
            model.Stage = stage;
            var view = new GarageView(new SilentAudioService(library), new HudControls());
            try {
                var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Garage.uxml").Instantiate();
                screen.style.position = Position.Absolute;
                screen.style.left = 0;
                screen.style.top = 0;
                screen.style.right = 0;
                screen.style.bottom = 0;
                document.rootVisualElement.Add(screen);
                view.Bind(model);
                // UIKit hands a view the UXML's own root, not the template container around it.
                view.Attach(screen.hierarchy[0]);
                // The editor has no touch screen: the phone sizes take the touch look by hand.
                screen.hierarchy[0].EnableInClassList("garage--touch", isTouch);
                view.ShowAsync(CancellationToken.None).Forget();
                foreach (var isToppers in new[] { false, true }) {
                    showTab.Invoke(view, new object[] { isToppers });
                    yield return Wait(0.8f);
                    Save(target, Path.Combine(directory, $"{save}_{(isToppers ? "toppers" : "cars")}_{width}x{height}.png"));
                }

                if (tapTooDear) {
                    showTab.Invoke(view, new object[] { false });
                    yield return Wait(0.3f);
                    var monster = catalog.Find("monster-truck");
                    // The first tap shows it on the plinth, the second tries to buy it.
                    select.Invoke(view, new object[] { monster, screen.Q<Button>($"garage-car-{monster.Id}") });
                    yield return Wait(0.6f);
                    Save(target, Path.Combine(directory, $"{save}_preview_{width}x{height}.png"));
                    select.Invoke(view, new object[] { monster, screen.Q<Button>($"garage-car-{monster.Id}") });
                    yield return Wait(0.25f);
                    Save(target, Path.Combine(directory, $"{save}_toodear_{width}x{height}.png"));
                    // A hat the coins pay for, tried on the hero by the first tap: gold frame and BUY on its tag.
                    showTab.Invoke(view, new object[] { true });
                    yield return Wait(0.3f);
                    pickTopper.Invoke(view, new object[] { "horns", screen.Q<Button>("garage-topper-horns") });
                    yield return Wait(0.6f);
                    Save(target, Path.Combine(directory, $"{save}_hat_{width}x{height}.png"));
                }
            }
            finally {
                view.Dispose();
                stage.Dispose();
                Object.Destroy(light.gameObject);
                Object.Destroy(host);
                Object.DestroyImmediate(target);
                Object.Destroy(panelSettings);
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) {
                yield return null;
            }
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
