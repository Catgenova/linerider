using System;
using System.Collections.Generic;

namespace NeonLineRider.Core
{
    /// <summary>Seeded daily challenge generator. Same date gives the same terrain as the web build.</summary>
    public static class Daily
    {
        private static readonly string[] Envs = { "mountain", "glacier", "desert", "forest", "rooftops", "moon", "machine" };

        public static string TodayKey()
        {
            DateTime d = DateTime.UtcNow;
            return $"{d.Year:0000}-{d.Month:00}-{d.Day:00}";
        }

        public static uint SeedFor(string key) => Rng.HashString("neon-daily:" + key);

        private static double JsRound(double v) => Math.Floor(v + 0.5);

        public static LevelDef Generate(uint seed, string key)
        {
            var rng = new Rng(seed);
            string environment = rng.Pick(Envs);
            var lines = new List<LevelLine> { new LevelLine(-40, 6, 100, 14) };
            var flags = new List<Vec2d>();
            double x = 100;
            double y = 14;
            int segments = rng.Int(4, 6);
            for (int i = 0; i < segments; i++)
            {
                double gap = rng.Range(70, 190);
                double drop = rng.Range(30, 110);
                double width = rng.Range(120, 300);
                double x0 = x + gap;
                double y0 = y + drop;
                double slope = rng.Range(-0.1, 0.35);
                string material = rng.Chance(0.2) ? (environment == "glacier" ? "ice" : "accel") : "normal";
                lines.Add(new LevelLine(x0, y0, x0 + width, y0 + width * slope, material));
                bool chance = rng.Chance(0.7);
                if (chance && flags.Count < 3)
                {
                    flags.Add(new Vec2d(x + gap * rng.Range(0.3, 0.7), y + drop * rng.Range(0.1, 0.6) - 20));
                }
                x = x0 + width;
                y = y0 + width * slope;
            }
            lines.Add(new LevelLine(x, y, x + 200, y));
            lines.Add(new LevelLine(x + 200, y, x + 200, y - 120));
            double budget = JsRound(90 + rng.Range(0, 80));
            var level = new LevelDef
            {
                Id = "daily-" + key,
                Name = "Daily " + key,
                Region = "peaks",
                Environment = environment,
                Mode = "reach",
                Tagline = "Same terrain for everyone today.",
                Briefing = "Today's seed builds the same terrain, flags, finish and ink budget for every player. Fastest time, least ink and highest trick score are ranked separately.",
                Budget = budget,
                Materials = new List<MaterialId> { MaterialId.Normal, MaterialId.Accel, MaterialId.Spring, MaterialId.Crumble },
                Rider = null,
                Start = new Vec2d(0, 0),
                Finish = new Zone(x + 120, y - 80, 50, 80),
                Flags = flags,
                Lines = lines,
                Zoom = 1,
                Focus = new Vec2d(x / 2, y / 2),
            };
            level.Objectives.Add(new Objective("finish"));
            level.Objectives.Add(new Objective("flags", flags.Count, true));
            level.Objectives.Add(new Objective("inkUnder", JsRound(budget * 0.6), true));
            level.Objectives.Add(new Objective("timeUnder", 40 * JsRound(6 + segments * 2.2), true));
            return level;
        }
    }
}
