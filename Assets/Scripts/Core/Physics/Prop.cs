using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    /// <summary>Definition of a physics prop (also the JSON shape stored in tracks and levels).</summary>
    public sealed class PropDef
    {
        public string Kind = "crate";
        public double X, Y;
        public double Width = 10, Height = 10;
        public double Angle;
        public double Radius;
        public double Friction = -1;
        public double Mass = 1;
        public double GravityScale = 1;
        public bool Explosive;
        public bool Dormant;

        public PropDef Clone() => (PropDef)MemberwiseClone();
    }

    /// <summary>
    /// A physics toy: either a rigid Verlet box (dominoes, crates, TNT, carts, cargo) or a rolling
    /// circle (boulders, snowballs). Boxes collide through their edges; circles through their radius.
    /// </summary>
    public sealed class Prop
    {
        public readonly int Id;
        public readonly string Kind;
        public readonly List<Point> Points = new List<Point>();
        public readonly List<Bone> Bones = new List<Bone>();
        public readonly List<int[]> Edges = new List<int[]>();
        public readonly double Radius;
        public readonly double Mass;
        public readonly bool Explosive;
        public double OriginX;
        public double OriginY;
        public readonly double Width;
        public readonly double Height;
        public double GravityScale;
        public bool Dormant;
        public bool Sleeping;
        private int _stillFrames;
        private double _anchorX;
        private double _anchorY;
        public bool Active = true;
        public double Spin;
        public int Fuse = -1;
        public double LastImpact;
        public double Damage;
        public bool Disturbed;

        public Prop(int id, PropDef init)
        {
            Id = id;
            Kind = init.Kind;
            Radius = init.Radius;
            Mass = init.Mass;
            Explosive = init.Explosive;
            GravityScale = init.GravityScale;
            Dormant = init.Dormant;
            OriginX = init.X;
            OriginY = init.Y;
            double friction = init.Friction >= 0 ? init.Friction : (Radius > 0 ? 0.04 : 0.4);
            if (Radius > 0)
            {
                Width = Height = Radius * 2;
                Points.Add(new Point(init.X, init.Y, friction));
                return;
            }
            double w = init.Width;
            double h = init.Height;
            Width = w;
            Height = h;
            double a = init.Angle;
            double cos = Math.Cos(a);
            double sin = Math.Sin(a);
            double[][] corners =
            {
                new[] { -w / 2, -h / 2 },
                new[] { w / 2, -h / 2 },
                new[] { w / 2, h / 2 },
                new[] { -w / 2, h / 2 },
            };
            for (int i = 0; i < corners.Length; i++)
            {
                double cx = corners[i][0];
                double cy = corners[i][1];
                var p = new Point(init.X + cx * cos - cy * sin, init.Y + cx * sin + cy * cos, friction) { Index = i };
                Points.Add(p);
            }
            Link(0, 1);
            Link(1, 2);
            Link(2, 3);
            Link(3, 0);
            Link(0, 2);
            Link(1, 3);
            Edges.Add(new[] { 0, 1 });
            Edges.Add(new[] { 1, 2 });
            Edges.Add(new[] { 2, 3 });
            Edges.Add(new[] { 3, 0 });
        }

        private void Link(int i, int j)
        {
            Point pa = Points[i];
            Point pb = Points[j];
            Bones.Add(new Bone(i, j, Math.Sqrt((pa.X - pb.X) * (pa.X - pb.X) + (pa.Y - pb.Y) * (pa.Y - pb.Y)), BoneKind.Normal, 1));
        }

        public bool IsCircle => Radius > 0;

        public Vec2d Center()
        {
            double x = 0, y = 0;
            foreach (Point p in Points)
            {
                x += p.X;
                y += p.Y;
            }
            return new Vec2d(x / Points.Count, y / Points.Count);
        }

        public Vec2d Velocity()
        {
            double x = 0, y = 0;
            foreach (Point p in Points)
            {
                x += p.X - p.Px;
                y += p.Y - p.Py;
            }
            return new Vec2d(x / Points.Count, y / Points.Count);
        }

        public double Angle()
        {
            if (IsCircle) return Spin;
            Point a = Points[0];
            Point b = Points[1];
            return Math.Atan2(b.Y - a.Y, b.X - a.X);
        }

        public void Wake()
        {
            Dormant = false;
            Sleeping = false;
            _stillFrames = 0;
        }

        /// <summary>Not participating in motion this frame.</summary>
        public bool Frozen => Dormant || Sleeping;

        /// <summary>Treat the current (settled) position as the origin and clear any disturbance.</summary>
        public void Rehome()
        {
            Vec2d c = Center();
            OriginX = c.X;
            OriginY = c.Y;
            Disturbed = false;
            LastImpact = 0;
            Fuse = -1;
            foreach (Point p in Points)
            {
                p.Px = p.X;
                p.Py = p.Y;
                p.Vx = 0;
                p.Vy = 0;
            }
        }

        public void Step(double gx, double gy)
        {
            if (!Active) return;
            if (Dormant || Sleeping)
            {
                foreach (Point p in Points)
                {
                    p.Vx = 0;
                    p.Vy = 0;
                    p.Px = p.X;
                    p.Py = p.Y;
                    p.Contact = null;
                }
                return;
            }
            double g = GravityScale;
            foreach (Point p in Points)
            {
                double vx = (p.X - p.Px) * 0.999;
                double vy = (p.Y - p.Py) * 0.999;
                p.Px = p.X - vx;
                p.Py = p.Y - vy;
                p.Step(gx * g, gy * g);
            }
            if (IsCircle)
            {
                Point p = Points[0];
                Spin += (p.X - p.Px) / Radius;
            }
            Vec2d c = Center();
            if (!Disturbed && Math.Abs(c.X - OriginX) + Math.Abs(c.Y - OriginY) > 3)
            {
                Disturbed = true;
            }
        }

        /// <summary>Call after the collision passes: fall asleep once the centre has stayed put for forty frames.</summary>
        public void UpdateSleep()
        {
            if (!Active || Frozen) return;
            Vec2d c = Center();
            if (Math.Abs(c.X - _anchorX) + Math.Abs(c.Y - _anchorY) > 2)
            {
                _anchorX = c.X;
                _anchorY = c.Y;
                _stillFrames = 0;
                return;
            }
            _stillFrames++;
            if (_stillFrames > 40)
            {
                Sleeping = true;
                foreach (Point p in Points)
                {
                    p.Px = p.X;
                    p.Py = p.Y;
                    p.Vx = 0;
                    p.Vy = 0;
                }
            }
        }

        public void Satisfy()
        {
            if (!Active || Frozen) return;
            for (int i = 0; i < Bones.Count; i++)
            {
                Bone bone = Bones[i];
                Point pa = Points[bone.A];
                Point pb = Points[bone.B];
                double dx = pa.X - pb.X;
                double dy = pa.Y - pb.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d == 0) continue;
                double scalar = ((d - bone.Rest) / d) * 0.5;
                pa.X -= dx * scalar;
                pa.Y -= dy * scalar;
                pb.X += dx * scalar;
                pb.Y += dy * scalar;
            }
        }

        public double Speed()
        {
            Vec2d v = Velocity();
            return Math.Sqrt(v.X * v.X + v.Y * v.Y);
        }
    }
}
