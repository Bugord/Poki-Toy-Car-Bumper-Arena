using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using VContainer.Unity;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// Reads keys through actions, the mouse from its device, and touch events through Enhanced Touch so even a tap
    /// between frames is kept. Each touch keeps its role until release: the first off a button steers, the others
    /// boost on press and never take over. A short steering touch also boosts on release unless the driver consumes
    /// it to start the round. The driver polls the drag; boost events carry their input method for analytics.
    /// </summary>
    public sealed class InputService : IInputService, IInitializable, IDisposable
    {
        public event Action<InputMethod> MoveUsed;
        public event Action<BoostInputMethod> BoostPressed;

        private readonly InputSystemActions inputSystemActions = new();
        private readonly IOnScreenControls onScreenControls;
        private readonly GameTuning.ControlSettings controls;

        public Vector2 CurrentMoveInput => inputSystemActions.Player.Move.ReadValue<Vector2>();

        // Browsers on phones also report a tap as a mouse click; a mouse press this soon after a finger is that echo.
        private const float TouchEchoTime = 1f;

        private Finger steeringFinger;
        private Vector2 touchStart;
        private Vector2 touchPosition;
        private double touchStartTime;
        private float tapSlopSquared;
        private bool canTapBoost;
        private int touchDragId;

        private float lastTouchTime = float.NegativeInfinity;
        private bool isMouseDragging;

        // The left button went down on an on-screen button; like such a finger, it never steers until released.
        private bool isMouseOnControl;

        // Counts the drags, for PointerDrag.Id.
        private int dragCount;
        private Vector2 dragStart;

        public InputService(IOnScreenControls onScreenControls, GameTuning tuning)
        {
            this.onScreenControls = onScreenControls;
            controls = tuning.Controls;
        }

        public bool TryGetDrag(out PointerDrag drag)
        {
            if (steeringFinger != null) {
                lastTouchTime = Time.unscaledTime;
                drag = new PointerDrag(touchDragId, touchStart, touchPosition, false);
                return true;
            }

            if (TryGetMouseDrag(out var position, out var isNew)) {
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

        private void OnFingerDown(Finger finger)
        {
            lastTouchTime = Time.unscaledTime;
            var touch = finger.lastTouch;
            if (onScreenControls.IsControlAt(touch.screenPosition)) {
                BoostPressed?.Invoke(BoostInputMethod.Button);
                return;
            }

            if (steeringFinger != null) {
                BoostPressed?.Invoke(BoostInputMethod.Finger);
                return;
            }

            steeringFinger = finger;
            touchDragId = ++dragCount;
            touchStart = touchPosition = touch.screenPosition;
            touchStartTime = touch.time;
            var slop = controls.TapBoostSlop * Mathf.Min(Screen.width, Screen.height);
            tapSlopSquared = slop * slop;
            canTapBoost = true;
            isMouseDragging = false;
            onScreenControls.BeginTouchSteering(touchStart);
            MoveUsed?.Invoke(InputMethod.Touch);
        }

        private void OnFingerMove(Finger finger)
        {
            lastTouchTime = Time.unscaledTime;
            if (finger == steeringFinger) {
                touchPosition = finger.lastTouch.screenPosition;
                // Keep the greatest excursion: a drag out and back is not a tap.
                canTapBoost &= (touchPosition - touchStart).sqrMagnitude <= tapSlopSquared;
            }
        }

        private void OnFingerUp(Finger finger)
        {
            lastTouchTime = Time.unscaledTime;
            if (finger != steeringFinger) {
                return;
            }

            var touch = finger.lastTouch;
            var isTap = canTapBoost && touch.phase == UnityEngine.InputSystem.TouchPhase.Ended &&
                touch.time - touchStartTime <= controls.TapBoostTime &&
                (touch.screenPosition - touchStart).sqrMagnitude <= tapSlopSquared;
            steeringFinger = null;
            canTapBoost = false;
            if (isTap) {
                BoostPressed?.Invoke(BoostInputMethod.Tap);
            }
        }

        public void CancelTapBoost()
        {
            canTapBoost = false;
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
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += OnFingerDown;
            Touch.onFingerMove += OnFingerMove;
            Touch.onFingerUp += OnFingerUp;
            inputSystemActions.Player.Enable();
            inputSystemActions.Player.Move.performed += OnMovePerformed;
            inputSystemActions.Player.Boost.performed += OnBoostPerformed;
            onScreenControls.BoostPressed += OnScreenBoostPressed;
        }

        public void Dispose()
        {
            Touch.onFingerUp -= OnFingerUp;
            Touch.onFingerMove -= OnFingerMove;
            Touch.onFingerDown -= OnFingerDown;
            EnhancedTouchSupport.Disable();
            steeringFinger = null;
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
            var method = context.control.device is Keyboard ? BoostInputMethod.Key :
                context.control.device is Mouse ? BoostInputMethod.Mouse : BoostInputMethod.Gamepad;
            BoostPressed?.Invoke(method);
        }

        private void OnScreenBoostPressed()
        {
            // Touch button presses come through OnFingerDown; the HUD forwards only mouse/pen presses here.
            if (Time.unscaledTime - lastTouchTime >= TouchEchoTime) {
                BoostPressed?.Invoke(BoostInputMethod.Button);
            }
        }
    }
}
