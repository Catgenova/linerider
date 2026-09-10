using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    public sealed class Bone
    {
        public readonly int A;
        public readonly int B;
        public readonly double Rest;
        public readonly BoneKind Kind;
        public readonly double Endurance;

        public Bone(int a, int b, double rest, BoneKind kind, double enduranceScale)
        {
            A = a;
            B = b;
            Rest = rest;
            Kind = kind;
            Endurance = Constants.Endurance * rest * 0.5 * enduranceScale;
        }
    }

    public struct ScarfNode
    {
        public double X, Y, Px, Py;
    }

    /// <summary>A rider: point masses, bones, and a cosmetic scarf. Dies when a mount bone snaps.</summary>
    public sealed class Rider
    {
        public readonly RiderModel Model;
        public readonly Point[] Points;
        public readonly Bone[] Bones;
        public readonly double[] Forces;
        public readonly ScarfNode[] Scarf;
        public double GravityScale;
        public double FrictionScale;
        public bool Dead;
        public int DeathFrame = -1;
        public int Age;
        /// <summary>Recent hip positions (x, y pairs) for rendering a trail.</summary>
        public readonly List<double> Trail = new List<double>();
        /// <summary>Cosmetic rescued passengers riding along.</summary>
        public int Passengers;

        public Rider(RiderModel model, double x, double y, double vx, double vy, double gravityScale = 1, double frictionScale = 1, double enduranceScale = 1, int scarfLength = 7)
        {
            Model = model;
            GravityScale = gravityScale;
            FrictionScale = frictionScale;
            Points = new Point[model.Points.Length];
            for (int i = 0; i < model.Points.Length; i++)
            {
                PointDef d = model.Points[i];
                var p = new Point(x + d.X, y + d.Y, d.Friction) { Index = i };
                p.SetVelocity(vx, vy);
                Points[i] = p;
            }
            Bones = new Bone[model.Bones.Length];
            for (int i = 0; i < model.Bones.Length; i++)
            {
                BoneDef b = model.Bones[i];
                PointDef pa = model.Points[b.A];
                PointDef pb = model.Points[b.B];
                double natural = Math.Sqrt((pa.X - pb.X) * (pa.X - pb.X) + (pa.Y - pb.Y) * (pa.Y - pb.Y));
                double rest = b.Kind == BoneKind.Repel ? natural * b.Factor : natural;
                Bones[i] = new Bone(b.A, b.B, rest, b.Kind, enduranceScale);
            }
            Forces = new double[Points.Length * 2];
            Point sh = Points[model.AnchorShoulder];
            Scarf = new ScarfNode[scarfLength];
            for (int i = 0; i < scarfLength; i++)
            {
                Scarf[i] = new ScarfNode { X = sh.X - i * 2, Y = sh.Y, Px = sh.X - i * 2, Py = sh.Y };
            }
        }

        /// <summary>Integrate all points with gravity plus any accumulated external forces.</summary>
        public void Step(double gx, double gy)
        {
            double g = GravityScale;
            for (int i = 0; i < Points.Length; i++)
            {
                Points[i].Step(gx * g + Forces[i * 2], gy * g + Forces[i * 2 + 1]);
            }
            Array.Clear(Forces, 0, Forces.Length);
            Age++;
        }

        /// <summary>One relaxation pass over every bone. Mount bones snap (and kill the rider) past their endurance.</summary>
        public void SatisfyBones()
        {
            Point[] pts = Points;
            for (int i = 0; i < Bones.Length; i++)
            {
                Bone bone = Bones[i];
                Point pa = pts[bone.A];
                Point pb = pts[bone.B];
                double dx = pa.X - pb.X;
                double dy = pa.Y - pb.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d == 0) continue;
                if (bone.Kind == BoneKind.Repel && d >= bone.Rest) continue;
                double scalar = ((d - bone.Rest) / d) * 0.5;
                if (bone.Kind == BoneKind.Mount)
                {
                    if (Dead) continue;
                    if (scalar > bone.Endurance)
                    {
                        Dead = true;
                        continue;
                    }
                }
                pa.X -= dx * scalar;
                pa.Y -= dy * scalar;
                pb.X += dx * scalar;
                pb.Y += dy * scalar;
            }
        }

        /// <summary>Dismount if the body has been pushed through the plane of the vehicle.</summary>
        public void CheckFlip()
        {
            if (Dead) return;
            Point tail = Points[Model.AnchorTail];
            Point nose = Points[Model.AnchorNose];
            Point hip = Points[Model.AnchorHip];
            Point sh = Points[Model.AnchorShoulder];
            double cross = (nose.X - tail.X) * (sh.Y - hip.Y) - (nose.Y - tail.Y) * (sh.X - hip.X);
            if (cross > 0) Dead = true;
        }

        public void StepScarf(double gy)
        {
            Point sh = Points[Model.AnchorShoulder];
            ScarfNode[] s = Scarf;
            s[0].Px = s[0].X;
            s[0].Py = s[0].Y;
            s[0].X = sh.X;
            s[0].Y = sh.Y;
            for (int i = 1; i < s.Length; i++)
            {
                double vx = (s[i].X - s[i].Px) * 0.86;
                double vy = (s[i].Y - s[i].Py) * 0.86 + gy * 0.15;
                s[i].Px = s[i].X;
                s[i].Py = s[i].Y;
                s[i].X += vx;
                s[i].Y += vy;
            }
            for (int i = 1; i < s.Length; i++)
            {
                double dx = s[i].X - s[i - 1].X;
                double dy = s[i].Y - s[i - 1].Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d == 0) d = 1;
                const double rest = 2.2;
                s[i].X = s[i - 1].X + (dx / d) * rest;
                s[i].Y = s[i - 1].Y + (dy / d) * rest;
            }
        }

        /// <summary>Orientation of the vehicle in radians (screen coordinates).</summary>
        public double Angle
        {
            get
            {
                Point t = Points[Model.AnchorTail];
                Point n = Points[Model.AnchorNose];
                return Math.Atan2(n.Y - t.Y, n.X - t.X);
            }
        }

        /// <summary>Centre of the vehicle.</summary>
        public Vec2d Center()
        {
            double x = 0, y = 0;
            foreach (int i in Model.Vehicle)
            {
                x += Points[i].X;
                y += Points[i].Y;
            }
            int n = Model.Vehicle.Length;
            return new Vec2d(x / n, y / n);
        }

        /// <summary>Average velocity of the vehicle points (px/frame).</summary>
        public Vec2d Velocity()
        {
            double x = 0, y = 0;
            foreach (int i in Model.Vehicle)
            {
                Point p = Points[i];
                x += p.X - p.Px;
                y += p.Y - p.Py;
            }
            int n = Model.Vehicle.Length;
            return new Vec2d(x / n, y / n);
        }

        public bool VehicleGrounded()
        {
            foreach (int i in Model.Vehicle) if (Points[i].Contact != null) return true;
            return false;
        }

        public bool BodyTouching()
        {
            foreach (Point p in Points)
            {
                if (p.Contact == null) continue;
                bool vehicle = false;
                foreach (int v in Model.Vehicle) if (v == p.Index) vehicle = true;
                if (!vehicle) return true;
            }
            return false;
        }
    }
}
