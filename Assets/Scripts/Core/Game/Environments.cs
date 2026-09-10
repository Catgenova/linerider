using System;
using System.Collections.Generic;

namespace NeonLineRider.Core
{
    public sealed class Environment
    {
        public string Id;
        public string Name;
        public string Blurb;
        public string Gimmick;
        public double GravityScale = 1;
        public double FrictionScale = 1;
        public bool HasWind;
        public double WindX, WindY, Gust, WindPeriod;
        /// <summary>Light radius around the rider in px; 0 = fully lit.</summary>
        public double Darkness;
        public string Bg0, Bg1, Accent, Accent2, GridColor, Fog, Skyline;
        public bool HasOrb;
        public string OrbColor;
        public double OrbSize, OrbX, OrbY;
    }

    public static class Environments
    {
        public static readonly Environment[] All =
        {
            new Environment { Id = "mountain", Name = "Neon Peaks", Blurb = "Jagged synth ridges above a sleepless city.", Gimmick = "Pure speed. Nothing but gravity and your line.", Bg0 = "#070317", Bg1 = "#1a0838", Accent = "#39f6ff", Accent2 = "#ff2bd6", GridColor = "#5030c8", Fog = "#7828dc", Skyline = "peaks", HasOrb = true, OrbColor = "#ff2bd6", OrbSize = 90, OrbX = 0.72, OrbY = 0.36 },
            new Environment { Id = "glacier", Name = "Glacier Grid", Blurb = "Frozen data shelves that never thaw.", Gimmick = "Every surface is ice. Friction is switched off, so speed only leaves through crashes.", FrictionScale = 0, Bg0 = "#03111c", Bg1 = "#0a2a45", Accent = "#a8f4ff", Accent2 = "#5cc8ff", GridColor = "#5ac8ff", Fog = "#78dcff", Skyline = "crystals", HasOrb = true, OrbColor = "#b8f0ff", OrbSize = 60, OrbX = 0.2, OrbY = 0.3 },
            new Environment { Id = "desert", Name = "Dune Circuit", Blurb = "Red mesas under a burnt orange sky.", Gimmick = "Crosswinds gust across the whole level and shove the rider mid-air.", HasWind = true, WindX = 0.02, WindY = 0, Gust = 0.05, WindPeriod = 140, Bg0 = "#1a0507", Bg1 = "#4a1608", Accent = "#ffa640", Accent2 = "#ff3d7f", GridColor = "#ff8c3c", Fog = "#ff6e28", Skyline = "mesas", HasOrb = true, OrbColor = "#ff7a1a", OrbSize = 120, OrbX = 0.6, OrbY = 0.42 },
            new Environment { Id = "forest", Name = "Data Forest", Blurb = "Fibre-optic trunks and bioluminescent canopies.", Gimmick = "Springy canopy lines and low visibility. Bounce your way through.", Bg0 = "#020f0a", Bg1 = "#08301c", Accent = "#4dff9d", Accent2 = "#c6ff4a", GridColor = "#3cdc78", Fog = "#28c878", Skyline = "trees" },
            new Environment { Id = "rooftops", Name = "Skyline District", Blurb = "Rooftop to rooftop across the megacity.", Gimmick = "Precision. Tight ink budgets, narrow ledges, long drops.", HasWind = true, WindX = 0, WindY = 0, Gust = 0.03, WindPeriod = 90, Bg0 = "#05061a", Bg1 = "#151040", Accent = "#ff2bd6", Accent2 = "#39f6ff", GridColor = "#ff3cc8", Fog = "#c828ff", Skyline = "city" },
            new Environment { Id = "cave", Name = "Undercity Caves", Blurb = "Beneath the grid, where the neon never reached.", Gimmick = "Darkness. Only a small halo around the rider is lit, so plan from memory.", Darkness = 170, Bg0 = "#020106", Bg1 = "#0a0416", Accent = "#c65cff", Accent2 = "#39f6ff", GridColor = "#783cdc", Fog = "#5014a0", Skyline = "stalactites" },
            new Environment { Id = "moon", Name = "Lunar Relay", Blurb = "A silent regolith bowl under a huge blue Earth.", Gimmick = "Low gravity. Jumps go forever, landings come slow.", GravityScale = 0.4, Bg0 = "#000005", Bg1 = "#0b0b26", Accent = "#f4f4ff", Accent2 = "#5cc8ff", GridColor = "#8c8cff", Fog = "#3c3ca0", Skyline = "craters", HasOrb = true, OrbColor = "#3d8bff", OrbSize = 140, OrbX = 0.78, OrbY = 0.28 },
            new Environment { Id = "machine", Name = "The Machine", Blurb = "Inside the engine that runs the city.", Gimmick = "Everything moves. Gears, pistons, and conveyors decide your timing.", Bg0 = "#0a0705", Bg1 = "#2a1508", Accent = "#fff04a", Accent2 = "#ff7a45", GridColor = "#ffc83c", Fog = "#ff9628", Skyline = "gears" },
        };

        private static readonly Dictionary<string, Environment> ById = Build();

        private static Dictionary<string, Environment> Build()
        {
            var d = new Dictionary<string, Environment>();
            foreach (var e in All) d[e.Id] = e;
            return d;
        }

        public static Environment Get(string id)
        {
            if (id != null && ById.TryGetValue(id, out var e)) return e;
            return All[0];
        }

        /// <summary>Build the wind function for an environment, or null if it has no wind.</summary>
        public static WindFn MakeWind(Environment e)
        {
            if (!e.HasWind) return null;
            double wx = e.WindX, wy = e.WindY, gustAmp = e.Gust, period = e.WindPeriod;
            return (double x, double y, int frame, out double fx, out double fy) =>
            {
                double phase = frame / period + x * 0.0015;
                double gust = Math.Sin(phase * Math.PI * 2) * Math.Sin(phase * 0.37 * Math.PI * 2 + 1.3);
                fx = wx + gust * gustAmp;
                fy = wy + Math.Sin(phase * 1.7) * gustAmp * 0.25;
            };
        }
    }
}
