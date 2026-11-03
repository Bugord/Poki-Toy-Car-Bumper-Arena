#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// A finger's quick stroke is a swipe (<see cref="IInputService.Swiped"/>): a virtual touch screen feeds the real
    /// <see cref="InputService"/>. A stroke held too long, one too short (a tap, which boosts) and a cancelled touch are
    /// not swipes.
    /// </summary>
    public sealed class TouchSwipeTests
    {
        private Touchscreen touchscreen;
        private InputSettings.EditorInputBehaviorInPlayMode previousRouting;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputService input;
        private GameTuning tuning;
        private readonly List<Vector2> swipes = new();
        private readonly List<BoostInputMethod> boosts = new();

        private static Vector2 Point => new(Screen.width * 0.5f, Screen.height * 0.4f);

        private static float ShortSide => Mathf.Min(Screen.width, Screen.height);

        [SetUp]
        public void SetUp()
        {
            // As in MouseControlsTests: a batch run has no focused Game view to route the device to.
            var settings = InputSystem.settings;
            previousRouting = settings.editorInputBehaviorInPlayMode;
            previousBackground = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            input = new InputService(new NoOnScreenControls(), tuning);
            input.Initialize();
            swipes.Clear();
            boosts.Clear();
            input.Swiped += swipes.Add;
            input.BoostPressed += boosts.Add;
        }

        [TearDown]
        public void TearDown()
        {
            input.BoostPressed -= boosts.Add;
            input.Swiped -= swipes.Add;
            input.Dispose();
            InputSystem.RemoveDevice(touchscreen);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
            InputSystem.settings.backgroundBehavior = previousBackground;
        }

        [UnityTest]
        public IEnumerator AQuickStrokeSwipesItsWay()
        {
            var end = Point + Vector2.up * (ShortSide * 0.3f);
            yield return SetTouch(Point, TouchPhase.Began);
            yield return SetTouch(Vector2.Lerp(Point, end, 0.5f), TouchPhase.Moved);
            yield return SetTouch(end, TouchPhase.Ended);

            Assert.That(swipes, Has.Count.EqualTo(1), "The stroke was not a swipe");
            Assert.That(Vector2.Angle(swipes[0], Vector2.up), Is.LessThan(1f), "The swipe lost its way");
            Assert.That(boosts, Is.Empty, "A swipe is not a tap");
        }

        [UnityTest]
        public IEnumerator AHeldStrokeDoesNotSwipe()
        {
            var end = Point + Vector2.up * (ShortSide * 0.3f);
            yield return SetTouch(Point, TouchPhase.Began);
            yield return SetTouch(end, TouchPhase.Moved);
            var until = Time.realtimeSinceStartup + tuning.Controls.SwipeTime + 0.15f;
            while (Time.realtimeSinceStartup < until) {
                yield return null;
            }

            yield return SetTouch(end, TouchPhase.Ended);

            Assert.That(swipes, Is.Empty, "A finger held on the joystick swiped as it lifted");
        }

        [UnityTest]
        public IEnumerator AShortStrokeIsATap()
        {
            var end = Point + Vector2.up * (ShortSide * tuning.Controls.TapBoostSlop * 0.5f);
            yield return SetTouch(Point, TouchPhase.Began);
            yield return SetTouch(end, TouchPhase.Ended);

            Assert.That(swipes, Is.Empty);
            Assert.That(boosts, Is.EqualTo(new[] { BoostInputMethod.Tap }));
        }

        [UnityTest]
        public IEnumerator ACancelledStrokeDoesNotSwipe()
        {
            var end = Point + Vector2.up * (ShortSide * 0.3f);
            yield return SetTouch(Point, TouchPhase.Began);
            yield return SetTouch(end, TouchPhase.Moved);
            yield return SetTouch(end, TouchPhase.Canceled);

            Assert.That(swipes, Is.Empty);
        }

        private IEnumerator SetTouch(Vector2 position, TouchPhase phase)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, phase = phase, position = position });
            // The event is taken in the next frame's input update, before the test resumes.
            yield return null;
        }
    }
}
#endif
