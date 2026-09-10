using NeonLineRider.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NeonLineRider.Unity
{
    /// <summary>Mouse and keyboard handling for the editor, camera and playback shortcuts.</summary>
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
            Vector3 m = Input.mousePosition;
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
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            if (Input.GetKeyDown(KeyCode.Escape))
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
            Vector3 mouse = Input.mousePosition;
            bool overUi = OverUi();

            // Wheel zoom.
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0 && !overUi)
            {
                game.Camera.ZoomAt(sx, sy, System.Math.Exp(scroll * 0.12));
                editor.Zoom = game.Camera.Zoom;
            }

            // Panning: middle button, or left button with the pan tool.
            bool panButton = Input.GetMouseButton(2) || (editor.Tool == ToolId.Pan && Input.GetMouseButton(0));
            if ((Input.GetMouseButtonDown(2) || (editor.Tool == ToolId.Pan && Input.GetMouseButtonDown(0))) && !overUi)
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
            if (Input.GetMouseButtonDown(0) && !overUi && editor.Tool != ToolId.Pan)
            {
                if (!game.HandlePlacementClick(w.X, w.Y))
                {
                    editor.PointerDown(w.X, w.Y, shift);
                    _drawing = true;
                }
            }
            if (Input.GetMouseButtonDown(1) && !overUi) editor.SecondaryClick(w.X, w.Y);
            editor.PointerMove(w.X, w.Y, shift);
            if (_drawing && !Input.GetMouseButton(0))
            {
                editor.PointerUp();
                _drawing = false;
            }

            // Keyboard panning.
            double panSpeed = 400 * Time.deltaTime / game.Camera.Zoom;
            if (Input.GetKey(KeyCode.LeftArrow)) { game.Camera.PanBy(panSpeed * game.Camera.Zoom, 0); game.Following = false; }
            if (Input.GetKey(KeyCode.RightArrow)) { game.Camera.PanBy(-panSpeed * game.Camera.Zoom, 0); game.Following = false; }
            if (Input.GetKey(KeyCode.UpArrow)) { game.Camera.PanBy(0, panSpeed * game.Camera.Zoom); game.Following = false; }
            if (Input.GetKey(KeyCode.DownArrow)) { game.Camera.PanBy(0, -panSpeed * game.Camera.Zoom); game.Following = false; }

            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (shift) editor.Redo();
                else editor.Undo();
                return;
            }
            if (ctrl && Input.GetKeyDown(KeyCode.Y))
            {
                editor.Redo();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space)) game.TogglePlay();
            if (Input.GetKeyDown(KeyCode.Backspace)) game.Stop();
            if (Input.GetKeyDown(KeyCode.R)) game.Restart(!shift);
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (shift) game.ClearFlag();
                else game.SetFlag();
            }
            if (Input.GetKeyDown(KeyCode.P)) game.SetTool(ToolId.Pencil);
            if (Input.GetKeyDown(KeyCode.L)) game.SetTool(ToolId.Line);
            if (Input.GetKeyDown(KeyCode.E)) game.SetTool(ToolId.Eraser);
            if (Input.GetKeyDown(KeyCode.H)) game.SetTool(ToolId.Pan);
            if (Input.GetKeyDown(KeyCode.V)) game.SetTool(ToolId.Flip);
            if (Input.GetKeyDown(KeyCode.O) && editor.Constraints.CanPlaceObjects) game.SetTool(ToolId.Object);
            if (Input.GetKeyDown(KeyCode.G)) game.ToggleGhost();
            if (Input.GetKeyDown(KeyCode.C))
            {
                game.Following = true;
                if (game.Run == null) game.Camera.Follow(game.Track.Start.X + 80, game.Track.Start.Y);
            }
            if (Input.GetKeyDown(KeyCode.Tab)) game.SwitchCoopPlayer();
            if (Input.GetKeyDown(KeyCode.Comma)) game.SetSpeed(System.Math.Max(0.25, game.Speed / 2));
            if (Input.GetKeyDown(KeyCode.Period)) game.SetSpeed(System.Math.Min(8, game.Speed * 2));
            KeyCode[] digitKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals };
            for (int i = 0; i < digitKeys.Length; i++)
            {
                if (Input.GetKeyDown(digitKeys[i]))
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
