// Minimal stand-ins for the UnityEngine API surface this project uses. They exist only so the
// Unity layer can be type-checked outside the editor; nothing here runs.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static float Distance(Vector2 a, Vector2 b) => (float)System.Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public struct Quaternion
    {
        public static Quaternion identity => new Quaternion();
        public static Quaternion Euler(float x, float y, float z) => new Quaternion();
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color clear => new Color(0, 0, 0, 0);
        public static implicit operator Color32(Color c) => new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(c.a * 255));
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color32 Lerp(Color32 a, Color32 b, float t) => a;
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
    }

    public sealed class RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Rad2Deg = 57.29578f;
        public const float Deg2Rad = 0.0174532924f;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Abs(float v) => Math.Abs(v);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object obj) { }
        public static void DontDestroyOnLoad(Object obj) { }
        public static T FindObjectOfType<T>() where T : Object => null;
        public static implicit operator bool(Object o) => o != null;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => base.GetHashCode();
    }

    public class GameObject : Object
    {
        public Transform transform { get; } = new Transform();
        public string tag { get; set; }
        public int layer { get; set; }
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] components) { this.name = name; }
        public T AddComponent<T>() where T : Component, new() => new T { gameObject = this };
        public T GetComponent<T>() where T : Component => null;
        public T GetComponentInChildren<T>() where T : Component => null;
        public void SetActive(bool v) { }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; set; } = new GameObject();
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => null;
        public T GetComponentInChildren<T>() where T : Component => null;
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion localRotation { get; set; }
        public Transform parent { get; set; }
        public int childCount { get; }
        public Transform GetChild(int i) => null;
        public void SetParent(Transform parent, bool worldPositionStays) { }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect { get; }
    }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    public class Camera : Behaviour
    {
        public static Camera main { get; }
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public float depth { get; set; }
    }

    public class AudioListener : Behaviour
    {
    }

    namespace Rendering
    {
        public enum IndexFormat { UInt16, UInt32 }
        public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    }

    public class Mesh : Object
    {
        public Rendering.IndexFormat indexFormat { get; set; }
        public void MarkDynamic() { }
        public void Clear(bool keepVertexLayout) { }
        public void SetVertices(List<Vector3> v) { }
        public void SetColors(List<Color32> c) { }
        public void SetTriangles(List<int> t, int submesh, bool calculateBounds) { }
        public void RecalculateBounds() { }
    }

    public class MeshFilter : Component
    {
        public Mesh sharedMesh { get; set; }
        public Mesh mesh { get; set; }
    }

    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public bool enabled { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
    }

    public class MeshRenderer : Renderer
    {
    }

    public class Shader : Object
    {
        public static Shader Find(string name) => null;
    }

    public class Material : Object
    {
        public Material(Shader s) { }
        public int renderQueue { get; set; }
        public Color color { get; set; }
        public void SetColor(string name, Color c) { }
    }

    public class Texture : Object
    {
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }

    public enum TextureFormat { RGBA32 = 4 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h, TextureFormat f, bool mipChain) { }
        public void SetPixels32(Color32[] px) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }

    public class Font : Object
    {
        public Material material { get; }
    }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum TextAlignment { Left, Center, Right }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }

    public class TextMesh : Component
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public float characterSize { get; set; }
        public TextAnchor anchor { get; set; }
        public TextAlignment alignment { get; set; }
        public Color color { get; set; }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }

    public static class Random
    {
        public static float value { get; }
        public static float Range(float a, float b) => a;
        public static int Range(int a, int b) => a;
    }

    public static class Screen
    {
        public static int width { get; }
        public static int height { get; }
        public static float dpi { get; }
        public static Rect safeArea { get; }
        public static int sleepTimeout { get; set; }
    }

    public static class SleepTimeout
    {
        public const int NeverSleep = -1;
        public const int SystemSetting = -2;
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch
    {
        public int fingerId;
        public Vector2 position;
        public Vector2 deltaPosition;
        public TouchPhase phase;
    }

    public static class Time
    {
        public static float deltaTime { get; }
        public static float unscaledDeltaTime { get; }
        public static float time { get; }
        public static int frameCount { get; }
    }

    public enum KeyCode
    {
        None, Backspace, Tab, Return, Escape, Space, Comma, Minus, Period, Equals,
        Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        UpArrow, DownArrow, RightArrow, LeftArrow, LeftShift, RightShift, LeftControl, RightControl, LeftCommand, RightCommand, LeftAlt, RightAlt,
    }

    public static class Input
    {
        public static Vector3 mousePosition { get; }
        public static Vector2 mouseScrollDelta { get; }
        public static bool GetMouseButton(int b) => false;
        public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButtonUp(int b) => false;
        public static bool GetKey(KeyCode k) => false;
        public static bool GetKeyDown(KeyCode k) => false;
        public static int touchCount { get; }
        public static bool touchSupported { get; }
        public static Touch GetTouch(int i) => default;
    }

    public static class Application
    {
        public static string persistentDataPath { get; }
        public static int targetFrameRate { get; set; }
        public static bool isMobilePlatform { get; }
        public static void Quit() { }
    }

    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogWarning(object m) { }
        public static void LogError(object m) { }
        public static void LogException(Exception e) { }
    }

    public class AudioClip : Object
    {
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => new AudioClip();
        public bool SetData(float[] data, int offsetSamples) => true;
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool loop { get; set; }
        public float volume { get; set; }
        public bool playOnAwake { get; set; }
        public bool isPlaying { get; }
        public void Play() { }
        public void Stop() { }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
    }

    public static class AudioSettings
    {
        public static double dspTime { get; }
        public static int outputSampleRate { get; }
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; }
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public Camera worldCamera { get; set; }
    }

    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    namespace Events
    {
        public delegate void UnityAction();

        public class UnityEvent
        {
            public void AddListener(UnityAction a) { }
            public void RemoveAllListeners() { }
            public void Invoke() { }
        }
    }

    namespace EventSystems
    {
        public class EventSystem : MonoBehaviour
        {
            public static EventSystem current { get; }
            public bool IsPointerOverGameObject() => false;
            public bool IsPointerOverGameObject(int pointerId) => false;
            public void RaycastAll(PointerEventData data, System.Collections.Generic.List<RaycastResult> results) { }
        }

        public class BaseEventData
        {
            public BaseEventData(EventSystem es) { }
        }

        public class PointerEventData : BaseEventData
        {
            public PointerEventData(EventSystem es) : base(es) { }
            public Vector2 position { get; set; }
        }

        public struct RaycastResult
        {
            public GameObject gameObject;
        }

        public class StandaloneInputModule : MonoBehaviour
        {
        }
    }

    namespace UI
    {
        public class Graphic : MonoBehaviour
        {
            public Color color { get; set; }
            public bool raycastTarget { get; set; }
            public RectTransform rectTransform { get; } = new RectTransform();
        }

        public class MaskableGraphic : Graphic
        {
        }

        public class Image : MaskableGraphic
        {
        }

        public class RawImage : MaskableGraphic
        {
            public Texture texture { get; set; }
        }

        public class Text : MaskableGraphic
        {
            public string text { get; set; }
            public Font font { get; set; }
            public int fontSize { get; set; }
            public TextAnchor alignment { get; set; }
            public FontStyle fontStyle { get; set; }
            public HorizontalWrapMode horizontalOverflow { get; set; }
            public VerticalWrapMode verticalOverflow { get; set; }
            public bool supportRichText { get; set; }
            public bool resizeTextForBestFit { get; set; }
            public float lineSpacing { get; set; }
        }

        public struct ColorBlock
        {
            public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
            public float colorMultiplier, fadeDuration;
        }

        public class Selectable : MonoBehaviour
        {
            public Graphic targetGraphic { get; set; }
            public ColorBlock colors { get; set; }
            public bool interactable { get; set; }
        }

        public class Button : Selectable
        {
            public class ButtonClickedEvent : Events.UnityEvent { }
            public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
        }

        public class InputField : Selectable
        {
            public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
            public string text { get; set; }
            public Text textComponent { get; set; }
            public Graphic placeholder { get; set; }
            public LineType lineType { get; set; }
            public int characterLimit { get; set; }
        }

        public class LayoutElement : MonoBehaviour
        {
            public float preferredWidth { get; set; }
            public float preferredHeight { get; set; }
            public float minWidth { get; set; }
            public float minHeight { get; set; }
            public float flexibleWidth { get; set; }
            public float flexibleHeight { get; set; }
            public bool ignoreLayout { get; set; }
        }

        public class LayoutGroup : MonoBehaviour
        {
            public RectOffset padding { get; set; }
            public TextAnchor childAlignment { get; set; }
        }

        public class HorizontalOrVerticalLayoutGroup : LayoutGroup
        {
            public float spacing { get; set; }
            public bool childForceExpandWidth { get; set; }
            public bool childForceExpandHeight { get; set; }
            public bool childControlWidth { get; set; }
            public bool childControlHeight { get; set; }
        }

        public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
        public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

        public class GridLayoutGroup : LayoutGroup
        {
            public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
            public Constraint constraint { get; set; }
            public int constraintCount { get; set; }
            public Vector2 cellSize { get; set; }
            public Vector2 spacing { get; set; }
        }

        public class ContentSizeFitter : MonoBehaviour
        {
            public enum FitMode { Unconstrained, MinSize, PreferredSize }
            public FitMode horizontalFit { get; set; }
            public FitMode verticalFit { get; set; }
        }

        public class ScrollRect : MonoBehaviour
        {
            public enum MovementType { Unrestricted, Elastic, Clamped }
            public RectTransform content { get; set; }
            public RectTransform viewport { get; set; }
            public bool horizontal { get; set; }
            public bool vertical { get; set; }
            public MovementType movementType { get; set; }
            public float scrollSensitivity { get; set; }
            public float verticalNormalizedPosition { get; set; }
        }

        public class RectMask2D : MonoBehaviour { }

        public class CanvasScaler : MonoBehaviour
        {
            public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
            public ScaleMode uiScaleMode { get; set; }
            public Vector2 referenceResolution { get; set; }
            public float matchWidthOrHeight { get; set; }
            public float scaleFactor { get; set; }
        }

        public class GraphicRaycaster : MonoBehaviour { }
    }
}

// ---------------------------------------------------------------- Input System package (com.unity.inputsystem)

namespace UnityEngine.InputSystem
{
    public enum Key
    {
        None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
        LeftShift, RightShift, LeftAlt, RightAlt, LeftCtrl, RightCtrl, LeftMeta, RightMeta, ContextMenu,
        Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause,
    }

    public class Keyboard
    {
        public static Keyboard current { get; }
        public Controls.KeyControl this[Key key] => null;
    }

    public class Touchscreen
    {
        public static Touchscreen current { get; }
        public Utilities.ReadOnlyArray<Controls.TouchControl> touches { get; }
    }

    public class Mouse
    {
        public static Mouse current { get; }
        public Controls.Vector2Control position { get; }
        public Controls.DeltaControl scroll { get; }
        public Controls.ButtonControl leftButton { get; }
        public Controls.ButtonControl rightButton { get; }
        public Controls.ButtonControl middleButton { get; }
    }

    namespace Controls
    {
        public class ButtonControl
        {
            public bool isPressed { get; }
            public bool wasPressedThisFrame { get; }
            public bool wasReleasedThisFrame { get; }
        }

        public class KeyControl : ButtonControl
        {
        }

        public class Vector2Control
        {
            public Vector2 ReadValue() => default;
        }

        public class DeltaControl : Vector2Control
        {
        }

        public class IntegerControl
        {
            public int ReadValue() => 0;
        }

        public class TouchControl
        {
            public ButtonControl press { get; }
            public IntegerControl touchId { get; }
            public Vector2Control position { get; }
        }
    }

    namespace Utilities
    {
        public struct ReadOnlyArray<T>
        {
            public int Count => 0;
            public T this[int index] => default;
        }
    }

    namespace UI
    {
        public class InputSystemUIInputModule : MonoBehaviour
        {
        }
    }
}
