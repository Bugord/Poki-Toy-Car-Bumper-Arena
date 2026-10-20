using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// Reads the move keys through the actions asset, and fingers and the mouse straight from their devices, so a drag
    /// knows which finger it follows. Polled: the car's driver asks every frame. Boost is a press, so it is an event:
    /// the Boost action (Space, the right mouse button, a gamepad's south button) and the on-screen button both raise
    /// <see cref="BoostPressed"/>.
    /// </summary>
    public sealed class InputService : IInputService, IInitializable, IDisposable
    {
        public event Action<InputMethod> MoveUsed;
        public event Action BoostPressed;

        private readonly InputSystemActions inputSystemActions = new();
        private readonly IOnScreenControls onScreenControls;

        public Vector2 CurrentMoveInput => inputSystemActions.Player.Move.ReadValue<Vector2>();

        // Browsers on phones also report a tap as a mouse click; a mouse press this soon after a finger is that echo.
        private const float TouchEchoTime = 1f;

        // The touch id of the steering finger, 0 while no finger steers.
        private int dragTouchId;

        // A finger that came down on an on-screen button; it never steers, even when it slides off the button.
        private int controlTouchId;

        private float lastTouchTime = float.NegativeInfinity;
        private bool isMouseDragging;

        // The left button went down on an on-screen button; like such a finger, it never steers until released.
        private bool isMouseOnControl;

        // Counts the drags, for PointerDrag.Id.
        private int dragCount;
        private Vector2 dragStart;

        public InputService(IOnScreenControls onScreenControls)
        {
            this.onScreenControls = onScreenControls;
        }

        public bool TryGetDrag(out PointerDrag drag)
        {
            if (TryGetTouchDrag(out var position, out var isNew) || TryGetMouseDrag(out position, out isNew)) {
                if (isNew) {
                    dragCount++;
                    dragStart = position;
                }

                drag = new PointerDrag(dragCount, dragStart, position, isMouseDragging);
                return true;
            }

            drag = default;
            return false;
        }

        private bool TryGetTouchDrag(out Vector2 screenPosition, out bool isNew)
        {
            screenPosition = default;
            isNew = false;
            var touchscreen = Touchscreen.current;
            if (touchscreen == null) {
                controlTouchId = 0;
                dragTouchId = 0;
                return false;
            }

            var isControlDown = false;
            var isDragDown = false;
            foreach (var touch in touchscreen.touches) {
                if (!touch.press.isPressed) {
                    continue;
                }

                lastTouchTime = Time.unscaledTime;
                var id = touch.touchId.ReadValue();
                if (id == controlTouchId) {
                    isControlDown = true;
                }
                else if (id == dragTouchId) {
                    screenPosition = touch.position.ReadValue();
                    isDragDown = true;
                }
            }

            if (!isControlDown) {
                controlTouchId = 0;
            }

            if (isDragDown) {
                return true;
            }

            // The finger is gone; the next one starts a new drag, unless it came down on an on-screen button.
            dragTouchId = 0;
            foreach (var touch in touchscreen.touches) {
                var id = touch.touchId.ReadValue();
                if (!touch.press.isPressed || id == controlTouchId) {
                    continue;
                }

                var position = touch.position.ReadValue();
                if (onScreenControls.IsControlAt(position)) {
                    controlTouchId = id;
                    continue;
                }

                dragTouchId = id;
                screenPosition = position;
                isNew = true;
                isMouseDragging = false;
                MoveUsed?.Invoke(InputMethod.Touch);
                return true;
            }

            return false;
        }

        // The mouse steers while its left button is held, as a finger does on a phone.
        private bool TryGetMouseDrag(out Vector2 screenPosition, out bool isNew)
        {
            screenPosition = default;
            isNew = false;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed || Time.unscaledTime - lastTouchTime < TouchEchoTime) {
                isMouseDragging = false;
                isMouseOnControl = false;
                return false;
            }

            if (isMouseOnControl) {
                return false;
            }

            screenPosition = mouse.position.ReadValue();
            if (!isMouseDragging) {
                if (onScreenControls.IsControlAt(screenPosition)) {
                    isMouseOnControl = true;
                    return false;
                }

                isMouseDragging = true;
                isNew = true;
                MoveUsed?.Invoke(InputMethod.Mouse);
            }

            return true;
        }

        public void Initialize()
        {
            inputSystemActions.Player.Enable();
            inputSystemActions.Player.Move.performed += OnMovePerformed;
            inputSystemActions.Player.Boost.performed += OnBoostPerformed;
            onScreenControls.BoostPressed += OnScreenBoostPressed;
        }

        public void Dispose()
        {
            onScreenControls.BoostPressed -= OnScreenBoostPressed;
            inputSystemActions.Player.Boost.performed -= OnBoostPerformed;
            inputSystemActions.Player.Move.performed -= OnMovePerformed;
            inputSystemActions.Player.Disable();
            inputSystemActions.Dispose();
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            switch (context.control.device) {
                case Keyboard:
                    MoveUsed?.Invoke(InputMethod.Keyboard);
                    break;
                case Gamepad:
                case Joystick:
                    MoveUsed?.Invoke(InputMethod.Gamepad);
                    break;
            }
        }

        private void OnBoostPerformed(InputAction.CallbackContext context)
        {
            BoostPressed?.Invoke();
        }

        private void OnScreenBoostPressed()
        {
            BoostPressed?.Invoke();
        }
    }
}
