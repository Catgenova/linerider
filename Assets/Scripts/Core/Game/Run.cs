using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    public sealed class RunEvent
    {
        /// <summary>"trick", "flag", "rescue", "finish", "death" or "message".</summary>
        public string Type;
        public double X, Y;
        public string Text;
        public string Color;
        public int Points;
    }

    public sealed class RunOptions
    {
        public Track Track;
        public LevelDef Level;
        public RiderDef RiderDef;
        public Environment Environment;
        public int SkipFrames;
        public bool Endless;
    }

    /// <summary>One playback of a track: owns the world, the rider, pickups, cargo and the objective tallies.</summary>
    public sealed class Run
    {
        public readonly World World = new World();
        public readonly Rider Rider;
        public readonly TrickTracker Tricks;
        public readonly LevelDef Level;
        public readonly Track Track;
        public readonly RiderDef RiderDef;
        public readonly bool Endless;
        public bool Finished;
        public int FinishFrame = -1;
        public bool Died;
        public string FailReason;
        public readonly bool[] Flags;
        public readonly bool[] Rescues;
        public Prop Cargo;
        public readonly List<Link> CargoLinks = new List<Link>();
        public double CargoIntegrity = 100;
        public bool CargoLost;
        public int Detonations;
        public double MaxX;
        public readonly List<RunEvent> Events = new List<RunEvent>();
        private readonly List<TrickEvent> _trickEvents = new List<TrickEvent>();
        private Vec2d _lastCargoV;
        private int _stalledFrames;
        private double _visMinX = double.PositiveInfinity, _visMinY = double.PositiveInfinity, _visMaxX = double.NegativeInfinity, _visMaxY = double.NegativeInfinity;
        private int _lastProgressFrame;
        private readonly Action<TrackChange> _changeHandler;

        public Run(RunOptions opts)
        {
            Track = opts.Track;
            Level = opts.Level;
            RiderDef = opts.RiderDef;
            Endless = opts.Endless;
            Environment env = opts.Environment;
            World world = World;
            world.GravityScale = env.GravityScale;
            world.FrictionScale = env.FrictionScale;
            world.Wind = Environments.MakeWind(env);
            foreach (LineData l in Track.SortedLines())
            {
                Line line = world.AddLine(l.Id, l.X1, l.Y1, l.X2, l.Y2, Materials.Get(l.Material), l.Flipped, l.LeftExt, l.RightExt, l.Multiplier);
                line.Player = l.Player;
            }
            LevelDef level = opts.Level;
            var entityDefs = new List<EntityDef>();
            if (level != null) entityDefs.AddRange(level.Entities);
            foreach (ObjectData o in Track.Objects.Values) entityDefs.Add(o.Def);
            var propDefs = new List<PropDef>();
            if (level != null) propDefs.AddRange(level.Props);
            foreach (PropData p in Track.Props.Values) propDefs.Add(p.Def);
            foreach (EntityDef def in entityDefs)
            {
                Entity e = EntityFactory.Build(def, world);
                if (e != null) world.AddEntity(e);
            }
            foreach (PropDef pd in propDefs) world.AddProp(new Prop(world.NextDynamicId++, pd));
            if (propDefs.Count > 0) world.SettleProps(200);

            Vec2d start = Track.Start;
            RiderDef rd = opts.RiderDef;
            Vec2d vel = Track.HasStartVelocity ? Track.StartVelocity : new Vec2d(rd.StartVelocity, 0);
            Rider = new Rider(rd.Model, start.X, start.Y, vel.X, vel.Y, rd.GravityScale, rd.FrictionScale, rd.EnduranceScale);
            world.AddRider(Rider);
            Tricks = new TrickTracker(Rider);
            MaxX = start.X;
            Flags = new bool[level?.Flags.Count ?? 0];
            Rescues = new bool[level?.Rescues.Count ?? 0];
            if (level != null && level.Mode == "delivery") AttachCargo();
            _changeHandler = ApplyChange;
            Track.OnChange = _changeHandler;
            for (int i = 0; i < opts.SkipFrames; i++) Step();
            Events.Clear();
        }

        public void Dispose()
        {
            if (Track.OnChange == _changeHandler) Track.OnChange = null;
        }

        /// <summary>Keep the world in sync with live edits (draw-while-riding).</summary>
        private void ApplyChange(TrackChange c)
        {
            World world = World;
            if (c.Type == "objects" || c.Line == null) return;
            if (c.Type == "remove") world.RemoveLine(c.Line.Id);
            else if (c.Type == "add") AddWorldLine(c.Line);
            else
            {
                world.RemoveLine(c.Line.Id);
                AddWorldLine(c.Line);
            }
            foreach (int id in c.Touched)
            {
                if (!Track.Lines.TryGetValue(id, out var data)) continue;
                world.RemoveLine(id);
                AddWorldLine(data);
            }
        }

        private void AddWorldLine(LineData l)
        {
            Line line = World.AddLine(l.Id, l.X1, l.Y1, l.X2, l.Y2, Materials.Get(l.Material), l.Flipped, l.LeftExt, l.RightExt, l.Multiplier);
            line.Player = l.Player;
        }

        private void AttachCargo()
        {
            Rider rider = Rider;
            Point peg = rider.Points[rider.Model.AnchorTail];
            var box = new Prop(World.NextDynamicId++, new PropDef
            {
                Kind = "cargo",
                X = peg.X - 2,
                Y = peg.Y - 9,
                Width = 6,
                Height = 6,
                Mass = 0.8,
                Friction = 0.5,
            });
            foreach (Point p in box.Points) p.SetVelocity(rider.Points[0].X - rider.Points[0].Px, 0);
            World.AddProp(box);
            Cargo = box;
            int[] anchors = { rider.Model.AnchorTail, rider.Model.AnchorHip, rider.Model.AnchorNose };
            foreach (int bi in new[] { 2, 3 })
            {
                foreach (int ai in anchors) CargoLinks.Add(World.AddLink(box.Points[bi], rider.Points[ai], 0.22, 0.85));
            }
            CargoLinks.Add(World.AddLink(box.Points[0], rider.Points[rider.Model.AnchorShoulder], 0.3, 0.85));
        }

        public int Frame => World.Frame;

        public double Seconds => (double)World.Frame / Constants.SimFps;

        public bool Done => Finished || FailReason != null;

        /// <summary>Advance one frame, collecting events for the HUD/effects.</summary>
        public void Step()
        {
            if (Done) return;
            World world = World;
            Rider rider = Rider;
            world.Step();
            _trickEvents.Clear();
            Tricks.Update(world, _trickEvents);
            foreach (TrickEvent t in _trickEvents)
            {
                string text = t.Points > 0 ? $"{t.Name} +{t.Points}{(t.Combo > 1 ? $" x{t.Combo}" : "")}" : t.Name;
                Events.Add(new RunEvent { Type = "trick", X = t.X, Y = t.Y, Text = text, Color = t.Points > 0 ? (t.Combo > 2 ? "#ff2bd6" : "#ffe93a") : "#ff4d4d", Points = t.Points });
            }
            Vec2d c = rider.Center();
            if (c.X > MaxX) MaxX = c.X;

            foreach (WorldEvent ev in world.Events)
            {
                if (ev.Type == WorldEventType.Explode) Detonations++;
                if (ev.Type == WorldEventType.Death && !Died)
                {
                    Died = true;
                    Events.Add(new RunEvent { Type = "death", X = c.X, Y = c.Y - 12, Text = "WIPEOUT", Color = "#ff4d4d" });
                }
                if (ev.Type == WorldEventType.Trigger)
                {
                    Events.Add(new RunEvent { Type = "message", X = ev.X, Y = ev.Y - 20, Text = ev.Label, Color = "#ffe93a" });
                }
            }
            if (rider.Dead && !Died)
            {
                Died = true;
                Events.Add(new RunEvent { Type = "death", X = c.X, Y = c.Y - 12, Text = "WIPEOUT", Color = "#ff4d4d" });
            }

            LevelDef level = Level;
            if (level != null)
            {
                CollectPickups(c, level);
                if (Cargo != null) UpdateCargo();
                if (level.Finish.HasValue && !Finished && !rider.Dead)
                {
                    if (level.Finish.Value.Contains(c.X, c.Y))
                    {
                        Finished = true;
                        FinishFrame = world.Frame;
                        Events.Add(new RunEvent { Type = "finish", X = c.X, Y = c.Y - 16, Text = "FINISH", Color = "#4dff9d" });
                    }
                }
                if (level.TimeLimit > 0 && world.Frame > level.TimeLimit * Constants.SimFps && !Finished)
                {
                    FailReason = "Out of time";
                }
            }
            else if (Track.Finish.HasValue && !Finished && !rider.Dead)
            {
                if (Track.Finish.Value.Contains(c.X, c.Y))
                {
                    Finished = true;
                    FinishFrame = world.Frame;
                    Events.Add(new RunEvent { Type = "finish", X = c.X, Y = c.Y - 16, Text = "FINISH", Color = "#4dff9d" });
                }
            }
            if (Died && FailReason == null && DeadFor() > 90 && !Finished)
            {
                FailReason = "Crashed";
            }
            if (!Finished && FailReason == null && !Died && !Endless && world.Frame > 40)
            {
                Vec2d v = rider.Velocity();
                if (Math.Abs(v.X) + Math.Abs(v.Y) < 0.25) _stalledFrames++;
                else _stalledFrames = 0;
                bool grew = false;
                if (c.X < _visMinX - 4) { _visMinX = c.X; grew = true; }
                if (c.X > _visMaxX + 4) { _visMaxX = c.X; grew = true; }
                if (c.Y < _visMinY - 4) { _visMinY = c.Y; grew = true; }
                if (c.Y > _visMaxY + 4) { _visMaxY = c.Y; grew = true; }
                if (grew) _lastProgressFrame = world.Frame;
                if (_stalledFrames > 120 || world.Frame - _lastProgressFrame > 320) FailReason = "Stalled";
            }
            if (FailReason == null && !Finished)
            {
                double maxY = 0;
                if (Track.Bounds(out _, out _, out _, out double by)) maxY = by;
                if (c.Y > maxY + 1500) FailReason = "Lost in the void";
            }
        }

        private int DeadFor() => Rider.DeathFrame >= 0 ? World.Frame - Rider.DeathFrame : 0;

        private void CollectPickups(Vec2d c, LevelDef level)
        {
            for (int i = 0; i < level.Flags.Count; i++)
            {
                if (Flags[i]) continue;
                Vec2d f = level.Flags[i];
                if (Math.Sqrt((c.X - f.X) * (c.X - f.X) + (c.Y - (f.Y - 8)) * (c.Y - (f.Y - 8))) < 16)
                {
                    Flags[i] = true;
                    Events.Add(new RunEvent { Type = "flag", X = f.X, Y = f.Y - 24, Text = "FLAG", Color = "#ffe93a" });
                }
            }
            for (int i = 0; i < level.Rescues.Count; i++)
            {
                if (Rescues[i]) continue;
                Vec2d r = level.Rescues[i];
                if (!Rider.Dead && Math.Sqrt((c.X - r.X) * (c.X - r.X) + (c.Y - (r.Y - 5)) * (c.Y - (r.Y - 5))) < 16)
                {
                    Rescues[i] = true;
                    Rider.Passengers++;
                    Events.Add(new RunEvent { Type = "rescue", X = r.X, Y = r.Y - 24, Text = "RESCUED", Color = "#ff7ae8" });
                }
            }
        }

        private void UpdateCargo()
        {
            Prop cargo = Cargo;
            if (!cargo.Active) return;
            Vec2d v = cargo.Velocity();
            double dvx = v.X - _lastCargoV.X;
            double dvy = v.Y - _lastCargoV.Y;
            _lastCargoV = v;
            double jerk = Math.Sqrt(dvx * dvx + dvy * dvy);
            if (World.Frame > 5 && jerk > 1.6)
            {
                double dmg = (jerk - 1.6) * 9;
                CargoIntegrity = Math.Max(0, CargoIntegrity - dmg);
                Vec2d c = cargo.Center();
                Events.Add(new RunEvent { Type = "message", X = c.X, Y = c.Y - 12, Text = $"-{Math.Round(dmg):0}%", Color = "#ff7ae8" });
            }
            if (!CargoLost)
            {
                bool broken = false;
                foreach (Link l in CargoLinks) if (l.Broken) broken = true;
                if (broken)
                {
                    CargoLost = true;
                    foreach (Link l in CargoLinks) l.Broken = true;
                    Vec2d c = cargo.Center();
                    Events.Add(new RunEvent { Type = "message", X = c.X, Y = c.Y - 12, Text = "CARGO LOST", Color = "#ff4d4d" });
                }
            }
            if (CargoIntegrity <= 0 && FailReason == null) FailReason = "Cargo destroyed";
        }

        public int ChaosScore()
        {
            double score = Detonations * 400;
            foreach (Prop prop in World.Props)
            {
                if (prop.Kind == "cargo") continue;
                if (prop.Disturbed) score += 100;
                Vec2d c = prop.Center();
                double d = Math.Sqrt((c.X - prop.OriginX) * (c.X - prop.OriginX) + (c.Y - prop.OriginY) * (c.Y - prop.OriginY));
                score += Math.Min(300, d / 2);
                if (!prop.Active) score += 200;
            }
            return (int)Math.Round(score);
        }

        public RunSummary Summary()
        {
            TrickTracker t = Tricks;
            int flagsCollected = 0;
            foreach (bool f in Flags) if (f) flagsCollected++;
            int rescued = 0;
            foreach (bool r in Rescues) if (r) rescued++;
            return new RunSummary
            {
                Finished = Finished,
                Died = Died,
                Frames = Finished ? FinishFrame : World.Frame,
                InkUsed = Track.InkUsed(),
                AirtimeFrames = t.AirtimeFrames,
                Flips = t.Flips,
                TrickScore = t.Score,
                FlagsCollected = flagsCollected,
                FlagsTotal = Flags.Length,
                Rescued = rescued,
                RescueTotal = Rescues.Length,
                CargoIntegrity = (int)Math.Round(CargoIntegrity),
                CargoLost = CargoLost,
                Chaos = ChaosScore(),
                NearMisses = t.NearMisses,
                HugeDrops = t.HugeDrops,
                Distance = (MaxX - Track.Start.X) / Constants.PxPerMeter,
                SurvivedFrames = Died ? Rider.DeathFrame : World.Frame,
                FailReason = FailReason,
            };
        }
    }

    public static class LevelLoader
    {
        /// <summary>Build a fresh editable track from a level definition (prebuilt geometry on the level layer).</summary>
        public static Track TrackFromLevel(LevelDef level)
        {
            var track = new Track { Start = level.Start, Finish = level.Finish };
            foreach (LevelLine l in level.Lines)
            {
                track.AddLine(l.X1, l.Y1, l.X2, l.Y2, Materials.ParseId(l.Material ?? "normal"), l.Flipped, l.Multiplier, LineLayer.Level);
            }
            foreach (EntityDef def in level.Entities)
            {
                foreach (LevelLine l in EntityFactory.StaticLines(def))
                {
                    track.AddLine(l.X1, l.Y1, l.X2, l.Y2, Materials.ParseId(l.Material ?? "normal"), l.Flipped, l.Multiplier, LineLayer.Level);
                }
            }
            return track;
        }
    }
}
