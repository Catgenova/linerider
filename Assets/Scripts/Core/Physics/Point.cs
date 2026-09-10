using System;

namespace NeonLineRider.Core
{
    /// <summary>A Verlet point mass. Velocity is implicit in (pos - prev); Vx/Vy caches the frame momentum.</summary>
    public sealed class Point
    {
        public double X;
        public double Y;
        public double Px;
        public double Py;
        public double Vx;
        public double Vy;
        public double Friction;
        /// <summary>Line touched most recently this frame, or null.</summary>
        public Line Contact;
        public int BounceFrame = -1;
        public int DragFrame = -1;
        public int CarryFrame = -1;
        public int Index;

        public Point(double x, double y, double friction)
        {
            X = x;
            Y = y;
            Px = x;
            Py = y;
            Friction = friction;
        }

        /// <summary>Apply a force and advance: momentum = (pos - prev) + force; prev = pos; pos += momentum.</summary>
        public void Step(double fx, double fy)
        {
            Vx = X - Px + fx;
            Vy = Y - Py + fy;
            Px = X;
            Py = Y;
            X += Vx;
            Y += Vy;
            Contact = null;
        }

        public void SetVelocity(double vx, double vy)
        {
            Px = X - vx;
            Py = Y - vy;
            Vx = vx;
            Vy = vy;
        }

        public double Speed
        {
            get
            {
                double dx = X - Px;
                double dy = Y - Py;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }
    }
}
