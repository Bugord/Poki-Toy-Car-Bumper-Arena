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
    /// modes popups, the XP boost's offer and the boost running, the daily gift (day 3, and day 7 with the mystery hat:
    /// the menu with DAILY ready, its popup and the gift just claimed), and once in Russian; then in every league (its sky, pedestal and badge) with the leagues list
    /// over it, and the NEW LEAGUE! popup: <c>Logs/menu/&lt;case&gt;_&lt;w&gt;x&lt;h&gt;.png</c>. Explicit: it is for
    /// looking at, and needs graphics.
    /// </summary>
    [Explicit]
    public sealed class MenuScreenshots
    {
        private static readonly (int Width, int Height, bool IsTouch)[] Sizes = {
            (1080, 1920, true), (1080, 2340, true), (2340, 1080, true),
            (1920, 1080, false), (2048, 1536, false),
        };

        // A level in each league (rookie, bronze, silver, gold, legend), partway into it, as the league boards show.
        private static readonly (string Id, int Level)[] Leagues = {
            ("rookie", 7), ("bronze", 15), ("silver", 31), ("gold", 62), ("legend", 120),
        };

        private static readonly (int Width, int Height, bool IsTouch)[] LeagueSizes = {
            (1080, 1920, true), (1080, 2340, true), (2340, 1080, true), (1920, 1080, false),
        };

        [UnityTest]
        public IEnumerator CaptureTheLeagues()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "menu");
            Directory.CreateDirectory(directory);
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            foreach (var size in LeagueSizes) {
                for (var tier = 0; tier < Leagues.Length; tier++) {
                    var (id, level) = Leagues[tier];
                    var xp = LevelStart(tuning, level) + LevelPrice(tuning, level) / 2;
                    // Seen already: the menu as it stays in the league, then its leagues list.
                    yield return Capture(directory, $"league-{id}", size, false, xp, tier, true);
                    if (tier is 2 or 4) {
                        // One league short: the first menu in it greets it with NEW LEAGUE!.
                        yield return Capture(directory, $"newleague-{id}", size, false, xp, tier - 1, false);
                    }
                }
            }

            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        [UnityTest]
        public IEnumerator CaptureTheMenu()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "menu");
            Directory.CreateDirectory(directory);
            foreach (var size in Sizes) {
                yield return Capture(directory, "menu", size, true);
                // The XP boost running: its tile blue with the matches left, ×2 XP on the bar.
                yield return Capture(directory, "boosted", size, false, boost: 3);
                // The daily gift on day 3 (coins, CLAIM ×2 beside CLAIM) and day 7 (the mystery hat).
                yield return Capture(directory, "daily", size, false, dailyStreak: 2);
                yield return Capture(directory, "daily7", size, false, dailyStreak: 6);
                // The day's tasks: under way with the first win still to come, then one done and the first win taken.
                yield return Capture(directory, "tasks", size, false, tasks: 1);
                yield return Capture(directory, "tasks-done", size, false, tasks: 2);
            }

            Strings.SetLanguage("ru");
            yield return Capture(directory, "ru", (1080, 1920, true), true);
            yield return Capture(directory, "ru", (1920, 1080, false), false);
            yield return Capture(directory, "ru-boosted", (1080, 1920, true), false, boost: 3);
            yield return Capture(directory, "ru-daily", (1080, 1920, true), false, dailyStreak: 2);
            yield return Capture(directory, "ru-daily", (1920, 1080, false), false, dailyStreak: 2);
            yield return Capture(directory, "ru-tasks", (1080, 1920, true), false, tasks: 2);
            yield return Capture(directory, "ru-tasks", (1920, 1080, false), false, tasks: 2);
            Strings.SetLanguage("en");
            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        /// <summary>
        /// CHOOSE MODE at every size, as it opens on QUICK MATCH and with the event of the day (HOT POTATO, as the build
        /// ships) picked, and the menu after it with the MODE plate on the event; also in Russian.
        /// <c>Logs/menu/modes*_&lt;w&gt;x&lt;h&gt;.png</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureTheModes()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "menu");
            Directory.CreateDirectory(directory);
            foreach (var size in Sizes) {
                yield return Capture(directory, "modes", size, false, modes: true);
            }

            Strings.SetLanguage("ru");
            yield return Capture(directory, "ru-modes", (1080, 1920, true), false, modes: true);
            yield return Capture(directory, "ru-modes", (1920, 1080, false), false, modes: true);
            Strings.SetLanguage("en");
            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        /// <summary>
        /// The menu and CHOOSE MODE with other modes of the day (stubs, forced as the dev console's event_day does):
        /// their colours, card pictures, names, rules (GOLD RUSH's the longest) and the clock on the card and the tile, in
        /// English, Russian and French. <c>Logs/menu/event-&lt;id&gt;*_&lt;w&gt;x&lt;h&gt;.png</c>, <c>ru-event-&lt;id&gt;*</c>
        /// and <c>fr-event-&lt;id&gt;*</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureTheEventsOfTheDay()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "menu");
            Directory.CreateDirectory(directory);
            try {
                foreach (var mode in new[] { EventMode.Crown, EventMode.GoldRush, EventMode.Football }) {
                    EventSchedule.Forced = mode;
                    var id = EventModes.IdOf(mode);
                    foreach (var language in new[] { "en", "ru", "fr" }) {
                        Strings.SetLanguage(language);
                        var prefix = language == "en" ? "" : language + "-";
                        yield return Capture(directory, $"{prefix}event-{id}", (1080, 1920, true), false, modes: true);
                        yield return Capture(directory, $"{prefix}event-{id}", (1920, 1080, false), false, modes: true);
                    }
                }
            }
            finally {
                EventSchedule.Forced = null;
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        private static IEnumerator Capture(string directory, string name, (int Width, int Height, bool IsTouch) size,
            bool withPopups, int xp = 540, int leagueSeen = 0, bool withLeagues = false, int boost = 0,
            int dailyStreak = 0, bool modes = false, int tasks = 0)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var arenas = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var (width, height, isTouch) = size;
            var storage = new RoundOverScreenshots.MemoryStorage();
            // Level 7 with the garage last seen at level 5: the SUV is new to the garage.
            storage.SetInt("xp_total", xp);
            storage.SetInt("league_seen", leagueSeen);
            storage.SetInt("garage_seen_level", 5);
            storage.SetString("cars_owned", "speedster");
            storage.SetString("car_selected", "speedster");
            storage.SetInt("car_color", 2);
            storage.SetInt("coins", 260);
            storage.SetString("toppers_owned", "party-hat");
            storage.SetString("topper_selected", "party-hat");
            storage.SetInt("xp_boost", boost);
            // The daily gift taken today (the tile shows the time to the next), or yesterday after this many days in a
            // row: today's waits. The menu does not open it by itself here; the daily cases open it by hand.
            var wallet = new PlayerWallet(storage, tuning);
            var daily = new PlayerDaily(storage, tuning, wallet);
            daily.SetLastClaim(System.DateTime.Now, dailyStreak > 0 ? 1 : 0, dailyStreak > 0 ? dailyStreak : 3);
            typeof(MenuViewModel).GetField("isDailyOffered", BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, true);
            var missions = new MissionTracker(storage, tuning);
            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            panelSettings.targetTexture = target;
            var host = new GameObject("Menu screenshot");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var progress = new PlayerProgress(storage, tuning, catalog);
            var dailyTasks = new DailyTasks(storage, tuning, progress);
            if (tasks > 0) {
                // A lost round with two KOs and a box, and for the second case a won one with more: the day's tasks
                // part-way, one of them done.
                var now = System.DateTime.Now;
                dailyTasks.Record(new MissionStats(true, RoundOutcome.Out, 3, 2, 0, false, 1, 1, 0, "sandbox"), now);
                if (tasks > 1) {
                    dailyTasks.Record(new MissionStats(true, RoundOutcome.Won, 1, 4, 0, true, 4, 4, 1, "tabletop"),
                        now);
                    daily.SetFirstWin(now, true);
                }
            }

            var model = new MenuViewModel(progress, wallet, missions, catalog, arenas, tuning, null,
                new MatchTests.RecordingPlatform(), null, daily, dailyTasks);
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
                if (modes) {
                    typeof(MenuView).GetMethod("OpenModes", Private)?.Invoke(view, null);
                    yield return Wait(0.3f);
                    Save(target, Path.Combine(directory, $"{name}_{width}x{height}.png"));
                    model.SelectMode(model.TodayEvent);
                    typeof(MenuView).GetMethod("ShowMode", Private)?.Invoke(view, null);
                    yield return Wait(0.3f);
                    Save(target, Path.Combine(directory, $"{name}-event_{width}x{height}.png"));
                    typeof(MenuView).GetMethod("ClosePopups", Private)?.Invoke(view, null);
                    yield return Wait(0.3f);
                    Save(target, Path.Combine(directory, $"{name}-menu_{width}x{height}.png"));
                }

                if (withPopups) {
                    foreach (var popup in new[] { "OpenMissions", "OpenModes", "OpenBoost" }) {
                        typeof(MenuView).GetMethod(popup, Private)?.Invoke(view, null);
                        yield return Wait(0.3f);
                        var shot = popup switch { "OpenMissions" => "missions", "OpenModes" => "modes", _ => "boost" };
                        Save(target, Path.Combine(directory,
                            $"{(name == "menu" ? "" : name + "-")}{shot}_{width}x{height}.png"));
                        typeof(MenuView).GetMethod("ClosePopups", Private)?.Invoke(view, null);
                    }
                }

                if (dailyStreak > 0) {
                    typeof(MenuView).GetMethod("OpenDaily", Private)?.Invoke(view, null);
                    yield return Wait(0.4f);
                    Save(target, Path.Combine(directory, $"{name}-popup_{width}x{height}.png"));
                    typeof(MenuView).GetMethod("PressDailyClaim", Private)?.Invoke(view, null);
                    yield return Wait(0.8f);
                    Save(target, Path.Combine(directory, $"{name}-claimed_{width}x{height}.png"));
                    yield return Wait(1.2f);
                    Save(target, Path.Combine(directory, $"{name}-after_{width}x{height}.png"));
                    typeof(MenuView).GetMethod("ClosePopups", Private)?.Invoke(view, null);
                }

                if (tasks > 0) {
                    // The gift is taken today: DAILY opens on the tasks.
                    typeof(MenuView).GetMethod("OpenDaily", Private)?.Invoke(view, null);
                    yield return Wait(0.4f);
                    Save(target, Path.Combine(directory, $"{name}-popup_{width}x{height}.png"));
                    typeof(MenuView).GetMethod("ClosePopups", Private)?.Invoke(view, null);
                }

                if (withLeagues) {
                    typeof(MenuView).GetMethod("OpenLeagues", Private)?.Invoke(view, null);
                    yield return Wait(0.4f);
                    Save(target, Path.Combine(directory, $"{name.Replace("league-", "ladder-")}_{width}x{height}.png"));
                    typeof(MenuView).GetMethod("ClosePopups", Private)?.Invoke(view, null);
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

        private static int LevelStart(GameTuning tuning, int level)
        {
            var total = 0;
            for (var at = 1; at < level; at++) {
                total += LevelPrice(tuning, at);
            }

            return total;
        }

        private static int LevelPrice(GameTuning tuning, int level)
        {
            var prices = tuning.Progress.LevelXp;
            return prices[Mathf.Min(level - 1, prices.Length - 1)];
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
