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
    /// it to start the round, and so does a short click of the left mouse button, by the same rule
    /// (<see cref="TapGesture"/>): the mouse has one button to drive and boost with, as a finger does, and its right
    /// button uses the item. The driver polls the drag; boost events carry their input method for analytics.
    /// </summary>
    public sealed class InputService : IInputService, IInitializable, IDisposable
    {
        public event Action<InputMethod> MoveUsed;
        public event Action<BoostInputMethod> BoostPressed;
        public event Action ItemPressed;

        private readonly InputSystemActions inputSystemActions = new();
        private readonly IOnScreenControls onScreenControls;
        private readonly GameTuning.ControlSettings controls;

        public Vector2 CurrentMoveInput => inputSystemActions.Player.Move.ReadValue<Vector2>();

        // Browsers on phones also report a tap as a mouse click; a mouse press this soon after a finger is that echo.
        private const float TouchEchoTime = 1f;

        private Finger steeringFinger;
        private Vector2 touchStart;
        private Vector2 touchPosition;
        private int touchDragId;
        private TapGesture touchTap;
        private TapGesture mouseTap;

        // Every press of the left button, from its event: a click that goes down and up within one frame leaves no
        // trace in the button's state, but its press still comes through an action.
        private readonly InputAction mouseLeft = new("MouseLeft", InputActionType.Button, "<Mouse>/leftButton");
        private int mousePresses;
        private int mousePressFrame = -1;

        // The presses the polls have dealt with, as a drag, a press on a button or a click; the rest are new.
        private int takenPresses;

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
            // A press on the screens over the game, like the settings button or an open popup, is theirs: the finger
            // neither steers nor boosts until it is lifted.
            if (onScreenControls.IsUiAt(touch.screenPosition)) {
                return;
            }

            // The item button sits beside boost: checked first, so a finger on it only ever uses the item.
            if (onScreenControls.IsItemAt(touch.screenPosition)) {
                ItemPressed?.Invoke();
                return;
            }

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
            touchTap.Begin(touchStart, touch.time, TapSlop);
            isMouseDragging = false;
            onScreenControls.BeginTouchSteering(touchStart);
            MoveUsed?.Invoke(InputMethod.Touch);
        }

        private void OnFingerMove(Finger finger)
        {
            lastTouchTime = Time.unscaledTime;
            if (finger == steeringFinger) {
                touchPosition = finger.lastTouch.screenPosition;
                touchTap.Move(touchPosition);
            }
        }

        private void OnFingerUp(Finger finger)
        {
            lastTouchTime = Time.unscaledTime;
            if (finger != steeringFinger) {
                return;
            }

            var touch = finger.lastTouch;
            var isTap = touchTap.End(touch.screenPosition, touch.time, controls.TapBoostTime) &&
                touch.phase == UnityEngine.InputSystem.TouchPhase.Ended;
            steeringFinger = null;
            if (isTap) {
                BoostPressed?.Invoke(BoostInputMethod.Tap);
            }
        }

        public void CancelTapBoost()
        {
            touchTap.Cancel();
            mouseTap.Cancel();
        }

        // The farthest a tap or a click may wander and still boost, in screen pixels.
        private float TapSlop => controls.TapBoostSlop * Mathf.Min(Screen.width, Screen.height);

        /// <summary>
        /// The mouse steers while its left button is held, as a finger does on a phone, and a short click of it boosts
        /// as it is let go, like a finger's tap. It is polled, so the release is noticed on the driver's next poll; a
        /// click that went down and up between two polls is taken whole. A press on the HUD's buttons or the screens
        /// over the game, or the echo of a touch, neither steers nor boosts.
        /// </summary>
        private bool TryGetMouseDrag(out Vector2 screenPosition, out bool isNew)
        {
            screenPosition = default;
            isNew = false;
            var mouse = Mouse.current;
            var isEcho = Time.unscaledTime - lastTouchTime < TouchEchoTime;
            if (mouse == null || !mouse.leftButton.isPressed || isEcho) {
                if (mouse != null && !isEcho) {
                    if (isMouseDragging) {
                        EndClick(mouse.position.ReadValue());
                    }
                    // Only this frame's: a press from while nothing polled (the result screen) is long over.
                    else if (!isMouseOnControl && mousePresses != takenPresses && mousePressFrame == Time.frameCount) {
                        TakeQuickClick(mouse.position.ReadValue());
                    }
                }

                takenPresses = mousePresses;
                mouseTap.Cancel();
                isMouseDragging = false;
                isMouseOnControl = false;
                return false;
            }

            if (isMouseOnControl) {
                return false;
            }

            screenPosition = mouse.position.ReadValue();
            // A press the driver did not poll the release of (it stops polling while the car is out) is a new one.
            if (isMouseDragging && mousePresses != takenPresses) {
                isMouseDragging = false;
            }

            if (!isMouseDragging) {
                takenPresses = mousePresses;
                if (IsOverControls(screenPosition)) {
                    isMouseOnControl = true;
                    return false;
                }

                isMouseDragging = true;
                isNew = true;
                mouseTap.Begin(screenPosition, Time.unscaledTimeAsDouble, TapSlop);
                MoveUsed?.Invoke(InputMethod.Mouse);
            }
            else {
                mouseTap.Move(screenPosition);
            }

            return true;
        }

        private bool IsOverControls(Vector2 screenPosition) => onScreenControls.IsControlAt(screenPosition) ||
            onScreenControls.IsItemAt(screenPosition) || onScreenControls.IsUiAt(screenPosition);

        private void EndClick(Vector2 screenPosition)
        {
            if (mouseTap.End(screenPosition, Time.unscaledTimeAsDouble, controls.TapBoostTime)) {
                BoostPressed?.Invoke(BoostInputMethod.Mouse);
            }
        }

        // Down and up between two polls: the click still counts as the drive's start (the first input of a round) and
        // as a boost, which the driver can cancel on the start as it does for a held click.
        private void TakeQuickClick(Vector2 screenPosition)
        {
            if (IsOverControls(screenPosition)) {
                return;
            }

            dragCount++;
            mouseTap.Begin(screenPosition, Time.unscaledTimeAsDouble, TapSlop);
            MoveUsed?.Invoke(InputMethod.Mouse);
            EndClick(screenPosition);
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
            inputSystemActions.Player.UseItem.performed += OnUseItemPerformed;
            onScreenControls.ItemPressed += OnScreenItemPressed;
            mouseLeft.performed += OnMouseLeftPressed;
            mouseLeft.Enable();
        }

        public void Dispose()
        {
            Touch.onFingerUp -= OnFingerUp;
            Touch.onFingerMove -= OnFingerMove;
            Touch.onFingerDown -= OnFingerDown;
            EnhancedTouchSupport.Disable();
            steeringFinger = null;
            onScreenControls.BoostPressed -= OnScreenBoostPressed;
            onScreenControls.ItemPressed -= OnScreenItemPressed;
            inputSystemActions.Player.UseItem.performed -= OnUseItemPerformed;
            inputSystemActions.Player.Boost.performed -= OnBoostPerformed;
            inputSystemActions.Player.Move.performed -= OnMovePerformed;
            inputSystemActions.Player.Disable();
            inputSystemActions.Dispose();
            mouseLeft.performed -= OnMouseLeftPressed;
            mouseLeft.Dispose();
        }

        private void OnMouseLeftPressed(InputAction.CallbackContext context)
        {
            mousePresses++;
            mousePressFrame = Time.frameCount;
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

        // The mouse boosts by a short click (TryGetMouseDrag), so the action only has keys and gamepads.
        private void OnBoostPerformed(InputAction.CallbackContext context)
        {
            var method = context.control.device is Keyboard ? BoostInputMethod.Key : BoostInputMethod.Gamepad;
            BoostPressed?.Invoke(method);
        }

        // The right mouse button uses the item, but not over the screens above the game (an open settings popup).
        private void OnUseItemPerformed(InputAction.CallbackContext context)
        {
            if (context.control.device is Mouse mouse && onScreenControls.IsUiAt(mouse.position.ReadValue())) {
                return;
            }

            ItemPressed?.Invoke();
        }

        private void OnScreenItemPressed()
        {
            // As for boost: a touch on the button came through OnFingerDown, and its echo as a click is left out.
            if (Time.unscaledTime - lastTouchTime >= TouchEchoTime) {
                ItemPressed?.Invoke();
            }
        }

        private void OnScreenBoostPressed()
        {
            // Touch button presses come through OnFingerDown; the HUD forwards only mouse/pen presses here.
            if (Time.unscaledTime - lastTouchTime >= TouchEchoTime) {
                BoostPressed?.Invoke(BoostInputMethod.Button);
            }
        }

        /// <summary>
        /// A press that boosts as it lifts when it was short and stayed put: a finger's tap or a mouse click. It keeps
        /// the greatest excursion, so a drag out and back is not a tap; a long hold or a cancel is not one either.
        /// </summary>
        private struct TapGesture
        {
            private Vector2 start;
            private double startTime;
            private float slopSquared;
            private bool isPossible;

            public void Begin(Vector2 position, double time, float slop)
            {
                start = position;
                startTime = time;
                slopSquared = slop * slop;
                isPossible = true;
            }

            public void Move(Vector2 position)
            {
                isPossible &= (position - start).sqrMagnitude <= slopSquared;
            }

            public void Cancel()
            {
                isPossible = false;
            }

            /// <returns>Whether the press that lifts here was a tap.</returns>
            public bool End(Vector2 position, double time, float maxTime)
            {
                Move(position);
                var isTap = isPossible && time - startTime <= maxTime;
                isPossible = false;
                return isTap;
            }
        }
    }
}
