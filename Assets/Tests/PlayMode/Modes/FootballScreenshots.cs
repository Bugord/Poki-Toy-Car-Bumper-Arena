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
    /// Pictures of the FOOTBALL mode for a person to look at, into <c>Logs/football/</c>, in English and Russian
    /// (<c>ru-</c>), in portrait 1080×1920 and on its side 2340×1080 (touch) and landscape 1920×1080 (mouse): the kick-off
    /// with the mode's card over the countdown and the arrow to the red goal (<c>kickoff</c>), play on the pitch
    /// (<c>play</c>), the ball's pointer when the ball is off the screen (<c>pointer</c>), a goal of blue and of red
    /// (<c>goal-blue</c>, <c>goal-red</c>), the last ten seconds (<c>clock-10</c>), the lobby (<c>lobby-search</c>,
    /// <c>lobby</c>), the result of a won, a lost and a level match (<c>result-won|lost|draw</c>, also 2048×1536) and the
    /// menu with FOOTBALL the event of the day, its card picked and not (<c>menu*</c>). Explicit: it asserts nothing about
    /// the layout and needs graphics.
    /// </summary>
    [Explicit("Writes screenshots of the FOOTBALL mode for a visual check; run it by name.")]
    public sealed class FootballScreenshots : SimulationSceneFixture
    {
        private const float SettleSeconds = 0.8f;
        // GOAL! pops in for 0.28 s and holds 1.25 s: caught while it holds.
        private const float GoalMoment = 0.6f;
        // The clock's note springs in for 0.35 s and holds for 1.4 s: caught while it holds.
        private const float NoteMoment = 0.7f;

        private static readonly string[] Languages = { "en", "ru" };

        // Mouse first: once the HUD has seen a finger it keeps its touch buttons.
        private static readonly (int Width, int Height, bool Touch)[] Sizes =
            { (1920, 1080, false), (1080, 1920, true), (2340, 1080, true) };

        private static readonly FieldInfo ElapsedField =
            typeof(ArenaRound).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo KickoffTimeProperty =
            typeof(HudViewModel).GetProperty(nameof(HudViewModel.KickoffStepTime));
        private static readonly FieldInfo IsDrivingField =
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly string[] Names = { "YOU", "ZOOMER", "BONKY", "SKID", "TURBO TOM", "DRIFTA" };
        private static readonly int[] Levels = { 8, 7, 10, 6, 9, 11 };

        private static string Directory_ => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "football");

        /// <summary>The kick-off before the round begins: the card over the countdown's 3 and the arrow to the red goal.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheKickoff()
        {
            Directory.CreateDirectory(Directory_);
            UseSpecial(SpecialRound.Football);
            var tuning = LoadTuning();
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var modes = container.Resolve<RoundModes>();
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();
            yield return null;
            yield return Hud(container, round, modes, tuning, (model, camera, scene, overlay, language, width, height) =>
                Kickoff(model, tuning, camera, scene, overlay, language, width, height));
        }

        /// <summary>Play, the ball's pointer, a goal of each team and the last ten seconds, in a real match.</summary>
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureThePlay()
        {
            Directory.CreateDirectory(Directory_);
            UseSpecial(SpecialRound.Football);
            var tuning = LoadTuning();
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var modes = container.Resolve<RoundModes>();
            var mode = container.Resolve<FootballMode>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();
            round.Begin();
            // The bots stand still but for the play picture: nothing else moves between pictures.
            IsDrivingField.SetValue(botDriver, true);
            yield return null;
            // A goal each before the pictures, so the score reads 1 : 1 and up.
            yield return Shoot(round, mode, round.Player, RoundTeams.Red);
            yield return WaitForPlay(mode);
            yield return Shoot(round, mode, round.Cars[1], RoundTeams.Blue);
            yield return WaitForPlay(mode);
            yield return Hud(container, round, modes, tuning, (model, camera, scene, overlay, language, width, height) =>
                Play(round, mode, botDriver, camera, scene, overlay, language, width, height));
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
            var sizes = new[] { (1080, 1920, true), (1920, 1080, false), (2340, 1080, true), (2048, 1536, false) };
            try {
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var name in new[] { "won", "lost", "draw" }) {
                        foreach (var (width, height, isTouch) in sizes) {
                            var (record, storage) = Record(name, tuning, catalog, podium, podiumHost.transform);
                            var panelSettings = Object.Instantiate(
                                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            panelSettings.targetTexture = target;
                            panelSettings.clearColor = true;
                            panelSettings.colorClearValue = new Color(0.47f, 0.74f, 0.4f, 1f);
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
            var rules = tuning.RulesOf(SpecialRound.Football);
            var bots = rules.BotCountFor(rules.Bots, 6);
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
                            model.ShowLobby(roster, bots, RoundResult.Nobody,
                                Strings.Get($"arena.{GameTuning.FootballSettings.PitchId}.name"), false, 0, null,
                                SpecialRound.Football, rules.Teams);
                            while (Time.realtimeSinceStartupAsDouble - model.StartedAt < 0.7f) {
                                yield return null;
                            }

                            Save(target, $"{Prefix(language)}lobby-search_{width}x{height}.png");
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

        /// <summary>The menu's own pictures (<see cref="MenuScreenshots"/>) with FOOTBALL the event of the day: CHOOSE MODE
        /// with its card not picked (<c>menu</c>) and picked (<c>menu-event</c>) and the menu after (<c>menu-menu</c>).</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheMenu()
        {
            Directory.CreateDirectory(Directory_);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var capture = typeof(MenuScreenshots).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null, "MenuScreenshots.Capture is gone.");
            try {
                EventSchedule.Forced = EventMode.Football;
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var size in Sizes.Select(size => (size.Width, size.Height, size.Touch))) {
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

        private delegate IEnumerator Shots(HudViewModel model, Camera camera, RenderTexture scene, RenderTexture overlay,
            string language, int width, int height);

        // A real HUD over the real scene for each language and size, each picture the scene under the HUD's own panel.
        private static IEnumerator Hud(IObjectResolver container, ArenaRound round, RoundModes modes, GameTuning tuning,
            Shots shots)
        {
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
                        SpecialRound.Football, 7, modes.Active));
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
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Football screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = overlay;
                            yield return RealSeconds(SettleSeconds);
                            yield return shots(model, camera, scene, overlay, language, width, height);
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
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Football screenshot");
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        private static IEnumerator Kickoff(HudViewModel model, GameTuning tuning, Camera camera, RenderTexture scene,
            RenderTexture overlay, string language, int width, int height)
        {
            model.ShowKickoff(3, tuning.Round.KickoffStep);
            // Held at the number's settled moment over a few frames: frames take long here.
            for (var frame = 0; frame < 4; frame++) {
                KickoffTimeProperty.SetValue(model, Time.unscaledTime - 0.32f);
                yield return null;
            }

            Shoot(camera, scene, overlay, $"{Prefix(language)}kickoff_{width}x{height}.png");
            model.ShowKickoff(HudViewModel.NoKickoff, 0f);
            yield return RealSeconds(0.3f);
            Shoot(camera, scene, overlay, $"{Prefix(language)}kickoff-arrow_{width}x{height}.png");
        }

        private static IEnumerator Play(ArenaRound round, FootballMode mode, BotDriver botDriver, Camera camera,
            RenderTexture scene, RenderTexture overlay, string language, int width, int height)
        {
            // A few seconds of the bots' own football; the player stands where it is.
            IsDrivingField.SetValue(botDriver, false);
            yield return GameSeconds(2.5f);
            IsDrivingField.SetValue(botDriver, true);
            Shoot(camera, scene, overlay, $"{Prefix(language)}play_{width}x{height}.png");

            // The ball far off in red's corner, the player by its own goal: the pointer at the screen's edge.
            round.Player.Movement.Place(new Vector3(-2f, 0.6f, -12f), Quaternion.identity);
            var body = mode.Ball.Body;
            body.position = new Vector3(8f, mode.Settings.BallRadius, 13f);
            body.linearVelocity = Vector3.zero;
            yield return RealSeconds(0.6f);
            Shoot(camera, scene, overlay, $"{Prefix(language)}pointer_{width}x{height}.png");

            // Blue scores: GOAL! with the player's YOU.
            yield return Shoot(round, mode, round.Player, RoundTeams.Red);
            yield return RealSeconds(GoalMoment);
            Shoot(camera, scene, overlay, $"{Prefix(language)}goal-blue_{width}x{height}.png");
            yield return WaitForPlay(mode);

            // Red scores.
            yield return Shoot(round, mode, round.Cars[3], RoundTeams.Blue);
            yield return RealSeconds(GoalMoment);
            Shoot(camera, scene, overlay, $"{Prefix(language)}goal-red_{width}x{height}.png");
            yield return WaitForPlay(mode);

            // The last ten seconds: FINAL 10 under the team score, the bar grown and the red frame.
            ElapsedField.SetValue(round, round.Duration - 10.6f);
            yield return RealSeconds(0.6f + NoteMoment);
            Shoot(camera, scene, overlay, $"{Prefix(language)}clock-10_{width}x{height}.png");
            ElapsedField.SetValue(round, 5f);
            yield return RealSeconds(0.5f);
        }

        private static IEnumerator Shoot(ArenaRound round, FootballMode mode, Car toucher, int goal)
        {
            var body = mode.Ball.Body;
            var end = FootballPitch.EndOf(goal);
            // A car the bots' play left in the goal mouth would stop the staged shot: it waits by the side line.
            var settings = mode.Settings;
            foreach (var car in round.Cars) {
                var at = car.Position;
                if (at.z * end > settings.GoalLine - 5f && Mathf.Abs(at.x) < settings.GoalHalfWidth + 2f) {
                    var side = at.x < 0f ? -1f : 1f;
                    car.Movement.Place(new Vector3(side * (settings.GoalHalfWidth + 4f), 0.6f,
                        end * (settings.GoalLine - 6f - round.IndexOf(car))), car.transform.rotation);
                }
            }

            mode.Ball.Forget();
            if (toucher != null) {
                mode.Ball.Touch(toucher);
            }

            body.position = new Vector3(0f, mode.Settings.BallRadius, end * (mode.Settings.GoalLine - 0.5f));
            body.linearVelocity = new Vector3(0f, 0f, end * 8f);
            var goals = mode.Goals;
            var timeout = Time.time + 1.5f;
            while (mode.Goals == goals && Time.time < timeout) {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(mode.Goals, Is.GreaterThan(goals), "The shot did not score.");
        }

        private static IEnumerator WaitForPlay(FootballMode mode)
        {
            var timeout = Time.time + mode.Settings.CelebrateTime + mode.Settings.KickoffWait + 4f;
            while (mode.State != FootballMode.Phase.Play && Time.time < timeout) {
                yield return null;
            }
        }

        // A FOOTBALL match as RoundState saves it, the player blue at index 0, the teams by turns; points are goals.
        private static (RoundRecord record, RoundOverScreenshots.MemoryStorage storage) Record(string name,
            GameTuning tuning, CarCatalog catalog, WinnerPodium podium, Transform podiumHost)
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetString("missions", "round-knockouts:0|knockouts:4|revenge:0");
            storage.SetInt("coins", 240);
            // Past every car of the level path and short of silver: no level's popup covers the result.
            storage.SetInt("xp_total", LevelStart(tuning, 16));
            var goals = name switch {
                "won" => new[] { 2, 1, 1, 0, 0, 0 },
                "lost" => new[] { 1, 2, 0, 1, 0, 0 },
                _ => new[] { 1, 1, 1, 0, 0, 0 },
            };
            // Own goals: the other team's alone.
            var own = name switch { "draw" => new[] { 0, 1 }, _ => new[] { 0, 0 } };
            var teamPoints = new int[2];
            for (var index = 0; index < goals.Length; index++) {
                teamPoints[index % 2] += goals[index];
            }

            teamPoints[0] += own[0];
            teamPoints[1] += own[1];
            var order = Enumerable.Range(0, goals.Length).OrderByDescending(index => goals[index]).ThenBy(index => index)
                .ToList();
            var standings = order.Select((index, at) => new RoundStanding(index, at + 1, 0, 0, goals[index],
                RoundTeams.TeamOf(index, 2))).ToList();
            var colors = Enumerable.Range(0, goals.Length).Select(index => RoundTeams.PaintOf(RoundTeams.TeamOf(index, 2)))
                .ToArray();
            var isDraw = teamPoints[0] == teamPoints[1];
            var isWon = teamPoints[0] > teamPoints[1];
            var carCount = goals.Length;
            var place = isWon ? 1 : isDraw ? Mathf.Max(1, carCount / 2) : carCount;
            var rules = tuning.RulesOf(SpecialRound.Football);
            var result = new RoundResult(isWon ? RoundOutcome.Won : RoundOutcome.Survived, place, carCount,
                rules.Duration, 0, RoundResult.Nobody, FallCause.None, false, false, standings, colors,
                isLoss: !isWon && !isDraw, bonusKnockouts: goals[0], isTimed: true, kind: SpecialRound.Football,
                knockoutRewardCap: rules.KnockoutRewardCap, teams: 2, teamPoints: teamPoints);
            var missions = new MissionTracker(storage, tuning);
            missions.Seed(1);
            var mission = missions.Record(new MissionStats(true, result.Outcome, result.Place, result.Knockouts, 3, false, 0,
                0, 0, GameTuning.FootballSettings.PitchId), 3, 3);
            var xp = new PlayerProgress(storage, tuning, catalog).Record(result, isWon ? tuning.Streaks : null, mission.Xp);
            var coins = new PlayerWallet(storage, tuning).Record(result, isWon, mission.Coins);
            var streak = isWon ? new StreakChange(0, 1, 2) : isDraw ? new StreakChange(1, 1, 2) : new StreakChange(0, 0, 2);
            var record = new RoundRecord(result, 1, 2, false, false).WithXp(xp)
                .WithRoster(Names, Levels, RoundResult.Nobody)
                .WithMeta(coins, mission, streak)
                .WithPodium(Podium(catalog, podium, podiumHost, "suv", colors[result.PodiumCar]));
            return (record, storage);
        }

        private static int LevelStart(GameTuning tuning, int level)
        {
            var prices = tuning.Progress.LevelXp;
            var total = 0;
            for (var at = 1; at < level; at++) {
                total += prices[Mathf.Min(at - 1, prices.Length - 1)];
            }

            return total;
        }

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

        private static IEnumerator GameSeconds(float seconds)
        {
            var until = Time.time + seconds;
            while (Time.time < until) {
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
            Debug.Log($"Football screenshot: {path}");
        }

        private static void Save(RenderTexture source, string file)
        {
            var texture = Read(source);
            var path = Path.Combine(Directory_, file);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Football screenshot: {path}");
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
