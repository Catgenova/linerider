using System;
using System.Collections.Generic;

namespace NeonLineRider.Core
{
    public enum WorldEventType
    {
        Death,
        Crumble,
        Break,
        Explode,
        Impact,
        Pickup,
        Trigger,
    }

    public sealed class WorldEvent
    {
        public WorldEventType Type;
        public int Rider;
        public Line Line;
        public double X, Y, Radius, Speed;
        public Prop Prop;
        public Entity Entity;
        public string Label;
    }

    public delegate void WindFn(double x, double y, int frame, out double fx, out double fy);

    /// <summary>A distance constraint between points of different bodies (e.g. cargo strapped to a sled).</summary>
    public sealed class Link
    {
        public Point A, B;
        public double Rest;
        public double Endurance;
        public bool Broken;
        public double Bias;
    }

    /// <summary>The simulation: static lines in a grid, riders, entities and props stepped in lockstep.</summary>
    public sealed class World
    {
        public int Frame;
        public readonly Dictionary<int, Line> Lines = new Dictionary<int, Line>();
        public readonly LineGrid Grid = new LineGrid();
        public readonly List<Rider> Riders = new List<Rider>();
        public readonly List<Entity> Entities = new List<Entity>();
        public readonly List<Prop> Props = new List<Prop>();
        public readonly List<Link> Links = new List<Link>();
        public double GravityX;
        public double GravityY = Constants.Gravity;
        public double GravityScale = 1;
        public double FrictionScale = 1;
        public WindFn Wind;
        public List<WorldEvent> Events = new List<WorldEvent>();
        public readonly HashSet<Line> Crumbling = new HashSet<Line>();
        private readonly List<Line> _near = new List<Line>();
        private CollideContext _ctx = new CollideContext { Frame = 0, FrictionScale = 1, RiderFriction = 1 };
        private readonly double[] _faceScratch = new double[4 * 3];
        private bool _settling;
        public int NextDynamicId = 1000000;

        public Line AddLine(int id, double x1, double y1, double x2, double y2, Material material = null, bool flipped = false, bool leftExt = false, bool rightExt = false, double multiplier = 1)
        {
            var line = new Line(id, x1, y1, x2, y2, material, flipped, leftExt, rightExt, multiplier);
            Lines[line.Id] = line;
            if (line.Material.Solid) Grid.Add(line);
            return line;
        }

        public Line RemoveLine(int id)
        {
            if (!Lines.TryGetValue(id, out var line)) return null;
            Lines.Remove(id);
            if (line.Material.Solid && !line.Dead) Grid.Remove(line);
            Crumbling.Remove(line);
            return line;
        }

        /// <summary>Remove a line from collision but keep it in the map (renders as gone).</summary>
        public void KillLine(Line line)
        {
            if (line.Dead) return;
            line.Dead = true;
            if (line.Material.Solid) Grid.Remove(line);
            Crumbling.Remove(line);
        }

        public int AddRider(Rider rider)
        {
            Riders.Add(rider);
            return Riders.Count - 1;
        }

        public void AddEntity(Entity entity)
        {
            Entities.Add(entity);
        }

        public void AddProp(Prop prop)
        {
            Props.Add(prop);
        }

        public Link AddLink(Point a, Point b, double endurance = double.PositiveInfinity, double bias = 0.5)
        {
            double rest = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            var link = new Link { A = a, B = b, Rest = rest, Endurance = endurance, Broken = false, Bias = bias };
            Links.Add(link);
            return link;
        }

        private void SatisfyLinks()
        {
            for (int i = 0; i < Links.Count; i++)
            {
                Link link = Links[i];
                if (link.Broken) continue;
                Point a = link.A;
                Point b = link.B;
                double dx = a.X - b.X;
                double dy = a.Y - b.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d == 0) continue;
                double scalar = (d - link.Rest) / d;
                if (scalar > link.Endurance)
                {
                    link.Broken = true;
                    continue;
                }
                a.X -= dx * scalar * link.Bias;
                a.Y -= dy * scalar * link.Bias;
                b.X += dx * scalar * (1 - link.Bias);
                b.Y += dy * scalar * (1 - link.Bias);
            }
        }

        /// <summary>Let props come to rest before the run starts. Riders and entities are not touched.</summary>
        public void SettleProps(int frames = 60)
        {
            if (Props.Count == 0) return;
            _settling = true;
            double gx = GravityX * GravityScale;
            double gy = GravityY * GravityScale;
            for (int f = 0; f < frames; f++)
            {
                _ctx.Frame = -frames + f;
                foreach (Prop prop in Props) prop.Step(gx, gy);
                for (int it = 0; it < Constants.Iterations; it++)
                {
                    foreach (Prop prop in Props) prop.Satisfy();
                    foreach (Prop prop in Props)
                    {
                        if (!prop.Active || prop.Frozen) continue;
                        if (prop.IsCircle) CollideCircle(prop);
                        else foreach (Point p in prop.Points) CollidePoint(p);
                    }
                    for (int i = 0; i < Props.Count; i++)
                    {
                        for (int j = i + 1; j < Props.Count; j++) CollideProps(Props[i], Props[j]);
                    }
                }
                foreach (Prop prop in Props) prop.UpdateSleep();
            }
            foreach (Prop prop in Props) prop.Rehome();
            Events = new List<WorldEvent>();
            Crumbling.Clear();
            foreach (Line line in Lines.Values) line.CrumbleAt = -1;
            _settling = false;
        }

        /// <summary>Advance the world by one simulation frame.</summary>
        public void Step()
        {
            Frame++;
            _ctx.Frame = Frame;
            _ctx.FrictionScale = FrictionScale;
            Events = new List<WorldEvent>();

            foreach (Entity e in Entities) if (e.Active) e.Update(this);
            foreach (Entity e in Entities) if (e.Active) e.ApplyForces(this);

            double gx = GravityX * GravityScale;
            double gy = GravityY * GravityScale;
            foreach (Rider rider in Riders)
            {
                if (Wind != null) ApplyWind(rider.Points, rider.Forces);
                rider.Step(gx, gy);
            }
            foreach (Prop prop in Props) prop.Step(gx, gy);

            for (int it = 0; it < Constants.Iterations; it++)
            {
                foreach (Rider rider in Riders) rider.SatisfyBones();
                foreach (Prop prop in Props) prop.Satisfy();
                if (Links.Count > 0) SatisfyLinks();
                foreach (Rider rider in Riders)
                {
                    _ctx.RiderFriction = rider.FrictionScale;
                    foreach (Point p in rider.Points) CollidePoint(p);
                }
                _ctx.RiderFriction = 1;
                foreach (Prop prop in Props)
                {
                    if (!prop.Active || prop.Frozen) continue;
                    if (prop.IsCircle) CollideCircle(prop);
                    else foreach (Point p in prop.Points) CollidePoint(p);
                }
                foreach (Rider rider in Riders)
                {
                    foreach (Prop prop in Props)
                    {
                        if (!prop.Active) continue;
                        CollidePointsWithProp(rider.Points, 1, prop, true);
                    }
                }
                for (int i = 0; i < Props.Count; i++)
                {
                    Prop a = Props[i];
                    if (!a.Active) continue;
                    for (int j = i + 1; j < Props.Count; j++)
                    {
                        Prop b = Props[j];
                        if (!b.Active) continue;
                        CollideProps(a, b);
                    }
                }
            }

            for (int i = 0; i < Riders.Count; i++)
            {
                Rider rider = Riders[i];
                rider.CheckFlip();
                if (rider.Dead && rider.DeathFrame < 0)
                {
                    rider.DeathFrame = Frame;
                    Events.Add(new WorldEvent { Type = WorldEventType.Death, Rider = i });
                }
                rider.StepScarf(gy);
                Point hip = rider.Points[rider.Model.AnchorHip];
                rider.Trail.Add(hip.X);
                rider.Trail.Add(hip.Y);
                if (rider.Trail.Count > 64) rider.Trail.RemoveRange(0, 2);
            }

            if (Crumbling.Count > 0)
            {
                var due = new List<Line>();
                foreach (Line line in Crumbling) if (Frame >= line.CrumbleAt) due.Add(line);
                foreach (Line line in due)
                {
                    KillLine(line);
                    Events.Add(new WorldEvent { Type = WorldEventType.Crumble, Line = line });
                }
            }

            foreach (Prop prop in Props)
            {
                if (!prop.Active) continue;
                prop.UpdateSleep();
                if (prop.Fuse > 0) prop.Fuse--;
                if (prop.Fuse == 0) Detonate(prop);
            }

            foreach (Entity e in Entities) if (e.Active) e.AfterStep(this);
        }

        private void ApplyWind(Point[] points, double[] forces)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Wind(points[i].X, points[i].Y, Frame, out double fx, out double fy);
                forces[i * 2] += fx;
                forces[i * 2 + 1] += fy;
            }
        }

        /// <summary>Collide a point against static lines then kinematic entity lines.</summary>
        private void CollidePoint(Point p)
        {
            List<Line> near = Grid.Near(p.X, p.Y, _near);
            for (int i = 0; i < near.Count; i++)
            {
                Line line = near[i];
                if (line.Collide(p, in _ctx) && line.CrumbleAt >= 0) Crumbling.Add(line);
            }
            foreach (Entity e in Entities)
            {
                if (!e.Active) continue;
                foreach (Line line in e.Lines) line.Collide(p, in _ctx);
            }
        }

        /// <summary>Rolling circle against lines: treat the contact point as the rim.</summary>
        private void CollideCircle(Prop prop)
        {
            Point p = prop.Points[0];
            double r = prop.Radius;
            List<Line> near = Grid.InRect(p.X - r - 2, p.Y - r - 2, p.X + r + 2, p.Y + r + 2, _near);
            for (int i = 0; i < near.Count; i++) CircleCheck(prop, p, r, near[i]);
            foreach (Entity e in Entities)
            {
                if (!e.Active) continue;
                foreach (Line line in e.Lines) CircleCheck(prop, p, r, line);
            }
        }

        private void CircleCheck(Prop prop, Point p, double r, Line line)
        {
            if (line.Dead || !line.Material.Solid) return;
            double rvx = p.Vx - line.Vx;
            double rvy = p.Vy - line.Vy;
            if (rvx * line.Nx + rvy * line.Ny <= 0) return;
            double sx = p.X - line.X1;
            double sy = p.Y - line.Y1;
            double doty = sx * line.Nx + sy * line.Ny;
            if (doty <= -r || doty >= Constants.LineZone) return;
            double dotx = (sx * line.Dx + sy * line.Dy) * line.InvLenSq;
            double margin = r * (line.Length > 0 ? 1 / line.Length : 0);
            if (dotx < line.LimitL - margin || dotx > line.LimitR + margin) return;
            double push = doty + r;
            p.X -= push * line.Nx;
            p.Y -= push * line.Ny;
            double f = p.Friction * line.Material.FrictionScale * FrictionScale;
            if (f != 0)
            {
                double fx = Math.Abs(line.Ny) * f * push;
                double fy = Math.Abs(line.Nx) * f * push;
                if (p.Px > p.X) fx = -fx;
                if (p.Py > p.Y) fy = -fy;
                if (Math.Abs(fx) > Math.Abs(p.X - p.Px)) fx = p.X - p.Px;
                if (Math.Abs(fy) > Math.Abs(p.Y - p.Py)) fy = p.Y - p.Py;
                p.Px += fx;
                p.Py += fy;
            }
            if (line.Material.Accel != 0)
            {
                double a = line.Material.Accel * line.Multiplier;
                p.Px -= line.Ux * a;
                p.Py -= line.Uy * a;
            }
            if (line.Material.CrumbleFrames != 0 && line.CrumbleAt < 0)
            {
                line.CrumbleAt = Frame + line.Material.CrumbleFrames;
                Crumbling.Add(line);
            }
            p.Contact = line;
        }

        /// <summary>
        /// Push points out of a prop's outline (box faces) or radius (circle). For boxes each point is
        /// resolved against its closest penetrated face only, using a whole-box inside test.
        /// </summary>
        private void CollidePointsWithProp(IList<Point> points, double pointMass, Prop prop, bool wakeProp, double margin = 1.2)
        {
            double share = prop.Mass / (prop.Mass + pointMass);
            if (prop.IsCircle)
            {
                Point c = prop.Points[0];
                double r = prop.Radius + (margin > 1.2 ? margin : 0);
                for (int i = 0; i < points.Count; i++)
                {
                    Point p = points[i];
                    double dx = p.X - c.X;
                    double dy = p.Y - c.Y;
                    double d2 = dx * dx + dy * dy;
                    if (d2 >= r * r || d2 == 0) continue;
                    double d = Math.Sqrt(d2);
                    double pen = r - d;
                    double nx = dx / d;
                    double ny = dy / d;
                    p.X += nx * pen * share;
                    p.Y += ny * pen * share;
                    if (!prop.Frozen)
                    {
                        c.X -= nx * pen * (1 - share);
                        c.Y -= ny * pen * (1 - share);
                    }
                    else if (wakeProp && (Math.Abs(p.X - p.Px) + Math.Abs(p.Y - p.Py) > 0.4 || pen > 2)) prop.Wake();
                    NoteImpact(p, prop);
                }
                return;
            }
            List<Point> pts = prop.Points;
            int nFaces = prop.Edges.Count;
            double[] fn = _faceScratch;
            for (int pi = 0; pi < points.Count; pi++)
            {
                Point p = points[pi];
                int negCount = 0;
                double minNeg = 0;
                double closestNeg = double.NegativeInfinity;
                int closestNegIdx = -1;
                double nearestD = double.PositiveInfinity;
                int nearestIdx = -1;
                int crossedIdx = -1;
                double crossedDp = 0;
                for (int i = 0; i < nFaces; i++)
                {
                    Point a = pts[prop.Edges[i][0]];
                    Point b = pts[prop.Edges[i][1]];
                    double ex = b.X - a.X;
                    double ey = b.Y - a.Y;
                    double len = Math.Sqrt(ex * ex + ey * ey);
                    if (len == 0) len = 1;
                    double nx = -ey / len;
                    double ny = ex / len;
                    double d = (p.X - a.X) * nx + (p.Y - a.Y) * ny;
                    double dp = (p.Px - a.X) * nx + (p.Py - a.Y) * ny;
                    fn[i * 3] = nx;
                    fn[i * 3 + 1] = ny;
                    fn[i * 3 + 2] = d;
                    if (d < 0)
                    {
                        negCount++;
                        if (d < minNeg) minNeg = d;
                        if (d > closestNeg)
                        {
                            closestNeg = d;
                            closestNegIdx = i;
                        }
                    }
                    if (d < nearestD)
                    {
                        nearestD = d;
                        nearestIdx = i;
                    }
                    if (dp < crossedDp)
                    {
                        crossedDp = dp;
                        crossedIdx = i;
                    }
                }
                int f = -1;
                if (negCount == 0) f = crossedIdx >= 0 ? crossedIdx : nearestIdx;
                else if (minNeg > -margin) f = closestNegIdx;
                if (f < 0) continue;
                Point fa = pts[prop.Edges[f][0]];
                Point fb = pts[prop.Edges[f][1]];
                double fex = fb.X - fa.X;
                double fey = fb.Y - fa.Y;
                double len2 = fex * fex + fey * fey;
                if (len2 == 0) len2 = 1;
                double t = ((p.X - fa.X) * fex + (p.Y - fa.Y) * fey) / len2;
                double fd = fn[f * 3 + 2];
                ResolveEdge(p, fa, fb, fn[f * 3], fn[f * 3 + 1], -(fd + margin), t, share, prop, wakeProp);
            }
        }

        private void ResolveEdge(Point p, Point a, Point b, double nx, double ny, double delta, double t, double share, Prop prop, bool wakeProp)
        {
            p.X += nx * delta * share;
            p.Y += ny * delta * share;
            if (prop.Frozen)
            {
                if (wakeProp && (Math.Abs(p.X - p.Px) + Math.Abs(p.Y - p.Py) > 0.4 || -delta > 2)) prop.Wake();
            }
            else
            {
                double tt = t < 0 ? 0 : t > 1 ? 1 : t;
                double wa = 1 - tt;
                double wb = tt;
                double norm = wa * wa + wb * wb;
                if (norm == 0) norm = 1;
                double k = (delta * (1 - share)) / norm;
                a.X -= nx * k * wa;
                a.Y -= ny * k * wa;
                b.X -= nx * k * wb;
                b.Y -= ny * k * wb;
            }
            double rvx = p.X - p.Px - (a.X - a.Px);
            double rvy = p.Y - p.Py - (a.Y - a.Py);
            double tx = ny;
            double ty = -nx;
            double vt = rvx * tx + rvy * ty;
            double fr = 0.3 * vt;
            p.Px += tx * fr * share;
            p.Py += ty * fr * share;
            NoteImpact(p, prop);
        }

        private void NoteImpact(Point p, Prop prop)
        {
            Vec2d v = prop.Velocity();
            double rvx = p.X - p.Px - v.X;
            double rvy = p.Y - p.Py - v.Y;
            double s = Math.Sqrt(rvx * rvx + rvy * rvy);
            if (s > prop.LastImpact) prop.LastImpact = s;
            if (_settling) return;
            if (s > 2.5)
            {
                Events.Add(new WorldEvent { Type = WorldEventType.Impact, X = p.X, Y = p.Y, Speed = s, Prop = prop });
                if (prop.Explosive && prop.Fuse < 0 && s > 4) prop.Fuse = 3;
            }
            if (prop.Kind == "cargo" && s > 1.5) prop.Damage += (s - 1.5) * 6;
        }

        private void CollideProps(Prop a, Prop b)
        {
            if (a.Frozen && b.Frozen) return;
            if (a.IsCircle && b.IsCircle)
            {
                Point pa = a.Points[0];
                Point pb = b.Points[0];
                double dx = pb.X - pa.X;
                double dy = pb.Y - pa.Y;
                double rr = a.Radius + b.Radius;
                double d2 = dx * dx + dy * dy;
                if (d2 >= rr * rr || d2 == 0) return;
                double d = Math.Sqrt(d2);
                double pen = rr - d;
                double nx = dx / d;
                double ny = dy / d;
                double shareA = a.Frozen ? 0 : b.Mass / (a.Mass + b.Mass);
                double shareB = b.Frozen ? 0 : a.Mass / (a.Mass + b.Mass);
                pa.X -= nx * pen * shareA;
                pa.Y -= ny * pen * shareA;
                pb.X += nx * pen * shareB;
                pb.Y += ny * pen * shareB;
                if (a.Frozen) a.Wake();
                if (b.Frozen) b.Wake();
                return;
            }
            if (a.IsCircle)
            {
                CollidePointsWithProp(a.Points, a.Mass, b, true, a.Radius);
                return;
            }
            if (b.IsCircle)
            {
                CollidePointsWithProp(b.Points, b.Mass, a, true, b.Radius);
                return;
            }
            CollidePointsWithProp(a.Points, a.Mass, b, true);
            CollidePointsWithProp(b.Points, b.Mass, a, true);
        }

        /// <summary>Radial impulse to everything nearby, detonating explosives in range.</summary>
        public void Explode(double x, double y, double radius, double strength)
        {
            Events.Add(new WorldEvent { Type = WorldEventType.Explode, X = x, Y = y, Radius = radius });
            foreach (Rider rider in Riders) foreach (Point p in rider.Points) Hit(p, 1, x, y, radius, strength);
            foreach (Prop prop in Props)
            {
                if (!prop.Active) continue;
                Vec2d c = prop.Center();
                double d = Math.Sqrt((c.X - x) * (c.X - x) + (c.Y - y) * (c.Y - y));
                if (d < radius)
                {
                    if (prop.Frozen) prop.Wake();
                    if (prop.Explosive && prop.Fuse < 0) prop.Fuse = 4 + (int)Math.Floor(d / 20);
                    prop.Disturbed = true;
                }
                foreach (Point p in prop.Points) Hit(p, prop.Mass, x, y, radius, strength);
            }
            foreach (Entity e in Entities)
            {
                if (!e.Active) continue;
                foreach (Line line in e.Lines)
                {
                    if (line.Dead) continue;
                    double mx = (line.X1 + line.X2) / 2;
                    double my = (line.Y1 + line.Y2) / 2;
                    if (e.Kind == "wall" && Math.Sqrt((mx - x) * (mx - x) + (my - y) * (my - y)) < radius)
                    {
                        KillLine(line);
                        Events.Add(new WorldEvent { Type = WorldEventType.Break, Line = line, X = mx, Y = my });
                    }
                }
            }
        }

        private static void Hit(Point p, double mass, double x, double y, double radius, double strength)
        {
            double dx = p.X - x;
            double dy = p.Y - y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= radius) return;
            double k = ((1 - d / radius) * strength) / mass;
            double nx = d == 0 ? 0 : dx / d;
            double ny = d == 0 ? -1 : dy / d;
            p.Px -= nx * k;
            p.Py -= ny * k;
        }

        public void Detonate(Prop prop)
        {
            if (!prop.Active) return;
            prop.Active = false;
            prop.Fuse = -1;
            Vec2d c = prop.Center();
            Explode(c.X, c.Y, 90, 14);
        }

        /// <summary>Solid lines within a radius of a point (near-miss helper).</summary>
        public List<Line> LinesNear(double x, double y, double radius, List<Line> output)
        {
            return Grid.InRect(x - radius, y - radius, x + radius, y + radius, output);
        }
    }
}
