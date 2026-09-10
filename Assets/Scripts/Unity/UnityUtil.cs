using System;
using System.Globalization;
using System.IO;
using NeonLineRider.Core;
using UnityEngine;

namespace NeonLineRider.Unity
{
    /// <summary>Coordinate and colour helpers. The simulation uses px with y down; Unity uses units with y up.</summary>
    public static class U
    {
        /// <summary>Unity units per simulation pixel (10 px = 1 unit = 1 metre).</summary>
        public const float Scale = 0.1f;

        public static Vector3 W(double x, double y, float z = 0f)
        {
            return new Vector3((float)(x * Scale), (float)(-y * Scale), z);
        }

        public static Vector2 ToSim(Vector3 unityPos)
        {
            return new Vector2(unityPos.x / Scale, -unityPos.y / Scale);
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(hex)) return new Color(1, 1, 1, alpha);
            string h = hex.TrimStart('#');
            if (h.Length == 3) h = new string(new[] { h[0], h[0], h[1], h[1], h[2], h[2] });
            if (h.Length < 6) return new Color(1, 1, 1, alpha);
            int r = int.Parse(h.Substring(0, 2), NumberStyles.HexNumber);
            int g = int.Parse(h.Substring(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(h.Substring(4, 2), NumberStyles.HexNumber);
            return new Color(r / 255f, g / 255f, b / 255f, alpha);
        }

        public static Color32 Hex32(string hex, float alpha = 1f)
        {
            return Hex(hex, alpha);
        }

        public static Color32 WithAlpha(Color32 c, float alpha)
        {
            c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
            return c;
        }

        public static string FormatTime(int? frames)
        {
            if (!frames.HasValue) return "--.--";
            return (frames.Value / 40.0).ToString("0.00", CultureInfo.InvariantCulture) + "s";
        }

        public static string FormatInk(double? m)
        {
            if (!m.HasValue) return "--";
            return m.Value.ToString("0.0", CultureInfo.InvariantCulture) + " m";
        }

        public static string F(double v, string format = "0.0")
        {
            return v.ToString(format, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Persists save data and the track library as JSON files next to the player's data.</summary>
    public sealed class FileStorage : IStorage
    {
        private readonly string _dir;

        public FileStorage()
        {
            _dir = Path.Combine(Application.persistentDataPath, "neon-line-rider");
            try
            {
                Directory.CreateDirectory(_dir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Save directory unavailable: " + ex.Message);
            }
        }

        private string PathFor(string key) => Path.Combine(_dir, key + ".json");

        public string Load(string key)
        {
            try
            {
                string p = PathFor(key);
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Load failed: " + ex.Message);
                return null;
            }
        }

        public void Save(string key, string value)
        {
            try
            {
                File.WriteAllText(PathFor(key), value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Save failed: " + ex.Message);
            }
        }
    }

    /// <summary>World-to-screen mapping with smooth follow, in simulation pixels (y down).</summary>
    public sealed class CameraModel
    {
        public double X;
        public double Y;
        public double Zoom = 2;
        public int Width = 1280;
        public int Height = 760;
        private double? _targetX;
        private double? _targetY;
        private double? _targetZoom;

        public void Resize(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
        }

        public Vec2d ToScreen(double wx, double wy)
        {
            return new Vec2d((wx - X) * Zoom + Width / 2.0, (wy - Y) * Zoom + Height / 2.0);
        }

        public Vec2d ToWorld(double sx, double sy)
        {
            return new Vec2d((sx - Width / 2.0) / Zoom + X, (sy - Height / 2.0) / Zoom + Y);
        }

        public void Viewport(out double left, out double top, out double right, out double bottom)
        {
            double hw = Width / 2.0 / Zoom;
            double hh = Height / 2.0 / Zoom;
            left = X - hw;
            top = Y - hh;
            right = X + hw;
            bottom = Y + hh;
        }

        public void PanBy(double dxScreen, double dyScreen)
        {
            X -= dxScreen / Zoom;
            Y -= dyScreen / Zoom;
            _targetX = _targetY = null;
        }

        public void ZoomAt(double sx, double sy, double factor)
        {
            Vec2d before = ToWorld(sx, sy);
            Zoom = Math.Min(12, Math.Max(0.15, Zoom * factor));
            Vec2d after = ToWorld(sx, sy);
            X += before.X - after.X;
            Y += before.Y - after.Y;
            _targetZoom = null;
        }

        public void Follow(double x, double y)
        {
            _targetX = x;
            _targetY = y;
        }

        public void StopFollowing()
        {
            _targetX = _targetY = null;
        }

        public void SetZoomTarget(double z)
        {
            _targetZoom = z;
        }

        public void SnapTo(double x, double y)
        {
            X = x;
            Y = y;
            _targetX = _targetY = null;
        }

        public void Update(double dt)
        {
            double k = 1 - Math.Exp(-dt * 8);
            if (_targetX.HasValue && _targetY.HasValue)
            {
                X += (_targetX.Value - X) * k;
                Y += (_targetY.Value - Y) * k;
            }
            if (_targetZoom.HasValue)
            {
                Zoom += (_targetZoom.Value - Zoom) * k;
                if (Math.Abs(_targetZoom.Value - Zoom) < 0.001) _targetZoom = null;
            }
        }
    }
}
