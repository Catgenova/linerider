using System.Collections.Generic;
using NeonLineRider.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NeonLineRider.Unity
{
    /// <summary>In-game overlay: ink meter, timer, objectives, tools, materials, playback.</summary>
    public sealed class Hud
    {
        private readonly GameController _game;
        private readonly System.Action _onMenu;
        public readonly RectTransform Root;
        private readonly RectTransform _toolbar, _palette, _objectPalette, _playbar, _objectives, _topLeft, _topRight;
        private readonly Text _title, _mode, _inkText, _inkText2, _stats, _message, _hint;
        private readonly RectTransform _inkFill, _inkFill2, _inkBox2;
        private readonly Image _inkFillImage;
        private readonly List<Text> _objectiveTexts = new List<Text>();
        private double _messageTimer;
        private string _lastStats = "";

        public Hud(Transform canvas, GameController game, System.Action onMenu)
        {
            _game = game;
            _onMenu = onMenu;
            Root = UiKit.Rect("HUD", canvas);
            UiKit.Stretch(Root);

            // Top left: menu + title.
            _topLeft = UiKit.Rect("TopLeft", Root);
            UiKit.Place(_topLeft, 0, 1, 12, -10, 420, 44, 0, 1);
            var tl = UiKit.HBox(_topLeft, "Row", 8);
            UiKit.Stretch(tl.GetComponent<RectTransform>());
            UiKit.Button(tl.transform, "MENU", () => _onMenu(), null, 16, 32, 36);
            var titleBox = UiKit.VBox(tl.transform, "Title", 0);
            _title = UiKit.Label(titleBox.transform, "", 15, Color.white, TextAnchor.MiddleLeft, true);
            _mode = UiKit.Label(titleBox.transform, "", 11, UiKit.Muted);

            // Top centre: ink bars.
            RectTransform center = UiKit.Rect("TopCenter", Root);
            UiKit.Place(center, 0.5f, 1, 0, -8, 280, 52, 0.5f, 1);
            _inkFillImage = BuildInkBar(center, 0, out _inkFill, out _inkText, UiKit.Cyan);
            _inkBox2 = UiKit.Rect("InkBox2", center);
            UiKit.Place(_inkBox2, 0.5f, 1, 0, -26, 280, 24, 0.5f, 1);
            BuildInkBar(_inkBox2, 0, out _inkFill2, out _inkText2, UiKit.Magenta);

            // Top right: stats.
            _topRight = UiKit.Rect("TopRight", Root);
            UiKit.Place(_topRight, 1, 1, -12, -8, 520, 44, 1, 1);
            _stats = UiKit.Label(_topRight, "", 13, Color.white, TextAnchor.MiddleRight, true);
            UiKit.Stretch(_stats.rectTransform);

            // Objectives.
            _objectives = UiKit.Rect("Objectives", Root);
            UiKit.Place(_objectives, 0, 1, 12, -62, 320, 200, 0, 1);
            var ov = UiKit.VBox(_objectives, "List", 4);
            UiKit.Stretch(ov.GetComponent<RectTransform>());
            ov.childForceExpandHeight = false;

            // Toolbar (left middle).
            _toolbar = UiKit.Rect("Toolbar", Root);
            UiKit.Place(_toolbar, 0, 0.5f, 12, 40, 120, 460, 0, 0.5f);
            var tv = UiKit.VBox(_toolbar, "List", 4);
            UiKit.Stretch(tv.GetComponent<RectTransform>());

            // Object palette and material palette (bottom left).
            _objectPalette = UiKit.Rect("ObjectPalette", Root);
            UiKit.Place(_objectPalette, 0, 0, 12, 96, 560, 90, 0, 0);
            _palette = UiKit.Rect("Palette", Root);
            UiKit.Place(_palette, 0, 0, 12, 12, 560, 84, 0, 0);

            // Playbar (bottom centre).
            _playbar = UiKit.Rect("Playbar", Root);
            UiKit.Place(_playbar, 0.5f, 0, 0, 12, 560, 40, 0.5f, 0);

            _message = UiKit.Label(Root, "", 36, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place(_message.rectTransform, 0.5f, 0.66f, 0, 0, 900, 60, 0.5f, 0.5f);
            _message.raycastTarget = false;
            _hint = UiKit.Label(Root, "", 11, UiKit.Muted, TextAnchor.LowerRight);
            UiKit.Place(_hint.rectTransform, 1, 0, -12, 12, 360, 40, 1, 0);
            Rebuild();
        }

        private static Image BuildInkBar(Transform parent, float yOffset, out RectTransform fill, out Text text, Color color)
        {
            RectTransform box = UiKit.Rect("InkBox", parent);
            UiKit.Place(box, 0.5f, 1, 0, yOffset, 260, 24, 0.5f, 1);
            Image bar = UiKit.Panel(box, "Bar", new Color(0, 0, 0, 0.5f));
            UiKit.Place(bar.rectTransform, 0.5f, 1, 0, 0, 260, 8, 0.5f, 1);
            UiKit.Border(bar.rectTransform, UiKit.PanelBorder);
            Image fillImg = UiKit.Panel(bar.transform, "Fill", color);
            fill = fillImg.rectTransform;
            fill.anchorMin = new Vector2(0, 0);
            fill.anchorMax = new Vector2(1, 1);
            fill.pivot = new Vector2(0, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            text = UiKit.Label(box, "", 11, Color.white, TextAnchor.UpperCenter, true);
            UiKit.Place(text.rectTransform, 0.5f, 1, 0, -10, 260, 16, 0.5f, 1);
            return fillImg;
        }

        private static void SetFill(RectTransform fill, double frac)
        {
            fill.anchorMax = new Vector2((float)System.Math.Max(0, System.Math.Min(1, frac)), 1);
        }

        public void SetVisible(bool v)
        {
            Root.gameObject.SetActive(v);
        }

        /// <summary>Rebuild tool/material/playback buttons (called on state changes).</summary>
        public void Rebuild()
        {
            GameController game = _game;
            Core.Editor editor = game.Editor;
            Transform tv = _toolbar.GetChild(0);
            UiKit.Clear(tv);
            var tools = new[]
            {
                (ToolId.Pencil, "Pencil (P)"), (ToolId.Line, "Line (L)"), (ToolId.Eraser, "Eraser (E)"), (ToolId.Flip, "Flip (V)"), (ToolId.Pan, "Pan (H)"), (ToolId.Object, "Objects (O)"),
            };
            foreach (var (id, label) in tools)
            {
                if (id == ToolId.Object && !editor.Constraints.CanPlaceObjects) continue;
                ToolId captured = id;
                UiKit.Button(tv, label, () => game.SetTool(captured), editor.Tool == id ? UiKit.Cyan : (Color?)UiKit.Muted, 11, 26);
            }
            UiKit.Spacer(tv, 4);
            UiKit.Button(tv, "Undo (Ctrl+Z)", () => editor.Undo(), UiKit.Muted, 11, 26);
            UiKit.Button(tv, "Redo (Ctrl+Y)", () => editor.Redo(), UiKit.Muted, 11, 26);
            UiKit.Button(tv, "Clear", () => { editor.ClearPlayerLines(); game.Stop(); }, UiKit.Muted, 11, 26);
            if (game.Mode == GameMode.Free || (game.Mode == GameMode.Library && !game.InkBudget.HasValue))
            {
                UiKit.Spacer(tv, 4);
                UiKit.Button(tv, "Set start", () => game.BeginPlacement("start"), UiKit.Muted, 11, 26);
                UiKit.Button(tv, "Set finish", () => game.BeginPlacement("finish"), UiKit.Muted, 11, 26);
            }
            if (game.CoopActive)
            {
                UiKit.Spacer(tv, 4);
                UiKit.Button(tv, "Switch to P" + (game.CoopPlayer == 1 ? 2 : 1) + " (Tab)", () => game.SwitchCoopPlayer(), game.CoopPlayer == 1 ? UiKit.Cyan : UiKit.Magenta, 11, 26);
            }

            UiKit.Clear(_objectPalette);
            if (editor.Constraints.CanPlaceObjects && editor.Tool == ToolId.Object)
            {
                var grid = UiKit.Grid(_objectPalette, "Grid", 108, 24, 4);
                var grt = grid.GetComponent<RectTransform>();
                grt.anchorMin = new Vector2(0, 0);
                grt.anchorMax = new Vector2(1, 0);
                grt.pivot = new Vector2(0, 0);
                grt.anchoredPosition = Vector2.zero;
                foreach (ObjectKindDef k in ObjectKinds.All)
                {
                    string kid = k.Id;
                    UiKit.Button(grid.transform, k.Name, () => game.SetObjectKind(kid), editor.ObjectKind == k.Id ? UiKit.Yellow : (Color?)UiKit.Muted, 10, 24);
                }
            }

            UiKit.Clear(_palette);
            var pg = UiKit.Grid(_palette, "Grid", 90, 24, 4);
            var prt = pg.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, 0);
            prt.anchorMax = new Vector2(1, 0);
            prt.pivot = new Vector2(0, 0);
            prt.anchoredPosition = Vector2.zero;
            foreach (MaterialId id in editor.Constraints.Materials)
            {
                Core.Material m = Materials.Get(id);
                MaterialId captured = id;
                UiKit.Button(pg.transform, m.Name + " " + m.Hotkey, () => game.SetMaterial(captured), U.Hex(m.Color, editor.Material == id ? 1f : 0.45f), 10, 24);
            }

            UiKit.Clear(_playbar);
            var pb = UiKit.HBox(_playbar, "Row", 4, 0, TextAnchor.MiddleCenter);
            var pbrt = pb.GetComponent<RectTransform>();
            pbrt.anchorMin = new Vector2(0.5f, 0);
            pbrt.anchorMax = new Vector2(0.5f, 0);
            pbrt.pivot = new Vector2(0.5f, 0);
            pbrt.anchoredPosition = Vector2.zero;
            var pfit = pb.GetComponent<ContentSizeFitter>();
            pfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bool playing = game.PlayState == PlayState.Play;
            UiKit.Button(pb.transform, playing ? "PAUSE" : "PLAY", () => game.TogglePlay(), UiKit.Lime, 14, 32, 44);
            UiKit.Button(pb.transform, "STOP", () => game.Stop(), null, 14, 32, 34);
            UiKit.Button(pb.transform, "RESTART", () => game.Restart(true), null, 14, 32, 34);
            UiKit.Button(pb.transform, game.FlagFrame > 0 ? "FLAG " + U.F(game.FlagFrame / 40.0) + "s" : "FLAG", () => { if (game.FlagFrame > 0) game.ClearFlag(); else game.SetFlag(); }, game.FlagFrame > 0 ? UiKit.Cyan : (Color?)UiKit.Muted, 12, 32, 40);
            foreach (double s in new[] { 0.5, 1, 2, 4 })
            {
                double captured = s;
                UiKit.Button(pb.transform, U.F(s, "0.#") + "x", () => game.SetSpeed(captured), game.Speed == s ? UiKit.Cyan : (Color?)UiKit.Muted, 11, 32, 40);
            }
            UiKit.Button(pb.transform, "ghost", () => game.ToggleGhost(), game.GhostEnabled ? UiKit.Cyan : (Color?)UiKit.Muted, 11, 32, 50);
            UiKit.Button(pb.transform, "SOUND", () => game.ToggleMusic(), game.Audio != null && game.Audio.Enabled ? UiKit.Cyan : (Color?)UiKit.Muted, 12, 32, 34);

            LevelDef level = game.Level;
            string modeLabel = game.Mode == GameMode.Free ? "FREE RIDE" : game.Mode == GameMode.Arcade ? "RUSH" : game.Mode == GameMode.Daily ? "DAILY" : game.Mode == GameMode.Coop ? "CO-OP" : game.Mode == GameMode.Library ? "COMMUNITY" : level != null ? level.Mode.ToUpperInvariant() : "";
            _title.text = level != null ? level.Name : game.LibraryTrack != null ? game.LibraryTrack.Title : game.Mode == GameMode.Arcade ? "Neon Rush" : "Free Ride";
            _mode.text = modeLabel + " - " + game.Environment.Name + " - " + game.RiderDef.Name;
            _inkBox2.gameObject.SetActive(game.CoopActive);
            _hint.text = game.Mode == GameMode.Arcade
                ? "Draw ahead of Bosh. Ink grows with distance. Space pauses."
                : "Space play - R restart - F flag - scroll zoom - middle-drag pan - right-click flips a line";

            Transform ov = _objectives.GetChild(0);
            UiKit.Clear(ov);
            _objectiveTexts.Clear();
            if (level != null)
            {
                foreach (Objective o in level.Objectives)
                {
                    Text t = UiKit.Label(ov, "", 13, UiKit.Text);
                    _objectiveTexts.Add(t);
                }
            }
            else if (game.Mode == GameMode.Library && game.LibraryTrack != null)
            {
                _objectiveTexts.Add(UiKit.Label(ov, "[ ] " + (game.Track.Finish.HasValue ? "Reach the finish" : "Ride free"), 13, UiKit.Text));
            }
            _lastStats = "";
            Update(0);
        }

        public void ShowMessage(string text, string color, bool big)
        {
            _message.text = text;
            _message.color = U.Hex(color);
            _message.fontSize = big ? 44 : 22;
            _message.gameObject.SetActive(true);
            _messageTimer = big ? 2.2 : 1.4;
        }

        /// <summary>Per-frame refresh of live values.</summary>
        public void Update(double dt)
        {
            GameController game = _game;
            Core.Editor editor = game.Editor;
            if (_messageTimer > 0)
            {
                _messageTimer -= dt;
                if (_messageTimer <= 0) _message.gameObject.SetActive(false);
            }
            double? budget = editor.Constraints.Budget;
            if (game.CoopActive)
            {
                double b1 = game.CoopBudgets[0], b2 = game.CoopBudgets[1];
                double u1 = game.Track.InkUsed(1), u2 = game.Track.InkUsed(2);
                SetFill(_inkFill, 1 - u1 / b1);
                _inkText.text = "P1 " + U.F(System.Math.Max(0, b1 - u1)) + " m";
                SetFill(_inkFill2, 1 - u2 / b2);
                _inkText2.text = "P2 " + U.F(System.Math.Max(0, b2 - u2)) + " m";
            }
            else if (!budget.HasValue)
            {
                SetFill(_inkFill, 1);
                _inkText.text = "unlimited - " + U.F(game.InkUsed) + " m drawn";
                _inkFillImage.color = UiKit.Cyan;
            }
            else
            {
                double used = game.InkUsed;
                double frac = System.Math.Max(0, System.Math.Min(1, 1 - used / budget.Value));
                SetFill(_inkFill, frac);
                _inkFillImage.color = frac < 0.15 ? UiKit.Pink : UiKit.Cyan;
                _inkText.text = "INK " + U.F(System.Math.Max(0, budget.Value - used)) + " / " + U.F(budget.Value, "0") + " m";
            }

            Run run = game.Run;
            var sb = new System.Text.StringBuilder();
            sb.Append("TIME ").Append(U.FormatTime(run?.Frame ?? 0));
            if (game.Mode == GameMode.Arcade) sb.Append("   DIST ").Append(U.F(game.MetersTravelled(), "0")).Append(" m   BEST ").Append(game.Progress.ArcadeBest).Append(" m");
            int score = run?.Tricks.Score ?? 0;
            sb.Append("   TRICKS ").Append(score);
            if (run != null && run.Tricks.Combo > 1) sb.Append(" x").Append(run.Tricks.Combo);
            LevelDef level = game.Level;
            if (level != null && level.Flags.Count > 0) sb.Append("   FLAGS ").Append(run != null ? CountTrue(run.Flags) : 0).Append("/").Append(level.Flags.Count);
            if (level != null && level.Rescues.Count > 0) sb.Append("   RESCUED ").Append(run != null ? CountTrue(run.Rescues) : 0).Append("/").Append(level.Rescues.Count);
            if (level != null && level.Mode == "delivery") sb.Append("   CARGO ").Append(run != null && run.CargoLost ? "LOST" : (run != null ? System.Math.Round(run.CargoIntegrity) : 100) + "%");
            if (level != null && level.Mode == "destruction") sb.Append("   CHAOS ").Append(run?.ChaosScore() ?? 0);
            if (level != null && level.TimeLimit > 0) sb.Append("   LIMIT ").Append(U.F(System.Math.Max(0, level.TimeLimit - (run != null ? run.Frame / 40.0 : 0)))).Append("s");
            string stats = sb.ToString();
            if (stats != _lastStats)
            {
                _stats.text = stats;
                _lastStats = stats;
            }

            if (level != null && _objectiveTexts.Count == level.Objectives.Count)
            {
                RunSummary summary = run?.Summary();
                for (int i = 0; i < level.Objectives.Count; i++)
                {
                    Objective o = level.Objectives[i];
                    bool done = summary != null && Objectives.Evaluate(o, summary);
                    string mark = done ? "<color=#c6ff4a>[x]</color>" : "[ ]";
                    string text = mark + " " + Objectives.Label(o) + (o.Optional ? "  <color=#ffe93a><size=9>MEDAL</size></color>" : "");
                    if (_objectiveTexts[i].text != text) _objectiveTexts[i].text = text;
                }
            }
        }

        private static int CountTrue(bool[] arr)
        {
            int n = 0;
            foreach (bool b in arr) if (b) n++;
            return n;
        }
    }
}
