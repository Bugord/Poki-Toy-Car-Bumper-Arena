using System.Collections;
using System.Collections.Generic;
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
    /// Pictures of the result screen with the missions, the coins and the streak, after a lost round and a won one, in
    /// portrait and landscape, on a desktop and a touch screen, as it springs in and once the missions have ticked:
    /// <c>Logs/round-over/&lt;case&gt;_&lt;moment&gt;_&lt;w&gt;x&lt;h&gt;_&lt;input&gt;.png</c>. Explicit: it is for
    /// looking at, and needs graphics.
    /// </summary>
    [Explicit]
    public sealed class RoundOverScreenshots
    {
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureTheResultWithMissions()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var arenas = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");

            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "round-over");
            Directory.CreateDirectory(directory);
            var shots = new[] {
                (1080, 1920, false), (1920, 1080, false), (1080, 1920, true), (2340, 1080, true), (2048, 1536, true),
            };
            var cases = new[] { "lost", "won", "offer", "gift", "double", "bought", "hat" };
            if (System.Environment.GetEnvironmentVariable("SHOTS_CASES") is { Length: > 0 } only) {
                cases = only.Split(',');
            }

            // SHOTS_LANG=fr,ru shoots every case again in each language, its code in the file name.
            var languages = new List<string> { "en" };
            if (System.Environment.GetEnvironmentVariable("SHOTS_LANG") is { Length: > 0 } extra) {
                languages = new List<string>(extra.Split(','));
            }

            foreach (var language in languages) {
                Strings.SetLanguage(language);
                var prefix = language == "en" ? "" : language + "_";

                foreach (var name in cases) {
                    // The gift waits behind the popup of the level the round opens, each closing by itself after 4 s.
                    // CLAIM ×2 is pressed after the first picture, its ad paying at once.
                    var moments = name == "gift" ? new[] { 0.6f, 3.0f, 20.0f }
                        : name == "double" ? new[] { 0.6f, 1.2f, 4.0f }
                        // The popup's BUY & EQUIP is pressed after the first picture: the celebration plays on.
                        : name is "bought" or "hat" ? new[] { 3.0f, 3.3f, 3.7f, 4.4f, 6.0f } : new[] { 0.6f, 3.0f };
                    foreach (var (width, height, isTouch) in shots) {
                        var record = Record(name, tuning, catalog);
                        var panelSettings = Object.Instantiate(
                            AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                        panelSettings.targetTexture = target;
                        var host = new GameObject("Result screenshot");
                        var document = host.AddComponent<UIDocument>();
                        document.panelSettings = panelSettings;
                        var controls = new HudControls();
                        typeof(HudControls).GetField("isTouchSeen", BindingFlags.Instance | BindingFlags.NonPublic)
                            ?.SetValue(controls, isTouch);
                        var progress = new PlayerProgress(record.storage, tuning, catalog);
                        var wallet = new PlayerWallet(record.storage, tuning);
                        if (name == "gift") {
                            // The free topper of an early win, before the result takes it.
                            wallet.TryGift(tuning.Toppers.GiftLevel);
                        }

                        var model = new RoundOverViewModel(catalog, arenas, tuning, progress, null,
                            new MatchTests.RecordingPlatform { Rewards = true }, wallet, null);
                        model.SetPayload(record.record);
                        // The gift's 3D hat as the arena scene's garage stage draws it, under a light like its sun.
                        Light light = null;
                        GarageStage stage = null;
                        if (name is "gift" or "hat") {
                            light = new GameObject("Gift screenshot light").AddComponent<Light>();
                            light.type = LightType.Directional;
                            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                            stage = new GarageStage(AssetDatabase.LoadAssetAtPath<Car>("Assets/Prefabs/Cars/Car.prefab"),
                                AssetDatabase.LoadAssetAtPath<MechanicArt>("Assets/Data/Arenas/data_mechanic_art.asset"),
                                host.transform);
                            model.Stage = stage;
                        }

                        var view = new RoundOverView(new SilentAudioService(library), controls);
                        var start = Time.realtimeSinceStartup;
                        try {
                            var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/RoundOver.uxml")
                                .Instantiate();
                            screen.style.position = Position.Absolute;
                            screen.style.left = 0;
                            screen.style.top = 0;
                            screen.style.right = 0;
                            screen.style.bottom = 0;
                            document.rootVisualElement.Add(screen);
                            view.Bind(model);
                            // UIKit hands a view the UXML's own root, not the template container around it.
                            view.Attach(screen.hierarchy[0]);
                            view.ShowAsync(CancellationToken.None).Forget();
                            foreach (var moment in moments) {
                                while (Time.realtimeSinceStartup - start < moment) {
                                    yield return null;
                                }

                                Save(target, Path.Combine(directory,
                                    $"{prefix}{name}_{moment:0.0}_{width}x{height}_{(isTouch ? "touch" : "desktop")}.png"));
                                if (name is "bought" or "hat" && moment < 3.1f) {
                                    var buy = screen.Q<Button>(className: "level-reward__buy");
                                    if (buy != null) {
                                        using var submit = NavigationSubmitEvent.GetPooled();
                                        submit.target = buy;
                                        buy.SendEvent(submit);
                                    }
                                }

                                if (name == "double" && moment < 1f) {
                                    typeof(RoundOverView).GetMethod("ClaimDouble",
                                        BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(view, null);
                                }
                            }
                        }
                        finally {
                            view.Dispose();
                            stage?.Dispose();
                            if (light != null) {
                                Object.Destroy(light.gameObject);
                            }

                            Object.Destroy(host);
                            Object.DestroyImmediate(target);
                            Object.Destroy(panelSettings);
                        }
                    }
                }
            }

            Strings.SetLanguage("en");
            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        // A round as RoundState saves it: a fall after a streak, or a win that keeps one going.
        private static (RoundRecord record, MemoryStorage storage) Record(string name, GameTuning tuning,
            CarCatalog catalog)
        {
            var storage = new MemoryStorage();
            storage.SetString("missions", "round-knockouts:0|knockouts:4|revenge:0");
            storage.SetInt("coins", 85);
            // The offer: the round lifts a new player to level 2, which opens the speedster for coins.
            storage.SetInt("xp_total", name is "offer" or "bought" ? 5 : 70);
            if (name == "hat") {
                // A player with the free top hat, past the car popups, at a plain level: the hat offer comes alone.
                storage.SetString("toppers_owned", "top-hat");
                storage.SetInt("xp_total", 1250);
            }
            if (name == "gift") {
                // Owned paid cars: their level popups close by themselves, and the gift comes after them.
                storage.SetString("cars_owned", "speedster,suv,truck,monster-truck");
            }

            var isWon = name != "lost";
            if (name == "double") {
                // Level 12 with every car open: the bonus crosses a plain level, so no popup covers the card.
                storage.SetInt("xp_total", 1250);
            }
            var result = new RoundResult(isWon ? RoundOutcome.Won : RoundOutcome.Out, isWon ? 1 : 3, 4, 40f,
                isWon ? 2 : 1, RoundResult.Nobody, FallCause.Drive, false, false);
            var missions = new MissionTracker(storage, tuning);
            missions.Seed(1);
            var round = missions.Record(new MissionStats(true, result.Outcome, result.Place, result.Knockouts, 3,
                false, 0, 0, 0, "sandbox"), 3, 3);
            var xp = new PlayerProgress(storage, tuning, catalog).Record(result, isWon ? tuning.Streaks : null, round.Xp);
            var coins = new PlayerWallet(storage, tuning).Record(result, isWon, round.Coins);
            var streak = isWon ? new StreakChange(2, 3, 2) : new StreakChange(3, 0, 2);
            var record = new RoundRecord(result, 1, 2, false, false).WithXp(xp)
                .WithRoster(new[] { "YOU", "ZOOMER", "BONKY", "SKID" }, new[] { 3, 2, 4, 1 }, RoundResult.Nobody)
                .WithMeta(coins, round, streak).WithPodium(Podium());
            return (record, storage);
        }

        // A grey square where the podium's car would turn: the screen takes a render texture there.
        private static RenderTexture Podium()
        {
            var texture = new RenderTexture(256, 256, 0);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, new Color(0.6f, 0.6f, 0.65f, 1f));
            RenderTexture.active = previous;
            return texture;
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
            Debug.Log($"Result screenshot: {path}");
        }

        internal sealed class MemoryStorage : IKeyValueStorage
        {
            private readonly Dictionary<string, string> values = new();

            public bool HasKey(string key) => values.ContainsKey(key);
            public string GetString(string key, string defaultValue = "") =>
                values.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetString(string key, string value) => values[key] = value;
            public void DeleteKey(string key) => values.Remove(key);
            public void DeleteAll() => values.Clear();
        }
    }
}
