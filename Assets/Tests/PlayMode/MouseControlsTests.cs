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

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The desktop mouse: its left button held drives, a short click of it boosts as it is let go, by the same rule as
    /// a finger's tap, and its right button uses the item. A virtual mouse feeds the real <see cref="InputService"/>,
    /// polled each frame as the player's driver polls it.
    /// </summary>
    public sealed class MouseControlsTests
    {
        private static readonly Vector2 Point = new(300f, 200f);

        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousRouting;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputService input;
        private GameTuning tuning;
        private readonly List<BoostInputMethod> boosts = new();
        private readonly List<InputMethod> moves = new();
        private int items;

        [SetUp]
        public void SetUp()
        {
            // The editor routes device input to the game only while the Game view has focus, and a batch run has none:
            // these settings send it there always, for this test alone.
            var settings = InputSystem.settings;
            previousRouting = settings.editorInputBehaviorInPlayMode;
            previousBackground = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            input = new InputService(new NoOnScreenControls(), tuning);
            input.Initialize();
            boosts.Clear();
            moves.Clear();
            items = 0;
            input.BoostPressed += boosts.Add;
            input.MoveUsed += moves.Add;
            input.ItemPressed += OnItem;
        }

        [TearDown]
        public void TearDown()
        {
            input.ItemPressed -= OnItem;
            input.MoveUsed -= moves.Add;
            input.BoostPressed -= boosts.Add;
            input.Dispose();
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
            InputSystem.settings.backgroundBehavior = previousBackground;
        }

        [UnityTest]
        public IEnumerator AShortClickBoostsAsItIsLetGo()
        {
            yield return SetMouse(Point, true);
            Assert.That(mouse.leftButton.isPressed, Is.True, "The virtual mouse's press did not reach the input system");
            Assert.That(Poll(), Is.True, "A held left button drives");
            Assert.That(boosts, Is.Empty, "The click boosts only as it is let go");
            yield return SetMouse(Point, false);
            Assert.That(Poll(), Is.False);
            Assert.That(boosts, Is.EqualTo(new[] { BoostInputMethod.Mouse }));
            Assert.That(moves, Is.EqualTo(new[] { InputMethod.Mouse }), "A click still reads as the mouse driving");
            Assert.That(items, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AClickBetweenTwoPollsStillBoosts()
        {
            QueueMouse(Point, true);
            QueueMouse(Point, false);
            yield return null;
            Poll();
            Poll();
            Assert.That(boosts, Is.EqualTo(new[] { BoostInputMethod.Mouse }), "Once, however often it is polled that frame");
            Assert.That(moves, Is.EqualTo(new[] { InputMethod.Mouse }));
        }

        [UnityTest]
        public IEnumerator AHoldDrivesWithoutBoosting()
        {
            yield return SetMouse(Point, true);
            var until = Time.unscaledTime + tuning.Controls.TapBoostTime + 0.15f;
            while (Time.unscaledTime < until) {
                Poll();
                yield return null;
            }

            yield return SetMouse(Point, false);
            Poll();
            Assert.That(boosts, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ADragOutAndBackDoesNotBoost()
        {
            var away = Point + Vector2.right * (tuning.Controls.TapBoostSlop * Mathf.Min(Screen.width, Screen.height) + 20f);
            yield return SetMouse(Point, true);
            Poll();
            yield return SetMouse(away, true);
            Poll();
            yield return SetMouse(Point, true);
            Poll();
            yield return SetMouse(Point, false);
            Poll();
            Assert.That(boosts, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TheRightButtonUsesTheItemAndDoesNotBoost()
        {
            yield return SetMouse(Point, false, true);
            Poll();
            yield return SetMouse(Point, false);
            Poll();
            Assert.That(items, Is.EqualTo(1));
            Assert.That(boosts, Is.Empty);
            Assert.That(moves, Is.Empty, "The right button does not drive");
        }

        [UnityTest]
        public IEnumerator ACancelledClickDoesNotBoost()
        {
            yield return SetMouse(Point, true);
            Poll();
            // The driver cancels the click that starts the first round.
            input.CancelTapBoost();
            yield return SetMouse(Point, false);
            Poll();
            Assert.That(boosts, Is.Empty);
        }

        private bool Poll() => input.TryGetDrag(out _);

        private void OnItem() => items++;

        private IEnumerator SetMouse(Vector2 position, bool left, bool right = false)
        {
            QueueMouse(position, left, right);
            // The event is taken in the next frame's input update, before the test resumes.
            yield return null;
        }

        private void QueueMouse(Vector2 position, bool left, bool right = false)
        {
            var state = new MouseState { position = position }
                .WithButton(MouseButton.Left, left)
                .WithButton(MouseButton.Right, right);
            InputSystem.QueueStateEvent(mouse, state);
        }
    }
}
#endif
