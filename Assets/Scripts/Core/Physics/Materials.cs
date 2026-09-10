using System.Collections.Generic;

namespace NeonLineRider.Core
{
    public enum MaterialId
    {
        Normal,
        Accel,
        Scenery,
        Ice,
        Mud,
        Spring,
        Sticky,
        Conveyor,
        Crumble,
        Booster,
        OneWay,
        Rail,
    }

    /// <summary>A track surface type. Colours are hex strings so the core stays engine-free.</summary>
    public sealed class Material
    {
        public MaterialId Id;
        public string Key;
        public string Name;
        public string Color;
        public string Glow;
        public double Cost;
        public bool Solid;
        public double FrictionScale;
        public double Accel;
        public double ConveyorSpeed;
        public double Restitution;
        public double Drag;
        public double Stickiness;
        public int CrumbleFrames;
        public bool OneWay;
        public bool Grind;
        public bool Arrows;
        public string Hotkey;
        public string Description;
    }

    public static class Materials
    {
        public static readonly Material[] All =
        {
            new Material { Id = MaterialId.Normal, Key = "normal", Name = "Track", Color = "#39f6ff", Glow = "#00c8ff", Cost = 1, Solid = true, FrictionScale = 1, Hotkey = "1", Description = "Standard neon rail. One-sided: solid on the side you drew from." },
            new Material { Id = MaterialId.Accel, Key = "accel", Name = "Accel", Color = "#ff3d7f", Glow = "#ff0055", Cost = 1.5, Solid = true, FrictionScale = 1, Accel = 0.1, Arrows = true, Hotkey = "2", Description = "Pushes the rider along the arrow on every contact." },
            new Material { Id = MaterialId.Scenery, Key = "scenery", Name = "Scenery", Color = "#4a5080", Glow = "#2a2f55", Cost = 0, Solid = false, FrictionScale = 0, Hotkey = "3", Description = "Decorative only. Free to draw, no collision." },
            new Material { Id = MaterialId.Ice, Key = "ice", Name = "Ice", Color = "#c8fbff", Glow = "#7ae8ff", Cost = 1, Solid = true, FrictionScale = 0, Hotkey = "4", Description = "Frictionless. Speed is kept but control is not." },
            new Material { Id = MaterialId.Mud, Key = "mud", Name = "Mud", Color = "#ffa640", Glow = "#ff7a00", Cost = 1, Solid = true, FrictionScale = 3, Drag = 0.9, Hotkey = "5", Description = "Thick and slow. Bleeds speed on every touch." },
            new Material { Id = MaterialId.Spring, Key = "spring", Name = "Spring", Color = "#c6ff4a", Glow = "#8cff00", Cost = 2, Solid = true, FrictionScale = 0.5, Restitution = 0.9, Hotkey = "6", Description = "Bounces the rider back off the surface." },
            new Material { Id = MaterialId.Sticky, Key = "sticky", Name = "Sticky", Color = "#c65cff", Glow = "#9b1cff", Cost = 1.5, Solid = true, FrictionScale = 6, Stickiness = 0.35, Hotkey = "7", Description = "Grabs the board. Great for killing speed before a drop." },
            new Material { Id = MaterialId.Conveyor, Key = "conveyor", Name = "Conveyor", Color = "#fff04a", Glow = "#ffd000", Cost = 2, Solid = true, FrictionScale = 1, ConveyorSpeed = 6, Arrows = true, Hotkey = "8", Description = "Drives the rider toward a fixed belt speed, either direction." },
            new Material { Id = MaterialId.Crumble, Key = "crumble", Name = "Crumble", Color = "#ff7a45", Glow = "#ff3c00", Cost = 0.75, Solid = true, FrictionScale = 1, CrumbleFrames = 30, Hotkey = "9", Description = "Shatters shortly after the first touch. Cheap but temporary." },
            new Material { Id = MaterialId.Booster, Key = "booster", Name = "Booster", Color = "#ff2bd6", Glow = "#ff00c8", Cost = 3, Solid = true, FrictionScale = 0, Accel = 0.35, Arrows = true, Hotkey = "0", Description = "A hard shove along the arrow. Expensive ink." },
            new Material { Id = MaterialId.OneWay, Key = "oneway", Name = "One-way", Color = "#4dff9d", Glow = "#00ff77", Cost = 1.25, Solid = true, FrictionScale = 1, OneWay = true, Arrows = true, Hotkey = "-", Description = "Solid only while moving along the arrow. Pass through the other way." },
            new Material { Id = MaterialId.Rail, Key = "rail", Name = "Grind rail", Color = "#f4f4ff", Glow = "#b0b8ff", Cost = 1.25, Solid = true, FrictionScale = 0, Grind = true, Hotkey = "=", Description = "Frictionless rail. Riding it scores grind points." },
        };

        private static readonly Dictionary<string, Material> ByKey = Build();

        private static Dictionary<string, Material> Build()
        {
            var d = new Dictionary<string, Material>();
            foreach (var m in All) d[m.Key] = m;
            return d;
        }

        public static Material Get(MaterialId id) => All[(int)id];

        public static Material Get(string key)
        {
            if (key != null && ByKey.TryGetValue(key, out var m)) return m;
            return All[0];
        }

        public static MaterialId ParseId(string key) => Get(key).Id;

        public static string KeyOf(MaterialId id) => All[(int)id].Key;
    }
}
