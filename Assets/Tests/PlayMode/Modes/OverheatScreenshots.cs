#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Bugord.App.Localization;
using Bugord.Core.Storage;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
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
    /// Pictures of the OVERHEAT mode for a person to look at, in portrait 1080×1920 and 2340×1080 (touch) and landscape
    /// 1920×1080 (mouse), in English and Russian, into <c>Logs/overheat/</c>: the mode's card over the countdown
    /// (<c>kickoff</c>), a real round with cars at every heat (<c>hud</c>), a car going red-hot (<c>redhot</c>), the player
    /// knocking a red-hot car off (<c>meltdown</c>), the last ten seconds (<c>clock-10</c>), the lobby (<c>lobby</c>), the
    /// result of a win and of a low place (<c>result-won|lost</c>) and the menu with OVERHEAT the event of the day: CHOOSE
    /// MODE with QUICK MATCH chosen (<c>menu</c>), with OVERHEAT chosen (<c>menu-event</c>) and the menu after it
    /// (<c>menu-menu</c>). Explicit: it asserts nothing about the layout and needs graphics.
    /// </summary>
    [Explicit("Writes screenshots of the OVERHEAT mode for a visual check; run it by name.")]
    public sealed class OverheatScreenshots : SimulationSceneFixture
    {
        private const int FramesPerSecond = 60;
        private const float SettleSeconds = 0.8f;
        // RED HOT! springs in and holds: caught while it holds.
        private const float PopMoment = 0.4f;
        // The clock's note springs in for 0.35 s and holds for 1.4 s: caught while it holds.
        private const float NoteMoment = 0.7f;

        private static readonly string[] Languages = { "en", "ru" };

        // Mouse first: once the HUD has seen a finger it keeps its touch buttons.
        private static readonly (int Width, int Height, bool Touch)[] Sizes =
            { (1920, 1080, false), (1080, 1920, true), (2340, 1080, true) };

        // The heat of each car in the picture of the round: cold, warm and red-hot side by side.
        private static readonly float[] Heats = { 0.45f, 0.1f, 0.3f, 0.55f, 0.66f, 0.85f, 1f };

        private static readonly PropertyInfo LastHitByProperty = typeof(Car).GetProperty(nameof(Car.LastHitBy));
        private static readonly PropertyInfo LastHitTimeProperty = typeof(Car).GetProperty(nameof(Car.LastHitTime));
        private static readonly FieldInfo ElapsedField =
            typeof(ArenaRound).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo KickoffTimeProperty =
            typeof(HudViewModel).GetProperty(nameof(HudViewModel.KickoffStepTime));
        private static readonly MethodInfo OnHitMethod =
            typeof(OverheatMode).GetMethod("OnHit", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly string[] Names = { "YOU", "ZOOMER", "BONKY", "SKID", "TURBO TOM", "DRIFTA", "WHEELIE" };
        private static readonly int[] Levels = { 8, 7, 10, 6, 9, 11, 5 };

        private static string Directory_ => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "overheat");

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureTheHud()
        {
            Directory.CreateDirectory(Directory_);
            UseSpecial(SpecialRound.Overheat);
            var tuning = LoadTuning();
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var modes = container.Resolve<RoundModes>();
            var mode = container.Resolve<OverheatMode>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();
            round.Begin();
            // A couple of points before the pictures, so the bar and the cars' heat have something to show.
            yield return KnockOff(round.Cars[1], round.Player);
            yield return KnockOff(round.Cars[4], round.Cars[2]);

            var camera = Camera.main;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            try {
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    var panelSettings = Object.Instantiate(
                        AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                    panelSettings.clearColor = true;
                    panelSettings.colorClearValue = Color.clear;
                    var host = new GameObject("Screenshot HUD");
                    var document = host.AddComponent<UIDocument>();
                    document.panelSettings = panelSettings;
                    var controls = new HudControls();
                    var model = new HudViewModel(null, controls);
                    model.SetPayload(new HudRound(round, null, tuning.Controls, false, 7, false, 0,
                        SpecialRound.Overheat, 7, modes.Active));
                    var view = new HudView(controls, container.Resolve<IAudioService>());
                    view.Bind(model);
                    var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
                    Stretch(hud);
                    var textures = new List<Object> { panelSettings, host };
                    try {
                        document.rootVisualElement.Add(hud);
                        // UIKit hands a view the UXML's own root, where its stylesheet is.
                        view.Attach(hud.hierarchy[0]);
                        view.ShowAsync(CancellationToken.None).Forget();
                        foreach (var (width, height, touch) in Sizes) {
                            if (touch) {
                                controls.BeginTouchSteering(Vector2.zero);
                            }

                            var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            var overlay = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            textures.Add(scene);
                            textures.Add(overlay);
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Overheat screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = overlay;
                            foreach (var car in round.Cars) {
                                yield return WaitIn(car);
                            }

                            SetHeats(round, mode);
                            yield return RealSeconds(SettleSeconds);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}hud_{width}x{height}.png");

                            // A car goes red-hot from the player's hit: sparks, RED HOT! over it, its chip beats.
                            var warm = round.Cars[3];
                            mode.SetHeat(warm, 0.66f);
                            OnHitMethod.Invoke(mode, new object[] {
                                round.IndexOf(warm),
                                new CarHit(round.Player, warm.Position, Vector3.right, 9f, false, false, false),
                            });
                            Assert.That(mode.IsRedHot(round.IndexOf(warm)), Is.True,
                                "The hit left the car short of red-hot.");
                            yield return RealSeconds(PopMoment);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}redhot_{width}x{height}.png");

                            // The player knocks a red-hot car off: MELTDOWN!
                            var hot = round.Cars[6];
                            yield return WaitIn(hot);
                            mode.SetHeat(hot, 1f);
                            yield return KnockOff(hot, round.Player);
                            yield return RealSeconds(0.5f);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}meltdown_{width}x{height}.png");

                            // The last ten seconds: the bar grown, the red frame, FINAL 10!
                            foreach (var car in round.Cars) {
                                yield return WaitIn(car);
                            }

                            SetHeats(round, mode);
                            ElapsedField.SetValue(round, round.Duration - 10.6f);
                            yield return RealSeconds(0.6f + NoteMoment);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}clock-10_{width}x{height}.png");
                            ElapsedField.SetValue(round, 5f);
                            yield return RealSeconds(0.5f);
                        }
                    }
                    finally {
                        camera.targetTexture = null;
                        view.Dispose();
                        foreach (var texture in textures) {
                            if (texture != null) {
                                Object.DestroyImmediate(texture);
                            }
                        }
                    }
                }
            }
            finally {
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Overheat screenshot");
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        /// <summary>The mode's card and its rule over the countdown's 3, before the round begins.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheKickoff()
        {
            Directory.CreateDirectory(Directory_);
            UseSpecial(SpecialRound.Overheat);
            var tuning = LoadTuning();
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var modes = container.Resolve<RoundModes>();
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();

            var camera = Camera.main;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            try {
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    var panelSettings = Object.Instantiate(
                        AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                    panelSettings.clearColor = true;
                    panelSettings.colorClearValue = Color.clear;
                    var host = new GameObject("Screenshot HUD");
                    var document = host.AddComponent<UIDocument>();
                    document.panelSettings = panelSettings;
                    var controls = new HudControls();
                    var model = new HudViewModel(null, controls);
                    model.SetPayload(new HudRound(round, null, tuning.Controls, false, 7, false, 0,
                        SpecialRound.Overheat, 7, modes.Active));
                    var view = new HudView(controls, container.Resolve<IAudioService>());
                    view.Bind(model);
                    var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
                    Stretch(hud);
                    var textures = new List<Object> { panelSettings, host };
                    try {
                        document.rootVisualElement.Add(hud);
                        view.Attach(hud.hierarchy[0]);
                        view.ShowAsync(CancellationToken.None).Forget();
                        foreach (var (width, height, touch) in Sizes) {
                            if (touch) {
                                controls.BeginTouchSteering(Vector2.zero);
                            }

                            var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            var overlay = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            textures.Add(scene);
                            textures.Add(overlay);
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Overheat screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = overlay;
                            yield return RealSeconds(SettleSeconds);
                            model.ShowKickoff(3, tuning.Round.KickoffStep);
                            // Held at the number's settled moment over a few frames: frames take long here.
                            for (var frame = 0; frame < 4; frame++) {
                                KickoffTimeProperty.SetValue(model, Time.unscaledTime - 0.32f);
                                yield return null;
                            }

                            Shoot(camera, scene, overlay, $"{Prefix(language)}kickoff_{width}x{height}.png");
                            model.ShowKickoff(HudViewModel.NoKickoff, 0f);
                        }
                    }
                    finally {
                        camera.targetTexture = null;
                        view.Dispose();
                        foreach (var texture in textures) {
                            if (texture != null) {
                                Object.DestroyImmediate(texture);
                            }
                        }
                    }
                }
            }
            finally {
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Overheat screenshot");
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheResult()
        {
            Directory.CreateDirectory(Directory_);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var arenas = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            var art = AssetDatabase.LoadAssetAtPath<MechanicArt>("Assets/Data/Arenas/data_mechanic_art.asset");

            var podiumHost = new GameObject("Podium screenshot");
            var sun = new GameObject("Podium screenshot light").AddComponent<Light>();
            sun.transform.SetParent(podiumHost.transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var podium = new WinnerPodium(null, art, podiumHost.transform);
            try {
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var name in new[] { "won", "lost" }) {
                        foreach (var (width, height, isTouch) in Sizes) {
                            var (record, storage) = Record(name, tuning, catalog, podium, podiumHost.transform);
                            var panelSettings = Object.Instantiate(
                                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            panelSettings.targetTexture = target;
                            panelSettings.clearColor = true;
                            panelSettings.colorClearValue = new Color(0.56f, 0.83f, 0.78f, 1f);
                            var host = new GameObject("Result screenshot");
                            var document = host.AddComponent<UIDocument>();
                            document.panelSettings = panelSettings;
                            var controls = new HudControls();
                            typeof(HudControls).GetField("isTouchSeen", BindingFlags.Instance | BindingFlags.NonPublic)
                                ?.SetValue(controls, isTouch);
                            var progress = new PlayerProgress(storage, tuning, catalog);
                            var wallet = new PlayerWallet(storage, tuning);
                            DailyGift.ForgetOffer();
                            RoundOverViewModel.ForgetVisit();
                            var model = new RoundOverViewModel(catalog, arenas, tuning, progress, null,
                                new MatchTests.RecordingPlatform(), wallet, null, null);
                            model.SetPayload(record);
                            var view = new RoundOverView(new SilentAudioService(library), controls);
                            var start = Time.realtimeSinceStartup;
                            try {
                                var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                                    "Assets/UI/Screens/RoundOver.uxml").Instantiate();
                                Stretch(screen);
                                document.rootVisualElement.Add(screen);
                                view.Bind(model);
                                view.Attach(screen.hierarchy[0]);
                                view.ShowAsync(CancellationToken.None).Forget();
                                foreach (var moment in new[] { 0.9f, 3.5f }) {
                                    while (Time.realtimeSinceStartup - start < moment) {
                                        yield return null;
                                    }

                                    Save(target, $"{Prefix(language)}result-{name}_{moment:0.0}_{width}x{height}.png");
                                }
                            }
                            finally {
                                view.Dispose();
                                Object.Destroy(host);
                                Object.DestroyImmediate(target);
                                Object.Destroy(panelSettings);
                            }
                        }
                    }
                }
            }
            finally {
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
                podium.Dispose();
                Object.Destroy(podiumHost);
            }
        }

        [UnityTest]
        public IEnumerator CaptureTheLobby()
        {
            Directory.CreateDirectory(Directory_);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var rules = tuning.RulesOf(SpecialRound.Overheat);
            var bots = rules.BotCountFor(6, 6);
            var roster = new SessionRoster(catalog, new PlayerCar(catalog));
            roster.ForRound(bots);
            try {
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var (width, height, _) in Sizes) {
                        var panelSettings = Object.Instantiate(
                            AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                        panelSettings.targetTexture = target;
                        var host = new GameObject("Lobby screenshot");
                        var document = host.AddComponent<UIDocument>();
                        document.panelSettings = panelSettings;
                        var model = new CurtainViewModel();
                        var view = new CurtainView();
                        try {
                            var curtain = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Curtain.uxml")
                                .Instantiate();
                            Stretch(curtain);
                            document.rootVisualElement.Add(curtain);
                            view.Bind(model);
                            view.Attach(curtain);
                            view.ShowAsync(CancellationToken.None).Forget();
                            model.ShowLobby(roster, bots, RoundResult.Nobody, Strings.Get("arena.playroom.name"), false,
                                0, null, SpecialRound.Overheat, rules.Teams, rules.PointsToWin);
                            yield return model.Ready(CancellationToken.None).ToCoroutine();
                            yield return null;
                            yield return null;
                            Save(target, $"{Prefix(language)}lobby_{width}x{height}.png");
                        }
                        finally {
                            view.Dispose();
                            Object.Destroy(host);
                            Object.DestroyImmediate(target);
                            Object.Destroy(panelSettings);
                        }
                    }
                }
            }
            finally {
                roster.Dispose();
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        /// <summary>The menu's own pictures (<see cref="MenuScreenshots"/>) with OVERHEAT the event of the day.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheMenu()
        {
            Directory.CreateDirectory(Directory_);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var capture = typeof(MenuScreenshots).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null, "MenuScreenshots.Capture is gone.");
            try {
                EventSchedule.Forced = EventMode.Overheat;
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var size in Sizes) {
                        yield return (IEnumerator)capture.Invoke(null, new object[] {
                            Directory_, $"{Prefix(language)}menu", size, false, 540, 0, false, 0, 0, true, 0,
                        });
                    }
                }
            }
            finally {
                EventSchedule.Forced = null;
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        private static void SetHeats(ArenaRound round, OverheatMode mode)
        {
            for (var index = 0; index < round.Cars.Count; index++) {
                mode.SetHeat(round.Cars[index], Heats[index % Heats.Length]);
            }
        }

        // An OVERHEAT round as RoundState saves it: every car for itself, places by points.
        private static (RoundRecord record, RoundOverScreenshots.MemoryStorage storage) Record(string name,
            GameTuning tuning, CarCatalog catalog, WinnerPodium podium, Transform podiumHost)
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetString("missions", "round-knockouts:0|knockouts:4|revenge:0");
            storage.SetInt("coins", 240);
            // Past every car of the level path and short of silver: no level's popup covers the result.
            storage.SetInt("xp_total", LevelStart(tuning, 16));
            var points = name == "won" ? new[] { 6, 4, 3, 3, 2, 1, 1 } : new[] { 1, 5, 4, 2, 3, 0, 2 };
            var falls = name == "won" ? new[] { 1, 2, 3, 2, 4, 3, 5 } : new[] { 4, 1, 2, 3, 2, 5, 3 };
            var order = Enumerable.Range(0, points.Length).OrderByDescending(index => points[index])
                .ThenBy(index => falls[index]).ThenBy(index => index).ToList();
            var standings = order.Select((index, at) => new RoundStanding(index, at + 1, points[index], 0, points[index]))
                .ToList();
            var colors = new[] { 0, 1, 2, 3, 4, 5, 6 };
            var carCount = points.Length;
            var place = order.IndexOf(0) + 1;
            var rules = tuning.RulesOf(SpecialRound.Overheat);
            var isWon = place == 1;
            var result = new RoundResult(isWon ? RoundOutcome.Won : RoundOutcome.Survived, place, carCount, 60f, points[0],
                RoundResult.Nobody, FallCause.None, false, false, standings, colors, falls: falls[0],
                knockedOff: falls[0], isLoss: place > rules.TopPlacesOf(carCount), isTimed: true,
                kind: SpecialRound.Overheat, knockoutRewardCap: rules.KnockoutRewardCap);
            var missions = new MissionTracker(storage, tuning);
            missions.Seed(1);
            var mission = missions.Record(new MissionStats(true, result.Outcome, result.Place, result.Knockouts, 3, false, 0,
                0, 0, "sandbox"), 3, 3);
            var xp = new PlayerProgress(storage, tuning, catalog).Record(result, isWon ? tuning.Streaks : null, mission.Xp);
            var coins = new PlayerWallet(storage, tuning).Record(result, isWon, mission.Coins);
            var streak = isWon ? new StreakChange(0, 1, 2) : new StreakChange(1, 0, 2);
            var record = new RoundRecord(result, 1, 2, false, false).WithXp(xp)
                .WithRoster(Names, Levels, RoundResult.Nobody)
                .WithMeta(coins, mission, streak)
                .WithPodium(Podium(catalog, podium, podiumHost, isWon ? "racer" : "suv", colors[result.PodiumCar]));
            return (record, storage);
        }

        // The total XP a player has at the start of a level, from the tuning's level prices (the last one repeats).
        private static int LevelStart(GameTuning tuning, int level)
        {
            var prices = tuning.Progress.LevelXp;
            var total = 0;
            for (var at = 1; at < level; at++) {
                total += prices[Mathf.Min(at - 1, prices.Length - 1)];
            }

            return total;
        }

        // The podium's picture of a catalog car in a colour, built as the arena builds the cars.
        private static RenderTexture Podium(CarCatalog catalog, WinnerPodium podium, Transform podiumHost, string carId,
            int colorId)
        {
            var look = AssetDatabase.LoadAssetAtPath<Car>("Assets/Prefabs/Cars/Car.prefab").GetComponent<CarLook>();
            var entry = catalog.Find(carId);
            var holder = new GameObject("Podium car");
            holder.transform.SetParent(podiumHost, false);
            var model = CarLook.BuildModel(entry, holder.transform, look.ModelRotation, look.ModelMaterial, look.Reference);
            CarModel.PaintBody(CarModel.FindBody(model, out _), entry.BodySwatch, look.SwatchOf(colorId));
            var texture = podium.Show(model, model.transform.localRotation, model.transform.localScale,
                look.ModelMaterial);
            holder.SetActive(false);
            return texture;
        }

        private static string Prefix(string language) => language == "en" ? "" : language + "-";

        private static void Stretch(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
        }

        private static IEnumerator RealSeconds(float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            for (var frame = 0; frame < 6 || Time.realtimeSinceStartup < until; frame++) {
                yield return null;
            }
        }

        // Until the car is back on the floor and solid again after its last fall.
        private static IEnumerator WaitIn(Car car)
        {
            for (var frame = 0; frame < 8 * FramesPerSecond && (car.IsOut || car.IsInvulnerable); frame++) {
                yield return null;
            }
        }

        private static IEnumerator KnockOff(Car car, Car by)
        {
            car.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            LastHitByProperty.SetValue(car, by);
            LastHitTimeProperty.SetValue(car, by != null ? Time.time : -100f);
            for (var frame = 0; frame < 5 * FramesPerSecond && !car.IsOut; frame++) {
                yield return null;
            }

            Assert.That(car.IsOut, Is.True, "The pushed car never fell.");
        }

        private static void Shoot(Camera camera, RenderTexture scene, RenderTexture overlay, string file)
        {
            camera.Render();
            var below = Read(scene);
            var above = Read(overlay);
            var a = above.GetPixels32();
            var b = below.GetPixels32();
            for (var i = 0; i < b.Length; i++) {
                var alpha = a[i].a / 255f;
                b[i] = new Color32((byte)Mathf.RoundToInt(a[i].r * alpha + b[i].r * (1f - alpha)),
                    (byte)Mathf.RoundToInt(a[i].g * alpha + b[i].g * (1f - alpha)),
                    (byte)Mathf.RoundToInt(a[i].b * alpha + b[i].b * (1f - alpha)), 255);
            }

            below.SetPixels32(b);
            var path = Path.Combine(Directory_, file);
            File.WriteAllBytes(path, below.EncodeToPNG());
            Object.DestroyImmediate(below);
            Object.DestroyImmediate(above);
            Debug.Log($"Overheat screenshot: {path}");
        }

        private static void Save(RenderTexture source, string file)
        {
            var texture = Read(source);
            var path = Path.Combine(Directory_, file);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Overheat screenshot: {path}");
        }

        private static Texture2D Read(RenderTexture source)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        // The player in the first car, green, at a middling level.
        private sealed class PlayerCar : IPlayerCar
        {
            public PlayerCar(CarCatalog catalog) => SelectedCar = catalog.Cars[0].Id;

            public string SelectedCar { get; }
            public int SelectedColor => 0;
            public int Level => 8;
            public bool SelectColor(int id) => false;
            public event System.Action<int> ColorChanged { add { } remove { } }
        }
    }
}
#endif
