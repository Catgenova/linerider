// Picks the input backend at compile time. The classic Input Manager is read whenever Unity has it
// enabled (Player > Active Input Handling: "Input Manager" or "Both"). When only the Input System
// package is active, its Mouse/Keyboard/Touchscreen devices are read instead; CYBER_INPUT_SYSTEM is
// defined by the assembly definition when the package is installed.
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER && CYBER_INPUT_SYSTEM
#define CYBER_USE_INPUT_SYSTEM
#endif

using System.Collections.Generic;
using UnityEngine;
#if CYBER_USE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace CyberRider.Unity
{
    /// <summary>One finger on the screen this frame. Positions are screen pixels, origin bottom-left.</summary>
    public struct TouchPoint
    {
        public int Id;
        public Vector2 Position;
        public bool Began;
        public bool Ended;
    }

    /// <summary>
    /// Mouse, keyboard and touch reads that work with either Unity input backend. Keys are named with
    /// the classic <see cref="KeyCode"/> values and translated when the Input System package is in use.
    /// </summary>
    public static class InputBridge
    {
        /// <summary>True when the Input System package is being read instead of UnityEngine.Input.</summary>
        public static bool UsingInputSystem =>
#if CYBER_USE_INPUT_SYSTEM
            true;
#else
            false;
#endif

        /// <summary>Add the uGUI input module that matches the active backend to an EventSystem object.</summary>
        public static void AddUiModule(GameObject eventSystem)
        {
#if CYBER_USE_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

#if CYBER_USE_INPUT_SYSTEM
        /// <summary>Pointer position in screen pixels, origin bottom-left (the UnityEngine.Input convention).</summary>
        public static Vector3 MousePosition
        {
            get
            {
                Mouse m = Mouse.current;
                if (m == null) return Vector3.zero;
                Vector2 p = m.position.ReadValue();
                return new Vector3(p.x, p.y, 0);
            }
        }

        /// <summary>Wheel movement this frame in notches. Windows reports raw 120-per-notch deltas; fold those down.</summary>
        public static float ScrollDelta
        {
            get
            {
                Mouse m = Mouse.current;
                if (m == null) return 0;
                float y = m.scroll.ReadValue().y;
                if (Mathf.Abs(y) > 20f) y /= 120f;
                return y;
            }
        }

        private static ButtonControl Button(int index)
        {
            Mouse m = Mouse.current;
            if (m == null) return null;
            switch (index)
            {
                case 0: return m.leftButton;
                case 1: return m.rightButton;
                case 2: return m.middleButton;
                default: return null;
            }
        }

        public static bool MouseButton(int index) => Button(index)?.isPressed ?? false;

        public static bool MouseButtonDown(int index) => Button(index)?.wasPressedThisFrame ?? false;

        public static bool MouseButtonUp(int index) => Button(index)?.wasReleasedThisFrame ?? false;

        private static KeyControl Control(KeyCode code)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return null;
            Key key = Translate(code);
            return key == Key.None ? null : kb[key];
        }

        public static bool IsKey(KeyCode code) => Control(code)?.isPressed ?? false;

        public static bool KeyDown(KeyCode code) => Control(code)?.wasPressedThisFrame ?? false;

        /// <summary>Map the classic key codes this game binds onto Input System keys.</summary>
        private static Key Translate(KeyCode code)
        {
            if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (code - KeyCode.A);
            if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9) return Key.Digit1 + (code - KeyCode.Alpha1);
            switch (code)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftCommand: return Key.LeftMeta;
                case KeyCode.RightCommand: return Key.RightMeta;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                default: return Key.None;
            }
        }

        public static bool TouchSupported => Touchscreen.current != null;

        private static int _touchFrame = -1;
        private static readonly List<TouchPoint> _touches = new List<TouchPoint>();

        /// <summary>Collect the fingers that are down, or lifted this frame, once per frame.</summary>
        private static void RefreshTouches()
        {
            if (_touchFrame == Time.frameCount) return;
            _touchFrame = Time.frameCount;
            _touches.Clear();
            Touchscreen ts = Touchscreen.current;
            if (ts == null) return;
            var controls = ts.touches;
            for (int i = 0; i < controls.Count; i++)
            {
                TouchControl t = controls[i];
                bool pressed = t.press.isPressed;
                bool released = t.press.wasReleasedThisFrame;
                if (!pressed && !released) continue;
                _touches.Add(new TouchPoint
                {
                    Id = t.touchId.ReadValue(),
                    Position = t.position.ReadValue(),
                    Began = t.press.wasPressedThisFrame,
                    Ended = released && !pressed,
                });
            }
        }

        /// <summary>Fingers down or lifted this frame.</summary>
        public static int TouchCount
        {
            get
            {
                RefreshTouches();
                return _touches.Count;
            }
        }

        public static TouchPoint GetTouch(int index)
        {
            RefreshTouches();
            return _touches[index];
        }
#else
        public static Vector3 MousePosition => Input.mousePosition;

        public static float ScrollDelta => Input.mouseScrollDelta.y;

        public static bool MouseButton(int index) => Input.GetMouseButton(index);

        public static bool MouseButtonDown(int index) => Input.GetMouseButtonDown(index);

        public static bool MouseButtonUp(int index) => Input.GetMouseButtonUp(index);

        public static bool IsKey(KeyCode code) => Input.GetKey(code);

        public static bool KeyDown(KeyCode code) => Input.GetKeyDown(code);

        public static bool TouchSupported => Input.touchSupported;

        /// <summary>Fingers down or lifted this frame.</summary>
        public static int TouchCount => Input.touchCount;

        public static TouchPoint GetTouch(int index)
        {
            Touch t = Input.GetTouch(index);
            return new TouchPoint
            {
                Id = t.fingerId,
                Position = t.position,
                Began = t.phase == TouchPhase.Began,
                Ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled,
            };
        }
#endif
    }
}
