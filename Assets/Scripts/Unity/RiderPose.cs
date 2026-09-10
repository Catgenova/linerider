using System;
using System.Collections.Generic;
using CyberRider.Core;

namespace CyberRider.Unity
{
    public struct Seg
    {
        public double X1, Y1, X2, Y2;

        public Seg(double x1, double y1, double x2, double y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }

    /// <summary>Joint positions used to draw a rider as a stick figure on a hoverboard.</summary>
    public sealed class RiderPose
    {
        public Vec2d Tail, Nose;
        public double Ux, Uy, Nx, Ny, Length, Speed;
        public readonly List<Seg> Segments = new List<Seg>();
        public Vec2d Head, HeadUp;
        public double ScarfDx, ScarfDy;
        public bool Standing;

        /// <summary>
        /// Derive a drawing pose from the physics rig. The classic sled rig keeps its exact collision
        /// behaviour, but while alive it is drawn standing on the deck, leaning with speed and flexing
        /// with the mount bones. Once dismounted the raw ragdoll points are drawn.
        /// </summary>
        public static RiderPose Compute(Rider rider, double time)
        {
            Point[] pts = rider.Points;
            RiderModel m = rider.Model;
            Point tail = pts[m.AnchorTail];
            Point nose = pts[m.AnchorNose];
            double ux = nose.X - tail.X;
            double uy = nose.Y - tail.Y;
            double length = Math.Sqrt(ux * ux + uy * uy);
            if (length == 0) length = 1;
            ux /= length;
            uy /= length;
            double nx = uy;
            double ny = -ux;
            Vec2d v = rider.Velocity();
            var pose = new RiderPose
            {
                Tail = new Vec2d(tail.X, tail.Y),
                Nose = new Vec2d(nose.X, nose.Y),
                Ux = ux,
                Uy = uy,
                Nx = nx,
                Ny = ny,
                Length = length,
                Speed = Math.Sqrt(v.X * v.X + v.Y * v.Y),
            };

            if (m.Id == "sled" && !rider.Dead)
            {
                double cx = (tail.X + nose.X) / 2;
                double cy = (tail.Y + nose.Y) / 2;
                Vec2d P(double a, double h) => new Vec2d(cx + ux * a + nx * h, cy + uy * a + ny * h);
                Point peg = pts[RiderModel.PEG];
                Point sh = pts[RiderModel.SHOULDER];
                double restX = peg.X + ux * 5 + nx * 5.5;
                double restY = peg.Y + uy * 5 + ny * 5.5;
                double dx = sh.X - restX;
                double dy = sh.Y - restY;
                double along = v.X * ux + v.Y * uy;
                double lean = Math.Max(-0.28, Math.Min(0.28, along * 0.035));
                double bob = Math.Sin(time * 7) * 0.12;
                Vec2d rearFoot = P(-4.4, 0);
                Vec2d frontFoot = P(4.6, 0);
                Vec2d rearKnee = P(-3.2 + lean * 2, 5.2 + bob);
                Vec2d frontKnee = P(4.0 + lean * 2, 5.4 + bob);
                Vec2d hipBase = P(0.8 + lean * 4, 9.4 + bob);
                var hip = new Vec2d(hipBase.X + dx * 0.6, hipBase.Y + dy * 0.6);
                Vec2d shoulderBase = P(1.6 + lean * 9, 15.6 + bob);
                var shoulder = new Vec2d(shoulderBase.X + dx * 1.4, shoulderBase.Y + dy * 1.4);
                Vec2d rearHandBase = P(-6.2 + lean * 6, 13.2 + bob);
                Vec2d frontHandBase = P(8.6 + lean * 9, 16.4 + bob);
                var rearHand = new Vec2d(rearHandBase.X + dx * 1.2, rearHandBase.Y + dy * 1.2);
                var frontHand = new Vec2d(frontHandBase.X + dx * 1.2, frontHandBase.Y + dy * 1.2);
                double hx = shoulder.X - hip.X;
                double hy = shoulder.Y - hip.Y;
                double hl = Math.Sqrt(hx * hx + hy * hy);
                if (hl == 0) hl = 1;
                hx /= hl;
                hy /= hl;
                pose.Segments.Add(new Seg(rearFoot.X, rearFoot.Y, rearKnee.X, rearKnee.Y));
                pose.Segments.Add(new Seg(rearKnee.X, rearKnee.Y, hip.X, hip.Y));
                pose.Segments.Add(new Seg(frontFoot.X, frontFoot.Y, frontKnee.X, frontKnee.Y));
                pose.Segments.Add(new Seg(frontKnee.X, frontKnee.Y, hip.X, hip.Y));
                pose.Segments.Add(new Seg(hip.X, hip.Y, shoulder.X, shoulder.Y));
                pose.Segments.Add(new Seg(shoulder.X, shoulder.Y, rearHand.X, rearHand.Y));
                pose.Segments.Add(new Seg(shoulder.X, shoulder.Y, frontHand.X, frontHand.Y));
                pose.Head = new Vec2d(shoulder.X + hx * 3.4, shoulder.Y + hy * 3.4);
                pose.HeadUp = new Vec2d(hx, hy);
                pose.ScarfDx = shoulder.X - sh.X;
                pose.ScarfDy = shoulder.Y - sh.Y;
                pose.Standing = true;
                return pose;
            }

            foreach (int[] s in m.DrawBody) pose.Segments.Add(new Seg(pts[s[0]].X, pts[s[0]].Y, pts[s[1]].X, pts[s[1]].Y));
            Point hipP = pts[m.AnchorHip];
            Point shP = pts[m.AnchorShoulder];
            double hx2 = shP.X - hipP.X;
            double hy2 = shP.Y - hipP.Y;
            double hl2 = Math.Sqrt(hx2 * hx2 + hy2 * hy2);
            if (hl2 == 0) hl2 = 1;
            hx2 /= hl2;
            hy2 /= hl2;
            pose.Head = new Vec2d(shP.X + hx2 * m.HeadOffset, shP.Y + hy2 * m.HeadOffset);
            pose.HeadUp = new Vec2d(hx2, hy2);
            pose.Standing = m.Id == "board";
            return pose;
        }
    }
}
