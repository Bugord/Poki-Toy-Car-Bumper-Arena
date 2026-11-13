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
    /// Pictures of a CROWN round for a person to look at, in portrait (touch) and landscape (mouse), in English and
    /// Russian: the kick-off with the mode's rule, the crown dropping and loose, a bot with it (STEAL THE CROWN!), the
    /// pointer while its holder is off the screen, the player taking it (GOT IT!, RUN!) and losing it (STOLEN!), then the
    /// result with the seconds table over the arena. Into <c>Logs/crown/&lt;language&gt;_&lt;moment&gt;_&lt;w&gt;x&lt;h&gt;.png</c>;
    /// the lobby goes to <c>Logs/lobby/crown_&lt;language&gt;_ready_*</c> (the lobby's own capture). Run by name, without
    /// -nographics; it asserts nothing about the layout.
    /// </summary>
    [Explicit("Writes screenshots of a CROWN round for a visual check; run it by name.")]
    public sealed class CrownScreenshots : SimulationSceneFixture
    {
        private const int SettleFrames = 12;
        private const float SettleSeconds = 0.5f;
        private const float RoundSeconds = 24f;

        private static readonly (int Width, int Height, bool Touch)[] Sizes = { (1920, 1080, false), (1080, 1920, true) };
        private static readonly string[] Languages = { "en", "ru" };

        private float? originalDuration;

        [TearDown]
        public void RestoreDuration()
        {
            if (originalDuration.HasValue) {
                DurationField().SetValue(LoadTuning().Crown, originalDuration.Value);
                originalDuration = null;
            }

            Time.timeScale = 1f;
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureACrownRound()
        {
            var tuning = LoadTuning();
            originalDuration = tuning.Crown.Duration;
            DurationField().SetValue(tuning.Crown, RoundSeconds);
            UseSpecial(SpecialRound.Crown);
            // SHOTS_MAP plays the round on another arena (into Logs/crown/<map>): sandbox fits the frame whole, so the
            // pointer to an off-screen crown shows only on a bigger one.
            var map = System.Environment.GetEnvironmentVariable("SHOTS_MAP") ?? "sandbox";
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, map);
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var crown = container.Resolve<CrownMode>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();

            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = Color.clear;
            var host = new GameObject("Crown screenshot HUD");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var controls = new HudControls();
            var model = new HudViewModel(null, controls);
            model.SetPayload(new HudRound(round, null, tuning.Controls, false, 7, false, special: SpecialRound.Crown,
                mode: crown));
            var view = new HudView(controls, container.Resolve<IAudioService>());
            view.Bind(model);
            var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
            Fill(hud);
            var camera = Camera.main;
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "crown", map == "sandbox" ? "" : map);
            Directory.CreateDirectory(directory);
            var owned = new List<Object> { panelSettings, host };
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            RoundResult? result = null;
            round.Finished += value => result = value;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud.hierarchy[0]);
                view.ShowAsync(CancellationToken.None).Forget();
                yield return Frames(10);

                // The countdown's first number with the mode's name and rule over it.
                model.ShowKickoff(3, 6f);
                yield return Shoot("kickoff", camera, panelSettings, controls, directory, owned,
                    () => model.ShowKickoff(3, 6f), new[] { "en", "ru", "de" });
                model.ShowKickoff(HudViewModel.NoKickoff, 0f);
                round.Begin();

                yield return Seconds(tuning.Crown.FirstDelay * 0.6f);
                yield return Shoot("drop", camera, panelSettings, controls, directory, owned);
                yield return Until(() => crown.Phase == CrownMode.CrownPhase.Loose, 3f);
                yield return Seconds(0.4f);
                yield return Shoot("grab", camera, panelSettings, controls, directory, owned);

                // A bot takes it and holds it a few seconds.
                var bot = round.Cars[2];
                Park(bot, crown.Spot, Vector3.forward);
                yield return Until(() => crown.Holder == bot, 1f);
                yield return Seconds(3.2f);
                yield return Shoot("bot", camera, panelSettings, controls, directory, owned);

                // The player on the near side, the holder in the far corner beyond the frame: the pointer shows the way.
                Park(round.Player, Corner(round.Edge, -camera.transform.right - camera.transform.forward), Vector3.forward);
                Park(bot, Corner(round.Edge, camera.transform.right + camera.transform.forward), Vector3.forward);
                if (crown.Holder != bot) {
                    StealFor(crown, bot, crown.Holder);
                }

                yield return Seconds(1f);
                Debug.Log($"Crown screenshot: the holder at {camera.WorldToViewportPoint(bot.Position)} of the view.");
                yield return Shoot("pointer", camera, panelSettings, controls, directory, owned);

                // The player bumps it off the bot: GOT IT!, then RUN! with its own seconds.
                // On the near side, clear of the bots crowding the middle: a frozen bot touching the player would
                // press the crown off it before RUN! shows.
                var clear = NearSide(round.Edge, camera);
                var across = camera.transform.right;
                across.y = 0f;
                across.Normalize();
                Park(round.Player, clear, across);
                Park(bot, clear + across * 2.6f, across);
                yield return Seconds(0.3f);
                StealFor(crown, round.Player, bot);
                yield return Seconds(0.25f);
                yield return Shoot("got", camera, panelSettings, controls, directory, owned,
                    () => RaiseTaken(crown, round.Player, bot));
                yield return Seconds(1.5f);
                yield return Shoot("run", camera, panelSettings, controls, directory, owned);

                // Another bot bumps it off the player: STOLEN!
                var thief = round.Cars[3];
                Park(thief, round.Player.Position - across * 2.6f, across);
                yield return Seconds(0.5f);
                StealFor(crown, thief, round.Player);
                yield return Seconds(0.25f);
                yield return Shoot("stolen", camera, panelSettings, controls, directory, owned,
                    () => RaiseTaken(crown, thief, round.Player));

                // And back to the player for the rest, so it wins on seconds.
                yield return Seconds(tuning.Crown.StealBackTime + 0.2f);
                StealFor(crown, round.Player, thief);
                yield return Until(() => result.HasValue, RoundSeconds + 5f);
                Assert.That(result.HasValue, Is.True, "The round did not end.");
                yield return Frames(30);
                yield return Shoot("end", camera, panelSettings, controls, directory, owned);
            }
            finally {
                view.Dispose();
                hud.RemoveFromHierarchy();
            }

            // The result over the arena, as ResultState shows it.
            if (result.HasValue) {
                var podium = container.Resolve<WinnerPodium>().Show(result.Value.PodiumCar);
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    foreach (var (width, height, touch) in Sizes) {
                        yield return ShootResult(result.Value, round, podium, tuning, camera, language, width, height,
                            touch, directory, owned);
                    }
                }
            }

            camera.targetTexture = null;
            PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Crown screenshot");
            foreach (var item in owned) {
                if (item != null) {
                    Object.DestroyImmediate(item);
                }
            }

            Strings.SetLanguage("en");
            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        /// <summary>The lobby before a crown round, with its card: the lobby screenshots' own capture.</summary>
        [UnityTest]
        public IEnumerator CaptureTheCrownLobby()
        {
            var capture = typeof(LobbyScreenshots).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var language in new[] { "en", "ru", "de" }) {
                yield return (IEnumerator)capture.Invoke(null,
                    new object[] { SpecialRound.Crown, language, $"crown_{language}", new float[0], 1 });
            }
        }

        // Every size and language of one moment, with the game's clock stopped while the HUD settles.
        private static IEnumerator Shoot(string moment, Camera camera, PanelSettings panelSettings, HudControls controls,
            string directory, List<Object> owned, System.Action beforeEach = null, string[] languages = null)
        {
            var timeScale = Time.timeScale;
            Time.timeScale = 0f;
            foreach (var language in languages ?? Languages) {
                Strings.SetLanguage(language);
                foreach (var (width, height, touch) in Sizes) {
                    if (touch) {
                        controls.BeginTouchSteering(Vector2.zero);
                    }

                    var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    var overlay = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    owned.Add(scene);
                    owned.Add(overlay);
                    PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Crown screenshot");
                    camera.targetTexture = scene;
                    panelSettings.targetTexture = overlay;
                    // A pop lives a second and a half: started again for each picture.
                    beforeEach?.Invoke();
                    yield return Settle();
                    camera.Render();
                    var picture = Compose(Read(scene), Read(overlay));
                    var path = Path.Combine(directory, $"{language}_{moment}_{width}x{height}.png");
                    File.WriteAllBytes(path, picture.EncodeToPNG());
                    Object.DestroyImmediate(picture);
                    Debug.Log($"Crown screenshot: {path}");
                }
            }

            Strings.SetLanguage("en");
            Time.timeScale = timeScale;
        }

        private static IEnumerator ShootResult(RoundResult result, ArenaRound round, Texture podium, GameTuning tuning,
            Camera camera, string language, int width, int height, bool touch, string directory, List<Object> owned)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var arenas = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetString("missions", "round-knockouts:0|knockouts:4|revenge:0");
            storage.SetInt("coins", 85);
            storage.SetInt("xp_total", 1440); // Level 14 with room: no new car or popup over the result.
            var missions = new MissionTracker(storage, tuning);
            missions.Seed(1);
            var missionRound = missions.Record(new MissionStats(true, result.Outcome, result.Place, result.Knockouts, 3,
                false, 0, 0, 0, "sandbox"), 3, 3);
            var progress = new PlayerProgress(storage, tuning, catalog);
            var wallet = new PlayerWallet(storage, tuning);
            var xp = progress.Record(result, null, missionRound.Xp);
            var coins = wallet.Record(result, false, missionRound.Coins);
            var record = new RoundRecord(result, 1, 2, false, false).WithXp(xp)
                .WithRoster(round.Names, round.Levels, RoundResult.Nobody)
                .WithMeta(coins, missionRound, default)
                .WithPodium(podium);

            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = Color.clear;
            var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var overlay = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            panelSettings.targetTexture = overlay;
            owned.Add(panelSettings);
            owned.Add(scene);
            owned.Add(overlay);
            var host = new GameObject("Crown result screenshot");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var controls = new HudControls();
            typeof(HudControls).GetField("isTouchSeen", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controls, touch);
            DailyGift.ForgetOffer();
            RoundOverViewModel.ForgetVisit();
            var model = new RoundOverViewModel(catalog, arenas, tuning, progress, null,
                new MatchTests.RecordingPlatform { Rewards = true }, wallet, null);
            model.SetPayload(record);
            var view = new RoundOverView(new SilentAudioService(library), controls);
            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Crown screenshot");
            camera.targetTexture = scene;
            try {
                var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/RoundOver.uxml").Instantiate();
                Fill(screen);
                document.rootVisualElement.Add(screen);
                view.Bind(model);
                view.Attach(screen.hierarchy[0]);
                view.ShowAsync(CancellationToken.None).Forget();
                var start = Time.realtimeSinceStartup;
                foreach (var moment in new[] { 0.8f, 3.5f }) {
                    while (Time.realtimeSinceStartup - start < moment) {
                        yield return null;
                    }

                    camera.Render();
                    var picture = Compose(Read(scene), Read(overlay));
                    var path = Path.Combine(directory,
                        $"{language}_result_{moment:0.0}_{width}x{height}.png");
                    File.WriteAllBytes(path, picture.EncodeToPNG());
                    Object.DestroyImmediate(picture);
                    Debug.Log($"Crown screenshot: {path}");
                }
            }
            finally {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        // A floor point on the side of the arena across from the camera's middle: off a portrait screen.
        // A point six metres toward the camera from the middle (less where the floor ends sooner), three clear of the fence.
        private static Vector3 NearSide(IArenaEdge edge, Camera camera)
        {
            var back = -camera.transform.forward;
            back.y = 0f;
            back.Normalize();
            for (var reach = 6f; reach > 0f; reach -= 0.5f) {
                var point = back * reach;
                if (edge.DistanceToEdge(point, 3f) >= 0f) {
                    return point;
                }
            }

            return Vector3.zero;
        }

        // As far along the direction as the floor goes, two clear of the fence.
        private static Vector3 Corner(IArenaEdge edge, Vector3 direction)
        {
            direction.y = 0f;
            direction.Normalize();
            for (var reach = edge.FullExtent; reach > 0f; reach -= 0.5f) {
                var point = direction * reach;
                if (edge.DistanceToEdge(point, 2f) >= 0f) {
                    return point;
                }
            }

            return Vector3.zero;
        }

        // The crown taken off a car as a bump takes it, with its effects, sound and event: the frozen bots and the
        // player driver (which drives the player with the test's empty input) would turn a physical ram into chance.
        private static void StealFor(CrownMode crown, Car thief, Car from)
        {
            typeof(CrownMode).GetMethod("Steal", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(crown, new object[] { thief, from, from.Position });
            Assert.That(crown.Holder, Is.SameAs(thief), "The crown did not change hands.");
        }

        private static void Park(Car car, Vector3 point, Vector3 facing)
        {
            car.Movement.Place(new Vector3(point.x, 0.05f, point.z), Quaternion.LookRotation(facing));
        }

        private static IEnumerator Until(System.Func<bool> isDone, float seconds)
        {
            var until = Time.time + seconds;
            while (!isDone() && Time.time < until) {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            var until = Time.time + seconds;
            while (Time.time < until) {
                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++) {
                yield return null;
            }
        }

        // The HUD's transitions run on the panel's real-time clock.
        private static IEnumerator Settle()
        {
            var settled = Time.realtimeSinceStartup + SettleSeconds;
            for (var frame = 0; frame < SettleFrames || Time.realtimeSinceStartup < settled; frame++) {
                yield return null;
            }
        }

        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
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

        private static Texture2D Compose(Texture2D scene, Texture2D overlay)
        {
            var below = scene.GetPixels32();
            var above = overlay.GetPixels32();
            for (var i = 0; i < below.Length; i++) {
                var alpha = above[i].a / 255f;
                below[i] = new Color32(
                    (byte)Mathf.RoundToInt(above[i].r * alpha + below[i].r * (1f - alpha)),
                    (byte)Mathf.RoundToInt(above[i].g * alpha + below[i].g * (1f - alpha)),
                    (byte)Mathf.RoundToInt(above[i].b * alpha + below[i].b * (1f - alpha)),
                    255);
            }

            scene.SetPixels32(below);
            scene.Apply();
            Object.DestroyImmediate(overlay);
            return scene;
        }

        // The crown's Taken again, so the HUD pops GOT IT! or STOLEN! afresh for the next picture.
        private static void RaiseTaken(CrownMode crown, Car holder, Car from)
        {
            var taken = (System.Action<Car, Car>)typeof(CrownMode)
                .GetField(nameof(CrownMode.Taken), BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(crown);
            taken?.Invoke(holder, from);
        }

        private static FieldInfo DurationField() =>
            typeof(GameTuning.CrownSettings).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
#endif
