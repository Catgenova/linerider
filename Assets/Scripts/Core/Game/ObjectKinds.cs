using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    public sealed class Placement
    {
        public EntityDef Entity;
        public PropDef Prop;
        public List<LevelLine> Lines;
    }

    public sealed class ObjectKindDef
    {
        public string Id;
        public string Name;
        public string Icon;
        /// <summary>"toys", "hazards" or "props".</summary>
        public string Group;
        public string Description;
        public Func<double, double, Placement> Make;
    }

    /// <summary>Everything the player can drop into a free-ride track, with sensible default sizes.</summary>
    public static class ObjectKinds
    {
        public static readonly ObjectKindDef[] All =
        {
            new ObjectKindDef { Id = "spring", Name = "Spring pad", Icon = "^", Group = "toys", Description = "A bouncy surface.", Make = (x, y) => new Placement { Lines = new List<LevelLine> { new LevelLine(x - 25, y, x + 25, y, "spring") } } },
            new ObjectKindDef { Id = "ramp", Name = "Ramp", Icon = "/", Group = "toys", Description = "A kicker with a vertical back.", Make = (x, y) => new Placement { Lines = new List<LevelLine> { new LevelLine(x - 40, y, x + 20, y - 30, "normal"), new LevelLine(x + 20, y - 30, x + 20, y, "normal") } } },
            new ObjectKindDef { Id = "seesaw", Name = "Seesaw", Icon = "=", Group = "toys", Description = "Tilts under weight.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "seesaw", X = x, Y = y, Length = 140 } } },
            new ObjectKindDef { Id = "pendulum", Name = "Swing", Icon = "|", Group = "toys", Description = "A bar swinging from a pivot above.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "pendulum", X = x, Y = y - 160, Length = 160, Width = 40, Amplitude = 0.8, Period = 180 } } },
            new ObjectKindDef { Id = "fanUp", Name = "Fan (up)", Icon = "^^", Group = "toys", Description = "Column of air pushing up.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "fan", X = x - 40, Y = y - 260, W = 80, H = 260, Fx = 0, Fy = -0.4 } } },
            new ObjectKindDef { Id = "fanRight", Name = "Fan (right)", Icon = ">>", Group = "toys", Description = "Air pushing to the right.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "fan", X = x, Y = y - 60, W = 220, H = 120, Fx = 0.25, Fy = -0.02 } } },
            new ObjectKindDef { Id = "magnet", Name = "Magnet", Icon = "o", Group = "toys", Description = "Pulls the rider in.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "magnet", X = x, Y = y, Radius = 160, Strength = 0.08 } } },
            new ObjectKindDef { Id = "cannon", Name = "Cannon", Icon = "*", Group = "toys", Description = "Launches at 45 degrees.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "cannon", X = x, Y = y, Angle = -45, Power = 11 } } },
            new ObjectKindDef { Id = "balloon", Name = "Balloon", Icon = "O", Group = "toys", Description = "Lifts the rider for three seconds.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "balloon", X = x, Y = y, Lift = 0.3, Duration = 120 } } },
            new ObjectKindDef { Id = "platformV", Name = "Lift", Icon = "v^", Group = "toys", Description = "Platform moving up and down.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "platform", X = x, Y = y, W = 70, Dx = 0, Dy = 60, Period = 200 } } },
            new ObjectKindDef { Id = "platformH", Name = "Shuttle", Icon = "<>", Group = "toys", Description = "Platform moving side to side.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "platform", X = x, Y = y, W = 70, Dx = 90, Dy = 0, Period = 220 } } },
            new ObjectKindDef { Id = "wall", Name = "Breakable wall", Icon = "#", Group = "hazards", Description = "Shatters when hit fast.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "wall", X1 = x, Y1 = y - 40, X2 = x, Y2 = y + 40, Threshold = 4 } } },
            new ObjectKindDef { Id = "gear", Name = "Gear", Icon = "@", Group = "hazards", Description = "Spinning toothed wheel.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "gear", X = x, Y = y, Radius = 60, Teeth = 8, Speed = 0.02 } } },
            new ObjectKindDef { Id = "train", Name = "Train", Icon = "[]", Group = "hazards", Description = "Shuttles back and forth.", Make = (x, y) => new Placement { Entity = new EntityDef { Type = "train", X = x, Y = y, W = 110, H = 28, Speed = 2, Range = 300 } } },
            new ObjectKindDef { Id = "domino", Name = "Domino", Icon = "|", Group = "props", Description = "Knock it over.", Make = (x, y) => new Placement { Prop = new PropDef { Kind = "domino", X = x, Y = y - 12, Width = 4, Height = 24, Mass = 0.5 } } },
            new ObjectKindDef { Id = "crate", Name = "Crate", Icon = "X", Group = "props", Description = "A pushable box.", Make = (x, y) => new Placement { Prop = new PropDef { Kind = "crate", X = x, Y = y - 10, Width = 20, Height = 20, Mass = 1.5 } } },
            new ObjectKindDef { Id = "tnt", Name = "TNT", Icon = "!", Group = "props", Description = "Explodes on a hard hit.", Make = (x, y) => new Placement { Prop = new PropDef { Kind = "tnt", X = x, Y = y - 9, Width = 18, Height = 18, Mass = 1, Explosive = true } } },
            new ObjectKindDef { Id = "boulder", Name = "Boulder", Icon = "()", Group = "props", Description = "Heavy and rolls.", Make = (x, y) => new Placement { Prop = new PropDef { Kind = "boulder", X = x, Y = y - 14, Radius = 14, Mass = 6 } } },
            new ObjectKindDef { Id = "cart", Name = "Cart", Icon = "__", Group = "props", Description = "Low, slippery, fast.", Make = (x, y) => new Placement { Prop = new PropDef { Kind = "cart", X = x, Y = y - 6, Width = 34, Height = 12, Mass = 1.2, Friction = 0.05 } } },
        };

        public static ObjectKindDef Get(string id)
        {
            foreach (var k in All) if (k.Id == id) return k;
            return null;
        }
    }
}
