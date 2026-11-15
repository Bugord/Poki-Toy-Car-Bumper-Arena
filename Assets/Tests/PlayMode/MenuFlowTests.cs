using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bugord.App;
using Bugord.App.Localization;
using Bugord.App.Platform;
using Bugord.Core.Storage;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
using ToyCarArena.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UIKit.Runtime.Core;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The whole app from the Boot scene, with the saves in memory and the platform recording its events: a first visit
    /// goes straight to its round and waits for the player, a returning one opens on the main menu, PLAY there reaches a
    /// round through the lobby (which counts down without waiting), and HOME on the result comes back to the menu.
    /// </summary>
    public sealed class MenuFlowTests
    {
        private const float Timeout = 30f;

        private RoundOverScreenshots.MemoryStorage storage;
        private MatchTests.RecordingPlatform platform;
        private HashSet<GameObject> keptRoots;
        private Scene testScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            storage = new RoundOverScreenshots.MemoryStorage();
            platform = new MatchTests.RecordingPlatform();
            // The daily gift's offer is the visit's: each test is a visit of its own.
            DailyGift.ForgetOffer();
            RoundOverViewModel.ForgetVisit();
            ShownScreens.Clear();
            keptRoots = new HashSet<GameObject>(PersistentRoots());
            testScene = SceneManager.GetActiveScene();
            // A visit's first menu reports its time; each test is a visit of its own.
            typeof(MenuState).GetField("isShown", System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic)?.SetValue(null, false);
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var scope = Object.FindAnyObjectByType<GameScope>();
            if (scope != null) {
                Object.Destroy(scope.gameObject);
            }

            yield return null;
            foreach (var root in PersistentRoots().Where(root => !keptRoots.Contains(root))) {
                Object.Destroy(root);
            }

            SceneManager.SetActiveScene(testScene);
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--) {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != testScene && scene.isLoaded) {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            typeof(Strings).GetField("table", System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic)?.SetValue(null, null);
            Time.timeScale = 1f;
            EventSchedule.Forced = null;
        }

        [UnityTest]
        public IEnumerator AFirstVisitGoesStraightToItsRound()
        {
            yield return Boot();
            yield return WaitFor(() => State == typeof(RoundState) && Round is { IsPlaced: true }, "the first round");
            yield return Wait(1f);

            Assert.That(State, Is.EqualTo(typeof(RoundState)));
            Assert.That(Round.HasBegun, Is.False, "the first visit's round waits for the player");
            Assert.That(platform.Events, Does.Contain(("controls", "start", "visible")));
            Assert.That(platform.Events.Any(e => e.Item1 == "menu"), Is.False);
            Assert.That(FindButton("menu__play"), Is.Null, "no menu on a first visit");

            // A new player's first round is the easy one.
            var tuning = Scope.Container.Resolve<GameTuning>();
            Assert.That(Scope.Container.Resolve<ISessionProgress>().RoundsBefore, Is.EqualTo(0));
            Assert.That(Difficulty.Level, Is.EqualTo(0f));
            Assert.That(Round.Cars.Count, Is.EqualTo(1 + tuning.Bots.Easy.Bots));
        }

        [UnityTest]
        public IEnumerator AReturningPlayersFirstRoundIsNotTheEasyOne()
        {
            ReturningPlayer();
            storage.SetInt(SessionProgress.RoundsKey, 20);
            yield return Boot();
            yield return WaitFor(() => IsShown(FindButton("menu__play")), "the menu");
            yield return Wait(0.5f);
            Press(FindButton("menu__play"));
            yield return WaitFor(() => State == typeof(RoundState) && Round is { HasBegun: true }, "the round");

            var tuning = Scope.Container.Resolve<GameTuning>();
            var progress = Scope.Container.Resolve<ISessionProgress>();
            Assert.That(progress.Round, Is.EqualTo(1), "the visit's first round");
            Assert.That(progress.RoundsBefore, Is.EqualTo(20));
            Assert.That(Difficulty.Level, Is.EqualTo(tuning.Bots.ReturnLevel).Within(1e-4f), "a warm-up, not easy");
            Assert.That(Round.Cars.Count, Is.EqualTo(1 + Difficulty.BotCount));
            Assert.That(Difficulty.BotCount, Is.GreaterThan(tuning.Bots.Easy.Bots));
        }

        [UnityTest]
        public IEnumerator AReturningVisitOpensOnTheMenu()
        {
            ReturningPlayer();
            yield return Boot();
            yield return WaitFor(() => State == typeof(MenuState) && IsShown(FindButton("menu__play")), "the menu");
            yield return Wait(0.5f);

            Assert.That(platform.Events, Does.Contain(("menu", "shown", "visible")));
            Assert.That(platform.Events.Any(e => e.Item1 == "menu" && e.Item3 == "start-time"), Is.True);
            Assert.That(platform.Events, Has.No.Member(("controls", "start", "visible")));
            Assert.That(Scope.Container.Resolve<LevelScene>().IsLoaded, Is.True, "the first arena loads behind the menu");
            Assert.That(ArenaLayers, Is.Zero, "the menu covers the arena, so it is not drawn");
        }

        [UnityTest]
        public IEnumerator PlayGoesThroughTheLobbyToACountedRound()
        {
            ReturningPlayer();
            yield return Boot();
            yield return WaitFor(() => IsShown(FindButton("menu__play")), "the menu");
            yield return Wait(0.5f);
            Press(FindButton("menu__play"));

            yield return WaitFor(() => State == typeof(RoundState), "the round after PLAY");
            Assert.That(platform.Events, Does.Contain(("menu", "play", "interact")));
            Assert.That(Scope.Container.Resolve<MatchLobby>().HasLed, Is.True, "PLAY takes the lobby");
            // No TAP TO START: the round counts down and begins by itself.
            yield return WaitFor(() => Round is { HasBegun: true }, "the countdown");
            Assert.That(platform.Events, Has.No.Member(("controls", "start", "visible")));
            Assert.That(ArenaLayers, Is.Not.Zero, "the round draws the arena again");
        }

        [UnityTest]
        public IEnumerator HomeOnTheResultComesBackToTheMenu()
        {
            ReturningPlayer();
            yield return Boot();
            yield return WaitFor(() => IsShown(FindButton("menu__play")), "the menu");
            yield return Wait(0.5f);
            Press(FindButton("menu__play"));
            yield return WaitFor(() => Round is { HasBegun: true }, "the round");

            // The player drops off the arena: the round ends and the result comes up.
            Round.Player.GetComponent<CarMovement>().Place(new Vector3(0f, -40f, 0f), Quaternion.identity);
            yield return WaitFor(() => State == typeof(ResultState) && IsShown(FindButton("home-button")), "the result");
            // The result measures its buttons once its screen has opened, which may be after they first show.
            yield return Wait(1f);
            Assert.That(platform.Events, Does.Contain(("result", "home", "visible")));
            Assert.That(ArenaLayers, Is.Not.Zero, "the result lies over the arena it ended on");
            Press(FindButton("home-button"));

            yield return WaitFor(() => State == typeof(MenuState) && IsShown(FindButton("menu__play")), "the menu again");
            yield return Wait(0.5f);
            Assert.That(platform.Events, Does.Contain(("result", "home", "interact")));
            Assert.That(platform.Events.Count(e => e == ("menu", "shown", "visible")), Is.EqualTo(2));
            Assert.That(platform.Events.Count(e => e.Item1 == "menu" && e.Item3 == "start-time"), Is.EqualTo(1),
                "only the visit's first menu reports its time");
        }

        [UnityTest]
        public IEnumerator TheEventOfTheDayPlaysFromItsTileToTheNextMatch()
        {
            // OVERHEAT the event of the day: its tile on the menu plays it, the result's NEXT MATCH keeps it.
            EventSchedule.Forced = EventMode.Overheat;
            ReturningPlayer();
            yield return Boot();
            yield return WaitFor(() => IsShown(FindButton("menu__play")), "the menu");
            yield return Wait(0.5f);
            var roots = Scope.Container.Resolve<IUIRootProvider>();
            var tile = roots.GetLayerRoot(UILayer.Screen).Q(className: "menu__tile--event")?.Q<Button>();
            Assert.That(IsShown(tile), Is.True, "the event's tile");
            Press(tile);

            yield return WaitFor(() => State == typeof(RoundState) && Round is { HasBegun: true }, "the event's round");
            Assert.That(platform.Events, Does.Contain(("menu", "event-overheat", "interact")));
            Assert.That(Rules.Kind, Is.EqualTo(SpecialRound.Overheat));
            Assert.That(Round.IsTimed, Is.True);

            // The clock runs out: the round ends on points and the result comes up.
            yield return EndTheClock();
            yield return WaitFor(() => State == typeof(ResultState) && IsShown(FindButton("play-again-button")),
                "the result");
            yield return Wait(1f);
            Assert.That(platform.Events.Any(e => e.Item1 == "special" && e.Item2 == "overheat" && e.Item3 != "start"),
                Is.True, "the mode's end is measured");
            Press(FindButton("play-again-button"));

            yield return WaitFor(() => State == typeof(RoundState) && Round is { HasBegun: true }, "the next match");
            Assert.That(Rules.Kind, Is.EqualTo(SpecialRound.Overheat), "NEXT MATCH keeps the visit's mode");
        }

        private static RoundRules Rules => Scope.Container.Resolve<LevelScene>().Resolve<RoundRules>();

        // Moves the round's clock to its last moment rather than waiting the whole minute.
        private static IEnumerator EndTheClock()
        {
            typeof(ArenaRound).GetField("elapsed", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)?.SetValue(Round, Round.Duration - 0.2f);
            yield return WaitFor(() => Round == null || Round.IsFinished, "the end of the round");
        }

        [UnityTest]
        public IEnumerator FootballFromTheMenuTileKeepsItsPitchThroughNextMatch()
        {
            ReturningPlayer();
            EventSchedule.Forced = EventMode.Football;
            object savedDuration = null;
            System.Reflection.FieldInfo duration = null;
            GameTuning.FootballSettings settings = null;
            try {
                yield return Boot();
                // A short match: the flow, not the football, is under test.
                settings = Scope.Container.Resolve<GameTuning>().Football;
                duration = settings.GetType().GetField("duration", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                savedDuration = duration.GetValue(settings);
                duration.SetValue(settings, 6f);

                yield return WaitFor(() => IsShown(EventTile), "the menu's FOOTBALL tile");
                yield return Wait(0.5f);
                Assert.That(platform.Events, Does.Contain(("menu", "event-football", "visible")));
                Press(EventTile);
                yield return WaitFor(() => State == typeof(RoundState) && Round is { HasBegun: true }, "the match");
                Assert.That(platform.Events, Does.Contain(("menu", "event-football", "interact")));
                Assert.That(Scope.Container.Resolve<ISessionProgress>().Mode, Is.EqualTo(EventMode.Football));
                ExpectFootballOnThePitch();
                Assert.That(platform.Events, Does.Contain(("special", "football", "start")));

                yield return WaitFor(() => State == typeof(ResultState) && IsShown(FindButton("play-again-button")),
                    "the result");
                Assert.That(platform.Events.Any(e => e.Item1 == "special" && e.Item2.StartsWith("football-goals-") &&
                    e.Item3 == "goals"), Is.True, "the match's goals are measured");
                Assert.That(platform.Events.Any(e => e.Item1 == "special" && e.Item2.StartsWith("football-team-")),
                    Is.True, "the team layer's ending is measured");
                yield return Wait(1f);
                Press(FindButton("play-again-button"));

                yield return WaitFor(() => State == typeof(RoundState) && Round is { HasBegun: true }, "the next match");
                Assert.That(Scope.Container.Resolve<ISessionProgress>().Round, Is.EqualTo(2));
                ExpectFootballOnThePitch();
                Assert.That(platform.Events.Count(e => e == ("special", "football", "start")), Is.EqualTo(2));
            } finally {
                EventSchedule.Forced = null;
                if (duration != null) {
                    duration.SetValue(settings, savedDuration);
                }
            }
        }

        // The round now plays FOOTBALL, on its own pitch, with its ball.
        private static void ExpectFootballOnThePitch()
        {
            var level = Scope.Container.Resolve<LevelScene>();
            Assert.That(level.Resolve<ArenaMap>().Id, Is.EqualTo("pitch"));
            Assert.That(level.Resolve<RoundRules>().Kind, Is.EqualTo(SpecialRound.Football));
            var mode = level.Resolve<ToyCarArena.Gameplay.Modes.FootballMode>();
            Assert.That(mode.IsActive, Is.True);
            Assert.That(mode.Ball, Is.Not.Null, "the ball is on the pitch");
            Assert.That(Round.Cars.Count, Is.EqualTo(6), "three a side");
        }

        // The menu's tile of the event of the day.
        private static Button EventTile
        {
            get {
                var root = Scope?.Container?.Resolve<IUIRootProvider>()?.GetLayerRoot(UILayer.Screen);
                return root?.Q<VisualElement>(className: "menu__tile--event")?.Q<Button>(className: "menu__tile-button");
            }
        }

        // The layers the arena's camera draws now.
        private static int ArenaLayers => Object.FindAnyObjectByType<ArenaCamera>().Camera.cullingMask;

        // A player back for a second visit: one visit counted, some XP and the cars of the first levels.
        private void ReturningPlayer()
        {
            storage.SetInt("visit_count", 1);
            storage.SetInt("visit_day", (int)(DateTime.Now.Date - DateTime.MinValue.Date).TotalDays);
            storage.SetInt("xp_total", 120);
        }

        private IEnumerator Boot()
        {
            using (LifetimeScope.Enqueue(builder => {
                builder.RegisterInstance<IKeyValueStorage>(storage);
                builder.RegisterInstance<IGamePlatform>(platform);
            })) {
                var loading = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Additive);
                while (!loading.isDone) {
                    yield return null;
                }
            }

            SceneManager.SetActiveScene(SceneManager.GetSceneByName("Boot"));
            Assert.That(Scope, Is.Not.Null, "Boot has its GameScope");
        }

        private static GameScope Scope => Object.FindAnyObjectByType<GameScope>();
        private static Type State => Scope?.Container?.Resolve<AppStateTracker>().CurrentState;

        private static RoundDifficulty Difficulty => Scope.Container.Resolve<LevelScene>().Resolve<RoundDifficulty>();

        private static ArenaRound Round
        {
            get {
                var level = Scope?.Container?.Resolve<LevelScene>();
                return level is { IsLoaded: true } ? level.Resolve<ArenaRound>() : null;
            }
        }

        // The screens and the popups of UIKit, whose own document is not among the scene's objects.
        private static Button FindButton(string nameOrClass)
        {
            var roots = Scope?.Container?.Resolve<IUIRootProvider>();
            if (roots == null) {
                return null;
            }

            foreach (var layer in new[] { UILayer.Screen, UILayer.Popup }) {
                var root = roots.GetLayerRoot(layer);
                var button = root?.Q<Button>(nameOrClass) ?? root?.Q<Button>(className: nameOrClass);
                if (button != null) {
                    return button;
                }
            }

            return null;
        }

        private static bool IsShown(VisualElement element) =>
            element?.panel != null && element.resolvedStyle.display != DisplayStyle.None &&
            element.worldBound.width > 0f;

        private static void Press(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            var start = Time.realtimeSinceStartup;
            while (!condition()) {
                if (Time.realtimeSinceStartup - start > Timeout) {
                    Assert.Fail($"Timed out waiting for {what}; the app is in {State?.Name ?? "no state"}.");
                }

                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) {
                yield return null;
            }
        }

        // The roots kept across scene loads, found through a probe object placed with them.
        private static IEnumerable<GameObject> PersistentRoots()
        {
            var probe = new GameObject("Persistent probe");
            Object.DontDestroyOnLoad(probe);
            var roots = probe.scene.GetRootGameObjects().Where(root => root != probe).ToList();
            Object.DestroyImmediate(probe);
            return roots;
        }
    }
}
