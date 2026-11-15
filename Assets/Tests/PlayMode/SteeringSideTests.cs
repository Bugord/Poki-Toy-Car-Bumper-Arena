using NUnit.Framework;
using ToyCarArena.UI;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The boost slot, and the item button in it, stays opposite the steering finger: chosen where steering begins, and
    /// changed when the finger is dragged well past the middle (the v0.18.0 playtest's player steered over the button),
    /// but not by a thumb that wanders about the middle.
    /// </summary>
    public sealed class SteeringSideTests
    {
        [Test]
        public void DraggingTheStickPastTheMiddleMovesTheButtonAcross()
        {
            var controls = new HudControls();
            var width = Screen.width;
            var y = Screen.height * 0.5f;
            var left = new Vector2(width * 0.2f, y);
            controls.BeginTouchSteering(left);
            Assert.That(controls.IsBoostOnLeft, Is.False, "steering on the left puts the button on the right");

            controls.ShowJoystick(left, new Vector2(width * 0.55f, y), 100f);
            Assert.That(controls.IsBoostOnLeft, Is.False, "just past the middle is not enough");

            controls.ShowJoystick(left, new Vector2(width * 0.8f, y), 100f);
            Assert.That(controls.IsBoostOnLeft, Is.True, "the finger on the right puts the button on the left");

            controls.ShowJoystick(left, new Vector2(width * 0.45f, y), 100f);
            Assert.That(controls.IsBoostOnLeft, Is.True, "back just past the middle keeps it");

            controls.ShowJoystick(left, new Vector2(width * 0.1f, y), 100f);
            Assert.That(controls.IsBoostOnLeft, Is.False);
        }
    }
}
