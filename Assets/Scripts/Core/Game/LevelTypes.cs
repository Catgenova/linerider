using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    /// <summary>A prebuilt line in a level definition.</summary>
    public sealed class LevelLine
    {
        public double X1, Y1, X2, Y2;
        public string Material;
        public bool Flipped;
        public double Multiplier = 1;

        public LevelLine(double x1, double y1, double x2, double y2, string material = null, bool flipped = false)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Material = material;
            Flipped = flipped;
        }
    }

    public struct Zone
    {
        public double X, Y, W, H;

        public Zone(double x, double y, double w, double h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public bool Contains(double px, double py) => px >= X && px <= X + W && py >= Y && py <= Y + H;
    }

    public sealed class Objective
    {
        public string Kind;
        public double Value;
        public bool HasValue;
        public bool Optional;
        public string Label;

        public Objective(string kind, double? value = null, bool optional = false, string label = null)
        {
            Kind = kind;
            HasValue = value.HasValue;
            Value = value ?? 0;
            Optional = optional;
            Label = label;
        }
    }

    public sealed class RegionDef
    {
        public string Id;
        public string Name;
        public string Environment;
        public string Blurb;
        public double MapX;
        public double MapY;
    }

    public sealed class LevelDef
    {
        public string Id;
        public string Name;
        public string Region;
        public string Environment;
        public string Mode;
        public string Tagline;
        public string Briefing;
        public double Budget;
        public List<MaterialId> Materials = new List<MaterialId>();
        /// <summary>Fixed rider id, or null to let the player choose.</summary>
        public string Rider;
        public Vec2d Start;
        public Zone? Finish;
        public List<Vec2d> Flags = new List<Vec2d>();
        public List<Vec2d> Rescues = new List<Vec2d>();
        public List<LevelLine> Lines = new List<LevelLine>();
        public List<EntityDef> Entities = new List<EntityDef>();
        public List<PropDef> Props = new List<PropDef>();
        public List<Objective> Objectives = new List<Objective>();
        /// <summary>Seconds before the run fails, 0 = none.</summary>
        public double TimeLimit;
        public int SurviveFrames;
        public double Zoom;
        public Vec2d? Focus;
    }

    public sealed class RunSummary
    {
        public bool Finished;
        public bool Died;
        public int Frames;
        public double InkUsed;
        public int AirtimeFrames;
        public int Flips;
        public int TrickScore;
        public int FlagsCollected;
        public int FlagsTotal;
        public int Rescued;
        public int RescueTotal;
        public int CargoIntegrity;
        public bool CargoLost;
        public int Chaos;
        public int NearMisses;
        public int HugeDrops;
        public double Distance;
        public int SurvivedFrames;
        public string FailReason;
    }

    public enum Medal
    {
        None,
        Bronze,
        Silver,
        Gold,
    }

    public sealed class ObjectiveResult
    {
        public Objective Objective;
        public bool Done;
    }

    public static class Objectives
    {
        public static string Label(Objective o)
        {
            if (!string.IsNullOrEmpty(o.Label)) return o.Label;
            double v = o.Value;
            switch (o.Kind)
            {
                case "finish": return "Reach the finish";
                case "flags": return o.HasValue ? $"Collect {v:0} flags" : "Collect all flags";
                case "rescue": return o.HasValue ? $"Rescue {v:0}" : "Rescue everyone";
                case "cargo": return $"Deliver cargo at {(o.HasValue ? v : 50):0}%+ integrity";
                case "survive": return $"Survive {((o.HasValue ? v : 400) / 40):0}s";
                case "chaos": return $"Cause {(o.HasValue ? v : 1000):0} chaos";
                case "airtime": return $"{((o.HasValue ? v : 80) / 40):0.0}s total airtime";
                case "flips": return $"Land {(o.HasValue ? v : 1):0} flip{((o.HasValue ? v : 1) > 1 ? "s" : "")}";
                case "inkUnder": return $"Use under {(o.HasValue ? v : 50):0} m of ink";
                case "timeUnder": return $"Finish under {((o.HasValue ? v : 400) / 40):0.0}s";
                case "trickScore": return $"Score {(o.HasValue ? v : 1000):0} trick points";
                case "noDeath": return "Do not crash";
                case "nearMiss": return $"{(o.HasValue ? v : 1):0} near miss{((o.HasValue ? v : 1) > 1 ? "es" : "")}";
                case "distance": return $"Travel {(o.HasValue ? v : 100):0} m";
                case "hugeDrop": return $"{(o.HasValue ? v : 1):0} huge drop{((o.HasValue ? v : 1) > 1 ? "s" : "")}";
                default: return o.Kind;
            }
        }

        public static bool Evaluate(Objective o, RunSummary s)
        {
            double v = o.Value;
            switch (o.Kind)
            {
                case "finish": return s.Finished;
                case "flags": return s.FlagsCollected >= (o.HasValue ? v : s.FlagsTotal);
                case "rescue": return s.Finished && s.Rescued >= (o.HasValue ? v : s.RescueTotal);
                case "cargo": return s.Finished && !s.CargoLost && s.CargoIntegrity >= (o.HasValue ? v : 50);
                case "survive": return s.SurvivedFrames >= (o.HasValue ? v : 400) && !s.Died;
                case "chaos": return s.Chaos >= (o.HasValue ? v : 1000);
                case "airtime": return s.AirtimeFrames >= (o.HasValue ? v : 80);
                case "flips": return s.Flips >= (o.HasValue ? v : 1);
                case "inkUnder": return s.Finished && s.InkUsed <= (o.HasValue ? v : 50) + 1e-6;
                case "timeUnder": return s.Finished && s.Frames <= (o.HasValue ? v : 400);
                case "trickScore": return s.TrickScore >= (o.HasValue ? v : 1000);
                case "noDeath": return s.Finished && !s.Died;
                case "nearMiss": return s.NearMisses >= (o.HasValue ? v : 1);
                case "distance": return s.Distance >= (o.HasValue ? v : 100);
                case "hugeDrop": return s.HugeDrops >= (o.HasValue ? v : 1);
                default: return false;
            }
        }

        public static List<ObjectiveResult> EvaluateLevel(LevelDef level, RunSummary s, out Medal medal, out bool complete)
        {
            var results = new List<ObjectiveResult>();
            foreach (Objective o in level.Objectives) results.Add(new ObjectiveResult { Objective = o, Done = Evaluate(o, s) });
            int optionalTotal = 0;
            int optionalDone = 0;
            complete = true;
            foreach (var r in results)
            {
                if (r.Objective.Optional)
                {
                    optionalTotal++;
                    if (r.Done) optionalDone++;
                }
                else if (!r.Done) complete = false;
            }
            medal = Medal.None;
            if (complete)
            {
                if (optionalTotal == 0 || optionalDone == optionalTotal) medal = Medal.Gold;
                else if (optionalDone * 2 >= optionalTotal) medal = Medal.Silver;
                else medal = Medal.Bronze;
            }
            return results;
        }

        public static int Rank(Medal m) => (int)m;

        public static string MedalName(Medal m) => m.ToString().ToLowerInvariant();

        public static Medal ParseMedal(string s)
        {
            switch (s)
            {
                case "bronze": return Medal.Bronze;
                case "silver": return Medal.Silver;
                case "gold": return Medal.Gold;
                default: return Medal.None;
            }
        }
    }
}
