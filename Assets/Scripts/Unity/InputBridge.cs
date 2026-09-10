// Picks the input backend at compile time. The classic Input Manager is read whenever Unity has it
// enabled (Player > Active Input Handling: "Input Manager" or "Both"). When only the Input System
// package is active, its Mouse/Keyboard devices are read instead; CYBER_INPUT_SYSTEM is defined by
// the assembly definition when the package is installed.
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER && CYBER_INPUT_SYSTEM
#define CYBER_USE_INPUT_SYSTEM
#endif

using UnityEngine;
#if CYBER_USE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace CyberRider.Unity
{
    /// <summary>
    /// Mouse and keyboard reads that work with either Unity input backend. Keys are named with the
    /// classic <see cref="KeyCode"/> values and translated when the Input System package is in use.
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
#else
        public static Vector3 MousePosition => Input.mousePosition;

        public static float ScrollDelta => Input.mouseScrollDelta.y;

        public static bool MouseButton(int index) => Input.GetMouseButton(index);

        public static bool MouseButtonDown(int index) => Input.GetMouseButtonDown(index);

        public static bool MouseButtonUp(int index) => Input.GetMouseButtonUp(index);

        public static bool IsKey(KeyCode code) => Input.GetKey(code);

        public static bool KeyDown(KeyCode code) => Input.GetKeyDown(code);
#endif
    }
}
