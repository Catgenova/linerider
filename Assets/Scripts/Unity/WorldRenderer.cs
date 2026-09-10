using System;
using System.Collections.Generic;
using NeonLineRider.Core;
using UnityEngine;
using CoreMaterial = NeonLineRider.Core.Material;
using Env = NeonLineRider.Core.Environment;

namespace NeonLineRider.Unity
{
    /// <summary>Draws the world with additive glow meshes, a parallax backdrop, effects and the cave halo.</summary>
    public sealed class WorldRenderer
    {
        private readonly Camera _camera;
        private readonly Transform _root;
        private readonly NeonMesh _bg, _bgGlow, _staticGlow, _staticCore, _dynGlow, _dynCore, _top;
        private readonly UnityEngine.Material _additive, _alpha, _staticGlowMat;
        private readonly Font _font;
        private readonly List<TextMesh> _labels = new List<TextMesh>();
        private int _labelsUsed;
        private int _lastRevision = -1;
        private Run _lastRun;
        private int _lastDead = -1;
        private bool _forceStatic = true;
        private readonly System.Random _rng = new System.Random();
        private readonly List<Vec2dF> _poly = new List<Vec2dF>();

        private const float ZBg = 9f, ZBgGlow = 8.5f, ZStaticGlow = 7f, ZStaticCore = 6.5f, ZDynGlow = 5f, ZDynCore = 4.5f, ZLabel = 3f, ZTop = 2f;

        public WorldRenderer(Camera camera, Transform root, Font font)
        {
            _camera = camera;
            _root = root;
            _font = font;
            Shader add = Resources.Load<Shader>("Shaders/NeonAdditive");
            Shader alp = Resources.Load<Shader>("Shaders/NeonAlpha");
            if (add == null) add = Shader.Find("Neon/Additive");
            if (alp == null) alp = Shader.Find("Neon/Alpha");
            if (add == null) add = Shader.Find("Sprites/Default");
            if (alp == null) alp = Shader.Find("Sprites/Default");
            _additive = new UnityEngine.Material(add) { renderQueue = 3001 };
            _alpha = new UnityEngine.Material(alp) { renderQueue = 3000 };
            _staticGlowMat = new UnityEngine.Material(add) { renderQueue = 3001 };
            _bg = new NeonMesh("Background", _alpha, ZBg, root);
            _bgGlow = new NeonMesh("BackgroundGlow", _additive, ZBgGlow, root);
            _staticGlow = new NeonMesh("TrackGlow", _staticGlowMat, ZStaticGlow, root);
            _staticCore = new NeonMesh("TrackCore", _alpha, ZStaticCore, root);
            _dynGlow = new NeonMesh("DynamicGlow", _additive, ZDynGlow, root);
            _dynCore = new NeonMesh("DynamicCore", _alpha, ZDynCore, root);
            _top = new NeonMesh("Overlay", _alpha, ZTop, root);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = U.Hex("#05020f");
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        // ------------------------------------------------------------------ frame

        public void Render(GameController game)
        {
            CameraModel cam = game.Camera;
            cam.Resize(Screen.width, Screen.height);
            double pulse = 0.5 + 0.5 * Math.Sin(game.Time * Math.PI * 2 * 1.6);
            Effects fx = game.Effects;
            double shakeX = fx.Shake > 0 ? (_rng.NextDouble() - 0.5) * fx.Shake / cam.Zoom : 0;
            double shakeY = fx.Shake > 0 ? (_rng.NextDouble() - 0.5) * fx.Shake / cam.Zoom : 0;
            _camera.orthographicSize = (float)(cam.Height / cam.Zoom / 2 * U.Scale);
            Vector3 pos = U.W(cam.X + shakeX, cam.Y + shakeY, -10f);
            _camera.transform.position = pos;
            _camera.backgroundColor = U.Hex(game.Environment.Bg1);
            _labelsUsed = 0;

            DrawBackground(game, pulse);
            DrawStatic(game, pulse);

            _dynGlow.Clear();
            _dynCore.Clear();
            DrawZones(game, pulse);
            World world = game.Run?.World ?? game.PreviewWorld;
            if (world != null)
            {
                foreach (Entity e in world.Entities) if (e.Active) DrawEntity(e, game, pulse);
                DrawEntityLines(world, cam.Zoom, pulse);
                foreach (Prop prop in world.Props) DrawProp(prop, cam.Zoom, pulse);
            }
            if (game.GhostRun != null && game.GhostEnabled && game.PlayState != PlayState.Edit)
            {
                foreach (Rider rider in game.GhostRun.World.Riders)
                {
                    DrawRider(rider, "#e0c8ff", "#b48cff", cam.Zoom, 0.38f, false, game.Time);
                    Vec2d c = rider.Center();
                    Label(c.X + 6, c.Y - 14, "GHOST", U.Hex("#e0c8ff", 0.5f), 4 / Math.Min(1, cam.Zoom / 2));
                }
            }
            if (game.Run != null)
            {
                foreach (Rider rider in game.Run.World.Riders) DrawRider(rider, game.RiderDef.Color, game.RiderDef.Glow, cam.Zoom, 1f, true, game.Time);
            }
            else DrawStartMarker(game, pulse);
            DrawEffects(fx, cam.Zoom);
            DrawEditorOverlay(game, pulse);
            _dynGlow.Apply();
            _dynCore.Apply();

            _top.Clear();
            DrawDarkness(game);
            if (fx.Flash > 0)
            {
                cam.Viewport(out double l, out double t, out double r, out double b);
                _top.Rect(l - 50, t - 50, r - l + 100, b - t + 100, U.Hex32(fx.FlashColor, (float)fx.Flash));
            }
            _top.Apply();
            for (int i = _labelsUsed; i < _labels.Count; i++) _labels[i].gameObject.SetActive(false);
        }

        private static double CoreWidth(double zoom)
        {
            double core = Math.Min(3.2, Math.Max(0.9, 1.6 / Math.Sqrt(zoom)));
            if (zoom < 1) core *= 1 / Math.Max(zoom, 0.25);
            return core;
        }

        // ------------------------------------------------------------------ labels

        private void Label(double x, double y, string text, Color color, double heightPx)
        {
            TextMesh tm;
            if (_labelsUsed < _labels.Count) tm = _labels[_labelsUsed];
            else
            {
                var go = new GameObject("Label");
                go.transform.SetParent(_root, false);
                tm = go.AddComponent<TextMesh>();
                tm.font = _font;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null && _font != null) mr.sharedMaterial = _font.material;
                _labels.Add(tm);
            }
            _labelsUsed++;
            tm.gameObject.SetActive(true);
            tm.text = text;
            tm.color = color;
            tm.characterSize = (float)(heightPx * U.Scale / 4.8);
            tm.transform.position = U.W(x, y, ZLabel);
        }

        // ------------------------------------------------------------------ background

        private static double Noise(double x, double seed)
        {
            double s = Math.Sin(x * 12.9898 + seed * 78.233) * 43758.5453;
            return s - Math.Floor(s);
        }

        private static double HeightAt(double x, string style, double seed)
        {
            switch (style)
            {
                case "peaks":
                {
                    double a = Math.Sin(x * 0.9 + seed) * 0.5 + Math.Sin(x * 2.3 + seed * 2) * 0.25 + Math.Sin(x * 5.1) * 0.12;
                    return 0.35 + a * 0.35;
                }
                case "crystals":
                {
                    double cell = Math.Floor(x * 3);
                    double h = Noise(cell, seed);
                    double frac = x * 3 - cell;
                    return 0.25 + h * 0.5 * (1 - Math.Abs(frac - 0.5) * 2);
                }
                case "mesas":
                {
                    double cell = Math.Floor(x * 1.2);
                    double h = Noise(cell, seed);
                    double frac = x * 1.2 - cell;
                    double edge = Math.Min(frac, 1 - frac) * 8;
                    return 0.2 + (h > 0.45 ? 0.35 * Math.Min(1, edge) : 0.05);
                }
                case "trees":
                {
                    double cell = Math.Floor(x * 6);
                    double h = Noise(cell, seed);
                    double frac = x * 6 - cell;
                    double trunk = Math.Abs(frac - 0.5) < 0.08 ? 1 : 0;
                    return 0.15 + h * 0.5 * trunk + (Math.Abs(frac - 0.5) < 0.3 ? 0.18 * h : 0);
                }
                case "city":
                {
                    double cell = Math.Floor(x * 4);
                    double h = Noise(cell, seed);
                    return 0.15 + h * h * 0.65;
                }
                case "stalactites":
                {
                    double a = Math.Sin(x * 3.1 + seed) * 0.5 + Math.Sin(x * 7.7) * 0.3;
                    return 0.2 + Math.Abs(a) * 0.35;
                }
                case "craters":
                {
                    double a = Math.Sin(x * 1.3 + seed) * 0.5 + Math.Sin(x * 3.9 + seed) * 0.2;
                    return 0.22 + a * 0.12;
                }
                case "gears":
                {
                    double a = Math.Sin(x * 6) > 0.3 ? 0.1 : 0;
                    return 0.3 + a + Math.Sin(x * 0.7 + seed) * 0.1;
                }
                default:
                    return 0.3;
            }
        }

        private Vec2dF S(CameraModel cam, double sx, double sy)
        {
            Vec2d w = cam.ToWorld(sx, sy);
            return new Vec2dF(w.X, w.Y);
        }

        private void DrawBackground(GameController game, double pulse)
        {
            CameraModel cam = game.Camera;
            Env env = game.Environment;
            double w = cam.Width;
            double h = cam.Height;
            _bg.Clear();
            _bgGlow.Clear();
            Vec2dF tl = S(cam, -40, -40), br = S(cam, w + 40, h + 40);
            _bg.RectGradient(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y, U.Hex32(env.Bg0), U.Hex32(env.Bg1));

            // Stars.
            Color32 accent = U.Hex32(env.Accent);
            for (int i = 0; i < 60; i++)
            {
                double sx = ((Noise(i, 3) * 4000 - cam.X * 0.05) % (w + 40)) - 20;
                double sy = ((Noise(i, 7) * 3000 - cam.Y * 0.05) % (h + 40)) - 20;
                double x = ((sx % (w + 40)) + w + 40) % (w + 40) - 20;
                double y = ((sy % (h + 40)) + h + 40) % (h + 40) - 20;
                double tw = 0.5 + 0.5 * Math.Sin(game.Time * 2 + i);
                Vec2dF p = S(cam, x, y);
                double size = 1.5 / cam.Zoom;
                _bg.Rect(p.X, p.Y, size, size, U.WithAlpha(accent, (float)(0.15 + 0.35 * tw)));
            }

            if (env.HasOrb)
            {
                double ox = env.OrbX * w - cam.X * 0.02;
                double oy = env.OrbY * h - cam.Y * 0.02;
                double r = env.OrbSize / cam.Zoom;
                Vec2dF c = S(cam, ox, oy);
                Color32 orb = U.Hex32(env.OrbColor);
                _bgGlow.DiscGradient(c.X, c.Y, r * 2.2, U.WithAlpha(orb, (float)(0.35 + 0.1 * pulse)), U.WithAlpha(orb, 0f), 40);
                _bg.Disc(c.X, c.Y, r * 0.5, U.WithAlpha(orb, 0.9f), 40);
                Color32 stripe = U.Hex32(env.Bg0);
                for (int i = 0; i < 5; i++)
                {
                    double yy = r * 0.05 + i * r * 0.09;
                    _bg.Rect(c.X - r, c.Y + yy, r * 2, r * 0.02 + i * r * 0.008, stripe);
                }
            }

            // Silhouette layers.
            double[][] layers = { new[] { 0.12, 0.62, 1 }, new[] { 0.25, 0.5, 2 } };
            string[] colors = { env.Fog, env.Bg0 };
            for (int li = 0; li < layers.Length; li++)
            {
                double parallax = layers[li][0];
                double baseY = layers[li][1];
                double seed = layers[li][2];
                Color32 color = U.WithAlpha(U.Hex32(colors[li]), 0.9f);
                Color32 edge = U.WithAlpha(U.Hex32(env.Accent2), (float)(0.25 + 0.15 * pulse));
                double step = 12;
                double horizon = h * baseY - cam.Y * parallax * 0.4;
                double prevX = 0;
                double prevY = 0;
                for (double sx = 0; sx <= w + step; sx += step)
                {
                    double wx = (sx + cam.X * parallax) * 0.004;
                    double hh = HeightAt(wx, env.Skyline, seed) * h * 0.5;
                    double y = horizon + h * 0.5 - hh;
                    if (sx > 0)
                    {
                        Vec2dF a = S(cam, prevX, prevY), b = S(cam, sx, y), c2 = S(cam, sx, h + 40), d = S(cam, prevX, h + 40);
                        _bg.QuadUnits(U.W(a.X, a.Y), U.W(b.X, b.Y), U.W(c2.X, c2.Y), U.W(d.X, d.Y), color);
                        _bg.Segment(a.X, a.Y, b.X, b.Y, 1 / cam.Zoom, edge, false);
                        if (env.Skyline == "city")
                        {
                            Color32 window = U.WithAlpha(accent, 0.35f);
                            for (double wy = y + 10; wy < h; wy += 16)
                            {
                                if (Noise(Math.Floor(sx / 14) * 31 + Math.Floor(wy / 16), seed) > 0.6)
                                {
                                    Vec2dF p = S(cam, sx + 4, wy);
                                    _bg.Rect(p.X, p.Y, 3 / cam.Zoom, 5 / cam.Zoom, window);
                                }
                            }
                        }
                    }
                    prevX = sx;
                    prevY = y;
                }
            }

            // World grid.
            Color32 grid = U.WithAlpha(U.Hex32(env.GridColor), 0.16f);
            double minor = cam.Zoom >= 1.5 ? 25 : cam.Zoom >= 0.6 ? 100 : 400;
            cam.Viewport(out double left, out double top, out double right, out double bottom);
            double x0 = Math.Floor(left / minor) * minor;
            double y0 = Math.Floor(top / minor) * minor;
            double gw = 1 / cam.Zoom;
            for (double x = x0; x <= right; x += minor) _bg.Segment(x, top, x, bottom, gw, grid, false);
            for (double y = y0; y <= bottom; y += minor) _bg.Segment(left, y, right, y, gw, grid, false);

            // Horizon glow band.
            Vec2dF bt = S(cam, 0, h * 0.55), bb = S(cam, w, h);
            _bgGlow.RectGradient(bt.X, bt.Y, bb.X - bt.X, bb.Y - bt.Y, U.WithAlpha(U.Hex32(env.Fog), 0f), U.WithAlpha(U.Hex32(env.Fog), (float)(0.18 * (0.5 + 0.5 * pulse))));
            _bg.Apply();
            _bgGlow.Apply();
        }

        // ------------------------------------------------------------------ static lines

        private void DrawStatic(GameController game, double pulse)
        {
            CameraModel cam = game.Camera;
            World world = game.Run?.World;
            int dead = 0;
            bool crumbling = false;
            if (world != null)
            {
                foreach (Line l in world.Lines.Values) if (l.Dead) dead++;
                crumbling = world.Crumbling.Count > 0;
            }
            bool rebuild = _forceStatic || game.Track.Revision != _lastRevision || game.Run != _lastRun || dead != _lastDead || crumbling;
            _staticGlowMat.SetColor("_Color", new Color(1, 1, 1, (float)((0.09 + 0.05 * pulse) / 0.14)));
            if (!rebuild) return;
            _forceStatic = false;
            _lastRevision = game.Track.Revision;
            _lastRun = game.Run;
            _lastDead = dead;
            _staticGlow.Clear();
            _staticCore.Clear();
            double core = CoreWidth(cam.Zoom);
            bool showArrows = cam.Zoom >= 0.7;
            bool showTicks = cam.Zoom >= 2.2 && game.Progress.ShowTicks;
            int frame = world?.Frame ?? 0;
            if (world != null)
            {
                foreach (Line l in world.Lines.Values)
                {
                    if (l.Dead) continue;
                    DrawTrackLine(l.X1, l.Y1, l.X2, l.Y2, l.Material, l.Flipped, l.Player, l.CrumbleAt, frame, core, showArrows, showTicks);
                }
            }
            else
            {
                foreach (LineData l in game.Track.Lines.Values)
                {
                    DrawTrackLine(l.X1, l.Y1, l.X2, l.Y2, Materials.Get(l.Material), l.Flipped, l.Player, -1, 0, core, showArrows, showTicks);
                }
            }
            _staticGlow.Apply();
            _staticCore.Apply();
        }

        private void DrawTrackLine(double x1, double y1, double x2, double y2, CoreMaterial mat, bool flipped, int player, int crumbleAt, int frame, double core, bool showArrows, bool showTicks)
        {
            bool p2 = player == 2 && mat.Id == MaterialId.Normal;
            Color32 glow = U.Hex32(p2 ? "#ff00c8" : mat.Glow);
            Color32 color = U.Hex32(p2 ? "#ff7ae8" : mat.Color);
            bool scenery = mat.Id == MaterialId.Scenery;
            _staticGlow.Segment(x1, y1, x2, y2, core * 6.5, U.WithAlpha(glow, scenery ? 0.05f : 0.14f));
            _staticGlow.Segment(x1, y1, x2, y2, core * 2.8, U.WithAlpha(glow, scenery ? 0.12f : 0.28f));
            if (crumbleAt >= 0)
            {
                // Dashed, flickering core for a line about to shatter.
                if (((frame - crumbleAt) & 2) == 0) DrawDashed(_staticCore, x1, y1, x2, y2, core * 0.9, U.Hex32("#ffffff", 0.7f), 3, 4);
                else _staticCore.Segment(x1, y1, x2, y2, core, color);
            }
            else _staticCore.Segment(x1, y1, x2, y2, core, color);
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            double s = flipped ? -1 : 1;
            double ux = (dx / len) * s;
            double uy = (dy / len) * s;
            double nx = -uy;
            double ny = ux;
            if (showArrows && mat.Arrows && len > 14)
            {
                int count = Math.Max(1, (int)Math.Floor(len / 28));
                Color32 white = U.Hex32("#ffffff", 0.85f);
                for (int i = 0; i < count; i++)
                {
                    double t = (i + 0.5) / count;
                    double cx = x1 + dx * t;
                    double cy = y1 + dy * t;
                    const double a = 3.2;
                    _staticCore.Segment(cx - ux * a - nx * a, cy - uy * a - ny * a, cx + ux * a, cy + uy * a, core * 0.8, white, false);
                    _staticCore.Segment(cx + ux * a, cy + uy * a, cx - ux * a + nx * a, cy - uy * a + ny * a, core * 0.8, white, false);
                }
            }
            if (showTicks && mat.Solid)
            {
                int count = (int)Math.Floor(len / 10);
                Color32 tick = U.Hex32("#ffffff", 0.3f);
                for (int i = 1; i <= count; i++)
                {
                    double t = (i - 0.5) / Math.Max(1, count);
                    double cx = x1 + dx * t;
                    double cy = y1 + dy * t;
                    _staticCore.Segment(cx, cy, cx + nx * 2.2, cy + ny * 2.2, core * 0.6, tick, false);
                }
            }
        }

        private static void DrawDashed(NeonMesh mesh, double x1, double y1, double x2, double y2, double width, Color32 color, double dash, double gap)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return;
            double ux = dx / len, uy = dy / len;
            double pos = 0;
            while (pos < len)
            {
                double end = Math.Min(len, pos + dash);
                mesh.Segment(x1 + ux * pos, y1 + uy * pos, x1 + ux * end, y1 + uy * end, width, color, false);
                pos += dash + gap;
            }
        }

        // ------------------------------------------------------------------ entities & props

        private void DrawEntityLines(World world, double zoom, double pulse)
        {
            double core = Math.Min(3.2, Math.Max(0.9, 1.6 / Math.Sqrt(zoom)));
            Color32 glow = U.Hex32("#ffd000", (float)(0.1 + 0.05 * pulse));
            Color32 coreColor = U.Hex32("#fff3a0");
            foreach (Entity e in world.Entities)
            {
                if (!e.Active) continue;
                foreach (Line l in e.Lines)
                {
                    if (l.Dead) continue;
                    _dynGlow.Segment(l.X1, l.Y1, l.X2, l.Y2, core * 6, glow);
                    _dynCore.Segment(l.X1, l.Y1, l.X2, l.Y2, core, coreColor);
                }
            }
        }

        private void DrawEntity(Entity e, GameController game, double pulse)
        {
            EntityDef d = e.Def;
            double time = game.Time;
            switch (e.Kind)
            {
                case "platform":
                    DrawDashed(_dynCore, d.X - d.Dx, d.Y - d.Dy, d.X + d.Dx, d.Y + d.Dy, 0.8, U.Hex32("#fff04a", 0.25f), 3, 3);
                    break;
                case "gear":
                {
                    Color32 c = U.Hex32("#fff04a", (float)(0.25 + 0.15 * pulse));
                    _dynCore.Circle(d.X, d.Y, d.Radius * 0.35, 1, c);
                    _dynCore.Circle(d.X, d.Y, d.Radius * 0.1, 1, c, 12);
                    break;
                }
                case "pendulum":
                {
                    var p = (Pendulum)e;
                    double ex = d.X - Math.Sin(p.Angle) * d.Length;
                    double ey = d.Y + Math.Cos(p.Angle) * d.Length;
                    _dynCore.Segment(d.X, d.Y, ex, ey, 1, U.Hex32("#fff04a", 0.7f), false);
                    _dynCore.Disc(d.X, d.Y, 2, U.Hex32("#fff04a"), 10);
                    break;
                }
                case "fan":
                {
                    _dynGlow.Rect(d.X, d.Y, d.W, d.H, U.Hex32("#39f6ff", (float)(0.05 + 0.03 * pulse)));
                    double len = Math.Sqrt(d.Fx * d.Fx + d.Fy * d.Fy);
                    if (len == 0) len = 1;
                    double ux = d.Fx / len, uy = d.Fy / len;
                    int streaks = Math.Max(3, (int)Math.Floor((d.W + d.H) / 30));
                    Color32 streak = U.Hex32("#39f6ff", 0.45f);
                    for (int i = 0; i < streaks; i++)
                    {
                        double t = ((time * 60 + i * 37) % 100) / 100;
                        double bx = d.X + ((i * 53) % Math.Max(1, d.W));
                        double by = d.Y + ((i * 31) % Math.Max(1, d.H));
                        double sx = bx + ux * t * Math.Max(d.W, d.H) * 0.5;
                        double sy = by + uy * t * Math.Max(d.W, d.H) * 0.5;
                        if (sx < d.X || sx > d.X + d.W || sy < d.Y || sy > d.Y + d.H) continue;
                        _dynCore.Segment(sx, sy, sx + ux * 8, sy + uy * 8, 0.8, streak, false);
                    }
                    break;
                }
                case "magnet":
                {
                    for (int i = 0; i < 3; i++)
                    {
                        double k = (time * 0.6 + i / 3.0) % 1;
                        _dynGlow.Circle(d.X, d.Y, d.Radius * (1 - k), 1, U.Hex32("#c65cff", (float)((1 - k) * 0.5)));
                    }
                    _dynCore.Disc(d.X, d.Y, 4, U.Hex32("#c65cff", (float)(0.7 + 0.3 * pulse)), 12);
                    break;
                }
                case "cannon":
                {
                    double a = d.Angle * Math.PI / 180;
                    _dynGlow.Circle(d.X, d.Y, 12, 6, U.Hex32("#ff3d7f", (float)(0.2 + 0.2 * pulse)));
                    double cos = Math.Cos(a), sin = Math.Sin(a);
                    Vec2dF P(double lx, double ly) => new Vec2dF(d.X + lx * cos - ly * sin, d.Y + lx * sin + ly * cos);
                    _poly.Clear();
                    _poly.Add(P(-4, -7));
                    _poly.Add(P(26, -7));
                    _poly.Add(P(26, 7));
                    _poly.Add(P(-4, 7));
                    _dynCore.Polygon(_poly, U.Hex32("#05021a", 0.85f));
                    for (int i = 0; i < 4; i++)
                    {
                        Vec2dF s1 = _poly[i], s2 = _poly[(i + 1) % 4];
                        _dynCore.Segment(s1.X, s1.Y, s2.X, s2.Y, 1.5, U.Hex32("#ff3d7f"), false);
                    }
                    _dynCore.Disc(d.X, d.Y, 10, U.Hex32("#05021a", 0.85f), 16);
                    _dynCore.Circle(d.X, d.Y, 10, 1.5, U.Hex32("#ff3d7f"), 16);
                    break;
                }
                case "wall":
                {
                    Color32 c = U.Hex32("#ff7a45", (float)(0.5 + 0.3 * pulse));
                    for (int i = 1; i < 6; i++)
                    {
                        double t = i / 6.0;
                        double x = d.X1 + (d.X2 - d.X1) * t;
                        double y = d.Y1 + (d.Y2 - d.Y1) * t;
                        _dynCore.Segment(x - 2, y - 2, x + 2, y + 2, 0.7, c, false);
                    }
                    break;
                }
                case "balloon":
                {
                    var b = (Balloon)e;
                    double x = d.X, y = d.Y;
                    if (b.Holder >= 0 && b.RiderRef != null)
                    {
                        Point sh = b.RiderRef.Points[b.RiderRef.Model.AnchorShoulder];
                        x = sh.X + Math.Sin(time * 3) * 2;
                        y = sh.Y - 18;
                        _dynCore.Segment(sh.X, sh.Y, x, y + 6, 0.6, U.Hex32("#ffffff", 0.6f), false);
                    }
                    else y += Math.Sin(time * 2 + d.X) * 2;
                    _dynGlow.Disc(x, y, 9, U.Hex32("#ff7ae8", (float)(0.15 + 0.1 * pulse)), 16);
                    _dynCore.Circle(x, y, 5.5, 1.2, U.Hex32("#ff7ae8"), 16);
                    break;
                }
                case "seesaw":
                    _dynCore.Segment(d.X, d.Y + 2, d.X - 8, d.Y + 16, 1, U.Hex32("#fff04a"), false);
                    _dynCore.Segment(d.X - 8, d.Y + 16, d.X + 8, d.Y + 16, 1, U.Hex32("#fff04a"), false);
                    _dynCore.Segment(d.X + 8, d.Y + 16, d.X, d.Y + 2, 1, U.Hex32("#fff04a"), false);
                    break;
                case "train":
                {
                    if (e.Lines.Count == 0) break;
                    Line l = e.Lines[0];
                    double x = (l.X1 + l.X2) / 2;
                    double y = l.Y1;
                    _dynGlow.Rect(x - d.W / 2, y, d.W, d.H, U.Hex32("#fff04a", (float)(0.06 + 0.04 * pulse)));
                    int wins = Math.Max(1, (int)Math.Floor(d.W / 14));
                    Color32 win = U.Hex32("#39f6ff", (float)(0.5 + 0.3 * pulse));
                    for (int i = 0; i < wins; i++) _dynGlow.Rect(x - d.W / 2 + 4 + i * 14, y + d.H * 0.25, 7, d.H * 0.3, win);
                    break;
                }
                case "rockfall":
                {
                    var r = (Rockfall)e;
                    if (r.Triggered) break;
                    DrawDashed(_dynCore, d.Trigger, d.Y - 200, d.Trigger, d.Y + 400, 0.8, U.Hex32("#ff7a45", (float)(0.2 + 0.2 * pulse)), 3, 5);
                    break;
                }
                case "bridge":
                {
                    var b = (Bridge)e;
                    Color32 c = U.Hex32("#ff7a45", (float)(b.TouchedAt >= 0 ? 0.6 + 0.4 * pulse : 0.3));
                    double seg = d.W / Math.Max(1, d.Segments);
                    for (int i = 0; i <= d.Segments; i++)
                    {
                        double x = d.X + i * seg;
                        _dynCore.Segment(x, d.Y, x, d.Y + 10, 0.7, c, false);
                    }
                    break;
                }
                case "collapse":
                    DrawDashed(_dynCore, d.X1, d.Y1 + 2, d.X2, d.Y2 + 2, 0.6, U.Hex32("#ff7a45", 0.5f), 2, 3);
                    break;
                case "avalanche":
                {
                    var a = (Avalanche)e;
                    double x = a.X;
                    _dynCore.RectGradient(x - 3000, -6000, 2400, 12000, U.Hex32("#ffffff", 0.75f), U.Hex32("#ffffff", 0.75f));
                    Vector3 p1 = U.W(x - 600, -6000), p2 = U.W(x, -6000), p3 = U.W(x, 6000), p4 = U.W(x - 600, 6000);
                    _dynCore.QuadUnits(p1, p2, p3, p4, U.Hex32("#c8f0ff", 0.55f), U.Hex32("#78dcff", 0.05f), U.Hex32("#78dcff", 0.05f), U.Hex32("#c8f0ff", 0.55f));
                    for (int i = 0; i < 40; i++)
                    {
                        double yy = ((i * 173 + time * 240) % 1400) - 700;
                        double xx = x - ((i * 97 + time * 90) % 260);
                        _dynGlow.Disc(xx, yy, 6 + (i % 5) * 4 + pulse * 2, U.Hex32("#ffffff", (float)(0.25 + 0.4 * ((i % 3) / 2.0))), 10);
                    }
                    break;
                }
            }
        }

        private void DrawProp(Prop prop, double zoom, double pulse)
        {
            if (!prop.Active) return;
            string color, glow;
            switch (prop.Kind)
            {
                case "domino": color = "#e8f4ff"; glow = "#7ad0ff"; break;
                case "crate": color = "#ffb347"; glow = "#ff7a00"; break;
                case "tnt": color = "#ff3d5c"; glow = "#ff0033"; break;
                case "cart": color = "#c6ff4a"; glow = "#8cff00"; break;
                case "cargo": color = "#ff7ae8"; glow = "#ff2bd6"; break;
                case "boulder": color = "#b8c0d8"; glow = "#7a86b0"; break;
                case "snowball": color = "#ffffff"; glow = "#a8f4ff"; break;
                case "rock": color = "#a09aa8"; glow = "#6a6478"; break;
                default: color = "#ffe93a"; glow = "#ffd000"; break;
            }
            double core = Math.Min(2.4, Math.Max(0.8, 1.4 / Math.Sqrt(zoom)));
            Color32 glowC = U.Hex32(glow, (float)(0.25 + 0.1 * pulse));
            Color32 fill = U.Hex32(prop.Kind == "tnt" && prop.Fuse >= 0 ? "#ff3c50" : "#05021a", prop.Kind == "tnt" && prop.Fuse >= 0 ? 0.6f : 0.8f);
            if (prop.IsCircle)
            {
                Point p = prop.Points[0];
                _dynGlow.Circle(p.X, p.Y, prop.Radius, core * 4, glowC);
                _dynCore.Disc(p.X, p.Y, prop.Radius, fill, 24);
                _dynCore.Circle(p.X, p.Y, prop.Radius, core * 1.2, U.Hex32(color));
                Color32 spoke = U.Hex32(color, 0.6f);
                for (int i = 0; i < 3; i++)
                {
                    double a = prop.Spin + i * 2.1;
                    _dynCore.Segment(p.X, p.Y, p.X + Math.Cos(a) * prop.Radius, p.Y + Math.Sin(a) * prop.Radius, core, spoke, false);
                }
                return;
            }
            _poly.Clear();
            foreach (Point p in prop.Points) _poly.Add(new Vec2dF(p.X, p.Y));
            _dynCore.Polygon(_poly, fill);
            for (int i = 0; i < _poly.Count; i++)
            {
                Vec2dF a = _poly[i], b = _poly[(i + 1) % _poly.Count];
                _dynGlow.Segment(a.X, a.Y, b.X, b.Y, core * 4, glowC);
                _dynCore.Segment(a.X, a.Y, b.X, b.Y, core * 1.2, U.Hex32(color));
            }
            if (prop.Kind == "tnt" || prop.Kind == "cargo" || prop.Kind == "crate")
            {
                Vec2d c = prop.Center();
                Label(c.X, c.Y, prop.Kind == "tnt" ? "TNT" : prop.Kind == "cargo" ? "FRAGILE" : "X", U.Hex(color), Math.Max(3, prop.Height * 0.35));
            }
        }

        // ------------------------------------------------------------------ riders

        private void DrawRider(Rider rider, string colorHex, string glowHex, double zoom, float alpha, bool trail, double time)
        {
            bool dead = rider.Dead;
            Color32 color = U.Hex32(dead ? "#ff4d4d" : colorHex, alpha);
            Color32 glow = U.Hex32(dead ? "#ff2020" : glowHex, alpha);
            double core = Math.Min(2.4, Math.Max(0.8, 1.4 / Math.Sqrt(zoom)));
            RiderPose pose = RiderPose.Compute(rider, time);

            if (trail && rider.Trail.Count >= 4)
            {
                int n = rider.Trail.Count / 2;
                for (int i = 1; i < n; i++)
                {
                    float a = (float)(i / (double)n * 0.35 * alpha);
                    _dynGlow.Segment(rider.Trail[(i - 1) * 2], rider.Trail[(i - 1) * 2 + 1], rider.Trail[i * 2], rider.Trail[i * 2 + 1], core * 2.5 * (i / (double)n), U.WithAlpha(glow, a), false);
                }
            }

            DrawHoverboard(pose, color, glow, core, alpha, time, dead);

            ScarfNode[] sc = rider.Scarf;
            for (int i = 1; i < sc.Length; i++)
            {
                Color32 c = U.Hex32(i % 2 == 0 ? "#ff2bd6" : "#ffffff", (float)(alpha * (1 - (i / (double)sc.Length) * 0.6)));
                _dynCore.Segment(sc[i - 1].X + pose.ScarfDx, sc[i - 1].Y + pose.ScarfDy, sc[i].X + pose.ScarfDx, sc[i].Y + pose.ScarfDy, core * 1.1, c);
            }

            Color32 body = U.Hex32("#f6f8ff", alpha);
            Color32 bodyGlow = U.WithAlpha(glow, 0.35f * alpha);
            foreach (Seg s in pose.Segments)
            {
                _dynGlow.Segment(s.X1, s.Y1, s.X2, s.Y2, core * 5, bodyGlow);
                _dynCore.Segment(s.X1, s.Y1, s.X2, s.Y2, core * 1.25, body);
                _dynCore.Disc(s.X2, s.Y2, core * 0.55, body, 8);
            }
            double headR = rider.Model.HeadRadius;
            Vec2d head = pose.Head;
            _dynGlow.Circle(head.X, head.Y, headR + core * 2, core * 3, U.WithAlpha(glow, 0.3f * alpha), 16);
            _dynCore.Disc(head.X, head.Y, headR, U.Hex32("#05021a", 0.9f * alpha), 16);
            _dynCore.Circle(head.X, head.Y, headR, core, body, 16);
            double baseAng = Math.Atan2(pose.HeadUp.Y, pose.HeadUp.X) - Math.PI / 2;
            double vr = headR * 0.6;
            double a0 = baseAng + Math.PI / 4 - 0.2;
            double a1 = baseAng + Math.PI * 0.95;
            double px = head.X + Math.Cos(a0) * vr, py = head.Y + Math.Sin(a0) * vr;
            for (int i = 1; i <= 6; i++)
            {
                double a = a0 + (a1 - a0) * i / 6;
                double x = head.X + Math.Cos(a) * vr, y = head.Y + Math.Sin(a) * vr;
                _dynCore.Segment(px, py, x, y, core, color, false);
                px = x;
                py = y;
            }
            for (int i = 0; i < rider.Passengers; i++)
            {
                double ox = head.X - pose.HeadUp.X * 6 - (i + 1) * 4 * pose.HeadUp.Y;
                double oy = head.Y - pose.HeadUp.Y * 6 + (i + 1) * 4 * pose.HeadUp.X;
                _dynCore.Circle(ox, oy - 3, 1.6, core * 0.8, U.Hex32("#ff7ae8", alpha), 10);
                _dynCore.Segment(ox, oy - 1.5, ox, oy + 3, core * 0.8, U.Hex32("#ff7ae8", alpha), false);
            }
        }

        /// <summary>A thin deck riding on the physics contact line, with pulsing thruster pads underneath.</summary>
        private void DrawHoverboard(RiderPose pose, Color32 color, Color32 glow, double core, float alpha, double time, bool dead)
        {
            double L = pose.Length;
            const double ext = 2.6;
            const double th = 2.3;
            Vec2d tail = pose.Tail;
            double ux = pose.Ux, uy = pose.Uy, nx = pose.Nx, ny = pose.Ny;
            Vec2dF P(double a, double h) => new Vec2dF(tail.X + ux * a + nx * h, tail.Y + uy * a + ny * h);
            _poly.Clear();
            _poly.Add(P(-ext, 1.3));
            _poly.Add(P(0, 0));
            _poly.Add(P(L, 0));
            _poly.Add(P(L + ext, 1.3));
            _poly.Add(P(L + ext, 1.3 + th * 0.6));
            _poly.Add(P(L * 0.78, th + 0.5));
            _poly.Add(P(L * 0.5, th + 0.9));
            _poly.Add(P(L * 0.22, th + 0.5));
            _poly.Add(P(-ext, 1.3 + th * 0.6));
            double angle = Math.Atan2(uy, ux);
            double pulse = 0.5 + 0.5 * Math.Sin(time * 14);
            float padAlpha = (float)((dead ? 0.15 : 0.45 + 0.35 * pulse + Math.Min(0.3, pose.Speed * 0.03)) * alpha);

            // Light spilling onto the ground under the deck.
            Vec2dF u0 = P(-ext, 0), u1 = P(L + ext, 0), u2 = P(L + ext + 2, -6), u3 = P(-ext - 2, -6);
            Color32 spill = U.WithAlpha(glow, (float)((dead ? 0.08 : 0.3) * alpha));
            Color32 none = U.WithAlpha(glow, 0f);
            _dynGlow.QuadUnits(U.W(u0.X, u0.Y), U.W(u1.X, u1.Y), U.W(u2.X, u2.Y), U.W(u3.X, u3.Y), spill, spill, none, none);
            foreach (double a in new[] { L * 0.27, L * 0.73 })
            {
                Vec2dF pad = P(a, -1.3);
                _dynGlow.Ellipse(pad.X, pad.Y, 4.4, 2.2, angle, U.WithAlpha(glow, padAlpha * 0.4f));
                _dynGlow.Ellipse(pad.X, pad.Y, 2.1, 0.9, angle, U.Hex32("#ffffff", padAlpha * 0.9f));
            }
            var outline = new List<Vec2dF>(_poly);
            for (int i = 0; i < outline.Count; i++)
            {
                Vec2dF a = outline[i], b = outline[(i + 1) % outline.Count];
                _dynGlow.Segment(a.X, a.Y, b.X, b.Y, core * 4, U.WithAlpha(glow, 0.35f * alpha));
            }
            _dynCore.Polygon(_poly, U.Hex32("#05021a", 0.92f * alpha));
            for (int i = 0; i < outline.Count; i++)
            {
                Vec2dF a = outline[i], b = outline[(i + 1) % outline.Count];
                _dynCore.Segment(a.X, a.Y, b.X, b.Y, core * 1.2, color);
            }
            Vec2dF s0 = P(1.2, th + 0.1), s1 = P(L - 1.2, th + 0.1);
            _dynCore.Segment(s0.X, s0.Y, s1.X, s1.Y, core * 0.7, U.Hex32("#ffffff", 0.55f * alpha), false);
        }

        private void DrawStartMarker(GameController game, double pulse)
        {
            Vec2d s = game.Track.Start;
            Color32 c = U.Hex32(game.RiderDef.Color, (float)(0.5 + 0.3 * pulse));
            DrawDashed(_dynCore, s.X - 2, s.Y - 8, s.X + 20, s.Y - 8, 1, c, 2, 2);
            DrawDashed(_dynCore, s.X + 20, s.Y - 8, s.X + 20, s.Y + 6, 1, c, 2, 2);
            DrawDashed(_dynCore, s.X + 20, s.Y + 6, s.X - 2, s.Y + 6, 1, c, 2, 2);
            DrawDashed(_dynCore, s.X - 2, s.Y + 6, s.X - 2, s.Y - 8, 1, c, 2, 2);
            _dynCore.Segment(s.X + 24, s.Y - 1, s.X + 30, s.Y - 1, 1, c, false);
            _dynCore.Segment(s.X + 27, s.Y - 4, s.X + 30, s.Y - 1, 1, c, false);
            _dynCore.Segment(s.X + 30, s.Y - 1, s.X + 27, s.Y + 2, 1, c, false);
            Label(s.X + 9, s.Y - 13, "START", U.Hex(game.RiderDef.Color), 4);
        }

        // ------------------------------------------------------------------ zones and markers

        private void DrawZones(GameController game, double pulse)
        {
            double t = game.Time;
            Zone? finish = game.CurrentFinish;
            if (finish.HasValue)
            {
                Zone z = finish.Value;
                _dynGlow.RectGradient(z.X, z.Y, z.W, z.H, U.Hex32("#4dff9d", 0.02f), U.Hex32("#4dff9d", 0.18f));
                double scan = z.Y + ((t * 40) % z.H);
                _dynGlow.Rect(z.X, scan, z.W, 1.5, U.Hex32("#4dff9d", (float)(0.25 + 0.2 * pulse)));
                Color32 c = U.Hex32("#4dff9d");
                DrawDashed(_dynCore, z.X, z.Y, z.X + z.W, z.Y, 1, c, 4, 3);
                DrawDashed(_dynCore, z.X + z.W, z.Y, z.X + z.W, z.Y + z.H, 1, c, 4, 3);
                DrawDashed(_dynCore, z.X + z.W, z.Y + z.H, z.X, z.Y + z.H, 1, c, 4, 3);
                DrawDashed(_dynCore, z.X, z.Y + z.H, z.X, z.Y, 1, c, 4, 3);
                Label(z.X + z.W / 2, z.Y - 6, "FINISH", U.Hex("#4dff9d"), 6);
            }
            foreach (Marker m in game.Markers)
            {
                if (m.Kind == "flag")
                {
                    double wave = Math.Sin(t * 6 + m.X) * 1.5;
                    Color32 c = U.Hex32(m.Done ? "#8080a0" : "#ffe93a", m.Done ? 0.3f : 1f);
                    _dynCore.Segment(m.X, m.Y, m.X, m.Y - 18, 1.2, c, false);
                    _poly.Clear();
                    _poly.Add(new Vec2dF(m.X, m.Y - 18));
                    _poly.Add(new Vec2dF(m.X + 9 + wave, m.Y - 14));
                    _poly.Add(new Vec2dF(m.X, m.Y - 10));
                    _dynCore.Polygon(_poly, c);
                    if (!m.Done) _dynGlow.Circle(m.X, m.Y - 9, 12 + pulse * 3, 1, U.Hex32("#ffe93a", (float)(0.25 + 0.25 * pulse)), 20);
                }
                else if (!m.Done)
                {
                    double bob = Math.Sin(t * 4 + m.X);
                    Color32 c = U.Hex32("#ff7ae8");
                    _dynCore.Segment(m.X, m.Y - 2 + bob, m.X, m.Y - 8 + bob, 1.2, c, false);
                    _dynCore.Segment(m.X - 3, m.Y - 4 + bob, m.X + 3, m.Y - 9 + bob, 1.2, c, false);
                    _dynCore.Segment(m.X, m.Y - 2 + bob, m.X - 2.5, m.Y + 2 + bob, 1.2, c, false);
                    _dynCore.Segment(m.X, m.Y - 2 + bob, m.X + 2.5, m.Y + 2 + bob, 1.2, c, false);
                    _dynCore.Circle(m.X, m.Y - 10.5 + bob, 2.2, 1.2, c, 12);
                    _dynGlow.Circle(m.X, m.Y - 5, 14 + pulse * 4, 1, U.Hex32("#ff7ae8", (float)(0.3 + 0.3 * pulse)), 20);
                    Label(m.X, m.Y - 18 + bob, "HELP!", U.Hex("#ff7ae8"), 4);
                }
            }
        }

        // ------------------------------------------------------------------ effects, overlay, darkness

        private void DrawEffects(Effects fx, double zoom)
        {
            foreach (Particle p in fx.Particles)
            {
                float a = (float)(1 - p.Life / p.Max);
                _dynGlow.Rect(p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size, U.Hex32(p.Color, a));
            }
            foreach (Ring r in fx.Rings)
            {
                double k = r.Life / r.Max;
                _dynGlow.Circle(r.X, r.Y, r.Radius * (0.2 + 0.8 * k), 2 / zoom + 0.5, U.Hex32(r.Color, (float)((1 - k) * 0.8)), 32);
            }
            foreach (Popup p in fx.Popups)
            {
                double k = p.Life / p.Max;
                float a = (float)(k < 0.8 ? 1 : 1 - (k - 0.8) / 0.2);
                double grow = 1 + Math.Sin(Math.Min(1, k * 4) * Math.PI) * 0.3;
                Label(p.X, p.Y, p.Text, U.Hex(p.Color, a), (11 / zoom) * p.Scale * grow);
            }
        }

        private void DrawEditorOverlay(GameController game, double pulse)
        {
            Core.Editor editor = game.Editor;
            double zoom = game.Camera.Zoom;
            if (editor.Preview != null)
            {
                double[] p = editor.Preview;
                CoreMaterial mat = Materials.Get(editor.Material);
                DrawDashed(_dynCore, p[0], p[1], p[2], p[3], 1.5 / zoom + 0.4, U.Hex32(mat.Color, 0.85f), 4 / zoom, 3 / zoom);
                double len = Math.Sqrt((p[2] - p[0]) * (p[2] - p[0]) + (p[3] - p[1]) * (p[3] - p[1])) / 10;
                Label(p[2] + 14 / zoom, p[3] - 8 / zoom, U.F(len * mat.Cost) + " m", U.Hex(mat.Color), 10 / zoom);
            }
            if (editor.Tool == ToolId.Eraser)
            {
                Vec2d c = editor.CursorWorld;
                _dynCore.Circle(c.X, c.Y, editor.EraserRadiusScreen / zoom, 1 / zoom, U.Hex32("#ff3d7f", 0.8f), 24);
            }
            if (editor.Tool == ToolId.Object && editor.Constraints.CanPlaceObjects)
            {
                Vec2d c = editor.CursorWorld;
                _dynCore.Circle(c.X, c.Y, 6 / zoom, 1 / zoom, U.Hex32("#ffe93a", 0.7f), 16);
            }
            if (editor.HoverId >= 0 && game.Track.Lines.TryGetValue(editor.HoverId, out LineData l))
            {
                _dynCore.Segment(l.X1, l.Y1, l.X2, l.Y2, 3 / zoom, U.Hex32("#ffffff", (float)(0.5 + 0.4 * pulse)));
            }
        }

        private void DrawDarkness(GameController game)
        {
            double radius = game.Environment.Darkness;
            if (radius <= 0) return;
            Vec2d f = game.FocusPoint();
            double r = radius * (game.Run != null ? 1 : 1.4);
            Color32 bg = U.Hex32("#020106");
            Color32 c0 = U.WithAlpha(bg, 0f);
            Color32 c1 = U.WithAlpha(bg, 0.75f);
            Color32 c2 = U.WithAlpha(bg, 0.97f);
            const int segs = 40;
            double r0 = r * 0.35, r1 = r * 0.7, r2 = r;
            game.Camera.Viewport(out double left, out double top, out double right, out double bottom);
            double big = Math.Max(right - left, bottom - top) * 2 + r;
            for (int i = 0; i < segs; i++)
            {
                double a = i / (double)segs * Math.PI * 2;
                double b = (i + 1) / (double)segs * Math.PI * 2;
                double ca = Math.Cos(a), sa = Math.Sin(a), cb = Math.Cos(b), sb = Math.Sin(b);
                _top.QuadUnits(U.W(f.X + ca * r0, f.Y + sa * r0), U.W(f.X + cb * r0, f.Y + sb * r0), U.W(f.X + cb * r1, f.Y + sb * r1), U.W(f.X + ca * r1, f.Y + sa * r1), c0, c0, c1, c1);
                _top.QuadUnits(U.W(f.X + ca * r1, f.Y + sa * r1), U.W(f.X + cb * r1, f.Y + sb * r1), U.W(f.X + cb * r2, f.Y + sb * r2), U.W(f.X + ca * r2, f.Y + sa * r2), c1, c1, c2, c2);
                _top.QuadUnits(U.W(f.X + ca * r2, f.Y + sa * r2), U.W(f.X + cb * r2, f.Y + sb * r2), U.W(f.X + cb * big, f.Y + sb * big), U.W(f.X + ca * big, f.Y + sa * big), c2, c2, c2, c2);
            }
            _top.DiscGradient(f.X, f.Y, r0, c0, c0, 20);
        }
    }
}
