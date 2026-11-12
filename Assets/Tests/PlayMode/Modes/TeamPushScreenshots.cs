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
    /// Pictures of the TEAM PUSH mode for a person to look at, in portrait 1080×1920 (touch) and landscape 1920×1080
    /// (mouse), in English and Russian, into <c>Logs/team-push/</c>: the HUD in a real round with the team score and a
    /// point's sticker in flight (<c>hud</c>, <c>point</c>), the result of a won, a lost and a level round
    /// (<c>result-won|lost|draw</c>), the lobby with the two teams (<c>lobby</c>) and the menu with TEAM PUSH as the event
    /// of the day (<c>menu</c>, <c>modes</c>). Explicit: it asserts nothing about the layout and needs graphics.
    /// </summary>
    [Explicit("Writes screenshots of the TEAM PUSH mode for a visual check; run it by name.")]
    public sealed class TeamPushScreenshots : SimulationSceneFixture
    {
        private const int FramesPerSecond = 60;
        private const float SettleSeconds = 0.8f;
        // The sticker springs in for 0.22 s and holds for 0.55 s: caught while it holds.
        private const float StickerMoment = 0.4f;

        private static readonly string[] Languages = { "en", "ru" };

        // Mouse first: once the HUD has seen a finger it keeps its touch buttons.
        private static readonly (int Width, int Height, bool Touch)[] Sizes = { (1920, 1080, false), (1080, 1920, true) };

        private static readonly PropertyInfo LastHitByProperty = typeof(Car).GetProperty(nameof(Car.LastHitBy));
        private static readonly PropertyInfo LastHitTimeProperty = typeof(Car).GetProperty(nameof(Car.LastHitTime));

        private static readonly string[] Names = { "YOU", "ZOOMER", "BONKY", "SKID", "TURBO TOM", "DRIFTA" };
        private static readonly int[] Levels = { 8, 7, 10, 6, 9, 11 };

        private static string Directory_ => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "team-push");

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheHud()
        {
            Directory.CreateDirectory(Directory_);
            UseSpecial(SpecialRound.TeamPush);
            var tuning = LoadTuning();
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var modes = container.Resolve<RoundModes>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();
            round.Begin();

            // A few points before the pictures: the player knocks a red car off, a red car falls by itself, a red car
            // knocks a blue one off.
            yield return KnockOff(round.Cars[1], round.Player);
            yield return KnockOff(round.Cars[3], null);
            yield return KnockOff(round.Cars[2], round.Cars[5]);

            var camera = Camera.main;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var victims = new[] { 5, 1, 3, 1, 5, 3 };
            var shot = 0;
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
                        SpecialRound.TeamPush, 7, modes.Active));
                    var view = new HudView(controls, container.Resolve<IAudioService>());
                    view.Bind(model);
                    var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
                    Stretch(hud);
                    var textures = new List<Object> { panelSettings, host };
                    try {
                        document.rootVisualElement.Add(hud);
                        // UIKit hands a view the UXML's own root, where its stylesheet is, not the template
                        // container around it: what the HUD adds to its root is styled only there.
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
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Team push screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = overlay;
                            yield return RealSeconds(SettleSeconds);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}hud_{width}x{height}.png");

                            // A point while the camera watches: the score pops and its sticker drops under the side.
                            var victim = round.Cars[victims[shot++ % victims.Length]];
                            yield return WaitIn(victim);
                            var before = round.Score.TeamPointsOf(RoundTeams.Blue);
                            yield return KnockOff(victim, round.Player);
                            Assert.That(round.Score.TeamPointsOf(RoundTeams.Blue), Is.GreaterThan(before),
                                "The player's knock-out gave blue no point.");
                            yield return RealSeconds(StickerMoment);
                            Shoot(camera, scene, overlay, $"{Prefix(language)}point_{width}x{height}.png");
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
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Team push screenshot");
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
            var sizes = new[] { (1080, 1920, true), (1920, 1080, false) };
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
            var rules = tuning.RulesOf(SpecialRound.TeamPush);
            var bots = rules.BotCountFor(rules.Bots, 6);
            var roster = new SessionRoster(catalog, new PlayerCar(catalog));
            roster.ForRound(bots);
            try {
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var (width, height) in new[] { (1080, 1920), (1920, 1080) }) {
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
                            model.ShowLobby(roster, bots, RoundResult.Nobody, Strings.Get("arena.tabletop.name"), false,
                                0, null, SpecialRound.TeamPush, rules.Teams);
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

        /// <summary>The menu's own pictures (<see cref="MenuScreenshots"/>) with TEAM PUSH the event of the day: the menu
        /// and its CHOOSE MODE popup, in <c>Logs/team-push</c>.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheMenu()
        {
            Directory.CreateDirectory(Directory_);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var capture = typeof(MenuScreenshots).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null, "MenuScreenshots.Capture is gone.");
            try {
                EventSchedule.Forced = EventMode.TeamPush;
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var size in new[] { (1080, 1920, true), (1920, 1080, false) }) {
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

        // A TEAM PUSH round as RoundState saves it, the player blue at index 0, the teams by turns.
        private static (RoundRecord record, RoundOverScreenshots.MemoryStorage storage) Record(string name,
            GameTuning tuning, CarCatalog catalog, WinnerPodium podium, Transform podiumHost)
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetString("missions", "round-knockouts:0|knockouts:4|revenge:0");
            storage.SetInt("coins", 240);
            // Past every car of the level path (MONSTER opens at 13) and short of silver (25): no level's popup covers
            // the result.
            storage.SetInt("xp_total", LevelStart(tuning, 16));
            // Points by car: a car's knock-outs, and the falls of the other team on its own (no car's).
            var points = name switch {
                "won" => new[] { 4, 2, 2, 3, 1, 1 },
                "lost" => new[] { 1, 4, 2, 3, 0, 2 },
                _ => new[] { 2, 3, 3, 1, 1, 2 },
            };
            var own = name switch { "won" => new[] { 2, 1 }, "lost" => new[] { 1, 2 }, _ => new[] { 1, 1 } };
            var teamPoints = new int[2];
            for (var index = 0; index < points.Length; index++) {
                teamPoints[index % 2] += points[index];
            }

            teamPoints[0] += own[0];
            teamPoints[1] += own[1];
            var order = Enumerable.Range(0, points.Length).OrderByDescending(index => points[index]).ThenBy(index => index)
                .ToList();
            var standings = order.Select((index, at) => new RoundStanding(index, at + 1, points[index], 0, points[index],
                RoundTeams.TeamOf(index, 2))).ToList();
            var colors = Enumerable.Range(0, points.Length).Select(index => RoundTeams.PaintOf(RoundTeams.TeamOf(index, 2)))
                .ToArray();
            var isDraw = teamPoints[0] == teamPoints[1];
            var isWon = teamPoints[0] > teamPoints[1];
            var carCount = points.Length;
            var place = isWon ? 1 : isDraw ? Mathf.Max(1, carCount / 2) : carCount;
            var result = new RoundResult(isWon ? RoundOutcome.Won : RoundOutcome.Survived, place, carCount, 60f, points[0],
                RoundResult.Nobody, FallCause.None, false, false, standings, colors, falls: name == "lost" ? 3 : 1,
                knockedOff: name == "lost" ? 2 : 1, isLoss: !isWon && !isDraw, isTimed: true, kind: SpecialRound.TeamPush,
                knockoutRewardCap: tuning.RulesOf(SpecialRound.TeamPush).KnockoutRewardCap, teams: 2,
                teamPoints: teamPoints);
            var missions = new MissionTracker(storage, tuning);
            missions.Seed(1);
            var mission = missions.Record(new MissionStats(true, result.Outcome, result.Place, result.Knockouts, 3, false, 0,
                0, 0, "sandbox"), 3, 3);
            var xp = new PlayerProgress(storage, tuning, catalog).Record(result, isWon ? tuning.Streaks : null, mission.Xp);
            var coins = new PlayerWallet(storage, tuning).Record(result, isWon, mission.Coins);
            var streak = isWon ? new StreakChange(0, 1, 2) : isDraw ? new StreakChange(1, 1, 2) : new StreakChange(0, 0, 2);
            var record = new RoundRecord(result, 1, 2, false, false).WithXp(xp)
                .WithRoster(Names, Levels, RoundResult.Nobody)
                .WithMeta(coins, mission, streak)
                .WithPodium(Podium(catalog, podium, podiumHost, "suv", colors[result.PodiumCar]));
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
            Debug.Log($"Team push screenshot: {path}");
        }

        private static void Save(RenderTexture source, string file)
        {
            var texture = Read(source);
            var path = Path.Combine(Directory_, file);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Team push screenshot: {path}");
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
