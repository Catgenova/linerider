using CyberRider.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CyberRider.Unity
{
    /// <summary>Mouse and keyboard handling for the editor, camera and playback shortcuts. Reads go through
    /// <see cref="InputBridge"/> so either Unity input backend works.</summary>
    public sealed class InputController
    {
        private readonly GameController _game;
        private readonly Screens _screens;
        private bool _panning;
        private Vector3 _lastMouse;
        private bool _drawing;

        public InputController(GameController game, Screens screens)
        {
            _game = game;
            _screens = screens;
        }

        private Vec2d MouseWorld(out double sx, out double sy)
        {
            Vector3 m = InputBridge.MousePosition;
            sx = m.x;
            sy = Screen.height - m.y;
            return _game.Camera.ToWorld(sx, sy);
        }

        private static bool OverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        public void Update()
        {
            GameController game = _game;
            Core.Editor editor = game.Editor;
            bool shift = InputBridge.IsKey(KeyCode.LeftShift) || InputBridge.IsKey(KeyCode.RightShift);
            bool ctrl = InputBridge.IsKey(KeyCode.LeftControl) || InputBridge.IsKey(KeyCode.RightControl) || InputBridge.IsKey(KeyCode.LeftCommand) || InputBridge.IsKey(KeyCode.RightCommand);

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
                return;
            }

            Vec2d w = MouseWorld(out double sx, out double sy);
            Vector3 mouse = InputBridge.MousePosition;
            bool overUi = OverUi();

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
