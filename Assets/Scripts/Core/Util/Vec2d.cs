using System;

namespace CyberRider.Core
{
    /// <summary>Double precision 2D vector used by the simulation (screen convention: y points down).</summary>
    public struct Vec2d
    {
        public double X;
        public double Y;

        public Vec2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2d Zero = new Vec2d(0, 0);

        public double Length => Math.Sqrt(X * X + Y * Y);

        public static Vec2d operator +(Vec2d a, Vec2d b) => new Vec2d(a.X + b.X, a.Y + b.Y);
        public static Vec2d operator -(Vec2d a, Vec2d b) => new Vec2d(a.X - b.X, a.Y - b.Y);
        public static Vec2d operator *(Vec2d a, double s) => new Vec2d(a.X * s, a.Y * s);

        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }

    public static class MathUtil
    {
        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        public static double Dist(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance from a point to a segment.</summary>
        public static double DistToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double lenSq = dx * dx + dy * dy;
            double t = 0;
            if (lenSq > 0)
            {
                t = ((px - x1) * dx + (py - y1) * dy) / lenSq;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
            }
            double cx = x1 + dx * t;
            double cy = y1 + dy * t;
            return Dist(px, py, cx, cy);
        }

        public static double AngleDiff(double a, double b)
        {
            double d = b - a;
            while (d > Math.PI) d -= Math.PI * 2;
            while (d < -Math.PI) d += Math.PI * 2;
            return d;
        }
    }
}
