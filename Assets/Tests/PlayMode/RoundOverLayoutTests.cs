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
using MemoryStorage = ToyCarArena.Tests.PlayMode.RoundOverScreenshots.MemoryStorage;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The result screen settles its layout: no GeometryChangedEvent handler of it keeps writing layout that moves what
    /// it reads, which UI Toolkit reports as "Layout update failed to stabilize" every frame (the dev build 0.17.1 froze
    /// at 0 fps on a 1289×725 browser window, the rival on the podium and the free hat's popup over it).
    /// </summary>
    public sealed class RoundOverLayoutTests
    {
        private const string LoopMessage = "failed to stabilize";
        // Long enough for the screen to spring in, the gift popup to pop and the first reward steps to play.
        private const float Watch = 4f;

        private static readonly (int width, int height)[] Sizes = {
            (1289, 725), (1288, 725), (1290, 725), (1920, 1080), (1080, 1920), (1280, 720), (1366, 768), (2340, 1080),
            (2560, 1080),
        };

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TheResultScreenSettlesItsLayout()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            Strings.SetLanguage("en");
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var arenas = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");

            var sizes = new List<(int, int)>(Sizes);
            // LAYOUT_SIZES=1280x725,1300x725 tries others.
            if (System.Environment.GetEnvironmentVariable("LAYOUT_SIZES") is { Length: > 0 } extra) {
                sizes.Clear();
                foreach (var size in extra.Split(',')) {
                    var parts = size.Split('x');
                    sizes.Add((int.Parse(parts[0]), int.Parse(parts[1])));
                }
            }

            var failures = new List<string>();
            var first = new Dictionary<string, string>();
            var current = "";
            var loops = 0;

            void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Error && message.Contains(LoopMessage)) {
                    loops++;
                    first.TryAdd(current, message);
                }
            }

            // The screen's errors are counted here, not failed on one by one: every size and case is tried.
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
            // A podium picture as the arena's camera gives it; its content does not matter to the layout.
            var podium = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32);
            try {
                foreach (var name in new[] { "rival-gift", "rival", "won" }) {
                    foreach (var (width, height) in sizes) {
                        foreach (var isTouch in new[] { false, true }) {
                            current = $"{name} {width}x{height} {(isTouch ? "touch" : "desktop")}";
                            loops = 0;
                            yield return Show(name, width, height, isTouch, podium, tuning, catalog, arenas, library);
                            if (loops > 0) {
                                failures.Add($"{current}: {loops}");
                            }
                        }
                    }
                }
            }
            finally {
                Application.logMessageReceived -= OnLog;
                LogAssert.ignoreFailingMessages = false;
                Object.DestroyImmediate(podium);
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }

            if (first.Count > 0) {
                var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(directory);
                using var writer = new StreamWriter(Path.Combine(directory, "round-over-layout.txt"));
                foreach (var (at, message) in first) {
                    writer.WriteLine("=== " + at);
                    writer.WriteLine(message);
                }
            }

            Assert.That(failures, Is.Empty, "Layout loops (see Logs/round-over-layout.txt):\n" + string.Join("\n", failures));
        }

        private static IEnumerator Show(string name, int width, int height, bool isTouch, RenderTexture podium,
            GameTuning tuning, CarCatalog catalog, ArenaCatalog arenas, AudioLibrary library)
        {
            var (record, storage) = Record(name, tuning, catalog, podium);
            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            panelSettings.targetTexture = target;
            var host = new GameObject("Result layout");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var controls = new HudControls();
            typeof(HudControls).GetField("isTouchSeen", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controls, isTouch);
            var progress = new PlayerProgress(storage, tuning, catalog);
            var wallet = new PlayerWallet(storage, tuning);
            if (name == "rival-gift") {
                wallet.TryGift(tuning.Toppers.GiftLevel);
            }

            var platform = new MatchTests.RecordingPlatform { Rewards = true };
            DailyGift.ForgetOffer();
            var model = new RoundOverViewModel(catalog, arenas, tuning, progress, null, platform, wallet, null, null);
            model.SetPayload(record);
            var view = new RoundOverView(new SilentAudioService(library), controls);
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
                view.Attach(screen.hierarchy[0]);
                view.ShowAsync(CancellationToken.None).Forget();
                var start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < Watch) {
                    yield return null;
                }
            }
            finally {
                view.Dispose();
                Object.Destroy(host);
                Object.DestroyImmediate(target);
                Object.Destroy(panelSettings);
            }

            // The panel goes with its document at the end of the frame.
            yield return null;
        }

        // A round as RoundState saves it, as RoundOverScreenshots builds it: the rival pushed the player off, with the
        // free hat's gift waiting (owned paid cars keep the level popups out of its way), or a won round.
        private static (RoundRecord record, MemoryStorage storage) Record(string name, GameTuning tuning,
            CarCatalog catalog, RenderTexture podium)
        {
            var storage = new MemoryStorage();
            storage.SetString("missions", "round-knockouts:0|knockouts:4|revenge:0");
            storage.SetInt("coins", 85);
            storage.SetInt("xp_total", 70);
            storage.SetString("cars_owned", "speedster,suv,truck,monster-truck");
            var isRival = name.StartsWith("rival");
            var isWon = !isRival;
            var result = new RoundResult(isWon ? RoundOutcome.Won : RoundOutcome.Out, isWon ? 1 : 3, 4, 40f,
                isWon ? 2 : 1, isRival ? 1 : RoundResult.Nobody, FallCause.Drive, false, false,
                colors: new[] { 0, 1, 2, 3 });
            var missions = new MissionTracker(storage, tuning);
            missions.Seed(1);
            var round = missions.Record(new MissionStats(true, result.Outcome, result.Place, result.Knockouts, 3,
                false, 0, 0, 0, "sandbox"), 3, 3);
            var xp = new PlayerProgress(storage, tuning, catalog).Record(result, isWon ? tuning.Streaks : null, round.Xp);
            var coins = new PlayerWallet(storage, tuning).Record(result, isWon, round.Coins);
            var streak = isWon ? new StreakChange(0, 1, 2) : new StreakChange(0, 0, 2);
            var record = new RoundRecord(result, 1, 2, false, false).WithXp(xp)
                .WithRoster(new[] { "YOU", "FUZZYGEAR", "BONKY", "SKID" }, new[] { 3, 2, 4, 1 }, RoundResult.Nobody)
                .WithMeta(coins, round, streak)
                .WithPodium(podium);
            return (record, storage);
        }
    }
}
