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
    /// Pictures of the GOLD RUSH mode for a person to look at, in landscape 1920×1080 (mouse) and portrait 1080×1920 and
    /// 2340×1080 (touch), in English and Russian (the result also in Spanish, for its coins column), into
    /// <c>Logs/gold-rush/</c>: the mode's card over the countdown (<c>kickoff</c>), a real round with coins on the floor and
    /// every car carrying some (<c>hud</c>), the player taking a coin (<c>take</c>), the player bumping coins out of the
    /// leader (<c>shake</c>), a bot bumping coins out of the player (<c>lost</c>), the last ten seconds (<c>clock-10</c>),
    /// the lobby (<c>lobby</c>), the result of a win and of a low place (<c>result-won|lost</c>) and the menu with GOLD
    /// RUSH the event of the day (<c>menu*</c>). Explicit: it asserts nothing about the layout and needs graphics.
    /// </summary>
    [Explicit("Writes screenshots of the GOLD RUSH mode for a visual check; run it by name.")]
    public sealed class GoldRushScreenshots : SimulationSceneFixture
    {
        private const int FramesPerSecond = 60;
        private const float SettleSeconds = 1f;
        // A coin flies, the pops spring in and hold: caught while they hold.
        private const float PopMoment = 0.3f;
        // The clock's note springs in for 0.35 s and holds for 1.4 s: caught while it holds.
        private const float NoteMoment = 0.7f;

        private static readonly string[] Languages = { "en", "ru" };

        // Mouse first: once the HUD has seen a finger it keeps its touch buttons.
        private static readonly (int Width, int Height, bool Touch)[] Sizes =
            { (1920, 1080, false), (1080, 1920, true), (2340, 1080, true) };

        // The coins each car carries in the pictures of the round: the third car leads and wears the pile.
        private static readonly int[] Coins = { 7, 3, 4, 12, 1, 5, 2 };

        private static readonly FieldInfo ElapsedField =
            typeof(ArenaRound).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo KickoffTimeProperty =
            typeof(HudViewModel).GetProperty(nameof(HudViewModel.KickoffStepTime));
        private static readonly MethodInfo OnHitMethod =
            typeof(GoldRushMode).GetMethod("OnHit", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly string[] Names = { "YOU", "ZOOMER", "BONKY", "SKID", "TURBO TOM", "DRIFTA", "WHEELIE" };
        private static readonly int[] Levels = { 8, 7, 10, 6, 9, 11, 5 };

        private static string Directory_ => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "gold-rush");

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureTheHud()
        {
            Directory.CreateDirectory(Directory_);
            UseSpecial(SpecialRound.GoldRush);
            var tuning = LoadTuning();
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var modes = container.Resolve<RoundModes>();
            var mode = container.Resolve<GoldRushMode>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            // Game time as real time: the fixture's fixed step runs the round out in the real-time waits of an
            // uncapped editor (the fixture puts the step back).
            Time.captureDeltaTime = 0f;
            round.Place();
            round.Begin();
            // The opening coins land and the first burst falls before the pictures.
            yield return RealSeconds(mode.Settings.FirstDelay + mode.Settings.BurstInterval + 1f);

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
                        SpecialRound.GoldRush, 7, modes.Active));
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
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Gold rush screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = overlay;
                            SetCoins(round, mode);
                            yield return RealSeconds(SettleSeconds);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}hud_{width}x{height}.png");

                            // The player drives over a coin: it flies into the car, the counter under it jumps.
                            var player = round.Player;
                            mode.PlaceCoin(Flat(player.Position) + player.transform.right * 1.2f);
                            yield return RealSeconds(PopMoment);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}take_{width}x{height}.png");

                            // The player bumps the leader: its coins burst out, a red -N over it.
                            var leader = round.Cars[3];
                            Bump(mode, round, leader, player);
                            yield return RealSeconds(PopMoment);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}shake_{width}x{height}.png");

                            // A bot bumps the player: the player's own coins fly.
                            Bump(mode, round, player, round.Cars[2]);
                            yield return RealSeconds(PopMoment);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}lost_{width}x{height}.png");

                            // The last ten seconds: the bar grown, the red frame, FINAL 10!
                            SetCoins(round, mode);
                            ElapsedField.SetValue(round, round.Duration - 10.6f);
                            yield return RealSeconds(0.6f + NoteMoment);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}clock-10_{width}x{height}.png");
                            ElapsedField.SetValue(round, 8f);
                            yield return RealSeconds(1.5f);
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
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Gold rush screenshot");
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
            UseSpecial(SpecialRound.GoldRush);
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
                        SpecialRound.GoldRush, 7, modes.Active));
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
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Gold rush screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = overlay;
                            yield return RealSeconds(0.8f);
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
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Gold rush screenshot");
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
                foreach (var language in new[] { "en", "ru", "es" }) {
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
            var rules = tuning.RulesOf(SpecialRound.GoldRush);
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
                            model.ShowLobby(roster, bots, RoundResult.Nobody, Strings.Get("arena.candy.name"), false,
                                0, null, SpecialRound.GoldRush, rules.Teams);
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

        /// <summary>The menu's own pictures (<see cref="MenuScreenshots"/>) with GOLD RUSH the event of the day.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheMenu()
        {
            Directory.CreateDirectory(Directory_);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var capture = typeof(MenuScreenshots).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null, "MenuScreenshots.Capture is gone.");
            try {
                EventSchedule.Forced = EventMode.GoldRush;
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

        // Every car to its picture's coins, whatever it carried before.
        private static void SetCoins(ArenaRound round, GoldRushMode mode)
        {
            for (var index = 0; index < round.Cars.Count; index++) {
                var car = round.Cars[index];
                var change = Coins[index % Coins.Length] - mode.CoinsOf(car);
                if (change > 0) {
                    mode.GiveCoins(car, change);
                }
                else if (change < 0) {
                    round.AddPoints(car, change);
                }
            }
        }

        // A hard bump of the victim by the other car, as the victim's Car.Hit reports it.
        private static void Bump(GoldRushMode mode, ArenaRound round, Car victim, Car by)
        {
            var normal = Flat(victim.Position - by.Position).normalized;
            OnHitMethod.Invoke(mode, new object[] {
                round.IndexOf(victim),
                new CarHit(by, victim.Position, normal, 10f, false, false, false),
            });
        }

        // A GOLD RUSH round as RoundState saves it: every car for itself, places by the coins carried at the end.
        private static (RoundRecord record, RoundOverScreenshots.MemoryStorage storage) Record(string name,
            GameTuning tuning, CarCatalog catalog, WinnerPodium podium, Transform podiumHost)
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetString("missions", "round-knockouts:0|knockouts:4|revenge:0");
            storage.SetInt("coins", 240);
            // Past every car of the level path and short of silver: no level's popup covers the result.
            storage.SetInt("xp_total", LevelStart(tuning, 16));
            var points = name == "won" ? new[] { 24, 17, 15, 11, 9, 6, 3 } : new[] { 5, 21, 16, 12, 8, 3, 9 };
            var falls = name == "won" ? new[] { 1, 2, 3, 2, 4, 3, 5 } : new[] { 4, 1, 2, 3, 2, 5, 3 };
            var order = Enumerable.Range(0, points.Length).OrderByDescending(index => points[index])
                .ThenBy(index => falls[index]).ThenBy(index => index).ToList();
            var standings = order.Select((index, at) => new RoundStanding(index, at + 1, 1, 0, points[index])).ToList();
            var colors = new[] { 0, 1, 2, 3, 4, 5, 6 };
            var carCount = points.Length;
            var place = order.IndexOf(0) + 1;
            var rules = tuning.RulesOf(SpecialRound.GoldRush);
            var isWon = place == 1;
            var result = new RoundResult(isWon ? RoundOutcome.Won : RoundOutcome.Survived, place, carCount, 60f, 1,
                RoundResult.Nobody, FallCause.None, false, false, standings, colors, falls: falls[0],
                knockedOff: falls[0], isLoss: place > rules.TopPlacesOf(carCount), isTimed: true,
                kind: SpecialRound.GoldRush, knockoutRewardCap: rules.KnockoutRewardCap);
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
                .WithPodium(Podium(catalog, podium, podiumHost, isWon ? "racer" : "truck", colors[result.PodiumCar]));
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

        private static Vector3 Flat(Vector3 value) => new(value.x, 0f, value.z);

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
            Debug.Log($"Gold rush screenshot: {path}");
        }

        private static void Save(RenderTexture source, string file)
        {
            var texture = Read(source);
            var path = Path.Combine(Directory_, file);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Gold rush screenshot: {path}");
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
