using System;
using UnityEngine;
using UnityEngine.UI;

namespace CyberRider.Unity
{
    /// <summary>Small helpers for building uGUI in code with the neon look.</summary>
    public static class UiKit
    {
        public static Font Font;
        /// <summary>Phone/tablet layout: finger-sized buttons and a denser HUD.</summary>
        public static bool Compact;
        /// <summary>Canvas scale (screen pixels per UI unit) chosen by <see cref="ComputeScale"/>.</summary>
        public static float Scale = 1f;

        public static float CanvasWidth => Screen.width / Scale;

        public static float CanvasHeight => Screen.height / Scale;

        /// <summary>Width of the notch-free part of the screen in UI units.</summary>
        public static float SafeWidth => Screen.safeArea.width / Scale;

        /// <summary>
        /// Pick the canvas scale for the current screen. Desktops scale with the window like a
        /// 1280 x 760 reference layout; handhelds scale with pixel density so buttons stay finger-sized,
        /// while keeping at least 760 x 400 units of canvas for the layout.
        /// </summary>
        public static float ComputeScale()
        {
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);
            float s;
            if (Compact)
            {
                float dpi = Screen.dpi > 0 ? Screen.dpi : 400f;
                s = Mathf.Clamp(dpi / 175f, 1.2f, 4f);
                s = Mathf.Min(s, Mathf.Min(w / 760f, h / 400f));
                s = Mathf.Max(s, 1f);
            }
            else s = Mathf.Sqrt((w / 1280f) * (h / 760f));
            Scale = s;
            return s;
        }

        /// <summary>Anchor a full-canvas rect to the screen's safe area (clear of notches and rounded corners).</summary>
        public static void ApplySafeArea(RectTransform rt)
        {
            Rect sa = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);
            rt.anchorMin = new Vector2(sa.x / w, sa.y / h);
            rt.anchorMax = new Vector2((sa.x + sa.width) / w, (sa.y + sa.height) / h);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        public static readonly Color Cyan = U.Hex("#39f6ff");
        public static readonly Color Magenta = U.Hex("#ff2bd6");
        public static readonly Color Lime = U.Hex("#c6ff4a");
        public static readonly Color Yellow = U.Hex("#ffe93a");
        public static readonly Color Pink = U.Hex("#ff3d7f");
        public static readonly Color Text = U.Hex("#e8ecff");
        public static readonly Color Muted = U.Hex("#8a92c8");
        public static readonly Color PanelColor = new Color(8 / 255f, 4 / 255f, 28 / 255f, 0.9f);
        public static readonly Color PanelBorder = new Color(57 / 255f, 246 / 255f, 255 / 255f, 0.25f);
        public static readonly Color ButtonBg = new Color(10 / 255f, 6 / 255f, 34 / 255f, 0.85f);

        public static void Init()
        {
            if (Font != null) return;
            try
            {
                Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                Font = null;
            }
            if (Font == null)
            {
                try
                {
                    Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (Exception)
                {
                    Font = null;
                }
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchor to all four edges with pixel insets.</summary>
        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Anchor at a normalised point with a pixel offset and size.</summary>
        public static void Place(RectTransform rt, float ax, float ay, float px, float py, float w, float h, float pivotX = -1, float pivotY = -1)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(pivotX < 0 ? ax : pivotX, pivotY < 0 ? ay : pivotY);
            rt.anchoredPosition = new Vector2(px, py);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        public static Image Border(Transform parent, Color color, float thickness = 1f)
        {
            // Four thin edges around the parent rect.
            Image last = null;
            for (int i = 0; i < 4; i++)
            {
                RectTransform rt = Rect("Border", parent);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                switch (i)
                {
                    case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, thickness); rt.anchoredPosition = Vector2.zero; break;
                    case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(0, thickness); rt.anchoredPosition = Vector2.zero; break;
                    case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(thickness, 0); rt.anchoredPosition = Vector2.zero; break;
                    default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(thickness, 0); rt.anchoredPosition = Vector2.zero; break;
                }
                last = img;
            }
            return last;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            RectTransform rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        public static LayoutElement Size(Component c, float preferredWidth = -1, float preferredHeight = -1, float minWidth = -1, float flexibleWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (minWidth >= 0) le.minWidth = minWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static Button Button(Transform parent, string label, Action onClick, Color? accent = null, int fontSize = 13, float height = 30, float minWidth = 0, bool primary = false)
        {
            Color acc = accent ?? (primary ? Magenta : Cyan);
            if (Compact) height = Mathf.Max(height, 36);
            RectTransform rt = Rect("Button", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = primary ? new Color(acc.r, acc.g, acc.b, 0.22f) : ButtonBg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            Border(rt, new Color(acc.r, acc.g, acc.b, primary ? 0.9f : 0.35f));
            Text t = Label(rt, label, fontSize, primary ? Color.white : Text, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 10, 2, 10, 2);
            var le = Size(rt, -1, height, minWidth);
            le.minHeight = height;
            return btn;
        }

        public static VerticalLayoutGroup VBox(Transform parent, string name, float spacing, int padding = 0, bool fit = true, TextAnchor align = TextAnchor.UpperLeft)
        {
            RectTransform rt = Rect(name, parent);
            var lg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            lg.spacing = spacing;
            lg.padding = new RectOffset(padding, padding, padding, padding);
            lg.childAlignment = align;
            lg.childForceExpandWidth = true;
            lg.childForceExpandHeight = false;
            lg.childControlWidth = true;
            lg.childControlHeight = true;
            if (fit)
            {
                var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return lg;
        }

        public static HorizontalLayoutGroup HBox(Transform parent, string name, float spacing, int padding = 0, TextAnchor align = TextAnchor.MiddleLeft, bool expandWidth = false)
        {
            RectTransform rt = Rect(name, parent);
            var lg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            lg.spacing = spacing;
            lg.padding = new RectOffset(padding, padding, padding, padding);
            lg.childAlignment = align;
            lg.childForceExpandWidth = expandWidth;
            lg.childForceExpandHeight = false;
            lg.childControlWidth = true;
            lg.childControlHeight = true;
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return lg;
        }

        public static GridLayoutGroup Grid(Transform parent, string name, float cellW, float cellH, float spacing, int padding = 0, bool fit = true)
        {
            RectTransform rt = Rect(name, parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, cellH);
            g.spacing = new Vector2(spacing, spacing);
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childAlignment = TextAnchor.UpperLeft;
            if (fit)
            {
                var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return g;
        }

        /// <summary>A vertical scroll view; returns the content transform to fill.</summary>
        public static RectTransform ScrollList(Transform parent, string name)
        {
            RectTransform root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Rect("Viewport", root);
            Stretch(viewport);
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            var vimg = viewport.gameObject.AddComponent<Image>();
            vimg.color = new Color(0, 0, 0, 0.01f);
            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 0);
            var lg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            lg.spacing = 8;
            lg.padding = new RectOffset(8, 8, 8, 8);
            lg.childForceExpandWidth = true;
            lg.childForceExpandHeight = false;
            lg.childControlWidth = true;
            lg.childControlHeight = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return content;
        }

        public static InputField Input(Transform parent, string placeholder, string initial = "", bool multiline = false, float height = 30)
        {
            RectTransform rt = Rect("Input", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.45f);
            Border(rt, PanelBorder);
            var field = rt.gameObject.AddComponent<InputField>();
            Text text = Label(rt, "", 14, Text, TextAnchor.UpperLeft);
            Stretch(text.rectTransform, 8, 6, 8, 6);
            text.supportRichText = false;
            Text ph = Label(rt, placeholder, 14, new Color(Muted.r, Muted.g, Muted.b, 0.7f), TextAnchor.UpperLeft);
            Stretch(ph.rectTransform, 8, 6, 8, 6);
            ph.fontStyle = FontStyle.Italic;
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = img;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.text = initial;
            var le = Size(rt, -1, height);
            le.minHeight = height;
            return field;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static Text Spacer(Transform parent, float height)
        {
            Text t = Label(parent, "", 4, Color.clear);
            Size(t, -1, height);
            return t;
        }
    }
}
