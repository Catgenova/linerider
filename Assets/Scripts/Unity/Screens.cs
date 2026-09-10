using System;
using System.Collections.Generic;
using CyberRider.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CyberRider.Unity
{
    /// <summary>Full-screen menus layered over the world: title, map, intro, results, library, riders, co-op, daily, pause.</summary>
    public sealed class Screens
    {
        private readonly GameController _game;
        private readonly Action _onEnterGame;
        private readonly RectTransform _root;
        private readonly Image _overlay;
        public string Current = "none";
        private int _selectedRegion;
        private bool _confirmReset;
        private readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>();
        private Action<string, string, bool> _notify;

        public Screens(Transform canvas, GameController game, Action onEnterGame, Action<string, string, bool> notify)
        {
            _game = game;
            _onEnterGame = onEnterGame;
            _notify = notify;
            _overlay = UiKit.Panel(canvas, "Overlay", new Color(3 / 255f, 1 / 255f, 10 / 255f, 0.92f));
            _root = _overlay.rectTransform;
            UiKit.Stretch(_root);
            _root.gameObject.SetActive(false);
        }

        public bool IsOpen => Current != "none";

        public void Hide()
        {
            Current = "none";
            UiKit.Clear(_root);
            _root.gameObject.SetActive(false);
        }

        /// <summary>Open a centred, scrollable panel and return its content column.</summary>
        private Transform Panel(string name, float width)
        {
            Current = name;
            UiKit.Clear(_root);
            _root.gameObject.SetActive(true);
            // Let the demo ride show through the menus that sit on top of it.
            bool attract = _game.Mode == GameMode.Attract;
            _overlay.color = new Color(3 / 255f, 1 / 255f, 10 / 255f, attract ? 0.5f : 0.92f);
            Image panel = UiKit.Panel(_root, "Panel", attract ? new Color(8 / 255f, 4 / 255f, 28 / 255f, 0.78f) : UiKit.PanelColor);
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0);
            prt.anchorMax = new Vector2(0.5f, 1);
            prt.pivot = new Vector2(0.5f, 0.5f);
            // Never wider than the notch-free screen; phones keep a slimmer margin.
            width = Mathf.Min(width, UiKit.SafeWidth - 24);
            prt.sizeDelta = new Vector2(width, UiKit.Compact ? -16 : -48);
            prt.anchoredPosition = Vector2.zero;
            UiKit.Border(prt, UiKit.PanelBorder);
            RectTransform content = UiKit.ScrollList(prt, "Scroll");
            UiKit.Stretch(content.parent.parent as RectTransform, 6, 6, 6, 6);
            var lg = content.GetComponent<VerticalLayoutGroup>();
            lg.padding = new RectOffset(22, 22, 18, 18);
            lg.spacing = 8;
            return content;
        }

        private Text Heading(Transform parent, string text)
        {
            Text t = UiKit.Label(parent, text, 24, Color.white, TextAnchor.MiddleLeft, true);
            return t;
        }

        private Text Para(Transform parent, string text, int size = 15, Color? color = null)
        {
            return UiKit.Label(parent, text, size, color ?? UiKit.Text);
        }

        private HorizontalLayoutGroup Row(Transform parent, TextAnchor align = TextAnchor.MiddleLeft)
        {
            return UiKit.HBox(parent, "Row", 8, 0, align);
        }

        private void Pill(Transform parent, string text, Color color)
        {
            Text t = UiKit.Label(parent, text, 11, color, TextAnchor.MiddleCenter, true);
            UiKit.Size(t, -1, 22, 60);
        }

        private void Stat(Transform parent, string k, string v)
        {
            var box = UiKit.VBox(parent, "Stat", 0, 8);
            var img = box.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.3f);
            UiKit.Label(box.transform, k, 10, UiKit.Muted);
            UiKit.Label(box.transform, v, 18, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Size(box, 130, -1, 110);
        }

        // ------------------------------------------------------------------ title

        public void Title()
        {
            GameController g = _game;
            if (g.Mode != GameMode.Attract) g.StartAttract();
            Transform c = Panel("title", 720);
            g.Progress.TotalMedals(out _, out _, out int gold);
            int done = 0;
            foreach (LevelDef l in Levels.All) if (g.Progress.IsComplete(l.Id)) done++;
            bool compact = UiKit.Compact;
            var logo = UiKit.Label(c, "CYBER", compact ? 52 : 72, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Size(logo, -1, compact ? 58 : 80);
            var logo2 = UiKit.Label(c, "RIDER", compact ? 26 : 34, UiKit.Magenta, TextAnchor.MiddleCenter, true);
            UiKit.Size(logo2, -1, compact ? 34 : 44);
            UiKit.Label(c, "Draw the line. Ride the pulse.", 13, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Spacer(c, compact ? 4 : 10);
            Transform menu = c;
            if (compact)
            {
                // Two columns of mode buttons so the menu fits a phone in landscape.
                float pw = Mathf.Min(720, UiKit.SafeWidth - 24);
                var mg = UiKit.Grid(c, "Menu", (pw - 52) / 2, 52, 8, 0, false);
                mg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                mg.constraintCount = 2;
                menu = mg.transform;
            }
            BigButton(menu, "ADVENTURE", done + "/" + Levels.All.Count + " levels · " + gold + " gold", () => Map(_selectedRegion));
            BigButton(menu, "FREE RIDE", "Unlimited ink, every material, place objects, publish your tracks", FreeRideEnv);
            BigButton(menu, "CYBER RUSH", "Draw while riding · best " + g.Progress.ArcadeBest + " m", () => { g.StartArcade(); Hide(); _onEnterGame(); });
            BigButton(menu, "DAILY CHALLENGE", Daily.TodayKey(), DailyScreen);
            BigButton(menu, "TRACK LIBRARY", "Community tracks, share codes, records", () => Library("all"));
            BigButton(menu, "CO-OP", "Two players, two colours of ink", Coop);
            BigButton(menu, "RIDERS", "Riding as " + g.RiderDef.Name, RidersScreen);
            UiKit.Spacer(c, compact ? 4 : 10);
            var row = Row(c, TextAnchor.MiddleCenter);
            UiKit.Button(row.transform, _confirmReset ? "click again to erase all progress" : "reset progress", () =>
            {
                if (_confirmReset)
                {
                    g.Progress.Reset();
                    _confirmReset = false;
                }
                else _confirmReset = true;
                Title();
            }, UiKit.Muted, 11, 26, 200);
            UiKit.Button(row.transform, "quit", () => Application.Quit(), UiKit.Muted, 11, 26, 80);
        }

        private void BigButton(Transform parent, string title, string sub, Action onClick)
        {
            Button b = UiKit.Button(parent, "<b>" + title + "</b>\n<size=12><color=#8a92c8>" + sub + "</color></size>", onClick, null, 14, 52);
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.alignment = TextAnchor.MiddleLeft;
        }

        private void FreeRideEnv()
        {
            Transform c = Panel("free", 720);
            Heading(c, "FREE RIDE");
            Para(c, "Pick a world. Each one brings its own physics gimmick.", 13, UiKit.Muted);
            foreach (Core.Environment e in Environments.All)
            {
                string id = e.Id;
                Button b = UiKit.Button(c, "<b>" + e.Name + "</b>\n<size=12>" + e.Gimmick + "</size>", () => { _game.StartFree(id); Hide(); _onEnterGame(); }, U.Hex(e.Accent), 13, 50);
                Text t = b.GetComponentInChildren<Text>();
                if (t != null) t.alignment = TextAnchor.MiddleLeft;
            }
            UiKit.Button(c, "< back", Title, UiKit.Muted, 12, 28);
        }

        // ------------------------------------------------------------------ adventure map

        public void Map(int regionIndex)
        {
            GameController g = _game;
            _selectedRegion = regionIndex;
            RegionDef region = Levels.Regions[regionIndex];
            Transform c = Panel("map", 1080);
            var head = Row(c);
            Heading(head.transform, "ADVENTURE MAP");
            UiKit.Button(head.transform, "< menu", Title, UiKit.Muted, 12, 28, 90);

            const float areaW = 1000, areaH = 300;
            RectTransform area = UiKit.Rect("Area", c);
            UiKit.Size(area.gameObject.AddComponent<LayoutElement>(), -1, areaH);
            var areaImg = area.gameObject.AddComponent<Image>();
            areaImg.color = new Color(20 / 255f, 8 / 255f, 60 / 255f, 0.35f);
            UiKit.Border(area, UiKit.PanelBorder);
            int furthest = 0;
            for (int i = 1; i < Levels.Regions.Length; i++)
            {
                RegionDef a = Levels.Regions[i - 1];
                RegionDef b = Levels.Regions[i];
                DrawMapLine(area, (float)a.MapX * areaW, -(float)a.MapY * areaH, (float)b.MapX * areaW, -(float)b.MapY * areaH);
            }
            for (int i = 0; i < Levels.Regions.Length; i++)
            {
                RegionDef r = Levels.Regions[i];
                bool unlocked = g.RegionUnlocked(i);
                if (unlocked) furthest = i;
                List<LevelDef> levels = Levels.InRegion(r.Id);
                int done = 0;
                foreach (LevelDef l in levels) if (g.Progress.IsComplete(l.Id)) done++;
                int captured = i;
                Color accent = U.Hex(Environments.Get(r.Environment).Accent);
                Button node = UiKit.Button(area, (unlocked ? "* " : "o ") + r.Name + "\n<size=10>" + (unlocked ? done + "/" + levels.Count : "locked") + "</size>", () => { if (unlocked) Map(captured); }, i == regionIndex ? accent : (Color?)new Color(accent.r, accent.g, accent.b, unlocked ? 0.6f : 0.25f), 11, 40, 120);
                var nrt = node.GetComponent<RectTransform>();
                nrt.anchorMin = nrt.anchorMax = new Vector2(0, 1);
                nrt.pivot = new Vector2(0.5f, 0.5f);
                nrt.anchoredPosition = new Vector2((float)r.MapX * areaW, -(float)r.MapY * areaH);
                nrt.sizeDelta = new Vector2(120, 40);
                var le = node.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = true;
            }
            RegionDef marker = Levels.Regions[furthest];
            Text bosh = UiKit.Label(area, "BOSH", 20, UiKit.Yellow, TextAnchor.MiddleCenter);
            UiKit.Place(bosh.rectTransform, 0, 1, (float)marker.MapX * areaW, -(float)marker.MapY * areaH + 34, 40, 30, 0.5f, 0.5f);

            Core.Environment env = Environments.Get(region.Environment);
            var rh = Row(c);
            UiKit.Label(rh.transform, region.Name, 16, U.Hex(env.Accent), TextAnchor.MiddleLeft, true);
            UiKit.Label(rh.transform, region.Blurb + " - " + env.Gimmick, 13, UiKit.Muted);
            var grid = UiKit.Grid(c, "Levels", 250, 96, 8);
            foreach (LevelDef lv in Levels.InRegion(region.Id)) LevelCard(grid.transform, lv, false);
        }

        private static void DrawMapLine(RectTransform area, float x1, float y1, float x2, float y2)
        {
            RectTransform rt = UiKit.Rect("MapLine", area);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 43 / 255f, 214 / 255f, 0.6f);
            img.raycastTarget = false;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x1, y1);
            float dx = x2 - x1, dy = y2 - y1;
            rt.sizeDelta = new Vector2(Mathf.Sqrt(dx * dx + dy * dy), 2);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        private void LevelCard(Transform parent, LevelDef lv, bool coop)
        {
            GameController g = _game;
            LevelRecord rec = g.Progress.Level(lv.Id);
            bool unlocked = g.LevelUnlocked(lv);
            string medal = rec.Medal == Medal.Gold ? "<color=#ffe93a>* GOLD</color>" : rec.Medal == Medal.Silver ? "<color=#c0c8e0>* SILVER</color>" : rec.Medal == Medal.Bronze ? "<color=#cd7f32>* BRONZE</color>" : "";
            string record = unlocked
                ? (rec.BestFrames.HasValue ? "best " + U.FormatTime(rec.BestFrames) + " - " + U.FormatInk(rec.BestInk) + " - " + rec.BestTrick + " pts" : "no record yet")
                : "complete the previous level";
            string label = "<b>" + lv.Name + "</b>  " + medal + "\n<size=10><color=#8a92c8>" + lv.Mode.ToUpperInvariant() + " - " + (coop ? Math.Round(lv.Budget * 0.6) + " m each" : lv.Budget + " m ink") + "</color></size>\n<size=12><color=#39f6ff>" + lv.Tagline + "</color></size>\n<size=10><color=#8a92c8>" + record + "</color></size>";
            Button b = UiKit.Button(parent, label, () => { if (unlocked) Intro(lv, coop); }, unlocked ? UiKit.Cyan : (Color?)new Color(0.3f, 0.3f, 0.4f), 12, 96);
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.alignment = TextAnchor.UpperLeft;
        }

        // ------------------------------------------------------------------ level intro

        public void Intro(LevelDef level, bool coop)
        {
            GameController g = _game;
            string riderId = level.Rider ?? g.RiderDef.Id;
            if (!g.RiderUnlocked(riderId)) riderId = "bosh";
            Transform c = Panel("intro", 680);
            Core.Environment env = Environments.Get(level.Environment);
            RegionDef region = Array.Find(Levels.Regions, r => r.Id == level.Region);
            UiKit.Label(c, (region != null ? region.Name : "") + " - " + level.Mode.ToUpperInvariant() + (coop ? " · CO-OP" : ""), 11, UiKit.Magenta);
            Heading(c, level.Name);
            Para(c, level.Tagline, 15, UiKit.Cyan);
            Para(c, level.Briefing);
            Para(c, "<b>" + env.Name + "</b> · " + env.Gimmick, 14, U.Hex(env.Accent));
            foreach (Objective o in level.Objectives)
            {
                Para(c, "[ ] " + Objectives.Label(o) + (o.Optional ? "  <color=#ffe93a><size=9>MEDAL</size></color>" : ""), 14);
            }
            var pills = Row(c);
            Pill(pills.transform, "INK " + (coop ? Math.Round(level.Budget * 0.6) + " m each" : level.Budget + " m"), UiKit.Text);
            foreach (MaterialId m in level.Materials) Pill(pills.transform, Materials.Get(m).Name, U.Hex(Materials.Get(m).Color));
            if (level.TimeLimit > 0) Pill(pills.transform, "LIMIT " + level.TimeLimit + "s", UiKit.Text);
            if (level.Rider != null) Para(c, "Rider locked: " + Riders.Get(level.Rider).Name, 13, UiKit.Muted);
            else
            {
                var riderRow = Row(c);
                foreach (RiderDef r in Riders.All)
                {
                    bool unlocked = g.RiderUnlocked(r.Id);
                    string rid = r.Id;
                    UiKit.Button(riderRow.transform, unlocked ? r.Name : r.Name + " (locked)", () => { if (unlocked) { riderId = rid; Intro(level, coop); } }, riderId == r.Id ? U.Hex(r.Color) : (Color?)new Color(0.4f, 0.4f, 0.5f), 11, 26, 90);
                }
            }
            var buttons = Row(c, TextAnchor.MiddleRight);
            UiKit.Button(buttons.transform, "< back", () => { if (coop) Coop(); else Map(_selectedRegion); }, UiKit.Muted, 12, 30, 90);
            UiKit.Button(buttons.transform, coop ? "START CO-OP" : "START", () => { g.LoadLevel(level, riderId, coop); Hide(); _onEnterGame(); }, null, 13, 32, 130, true);
        }

        // ------------------------------------------------------------------ results

        public void Results(ResultsInfo info)
        {
            GameController g = _game;
            RunSummary s = info.Summary;
            string heading = info.Mode == GameMode.Arcade ? "RUN OVER" : info.Complete ? "LEVEL COMPLETE" : s.FailReason != null ? s.FailReason.ToUpperInvariant() : "NOT YET";
            Transform c = Panel("results", 680);
            Text h = Heading(c, heading);
            h.color = info.Complete ? U.Hex("#4dff9d") : UiKit.Pink;
            if (info.Level != null) Para(c, info.Level.Name, 13, UiKit.Muted);
            if (info.Mode == GameMode.Campaign || info.Mode == GameMode.Daily)
            {
                string medalColor = info.Medal == Medal.Gold ? "#ffe93a" : info.Medal == Medal.Silver ? "#e0e6ff" : info.Medal == Medal.Bronze ? "#cd7f32" : "#8a92c8";
                Para(c, info.Medal == Medal.None ? "no medal" : info.Medal.ToString().ToUpperInvariant() + " MEDAL", 14, U.Hex(medalColor));
            }
            Improvements imp = info.Improvements;
            if (imp != null)
            {
                var badges = Row(c);
                if (imp.NewMedal) Pill(badges.transform, "NEW MEDAL: " + info.Medal.ToString().ToUpperInvariant(), UiKit.Lime);
                if (imp.NewTime) Pill(badges.transform, "NEW BEST TIME", UiKit.Lime);
                if (imp.NewInk) Pill(badges.transform, "LEAST INK", UiKit.Lime);
                if (imp.NewTrick) Pill(badges.transform, info.Mode == GameMode.Arcade ? "NEW BEST DISTANCE" : "NEW TRICK RECORD", UiKit.Lime);
            }
            var stats = Row(c);
            Stat(stats.transform, "TIME", s.Finished ? U.FormatTime(s.Frames) : "--");
            Stat(stats.transform, "INK USED", U.F(s.InkUsed) + " m");
            if (info.Level != null) Stat(stats.transform, "INK LEFT", U.F(Math.Max(0, info.Level.Budget - s.InkUsed)) + " m");
            Stat(stats.transform, "TRICKS", s.TrickScore.ToString());
            Stat(stats.transform, "AIRTIME", U.F(s.AirtimeFrames / 40.0) + "s");
            var stats2 = Row(c);
            if (info.Mode == GameMode.Arcade) Stat(stats2.transform, "DISTANCE", U.F(s.Distance, "0") + " m");
            else Stat(stats2.transform, "FLIPS", s.Flips.ToString());
            if (s.FlagsTotal > 0) Stat(stats2.transform, "FLAGS", s.FlagsCollected + "/" + s.FlagsTotal);
            if (s.RescueTotal > 0) Stat(stats2.transform, "RESCUED", s.Rescued + "/" + s.RescueTotal);
            if (info.Level != null && info.Level.Mode == "delivery") Stat(stats2.transform, "CARGO", s.CargoLost ? "LOST" : s.CargoIntegrity + "%");
            if (info.Level != null && info.Level.Mode == "destruction") Stat(stats2.transform, "CHAOS", s.Chaos.ToString());
            foreach (ObjectiveResult r in info.Results)
            {
                Para(c, (r.Done ? "<color=#c6ff4a>[x]</color> " : "[ ] ") + Objectives.Label(r.Objective) + (r.Objective.Optional ? "  <color=#ffe93a><size=9>MEDAL</size></color>" : ""), 14);
            }
            var buttons = Row(c, TextAnchor.MiddleRight);
            if (info.Mode == GameMode.Arcade)
            {
                UiKit.Button(buttons.transform, "RUN AGAIN", () => { Hide(); g.StartArcade(); }, null, 12, 30, 120, true);
            }
            else
            {
                UiKit.Button(buttons.transform, "RETRY (keep lines)", () => { Hide(); g.Restart(true); }, null, 12, 30, 150);
                UiKit.Button(buttons.transform, "REBUILD (wipe)", () => { Hide(); g.Restart(false); }, null, 12, 30, 130);
            }
            if (info.Next != null && info.Complete && g.LevelUnlocked(info.Next))
            {
                LevelDef next = info.Next;
                UiKit.Button(buttons.transform, "NEXT: " + next.Name + " >", () => Intro(next, false), null, 12, 30, 180, true);
            }
            if (info.Mode == GameMode.Free || info.Mode == GameMode.Library) UiKit.Button(buttons.transform, "KEEP EDITING", () => { Hide(); g.Stop(); }, null, 12, 30, 120);
            if (info.Mode == GameMode.Free) UiKit.Button(buttons.transform, "PUBLISH TRACK", PublishForm, null, 12, 30, 130);
            UiKit.Button(buttons.transform, info.Mode == GameMode.Campaign ? "MAP" : "MENU", () => { if (info.Mode == GameMode.Campaign) Map(_selectedRegion); else Title(); }, UiKit.Muted, 12, 30, 80);
        }

        // ------------------------------------------------------------------ pause

        public void Pause()
        {
            GameController g = _game;
            bool wasPlaying = g.PlayState == PlayState.Play;
            if (wasPlaying) g.Pause();
            Transform c = Panel("pause", 640);
            Heading(c, "PAUSED");
            if (g.Level != null) Para(c, g.Level.Briefing);
            var buttons = Row(c, TextAnchor.MiddleRight);
            UiKit.Button(buttons.transform, "RESUME", () => { Hide(); if (wasPlaying) g.Play(); }, null, 12, 30, 100, true);
            UiKit.Button(buttons.transform, "RESTART", () => { Hide(); g.Restart(true); }, null, 12, 30, 100);
            UiKit.Button(buttons.transform, "WIPE LINES", () => { Hide(); g.Restart(false); g.Stop(); }, null, 12, 30, 110);
            if (g.Mode == GameMode.Free) UiKit.Button(buttons.transform, "PUBLISH TRACK", PublishForm, null, 12, 30, 130);
            UiKit.Button(buttons.transform, "QUIT TO MENU", () => { g.Stop(); Title(); }, UiKit.Muted, 12, 30, 120);
        }

        // ------------------------------------------------------------------ riders

        public void RidersScreen()
        {
            GameController g = _game;
            Transform c = Panel("riders", 1000);
            var head = Row(c);
            Heading(head.transform, "RIDERS");
            UiKit.Button(head.transform, "< menu", Title, UiKit.Muted, 12, 28, 90);
            var grid = UiKit.Grid(c, "Riders", 232, 150, 10);
            foreach (RiderDef r in Riders.All)
            {
                bool unlocked = g.RiderUnlocked(r.Id);
                string rid = r.Id;
                string label = "<b>" + r.Name + "</b>  <size=10><color=#8a92c8>" + r.Tagline.ToUpperInvariant() + "</color></size>\n<size=11>" + r.Description + "</size>\n<size=10><color=#8a92c8>gravity " + U.F(r.GravityScale, "0.00") + " · grip " + U.F(r.FrictionScale, "0.00") + " · toughness " + U.F(r.EnduranceScale, "0.00") + "</color></size>" + (unlocked ? "" : "\n<size=10><color=#ffe93a>Unlock: complete " + r.Unlock + "</color></size>");
                Button b = UiKit.Button(grid.transform, label, () => { if (unlocked) { g.SetRider(rid); RidersScreen(); } }, g.RiderDef.Id == r.Id ? U.Hex(r.Color) : (Color?)new Color(0.4f, 0.4f, 0.5f, unlocked ? 1f : 0.5f), 12, 150);
                Text t = b.GetComponentInChildren<Text>();
                if (t != null) t.alignment = TextAnchor.UpperLeft;
            }
        }

        // ------------------------------------------------------------------ co-op

        public void Coop()
        {
            GameController g = _game;
            Transform c = Panel("coop", 1000);
            var head = Row(c);
            Heading(head.transform, "CO-OP");
            UiKit.Button(head.transform, "< menu", Title, UiKit.Muted, 12, 28, 90);
            Para(c, "Two players share one screen and one rider. Player 1 draws in cyan, player 2 in magenta, each with their own ink budget. Press Tab (or the switch button) to hand over the pen. Finish the level together.");
            UiKit.Button(c, "FREE CANVAS (120 m each)", () => { g.StartCoopFree("rooftops", 120); Hide(); _onEnterGame(); }, null, 13, 32, 0, true);
            Para(c, "Campaign levels", 14, UiKit.Muted);
            var grid = UiKit.Grid(c, "Levels", 250, 96, 8);
            foreach (LevelDef lv in Levels.All)
            {
                if (!g.LevelUnlocked(lv) || lv.Mode == "destruction") continue;
                LevelCard(grid.transform, lv, true);
            }
        }

        // ------------------------------------------------------------------ daily

        public void DailyScreen()
        {
            GameController g = _game;
            string key = Daily.TodayKey();
            g.Progress.Daily.TryGetValue(key, out DailyRecord rec);
            Transform c = Panel("daily", 640);
            UiKit.Label(c, "DAILY CHALLENGE", 11, UiKit.Magenta);
            Heading(c, key);
            Para(c, "Everyone gets the same terrain, flags, finish and ink budget today. Three boards: fastest time, least ink, highest trick score. Records are stored on this device.");
            var stats = Row(c);
            Stat(stats.transform, "BEST TIME", rec != null && rec.Time.HasValue ? U.FormatTime(rec.Time) : "--");
            Stat(stats.transform, "LEAST INK", rec != null && rec.Ink.HasValue ? U.F(rec.Ink.Value) + " m" : "--");
            Stat(stats.transform, "TRICK SCORE", (rec?.Trick ?? 0).ToString());
            var buttons = Row(c, TextAnchor.MiddleRight);
            UiKit.Button(buttons.transform, "< menu", Title, UiKit.Muted, 12, 30, 90);
            UiKit.Button(buttons.transform, "PLAY TODAY", () => { g.StartDaily(); Hide(); _onEnterGame(); }, null, 13, 32, 130, true);
        }

        // ------------------------------------------------------------------ library

        public void Library(string filter)
        {
            GameController g = _game;
            Transform c = Panel("library", 1080);
            var head = Row(c);
            Heading(head.transform, "TRACK LIBRARY");
            UiKit.Button(head.transform, "< menu", Title, UiKit.Muted, 12, 28, 90);
            var filters = Row(c);
            foreach (string f in new[] { "all", "mine", "builtin" })
            {
                string captured = f;
                UiKit.Button(filters.transform, f.ToUpperInvariant(), () => Library(captured), filter == f ? UiKit.Cyan : (Color?)UiKit.Muted, 11, 26, 80);
            }
            Para(c, "Tracks live on this device. Share codes move them between players (and to the web build).", 12, UiKit.Muted);
            var items = new List<PublishedTrack>();
            foreach (PublishedTrack t in g.Store.List())
            {
                if (filter == "mine" && t.Builtin) continue;
                if (filter == "builtin" && !t.Builtin) continue;
                items.Add(t);
            }
            if (items.Count == 0) Para(c, "Nothing here yet. Build something in Free Ride and publish it.", 13, UiKit.Muted);
            var grid = UiKit.Grid(c, "Tracks", 330, 250, 12);
            foreach (PublishedTrack t in items) TrackCard(grid.transform, t, () => Library(filter));
            Para(c, "Import", 14, UiKit.Muted);
            var importRow = Row(c);
            InputField field = UiKit.Input(importRow.transform, "Paste a share code (CYR1.) to import a track");
            UiKit.Size(field, 760, 30, 400);
            UiKit.Button(importRow.transform, "IMPORT", () =>
            {
                PublishedTrack decoded = ShareCodes.Decode(field.text);
                if (decoded == null)
                {
                    _notify("THAT CODE DID NOT DECODE", "#ff3d7f", false);
                    return;
                }
                g.Store.Publish(decoded);
                Library("mine");
            }, null, 12, 30, 100);
        }

        private void TrackCard(Transform parent, PublishedTrack t, Action refresh)
        {
            GameController g = _game;
            Core.Environment env = Environments.Get(t.Environment);
            var card = UiKit.VBox(parent, "Card", 4, 8);
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = UiKit.ButtonBg;
            UiKit.Border(card.GetComponent<RectTransform>(), U.Hex(env.Accent, 0.5f));
            RectTransform thumbRt = UiKit.Rect("Thumb", card.transform);
            var raw = thumbRt.gameObject.AddComponent<RawImage>();
            if (!_thumbs.TryGetValue(t.Id, out Texture2D tex))
            {
                try
                {
                    tex = ThumbnailPainter.Paint(t.LoadTrack(), env, 300, 100);
                }
                catch (Exception)
                {
                    tex = null;
                }
                _thumbs[t.Id] = tex;
            }
            raw.texture = tex;
            UiKit.Size(raw, -1, 100);
            string stars = new string('*', Mathf.Clamp(t.Difficulty, 0, 5)) + new string('-', 5 - Mathf.Clamp(t.Difficulty, 0, 5));
            UiKit.Label(card.transform, "<b>" + t.Title + "</b>  <color=#ffe93a>" + stars + "</color>", 13, Color.white);
            UiKit.Label(card.transform, "by " + t.Author + " - " + env.Name + " - " + (t.Budget.HasValue ? t.Budget.Value + " m ink" : "free ride") + (t.Tags.Count > 0 ? " · #" + string.Join(" #", t.Tags.ToArray()) : ""), 10, UiKit.Muted);
            UiKit.Label(card.transform, "best " + U.FormatTime(t.Records.BestFrames) + " - " + U.FormatInk(t.Records.BestInk) + " - " + t.Records.BestTrick + " pts · " + t.Plays + " plays", 10, UiKit.Muted);
            var row = Row(card.transform);
            UiKit.Button(row.transform, "PLAY", () => { g.PlayLibrary(t); Hide(); _onEnterGame(); }, null, 11, 26, 60, true);
            UiKit.Button(row.transform, "LIKE " + t.Likes, () => { t.Liked = !t.Liked; t.Likes += t.Liked ? 1 : -1; g.Store.Update(t); refresh(); }, t.Liked ? UiKit.Magenta : (Color?)UiKit.Muted, 11, 26, 56);
            UiKit.Button(row.transform, "SHARE CODE", () =>
            {
                string code = ShareCodes.Encode(t);
                GUIUtility.systemCopyBuffer = code;
                _notify("SHARE CODE COPIED", "#4dff9d", false);
            }, null, 11, 26, 100);
            if (!t.Builtin) UiKit.Button(row.transform, "DELETE", () => { g.Store.Remove(t.Id); refresh(); }, UiKit.Muted, 11, 26, 70);
        }

        public void PublishForm()
        {
            GameController g = _game;
            Transform c = Panel("publish", 640);
            Heading(c, "PUBLISH TRACK");
            Para(c, "Your current free-ride drawing, objects, start point and finish zone become a community track. Set an ink budget to turn it into a puzzle for others.", 13, UiKit.Muted);
            Para(c, "Title", 11, UiKit.Muted);
            InputField title = UiKit.Input(c, "Track title", "Untitled run");
            Para(c, "Author", 11, UiKit.Muted);
            InputField author = UiKit.Input(c, "Your name", "Anonymous");
            Para(c, "Description", 11, UiKit.Muted);
            InputField desc = UiKit.Input(c, "Description", "", true, 64);
            Para(c, "Tags (comma separated)", 11, UiKit.Muted);
            InputField tags = UiKit.Input(c, "speed, stunt, puzzle");
            Para(c, "Difficulty 1-5", 11, UiKit.Muted);
            InputField diff = UiKit.Input(c, "2", "2");
            Para(c, "Ink budget in metres (empty = free ride)", 11, UiKit.Muted);
            InputField budget = UiKit.Input(c, "");
            var buttons = Row(c, TextAnchor.MiddleRight);
            UiKit.Button(buttons.transform, "cancel", Hide, UiKit.Muted, 12, 30, 90);
            UiKit.Button(buttons.transform, "PUBLISH", () =>
            {
                var pub = new PublishedTrack
                {
                    Title = string.IsNullOrWhiteSpace(title.text) ? "Untitled run" : title.text.Trim(),
                    Author = string.IsNullOrWhiteSpace(author.text) ? "Anonymous" : author.text.Trim(),
                    Description = desc.text.Trim(),
                    Difficulty = int.TryParse(diff.text, out int d) ? Mathf.Clamp(d, 1, 5) : 2,
                    Environment = g.Environment.Id,
                    Budget = double.TryParse(budget.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double b) && b > 0 ? b : (double?)null,
                    TrackJson = g.Track.ToJson(),
                };
                foreach (string tag in tags.text.Split(','))
                {
                    string tt = tag.Trim().ToLowerInvariant();
                    if (tt.Length > 0) pub.Tags.Add(tt);
                }
                g.Store.Publish(pub);
                _notify("PUBLISHED " + pub.Title.ToUpperInvariant(), "#4dff9d", false);
                Library("mine");
            }, null, 12, 30, 110, true);
        }
    }
}
