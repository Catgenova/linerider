using System;

namespace CyberRider.Core
{
    public struct CollideContext
    {
        public int Frame;
        public double FrictionScale;
        public double RiderFriction;
    }

    /// <summary>
    /// A one-sided collision segment. The normal points into the solid side; points that end a frame
    /// within LineZone px past the surface while moving into it are projected back onto the surface.
    /// </summary>
    public sealed class Line
    {
        public readonly int Id;
        public double X1, Y1, X2, Y2;
        public Material Material;
        public bool Flipped;
        public bool LeftExt;
        public bool RightExt;
        public double Multiplier = 1;

        public double Dx, Dy, Length, InvLenSq;
        public double Ux, Uy;
        public double Nx, Ny;
        public double LimitL, LimitR = 1;

        /// <summary>Frame at which a crumbling line disappears, or -1.</summary>
        public int CrumbleAt = -1;
        public bool Dead;
        /// <summary>Velocity of the surface (kinematic platforms), px/frame.</summary>
        public double Vx, Vy;
        /// <summary>Owning entity id for dynamic lines, else -1.</summary>
        public int Owner = -1;
        /// <summary>Which player drew it (co-op), 0 = level/default.</summary>
        public int Player;
        /// <summary>Scratch stamp used by the grid for de-duplication.</summary>
        internal int GridStamp;

        public Line(int id, double x1, double y1, double x2, double y2, Material material = null, bool flipped = false, bool leftExt = false, bool rightExt = false, double multiplier = 1)
        {
            Id = id;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Material = material ?? Materials.Get(MaterialId.Normal);
            Flipped = flipped;
            LeftExt = leftExt;
            RightExt = rightExt;
            Multiplier = multiplier;
            Recompute();
        }

        public void Recompute()
        {
            Dx = X2 - X1;
            Dy = Y2 - Y1;
            double lenSq = Dx * Dx + Dy * Dy;
            Length = Math.Sqrt(lenSq);
            InvLenSq = lenSq > 0 ? 1 / lenSq : 0;
            double inv = Length > 0 ? 1 / Length : 0;
            double s = Flipped ? -1 : 1;
            Ux = Dx * inv * s;
            Uy = Dy * inv * s;
            // Drawn left-to-right => normal points down (+y), so the top face is rideable.
            Nx = -Dy * inv * s;
            Ny = Dx * inv * s;
            double ext = Length > 0 ? Math.Min(Constants.MaxExtensionRatio, Constants.ExtensionPx / Length) : 0;
            LimitL = LeftExt ? -ext : 0;
            LimitR = RightExt ? 1 + ext : 1;
        }

        public bool Solid => Material.Solid && !Dead;

        /// <summary>Extended endpoints (used for grid registration).</summary>
        public void ExtendedBounds(out double ex1, out double ey1, out double ex2, out double ey2)
        {
            double el = LeftExt ? Math.Min(Constants.ExtensionPx, Length * Constants.MaxExtensionRatio) : 0;
            double er = RightExt ? Math.Min(Constants.ExtensionPx, Length * Constants.MaxExtensionRatio) : 0;
            double inv = Length > 0 ? 1 / Length : 0;
            double dxu = Dx * inv;
            double dyu = Dy * inv;
            ex1 = X1 - dxu * el;
            ey1 = Y1 - dyu * el;
            ex2 = X2 + dxu * er;
            ey2 = Y2 + dyu * er;
        }

        /// <summary>Resolve a point against this line. Returns true on contact.</summary>
        public bool Collide(Point p, in CollideContext ctx)
        {
            if (Dead || !Material.Solid) return false;
            double rvx = p.Vx - Vx;
            double rvy = p.Vy - Vy;
            if (rvx * Nx + rvy * Ny <= 0) return false;
            double sx = p.X - X1;
            double sy = p.Y - Y1;
            double doty = sx * Nx + sy * Ny;
            if (doty <= 0 || doty >= Constants.LineZone) return false;
            double dotx = (sx * Dx + sy * Dy) * InvLenSq;
            if (dotx < LimitL || dotx > LimitR) return false;
            Material m = Material;
            if (m.OneWay && rvx * Ux + rvy * Uy <= 0) return false;

            p.X -= doty * Nx;
            p.Y -= doty * Ny;

            double f = p.Friction * m.FrictionScale * ctx.FrictionScale * ctx.RiderFriction;
            if (f != 0)
            {
                double fx = Math.Abs(Ny) * f * doty;
                double fy = Math.Abs(Nx) * f * doty;
                if (p.Px > p.X) fx = -fx;
                if (p.Py > p.Y) fy = -fy;
                if (Math.Abs(fx) > Math.Abs(p.X - p.Px)) fx = p.X - p.Px;
                if (Math.Abs(fy) > Math.Abs(p.Y - p.Py)) fy = p.Y - p.Py;
                p.Px += fx;
                p.Py += fy;
            }

            if ((Vx != 0 || Vy != 0) && p.CarryFrame != ctx.Frame)
            {
                p.CarryFrame = ctx.Frame;
                p.Px -= Vx;
                p.Py -= Vy;
            }

            if (m.Accel != 0)
            {
                double a = m.Accel * Multiplier;
                p.Px -= Ux * a;
                p.Py -= Uy * a;
            }
            if (m.ConveyorSpeed != 0)
            {
                double cvx = p.X - p.Px;
                double cvy = p.Y - p.Py;
                double vt = cvx * Ux + cvy * Uy;
                double delta = m.ConveyorSpeed * Multiplier - vt;
                if (delta > 0.15) delta = 0.15;
                else if (delta < -0.15) delta = -0.15;
                p.Px -= Ux * delta;
                p.Py -= Uy * delta;
            }
            if (m.Restitution != 0 && p.BounceFrame != ctx.Frame)
            {
                p.BounceFrame = ctx.Frame;
                double vn = p.Vx * Nx + p.Vy * Ny;
                double k = (1 + m.Restitution) * vn;
                double nvx = p.Vx - k * Nx;
                double nvy = p.Vy - k * Ny;
                p.SetVelocity(nvx, nvy);
            }
            if ((m.Drag != 0 || m.Stickiness != 0) && p.DragFrame != ctx.Frame)
            {
                p.DragFrame = ctx.Frame;
                double cvx = p.X - p.Px;
                double cvy = p.Y - p.Py;
                if (m.Drag != 0)
                {
                    cvx *= m.Drag;
                    cvy *= m.Drag;
                }
                if (m.Stickiness != 0)
                {
                    double vn = cvx * Nx + cvy * Ny;
                    cvx = (cvx - vn * Nx) * (1 - m.Stickiness);
                    cvy = (cvy - vn * Ny) * (1 - m.Stickiness);
                }
                p.Px = p.X - cvx;
                p.Py = p.Y - cvy;
            }
            if (m.CrumbleFrames != 0 && CrumbleAt < 0)
            {
                CrumbleAt = ctx.Frame + m.CrumbleFrames;
            }
            p.Contact = this;
            return true;
        }
    }
}
