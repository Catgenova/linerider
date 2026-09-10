using System;
using System.Collections.Generic;

namespace NeonLineRider.Core
{
    /// <summary>
    /// Flat definition of any entity (mirrors the JSON shape used by levels and published tracks).
    /// Only the fields relevant to <see cref="Type"/> are read.
    /// </summary>
    public sealed class EntityDef
    {
        public string Type = "platform";
        public double X, Y, W, H;
        public double Dx, Dy;
        public double Period = 100;
        public double Phase;
        public string Material;
        public double Thickness = 3;
        public double Radius;
        public int Teeth;
        public double Speed;
        public double Length;
        public double Width;
        public double Amplitude;
        public double Fx, Fy;
        public double Strength;
        public double Angle;
        public double Power;
        public double X1, Y1, X2, Y2;
        public double Threshold;
        public double Lift;
        public int Duration;
        public double MaxAngle = 0.45;
        public double Range;
        public int Count;
        public double Spread;
        public double Trigger;
        public int Segments;
        public int Delay;
        public int At;
        public double StartX;
        public double Accel;
        public double Push;
        public string Label;

        public EntityDef Clone() => (EntityDef)MemberwiseClone();

        /// <summary>Anchor point used for hover/erase on a placed entity.</summary>
        public Vec2d Anchor()
        {
            switch (Type)
            {
                case "wall":
                case "collapse":
                    return new Vec2d((X1 + X2) / 2, (Y1 + Y2) / 2);
                case "fan":
                    return new Vec2d(X + W / 2, Y + H / 2);
                case "avalanche":
                    return new Vec2d(StartX, 0);
                case "pendulum":
                    return new Vec2d(X, Y + Length);
                default:
                    return new Vec2d(X, Y);
            }
        }
    }

    /// <summary>Base for entities whose lines are placed from local coordinates each frame.</summary>
    public abstract class Kinematic : Entity
    {
        protected readonly List<double[]> Local = new List<double[]>();

        protected static Line NewLine(World world, double x1, double y1, double x2, double y2, string material, int owner)
        {
            var line = new Line(world.NextDynamicId++, x1, y1, x2, y2, Materials.Get(material ?? "normal"));
            line.Owner = owner;
            return line;
        }

        protected void BuildLines(World world, string material)
        {
            foreach (var _ in Local) Lines.Add(NewLine(world, 0, 0, 0, 0, material, Id));
        }

        /// <summary>Place all lines given a translation and rotation, computing velocities from the previous placement.</summary>
        protected void Place(double tx, double ty, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            for (int i = 0; i < Lines.Count; i++)
            {
                double[] l = Local[i];
                Line line = Lines[i];
                double nx1 = tx + l[0] * cos - l[1] * sin;
                double ny1 = ty + l[0] * sin + l[1] * cos;
                double nx2 = tx + l[2] * cos - l[3] * sin;
                double ny2 = ty + l[2] * sin + l[3] * cos;
                double omx = (line.X1 + line.X2) / 2;
                double omy = (line.Y1 + line.Y2) / 2;
                line.X1 = nx1;
                line.Y1 = ny1;
                line.X2 = nx2;
                line.Y2 = ny2;
                line.Recompute();
                line.Vx = (nx1 + nx2) / 2 - omx;
                line.Vy = (ny1 + ny2) / 2 - omy;
            }
        }
    }

    /// <summary>A bar that oscillates along (dx,dy). Rideable on top and solid underneath.</summary>
    public sealed class MovingPlatform : Kinematic
    {
        public override string Kind => "platform";

        public MovingPlatform(World world, EntityDef def)
        {
            Def = def;
            double t = def.Thickness;
            double w = def.W;
            Local.Add(new[] { -w / 2, 0, w / 2, 0 });
            Local.Add(new[] { w / 2, t, -w / 2, t });
            Local.Add(new[] { w / 2, 0, w / 2, t });
            Local.Add(new[] { -w / 2, t, -w / 2, 0 });
            BuildLines(world, def.Material);
            Place(def.X, def.Y, 0);
            Update(world);
        }

        public override void Update(World world)
        {
            EntityDef d = Def;
            double phase = (world.Frame / d.Period) * Math.PI * 2 + d.Phase;
            double s = Math.Sin(phase);
            Place(d.X + d.Dx * s, d.Y + d.Dy * s, 0);
        }
    }

    /// <summary>A rotating toothed wheel.</summary>
    public sealed class Gear : Kinematic
    {
        public override string Kind => "gear";
        public double Rotation;

        public Gear(World world, EntityDef def)
        {
            Def = def;
            int n = def.Teeth * 4;
            var pts = new List<double[]>();
            for (int i = 0; i < n; i++)
            {
                double a = ((double)i / n) * Math.PI * 2;
                double r = i % 4 < 2 ? def.Radius : def.Radius * 0.82;
                pts.Add(new[] { Math.Cos(a) * r, Math.Sin(a) * r });
            }
            for (int i = 0; i < n; i++)
            {
                double[] p1 = pts[i];
                double[] p2 = pts[(i + 1) % n];
                Local.Add(new[] { p1[0], p1[1], p2[0], p2[1] });
            }
            BuildLines(world, "normal");
            Place(def.X, def.Y, 0);
        }

        public override void Update(World world)
        {
            Rotation = world.Frame * Def.Speed;
            Place(Def.X, Def.Y, Rotation);
        }
    }

    /// <summary>A swinging bar hanging from a pivot.</summary>
    public sealed class Pendulum : Kinematic
    {
        public override string Kind => "pendulum";
        public double Angle;

        public Pendulum(World world, EntityDef def)
        {
            Def = def;
            double w = def.Width;
            double L = def.Length;
            Local.Add(new[] { -w / 2, L, w / 2, L });
            Local.Add(new[] { w / 2, L + 3, -w / 2, L + 3 });
            Local.Add(new[] { w / 2, L, w / 2, L + 3 });
            Local.Add(new[] { -w / 2, L + 3, -w / 2, L });
            BuildLines(world, "normal");
            Place(def.X, def.Y, 0);
        }

        public override void Update(World world)
        {
            EntityDef d = Def;
            Angle = d.Amplitude * Math.Sin((world.Frame / d.Period) * Math.PI * 2 + d.Phase);
            Place(d.X, d.Y, Angle);
        }
    }

    /// <summary>A rectangular zone that pushes rider points.</summary>
    public sealed class Fan : Entity
    {
        public override string Kind => "fan";

        public Fan(EntityDef def)
        {
            Def = def;
        }

        public override void Update(World world)
        {
        }

        public override void ApplyForces(World world)
        {
            EntityDef d = Def;
            foreach (Rider rider in world.Riders)
            {
                for (int i = 0; i < rider.Points.Length; i++)
                {
                    Point p = rider.Points[i];
                    if (p.X < d.X || p.X > d.X + d.W || p.Y < d.Y || p.Y > d.Y + d.H) continue;
                    rider.Forces[i * 2] += d.Fx;
                    rider.Forces[i * 2 + 1] += d.Fy;
                }
            }
            foreach (Prop prop in world.Props)
            {
                if (!prop.Active || prop.Dormant) continue;
                foreach (Point p in prop.Points)
                {
                    if (p.X < d.X || p.X > d.X + d.W || p.Y < d.Y || p.Y > d.Y + d.H) continue;
                    if (prop.Sleeping) prop.Wake();
                    p.Px -= d.Fx / prop.Mass;
                    p.Py -= d.Fy / prop.Mass;
                }
            }
        }
    }

    /// <summary>Attracts rider points within a radius.</summary>
    public sealed class Magnet : Entity
    {
        public override string Kind => "magnet";

        public Magnet(EntityDef def)
        {
            Def = def;
        }

        public override void Update(World world)
        {
        }

        public override void ApplyForces(World world)
        {
            EntityDef d = Def;
            foreach (Rider rider in world.Riders)
            {
                for (int i = 0; i < rider.Points.Length; i++)
                {
                    Point p = rider.Points[i];
                    double dx = d.X - p.X;
                    double dy = d.Y - p.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist > d.Radius || dist < 1) continue;
                    double k = (d.Strength * (1 - dist / d.Radius)) / dist;
                    rider.Forces[i * 2] += dx * k;
                    rider.Forces[i * 2 + 1] += dy * k;
                }
            }
        }
    }

    /// <summary>Grabs the rider and launches them along the barrel.</summary>
    public sealed class Cannon : Entity
    {
        public override string Kind => "cannon";
        private int _cooldown;
        public int Fired;

        public Cannon(EntityDef def)
        {
            Def = def;
        }

        public override void Update(World world)
        {
            if (_cooldown > 0) _cooldown--;
        }

        public override void AfterStep(World world)
        {
            if (_cooldown > 0) return;
            EntityDef d = Def;
            double a = (d.Angle * Math.PI) / 180;
            foreach (Rider rider in world.Riders)
            {
                if (rider.Dead) continue;
                Vec2d c = rider.Center();
                if (Math.Sqrt((c.X - d.X) * (c.X - d.X) + (c.Y - d.Y) * (c.Y - d.Y)) > 14) continue;
                double mx = d.X + Math.Cos(a) * 26;
                double my = d.Y + Math.Sin(a) * 26;
                double offX = mx - c.X;
                double offY = my - c.Y;
                double vx = Math.Cos(a) * d.Power;
                double vy = Math.Sin(a) * d.Power;
                foreach (Point p in rider.Points)
                {
                    p.X += offX;
                    p.Y += offY;
                    p.SetVelocity(vx, vy);
                }
                for (int i = 0; i < rider.Scarf.Length; i++)
                {
                    rider.Scarf[i].X += offX;
                    rider.Scarf[i].Y += offY;
                    rider.Scarf[i].Px = rider.Scarf[i].X;
                    rider.Scarf[i].Py = rider.Scarf[i].Y;
                }
                _cooldown = 60;
                Fired++;
                world.Events.Add(new WorldEvent { Type = WorldEventType.Trigger, Entity = this, X = mx, Y = my, Label = "LAUNCH" });
            }
        }
    }

    /// <summary>Lines that shatter when hit hard enough.</summary>
    public sealed class BreakableWall : Entity
    {
        public override string Kind => "wall";

        public BreakableWall(World world, EntityDef def)
        {
            Def = def;
            var a = new Line(world.NextDynamicId++, def.X1, def.Y1, def.X2, def.Y2) { Owner = Id };
            var b = new Line(world.NextDynamicId++, def.X2, def.Y2, def.X1, def.Y1) { Owner = Id };
            Lines.Add(a);
            Lines.Add(b);
        }

        public override void Update(World world)
        {
        }

        public override void AfterStep(World world)
        {
            if (!Active) return;
            bool broke = false;
            foreach (Rider rider in world.Riders)
            {
                foreach (Point p in rider.Points)
                {
                    if (p.Contact != null && p.Contact.Owner == Id && Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy) >= Def.Threshold) broke = true;
                }
            }
            foreach (Prop prop in world.Props)
            {
                if (!prop.Active) continue;
                foreach (Point p in prop.Points)
                {
                    if (p.Contact != null && p.Contact.Owner == Id && Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy) * prop.Mass >= Def.Threshold) broke = true;
                }
            }
            bool anyAlive = false;
            foreach (Line l in Lines) if (!l.Dead) anyAlive = true;
            if (!anyAlive) Active = false;
            if (broke)
            {
                foreach (Line l in Lines)
                {
                    if (l.Dead) continue;
                    world.KillLine(l);
                }
                world.Events.Add(new WorldEvent { Type = WorldEventType.Break, Line = Lines[0], X = (Def.X1 + Def.X2) / 2, Y = (Def.Y1 + Def.Y2) / 2 });
                Active = false;
            }
        }
    }

    /// <summary>Lifts the rider for a while once grabbed.</summary>
    public sealed class Balloon : Entity
    {
        public override string Kind => "balloon";
        public int Holder = -1;
        public int Remaining;
        public Rider RiderRef;

        public Balloon(EntityDef def)
        {
            Def = def;
        }

        public override void Update(World world)
        {
        }

        public override void ApplyForces(World world)
        {
            if (Holder < 0) return;
            Rider rider = Holder < world.Riders.Count ? world.Riders[Holder] : null;
            if (rider == null || rider.Dead)
            {
                Active = false;
                return;
            }
            double per = Def.Lift / rider.Points.Length;
            for (int i = 0; i < rider.Points.Length; i++) rider.Forces[i * 2 + 1] -= per;
        }

        public override void AfterStep(World world)
        {
            if (Holder >= 0)
            {
                Remaining--;
                if (Remaining <= 0)
                {
                    Rider rider = world.Riders[Holder];
                    Point sh = rider.Points[rider.Model.AnchorShoulder];
                    world.Events.Add(new WorldEvent { Type = WorldEventType.Trigger, Entity = this, X = sh.X, Y = sh.Y - 16, Label = "POP" });
                    Active = false;
                }
                return;
            }
            EntityDef d = Def;
            for (int i = 0; i < world.Riders.Count; i++)
            {
                Rider rider = world.Riders[i];
                if (rider.Dead) continue;
                Vec2d c = rider.Center();
                if (Math.Sqrt((c.X - d.X) * (c.X - d.X) + (c.Y - d.Y) * (c.Y - d.Y)) < 16)
                {
                    Holder = i;
                    RiderRef = rider;
                    Remaining = d.Duration;
                    world.Events.Add(new WorldEvent { Type = WorldEventType.Pickup, Entity = this, X = d.X, Y = d.Y });
                }
            }
        }
    }

    /// <summary>A beam that tilts under the rider's weight.</summary>
    public sealed class Seesaw : Kinematic
    {
        public override string Kind => "seesaw";
        public double Angle;
        private double _angVel;

        public Seesaw(World world, EntityDef def)
        {
            Def = def;
            double L = def.Length;
            Local.Add(new[] { -L / 2, 0, L / 2, 0 });
            Local.Add(new[] { L / 2, 3, -L / 2, 3 });
            BuildLines(world, "normal");
            Place(def.X, def.Y, 0);
        }

        public override void Update(World world)
        {
            double max = Def.MaxAngle;
            _angVel *= 0.97;
            Angle += _angVel;
            if (Angle > max)
            {
                Angle = max;
                _angVel *= -0.2;
            }
            else if (Angle < -max)
            {
                Angle = -max;
                _angVel *= -0.2;
            }
            Place(Def.X, Def.Y, Angle);
        }

        public override void AfterStep(World world)
        {
            double torque = 0;
            double cos = Math.Cos(Angle);
            double sin = Math.Sin(Angle);
            foreach (Rider rider in world.Riders)
            {
                foreach (Point p in rider.Points)
                {
                    if (p.Contact == null || p.Contact.Owner != Id) continue;
                    torque += (p.X - Def.X) * cos + (p.Y - Def.Y) * sin;
                }
            }
            foreach (Prop prop in world.Props)
            {
                if (!prop.Active) continue;
                foreach (Point p in prop.Points)
                {
                    if (p.Contact == null || p.Contact.Owner != Id) continue;
                    torque += ((p.X - Def.X) * cos + (p.Y - Def.Y) * sin) * prop.Mass;
                }
            }
            _angVel += torque * 0.00035;
        }
    }

    /// <summary>A box that shuttles back and forth along the x axis.</summary>
    public sealed class Train : Kinematic
    {
        public override string Kind => "train";

        public Train(World world, EntityDef def)
        {
            Def = def;
            double w = def.W;
            double h = def.H;
            Local.Add(new[] { -w / 2, -h, w / 2, -h });
            Local.Add(new[] { w / 2, -h, w / 2, 0 });
            Local.Add(new[] { w / 2, 0, -w / 2, 0 });
            Local.Add(new[] { -w / 2, 0, -w / 2, -h });
            BuildLines(world, "normal");
            Place(def.X, def.Y, 0);
        }

        public override void Update(World world)
        {
            EntityDef d = Def;
            double t = (world.Frame * Math.Abs(d.Speed)) % (d.Range * 2);
            double offset = t < d.Range ? t : d.Range * 2 - t;
            Place(d.X + (d.Speed >= 0 ? offset : -offset), d.Y, 0);
        }
    }

    /// <summary>Dormant rocks that drop when the rider crosses a trigger line.</summary>
    public sealed class Rockfall : Entity
    {
        public override string Kind => "rockfall";
        private readonly List<Prop> _rocks = new List<Prop>();
        public bool Triggered;

        public Rockfall(World world, EntityDef def)
        {
            Def = def;
            double r = def.Radius > 0 ? def.Radius : 9;
            for (int i = 0; i < def.Count; i++)
            {
                var prop = new Prop(world.NextDynamicId++, new PropDef
                {
                    Kind = "rock",
                    X = def.X + (i - (def.Count - 1) / 2.0) * def.Spread,
                    Y = def.Y - (i % 2) * r * 2.2,
                    Radius = r * (0.8 + ((i * 7) % 5) * 0.1),
                    Mass = 3,
                    Dormant = true,
                });
                world.AddProp(prop);
                _rocks.Add(prop);
            }
        }

        public override void Update(World world)
        {
            if (Triggered) return;
            foreach (Rider rider in world.Riders)
            {
                if (rider.Center().X >= Def.Trigger)
                {
                    Triggered = true;
                    foreach (Prop rock in _rocks) rock.Wake();
                    world.Events.Add(new WorldEvent { Type = WorldEventType.Trigger, Entity = this, X = Def.X, Y = Def.Y, Label = "ROCKFALL!" });
                }
            }
        }
    }

    /// <summary>A bridge whose segments drop one after another once touched.</summary>
    public sealed class Bridge : Entity
    {
        public override string Kind => "bridge";
        public int TouchedAt = -1;

        public Bridge(World world, EntityDef def)
        {
            Def = def;
            double seg = def.W / def.Segments;
            for (int i = 0; i < def.Segments; i++)
            {
                var line = new Line(world.NextDynamicId++, def.X + i * seg, def.Y, def.X + (i + 1) * seg, def.Y, null, false, true, true) { Owner = Id };
                Lines.Add(line);
            }
        }

        public override void Update(World world)
        {
            if (TouchedAt < 0) return;
            int elapsed = world.Frame - TouchedAt;
            bool allDead = true;
            for (int i = 0; i < Lines.Count; i++)
            {
                Line line = Lines[i];
                if (!line.Dead && elapsed >= Def.Delay * (i + 1))
                {
                    world.KillLine(line);
                    world.Events.Add(new WorldEvent { Type = WorldEventType.Crumble, Line = line });
                }
                if (!line.Dead) allDead = false;
            }
            if (allDead) Active = false;
        }

        public override void AfterStep(World world)
        {
            if (TouchedAt >= 0) return;
            foreach (Rider rider in world.Riders)
            {
                foreach (Point p in rider.Points)
                {
                    if (p.Contact != null && p.Contact.Owner == Id)
                    {
                        TouchedAt = world.Frame;
                        return;
                    }
                }
            }
        }
    }

    /// <summary>A line that disappears at a fixed frame (collapsing structures on a timer).</summary>
    public sealed class TimedCollapse : Entity
    {
        public override string Kind => "collapse";

        public TimedCollapse(World world, EntityDef def)
        {
            Def = def;
            Lines.Add(new Line(world.NextDynamicId++, def.X1, def.Y1, def.X2, def.Y2) { Owner = Id });
        }

        public override void Update(World world)
        {
            Line line = Lines[0];
            if (!line.Dead && world.Frame >= Def.At)
            {
                world.KillLine(line);
                world.Events.Add(new WorldEvent { Type = WorldEventType.Crumble, Line = line });
                Active = false;
            }
        }
    }

    /// <summary>A wall of destruction that sweeps left to right. Riders it catches are killed.</summary>
    public sealed class Avalanche : Entity
    {
        public override string Kind => "avalanche";
        public double X;
        public bool Caught;

        public Avalanche(EntityDef def)
        {
            Def = def;
            X = def.StartX;
        }

        public override void Update(World world)
        {
            EntityDef d = Def;
            double t = world.Frame - d.Delay;
            if (t <= 0)
            {
                X = d.StartX;
                return;
            }
            X = d.StartX + d.Speed * t + 0.5 * d.Accel * t * t;
        }

        public override void AfterStep(World world)
        {
            foreach (Rider rider in world.Riders)
            {
                if (rider.Dead) continue;
                if (rider.Center().X < X)
                {
                    rider.Dead = true;
                    Caught = true;
                    world.Events.Add(new WorldEvent { Type = WorldEventType.Trigger, Entity = this, X = X, Y = rider.Center().Y, Label = "CAUGHT" });
                }
            }
        }
    }

    /// <summary>A giant rolling ball released when the rider crosses a trigger, pushed along relentlessly.</summary>
    public sealed class Snowball : Entity
    {
        public override string Kind => "snowball";
        public readonly Prop Prop;
        public bool Triggered;

        public Snowball(World world, EntityDef def)
        {
            Def = def;
            Prop = new Prop(world.NextDynamicId++, new PropDef
            {
                Kind = "snowball",
                X = def.X,
                Y = def.Y,
                Radius = def.Radius,
                Mass = 40,
                Dormant = true,
                Friction = 0.02,
            });
            world.AddProp(Prop);
        }

        public override void Update(World world)
        {
            if (!Triggered)
            {
                foreach (Rider rider in world.Riders)
                {
                    if (rider.Center().X >= Def.Trigger)
                    {
                        Triggered = true;
                        Prop.Wake();
                        world.Events.Add(new WorldEvent { Type = WorldEventType.Trigger, Entity = this, X = Def.X, Y = Def.Y, Label = "RUN!" });
                    }
                }
                return;
            }
            Point p = Prop.Points[0];
            if (Prop.Sleeping) Prop.Wake();
            if (p.Contact != null) p.Px -= Def.Push;
        }

        public override void AfterStep(World world)
        {
            if (!Triggered) return;
            Point c = Prop.Points[0];
            foreach (Rider rider in world.Riders)
            {
                if (rider.Dead) continue;
                Vec2d rc = rider.Center();
                if (Math.Sqrt((rc.X - c.X) * (rc.X - c.X) + (rc.Y - c.Y) * (rc.Y - c.Y)) < Def.Radius + 4) rider.Dead = true;
            }
        }
    }

    public static class EntityFactory
    {
        /// <summary>Build an entity from its definition and register any props it needs. Null for line-only kinds.</summary>
        public static Entity Build(EntityDef def, World world)
        {
            switch (def.Type)
            {
                case "platform": return new MovingPlatform(world, def);
                case "gear": return new Gear(world, def);
                case "pendulum": return new Pendulum(world, def);
                case "fan": return new Fan(def);
                case "magnet": return new Magnet(def);
                case "cannon": return new Cannon(def);
                case "wall": return new BreakableWall(world, def);
                case "balloon": return new Balloon(def);
                case "seesaw": return new Seesaw(world, def);
                case "train": return new Train(world, def);
                case "rockfall": return new Rockfall(world, def);
                case "bridge": return new Bridge(world, def);
                case "collapse": return new TimedCollapse(world, def);
                case "avalanche": return new Avalanche(def);
                case "snowball": return new Snowball(world, def);
                default: return null;
            }
        }

        /// <summary>Static lines some "object" definitions expand into (springs, ramps).</summary>
        public static List<LevelLine> StaticLines(EntityDef def)
        {
            var list = new List<LevelLine>();
            if (def.Type == "spring")
            {
                double a = (def.Angle * Math.PI) / 180;
                double hx = (Math.Cos(a) * def.W) / 2;
                double hy = (Math.Sin(a) * def.W) / 2;
                list.Add(new LevelLine(def.X - hx, def.Y - hy, def.X + hx, def.Y + hy, "spring"));
            }
            else if (def.Type == "ramp")
            {
                list.Add(new LevelLine(def.X, def.Y, def.X + def.W, def.Y - def.H, "normal"));
                list.Add(new LevelLine(def.X + def.W, def.Y - def.H, def.X + def.W, def.Y, "normal"));
            }
            return list;
        }
    }
}
