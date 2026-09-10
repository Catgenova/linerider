using System;
using NeonLineRider.Core;
using UnityEngine;

namespace NeonLineRider.Unity
{
    /// <summary>Paints a small preview of a track into a texture for the library cards.</summary>
    public static class ThumbnailPainter
    {
        public static Texture2D Paint(Track track, Core.Environment env, int width = 320, int height = 180)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            Color32 top = U.Hex32(env.Bg0);
            Color32 bottom = U.Hex32(env.Bg1);
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                var c = Color32.Lerp(bottom, top, t);
                for (int x = 0; x < width; x++) pixels[y * width + x] = c;
            }
            if (track.Bounds(out double minX, out double minY, out double maxX, out double maxY))
            {
                const int pad = 10;
                double sx = (width - pad * 2) / Math.Max(1, maxX - minX);
                double sy = (height - pad * 2) / Math.Max(1, maxY - minY);
                double s = Math.Min(sx, sy);
                double ox = pad + ((width - pad * 2) - (maxX - minX) * s) / 2;
                double oy = pad + ((height - pad * 2) - (maxY - minY) * s) / 2;
                foreach (LineData l in track.Lines.Values)
                {
                    Core.Material m = Materials.Get(l.Material);
                    Color32 c = U.Hex32(m.Color, m.Solid ? 1f : 0.4f);
                    DrawLine(pixels, width, height, ox + (l.X1 - minX) * s, oy + (l.Y1 - minY) * s, ox + (l.X2 - minX) * s, oy + (l.Y2 - minY) * s, c);
                }
                if (track.Finish.HasValue)
                {
                    Zone z = track.Finish.Value;
                    Color32 g = U.Hex32("#4dff9d");
                    double x0 = ox + (z.X - minX) * s, y0 = oy + (z.Y - minY) * s, x1 = x0 + z.W * s, y1 = y0 + z.H * s;
                    DrawLine(pixels, width, height, x0, y0, x1, y0, g);
                    DrawLine(pixels, width, height, x1, y0, x1, y1, g);
                    DrawLine(pixels, width, height, x1, y1, x0, y1, g);
                    DrawLine(pixels, width, height, x0, y1, x0, y0, g);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static void DrawLine(Color32[] px, int w, int h, double x0, double y0, double x1, double y1, Color32 c)
        {
            double dx = x1 - x0;
            double dy = y1 - y0;
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)));
            if (steps <= 0) steps = 1;
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                int x = (int)Math.Round(x0 + dx * t);
                int y = (int)Math.Round(y0 + dy * t);
                // Texture rows go bottom-up; the sim's y points down.
                int ty = h - 1 - y;
                if (x < 0 || x >= w || ty < 0 || ty >= h) continue;
                px[ty * w + x] = c;
            }
        }
    }
}
