using System.Collections.Generic;
using CyberRider.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CyberRider.Unity
{
    /// <summary>
    /// Mouse, touch and keyboard handling for the editor, camera and playback shortcuts. Reads go
    /// through <see cref="InputBridge"/> so either Unity input backend works. On a touchscreen one
    /// finger drives the current tool and two fingers pan and pinch-zoom.
    /// </summary>
    public sealed class InputController
    {
        private readonly GameController _game;
        private readonly Screens _screens;
        private bool _panning;
        private Vector3 _lastMouse;
        private bool _drawing;
        private int _drawTouch = -1;
        private Vector2 _lastTouch;
        private bool _pinching;
        private bool _gestureLock;
        private Vector2 _pinchCenter;
        private float _pinchDist;
        private readonly List<TouchPoint> _active = new List<TouchPoint>();
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();

        public InputController(GameController game, Screens screens)
        {
            _game = game;
            _screens = screens;
        }

        /// <summary>True when a UI element sits under a screen point (origin bottom-left).</summary>
        private bool OverUiAt(Vector2 screenPos)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            _hits.Clear();
            es.RaycastAll(data, _hits);
            return _hits.Count > 0;
        }

        private Vec2d ToWorld(Vector2 screenPos, out double sx, out double sy)
        {
            sx = screenPos.x;
            sy = Screen.height - screenPos.y;
            return _game.Camera.ToWorld(sx, sy);
        }

        public void Update()
        {
            GameController game = _game;
            Core.Editor editor = game.Editor;
            bool shift = InputBridge.IsKey(KeyCode.LeftShift) || InputBridge.IsKey(KeyCode.RightShift);
            bool ctrl = InputBridge.IsKey(KeyCode.LeftControl) || InputBridge.IsKey(KeyCode.RightControl) || InputBridge.IsKey(KeyCode.LeftCommand) || InputBridge.IsKey(KeyCode.RightCommand);

            // Escape (the Android back button reports as Escape too).
            if (InputBridge.KeyDown(KeyCode.Escape))
            {
                if (_screens.IsOpen)
                {
                    if (_screens.Current == "pause") _screens.Hide();
                }
                else _screens.Pause();
                return;
            }
            if (_screens.IsOpen)
            {
                _drawing = false;
                _panning = false;
                _pinching = false;
                _drawTouch = -1;
                return;
            }

            if (InputBridge.TouchCount > 0) UpdateTouch(game, editor, shift);
            else UpdateMouse(game, editor, shift);
            UpdateKeys(game, editor, shift, ctrl);
        }

        private void UpdateMouse(GameController game, Core.Editor editor, bool shift)
        {
            Vector3 mouse = InputBridge.MousePosition;
            var mouse2 = new Vector2(mouse.x, mouse.y);
            Vec2d w = ToWorld(mouse2, out double sx, out double sy);
            bool overUi = OverUiAt(mouse2);

            // Wheel zoom.
            float scroll = Mathf.Clamp(InputBridge.ScrollDelta, -3f, 3f);
            if (scroll != 0 && !overUi)
            {
                game.Camera.ZoomAt(sx, sy, System.Math.Exp(scroll * 0.12));
                editor.Zoom = game.Camera.Zoom;
            }

            // Panning: middle button, or left button with the pan tool.
            bool panButton = InputBridge.MouseButton(2) || (editor.Tool == ToolId.Pan && InputBridge.MouseButton(0));
            if ((InputBridge.MouseButtonDown(2) || (editor.Tool == ToolId.Pan && InputBridge.MouseButtonDown(0))) && !overUi)
            {
                _panning = true;
                _lastMouse = mouse;
                game.Following = false;
            }
            if (_panning)
            {
                if (panButton)
                {
                    Vector3 delta = mouse - _lastMouse;
                    game.Camera.PanBy(delta.x, -delta.y);
                    _lastMouse = mouse;
                }
                else _panning = false;
            }

            // Drawing.
            if (InputBridge.MouseButtonDown(0) && !overUi && editor.Tool != ToolId.Pan)
            {
                if (!game.HandlePlacementClick(w.X, w.Y))
                {
                    editor.PointerDown(w.X, w.Y, shift);
                    _drawing = true;
                }
            }
            if (InputBridge.MouseButtonDown(1) && !overUi) editor.SecondaryClick(w.X, w.Y);
            editor.PointerMove(w.X, w.Y, shift);
            if (_drawing && !InputBridge.MouseButton(0))
            {
                editor.PointerUp();
                _drawing = false;
            }
        }

        private void UpdateTouch(GameController game, Core.Editor editor, bool shift)
        {
            int n = InputBridge.TouchCount;
            _active.Clear();
            for (int i = 0; i < n; i++)
            {
                TouchPoint t = InputBridge.GetTouch(i);
                if (!t.Ended) _active.Add(t);
            }

            if (_active.Count >= 2)
            {
                // A second finger turns the stroke into a pan/zoom gesture; the stroke is discarded.
                if (_drawing)
                {
                    editor.DiscardDrag();
                    _drawing = false;
                }
                _panning = false;
                _drawTouch = -1;
                Vector2 a = _active[0].Position;
                Vector2 b = _active[1].Position;
                Vector2 c = (a + b) * 0.5f;
                float d = Vector2.Distance(a, b);
                if (_pinching)
                {
                    if (_pinchDist > 1f && d > 1f) game.Camera.ZoomAt(c.x, Screen.height - c.y, d / _pinchDist);
                    game.Camera.PanBy(c.x - _pinchCenter.x, -(c.y - _pinchCenter.y));
                    game.Following = false;
                    editor.Zoom = game.Camera.Zoom;
                }
                _pinching = true;
                _gestureLock = true;
                _pinchCenter = c;
                _pinchDist = d;
                return;
            }

            _pinching = false;
            if (_active.Count == 0) _gestureLock = false;
            for (int i = 0; i < n; i++)
            {
                TouchPoint t = InputBridge.GetTouch(i);
                Vec2d w = ToWorld(t.Position, out _, out _);
                if (t.Began && !t.Ended && _drawTouch < 0 && !_gestureLock)
                {
                    if (OverUiAt(t.Position)) continue;
                    _drawTouch = t.Id;
                    _lastTouch = t.Position;
                    if (editor.Tool == ToolId.Pan)
                    {
                        _panning = true;
                        game.Following = false;
                    }
                    else if (!game.HandlePlacementClick(w.X, w.Y))
                    {
                        editor.PointerDown(w.X, w.Y, shift);
                        _drawing = true;
                    }
                }
                else if (t.Id == _drawTouch)
                {
                    if (_panning)
                    {
                        game.Camera.PanBy(t.Position.x - _lastTouch.x, -(t.Position.y - _lastTouch.y));
                        _lastTouch = t.Position;
                    }
                    else editor.PointerMove(w.X, w.Y, shift);
                    if (t.Ended)
                    {
                        if (_drawing) editor.PointerUp();
                        _drawing = false;
                        _panning = false;
                        _drawTouch = -1;
                    }
                }
            }
        }

        private void UpdateKeys(GameController game, Core.Editor editor, bool shift, bool ctrl)
        {
            // Keyboard panning.
            double panSpeed = 400 * Time.deltaTime / game.Camera.Zoom;
            if (InputBridge.IsKey(KeyCode.LeftArrow)) { game.Camera.PanBy(panSpeed * game.Camera.Zoom, 0); game.Following = false; }
            if (InputBridge.IsKey(KeyCode.RightArrow)) { game.Camera.PanBy(-panSpeed * game.Camera.Zoom, 0); game.Following = false; }
            if (InputBridge.IsKey(KeyCode.UpArrow)) { game.Camera.PanBy(0, panSpeed * game.Camera.Zoom); game.Following = false; }
            if (InputBridge.IsKey(KeyCode.DownArrow)) { game.Camera.PanBy(0, -panSpeed * game.Camera.Zoom); game.Following = false; }

            if (ctrl && InputBridge.KeyDown(KeyCode.Z))
            {
                if (shift) editor.Redo();
                else editor.Undo();
                return;
            }
            if (ctrl && InputBridge.KeyDown(KeyCode.Y))
            {
                editor.Redo();
                return;
            }
            if (InputBridge.KeyDown(KeyCode.Space)) game.TogglePlay();
            if (InputBridge.KeyDown(KeyCode.Backspace)) game.Stop();
            if (InputBridge.KeyDown(KeyCode.R)) game.Restart(!shift);
            if (InputBridge.KeyDown(KeyCode.F))
            {
                if (shift) game.ClearFlag();
                else game.SetFlag();
            }
            if (InputBridge.KeyDown(KeyCode.P)) game.SetTool(ToolId.Pencil);
            if (InputBridge.KeyDown(KeyCode.L)) game.SetTool(ToolId.Line);
            if (InputBridge.KeyDown(KeyCode.E)) game.SetTool(ToolId.Eraser);
            if (InputBridge.KeyDown(KeyCode.H)) game.SetTool(ToolId.Pan);
            if (InputBridge.KeyDown(KeyCode.V)) game.SetTool(ToolId.Flip);
            if (InputBridge.KeyDown(KeyCode.O) && editor.Constraints.CanPlaceObjects) game.SetTool(ToolId.Object);
            if (InputBridge.KeyDown(KeyCode.G)) game.ToggleGhost();
            if (InputBridge.KeyDown(KeyCode.C))
            {
                game.Following = true;
                if (game.Run == null) game.Camera.Follow(game.Track.Start.X + 80, game.Track.Start.Y);
            }
            if (InputBridge.KeyDown(KeyCode.Tab)) game.SwitchCoopPlayer();
            if (InputBridge.KeyDown(KeyCode.Comma)) game.SetSpeed(System.Math.Max(0.25, game.Speed / 2));
            if (InputBridge.KeyDown(KeyCode.Period)) game.SetSpeed(System.Math.Min(8, game.Speed * 2));
            KeyCode[] digitKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals };
            for (int i = 0; i < digitKeys.Length; i++)
            {
                if (InputBridge.KeyDown(digitKeys[i]))
                {
                    string hotkey = i < 9 ? (i + 1).ToString() : i == 9 ? "0" : i == 10 ? "-" : "=";
                    foreach (Core.Material m in Materials.All)
                    {
                        if (m.Hotkey == hotkey) game.SetMaterial(m.Id);
                    }
                }
            }
        }
    }
}
