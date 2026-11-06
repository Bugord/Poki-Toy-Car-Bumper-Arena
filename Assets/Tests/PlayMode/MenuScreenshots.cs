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
using ToyCarArena.Gameplay.Arena;
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
    /// Pictures of the main menu upright and wide, with a finger (phones) and with a mouse (desktops), for a returning
    /// player (level 7, a topper worn, coins for the shop, a new car not yet seen in the garage), with its missions and
    /// modes popups, and once in Russian: <c>Logs/menu/&lt;case&gt;_&lt;w&gt;x&lt;h&gt;.png</c>. Explicit: it is for
    /// looking at, and needs graphics.
    /// </summary>
    [Explicit]
    public sealed class MenuScreenshots
    {
        private static readonly (int Width, int Height, bool IsTouch)[] Sizes = {
            (1080, 1920, true), (1080, 2340, true), (2340, 1080, true),
            (1920, 1080, false), (2048, 1536, false),
        };

        [UnityTest]
        public IEnumerator CaptureTheMenu()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "menu");
            Directory.CreateDirectory(directory);
            foreach (var size in Sizes) {
                yield return Capture(directory, "menu", size, true);
            }

            Strings.SetLanguage("ru");
            yield return Capture(directory, "ru", (1080, 1920, true), false);
            yield return Capture(directory, "ru", (1920, 1080, false), false);
            Strings.SetLanguage("en");
            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        private static IEnumerator Capture(string directory, string name, (int Width, int Height, bool IsTouch) size,
            bool withPopups)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var arenas = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var (width, height, isTouch) = size;
            var storage = new RoundOverScreenshots.MemoryStorage();
            // Level 7 with the garage last seen at level 5: the SUV is new to the garage.
            storage.SetInt("xp_total", 540);
            storage.SetInt("garage_seen_level", 5);
            storage.SetString("cars_owned", "speedster");
            storage.SetString("car_selected", "speedster");
            storage.SetInt("car_color", 2);
            storage.SetInt("coins", 260);
            storage.SetString("toppers_owned", "party-hat");
            storage.SetString("topper_selected", "party-hat");
            var missions = new MissionTracker(storage, tuning);
            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            panelSettings.targetTexture = target;
            var host = new GameObject("Menu screenshot");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var model = new MenuViewModel(new PlayerProgress(storage, tuning, catalog), new PlayerWallet(storage, tuning),
                missions, catalog, arenas, tuning, null, new MatchTests.RecordingPlatform(), null);
            var light = new GameObject("Menu screenshot light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var stage = new GarageStage(AssetDatabase.LoadAssetAtPath<Car>("Assets/Prefabs/Cars/Car.prefab"),
                AssetDatabase.LoadAssetAtPath<MechanicArt>("Assets/Data/Arenas/data_mechanic_art.asset"), host.transform);
            model.Stage = stage;
            var view = new MenuView(new SilentAudioService(library), new HudControls());
            try {
                var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Menu.uxml").Instantiate();
                screen.style.position = Position.Absolute;
                screen.style.left = 0;
                screen.style.top = 0;
                screen.style.right = 0;
                screen.style.bottom = 0;
                document.rootVisualElement.Add(screen);
                view.Bind(model);
                // UIKit hands a view the UXML's own root, not the template container around it.
                var root = screen.hierarchy[0];
                view.Attach(root);
                // The editor has no touch screen: the phone sizes take the touch look by hand.
                typeof(MenuView).GetField("isTouch", Private)?.SetValue(view, isTouch);
                root.EnableInClassList("menu--desktop", !isTouch);
                view.ShowAsync(CancellationToken.None).Forget();
                yield return Wait(0.5f);
                typeof(MenuView).GetMethod("Place", Private)?.Invoke(view, null);
                yield return Wait(0.6f);
                Save(target, Path.Combine(directory, $"{name}_{width}x{height}.png"));
                if (withPopups) {
                    foreach (var popup in new[] { "OpenMissions", "OpenModes" }) {
                        typeof(MenuView).GetMethod(popup, Private)?.Invoke(view, null);
                        yield return Wait(0.3f);
                        var shot = popup == "OpenMissions" ? "missions" : "modes";
                        Save(target, Path.Combine(directory, $"{shot}_{width}x{height}.png"));
                        typeof(MenuView).GetMethod("ClosePopups", Private)?.Invoke(view, null);
                    }
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
            Debug.Log($"Menu screenshot: {path}");
        }
    }
}
