using System;
using System.Collections.Generic;

namespace NeonLineRider.Core
{
    public sealed class TrickEvent
    {
        public string Name;
        public int Points;
        public double X, Y;
        public int Combo;
    }

    /// <summary>Detects flips, airtime, drops, near misses, grinds, manuals and clean landings for one rider.</summary>
    public sealed class TrickTracker
    {
        public int Score;
        public int Combo;
        public int ComboTimer;
        public int AirtimeFrames;
        public int Flips;
        public int Backflips;
        public int Frontflips;
        public int NearMisses;
        public int CleanLandings;
        public int HugeDrops;
        public int GrindFrames;
        public int Manuals;
        public double BestAir;
        private bool _airborne;
        private int _airFrames;
        private double _rotation;
        private double _lastAngle;
        private double _takeoffY;
        private int _grindRun;
        private int _manualRun;
        private readonly Dictionary<int, int> _nearMissSeen = new Dictionary<int, int>();
        private readonly List<Line> _scratch = new List<Line>();
        private bool _bailed;
        private readonly Rider _rider;

        public TrickTracker(Rider rider)
        {
            _rider = rider;
            _lastAngle = rider.Angle;
        }

        /// <summary>Call once per simulation frame after the world has stepped.</summary>
        public void Update(World world, List<TrickEvent> output)
        {
            Rider rider = _rider;
            if (rider.Dead)
            {
                if (!_bailed)
                {
                    _bailed = true;
                    Combo = 0;
                    ComboTimer = 0;
                    if (_airborne)
                    {
                        Vec2d bc = rider.Center();
                        output.Add(new TrickEvent { Name = "BAIL", Points = 0, X = bc.X, Y = bc.Y - 14, Combo = 0 });
                    }
                    _airborne = false;
                }
                return;
            }
            if (ComboTimer > 0)
            {
                ComboTimer--;
                if (ComboTimer == 0) Combo = 0;
            }
            double angle = rider.Angle;
            double delta = MathUtil.AngleDiff(_lastAngle, angle);
            _lastAngle = angle;
            bool grounded = false;
            bool grindContact = false;
            foreach (Point p in rider.Points)
            {
                if (p.Contact != null)
                {
                    grounded = true;
                    if (p.Contact.Material.Grind) grindContact = true;
                }
            }
            Vec2d c = rider.Center();
            Vec2d v = rider.Velocity();

            if (!grounded)
            {
                _airFrames++;
                if (_airFrames == 5)
                {
                    _airborne = true;
                    _rotation = 0;
                    _takeoffY = c.Y;
                }
                if (_airborne)
                {
                    _rotation += delta;
                    AirtimeFrames++;
                    CheckNearMiss(world, output);
                }
                _grindRun = 0;
                _manualRun = 0;
            }
            else
            {
                if (_airborne) Land(c, v, output);
                _airborne = false;
                _airFrames = 0;
                if (grindContact)
                {
                    _grindRun++;
                    GrindFrames++;
                    if (_grindRun % 40 == 0) Award("GRIND", 60, c.X, c.Y - 14, output);
                }
                else _grindRun = 0;
                CheckManual(c, output);
            }
        }

        private void Land(Vec2d c, Vec2d v, List<TrickEvent> output)
        {
            int air = _airFrames;
            Rider rider = _rider;
            int spins = (int)Math.Floor((Math.Abs(_rotation) + 0.6) / (Math.PI * 2));
            if (spins >= 1)
            {
                bool back = (v.X >= 0 && _rotation < 0) || (v.X < 0 && _rotation > 0);
                string baseName = back ? "BACKFLIP" : "FRONTFLIP";
                string name = spins > 1 ? $"{spins}x {baseName}" : baseName;
                Flips += spins;
                if (back) Backflips += spins;
                else Frontflips += spins;
                Award(name, 500 * spins + (spins > 1 ? 250 * (spins - 1) : 0), c.X, c.Y - 18, output);
            }
            double seconds = (double)air / Constants.SimFps;
            if (seconds > BestAir) BestAir = seconds;
            if (seconds >= 1) Award($"AIRTIME {seconds:0.0}s", (int)Math.Round(seconds * 60), c.X, c.Y - 10, output);
            double drop = c.Y - _takeoffY;
            if (drop >= 150)
            {
                HugeDrops++;
                Award("HUGE DROP", (int)Math.Round(drop / 2.5), c.X, c.Y - 26, output);
            }
            Line contact = null;
            foreach (int i in rider.Model.Vehicle)
            {
                Point p = rider.Points[i];
                if (p.Contact != null) contact = p.Contact;
            }
            if (contact != null && !rider.Dead && air >= 12)
            {
                double lineAngle = Math.Atan2(contact.Dy, contact.Dx);
                double diff = Math.Abs(MathUtil.AngleDiff(lineAngle, rider.Angle));
                if (diff > Math.PI / 2) diff = Math.PI - diff;
                if (diff < 0.2)
                {
                    CleanLandings++;
                    Award("CLEAN LANDING", 150, c.X, c.Y - 34, output);
                }
            }
        }

        private void CheckNearMiss(World world, List<TrickEvent> output)
        {
            Rider rider = _rider;
            Point sh = rider.Points[rider.Model.AnchorShoulder];
            Point hip = rider.Points[rider.Model.AnchorHip];
            double hx = sh.X + (sh.X - hip.X) * 0.6;
            double hy = sh.Y + (sh.Y - hip.Y) * 0.6;
            List<Line> lines = world.LinesNear(hx, hy, 9, _scratch);
            for (int li = 0; li < lines.Count; li++)
            {
                Line line = lines[li];
                if (line.Dead || !line.Material.Solid) continue;
                if (_nearMissSeen.TryGetValue(line.Id, out int seen) && world.Frame - seen < 120) continue;
                double dx = line.X2 - line.X1;
                double dy = line.Y2 - line.Y1;
                double len2 = dx * dx + dy * dy;
                if (len2 == 0) continue;
                double t = ((hx - line.X1) * dx + (hy - line.Y1) * dy) / len2;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double cx = line.X1 + dx * t;
                double cy = line.Y1 + dy * t;
                double d = Math.Sqrt((hx - cx) * (hx - cx) + (hy - cy) * (hy - cy));
                if (d < 7 && d > 0.5)
                {
                    _nearMissSeen[line.Id] = world.Frame;
                    NearMisses++;
                    Award("NEAR MISS", 100, hx, hy - 12, output);
                }
            }
        }

        private void CheckManual(Vec2d c, List<TrickEvent> output)
        {
            Rider rider = _rider;
            if (rider.Model.Id != "sled") return;
            Line tail = rider.Points[rider.Model.AnchorTail].Contact;
            Line nose = rider.Points[rider.Model.AnchorNose].Contact;
            bool body = rider.BodyTouching();
            if (tail != null && nose == null && !body && Math.Abs(rider.Velocity().X) > 2)
            {
                _manualRun++;
                if (_manualRun == 30)
                {
                    Manuals++;
                    Award("MANUAL", 120, c.X, c.Y - 16, output);
                }
            }
            else _manualRun = 0;
        }

        private void Award(string name, int baseScore, double x, double y, List<TrickEvent> output)
        {
            Combo++;
            ComboTimer = 100;
            double mult = Math.Min(3, 1 + (Combo - 1) * 0.25);
            int points = (int)Math.Round(baseScore * mult);
            Score += points;
            output.Add(new TrickEvent { Name = name, Points = points, X = x, Y = y, Combo = Combo });
        }
    }
}
