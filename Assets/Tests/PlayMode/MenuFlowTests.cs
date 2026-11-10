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
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
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
            Press(FindButton("home-button"));

            yield return WaitFor(() => State == typeof(MenuState) && IsShown(FindButton("menu__play")), "the menu again");
            yield return Wait(0.5f);
            Assert.That(platform.Events, Does.Contain(("result", "home", "interact")));
            Assert.That(platform.Events.Count(e => e == ("menu", "shown", "visible")), Is.EqualTo(2));
            Assert.That(platform.Events.Count(e => e.Item1 == "menu" && e.Item3 == "start-time"), Is.EqualTo(1),
                "only the visit's first menu reports its time");
        }

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
