namespace NeonLineRider.Core
{
    public sealed class RiderDef
    {
        public string Id;
        public string Name;
        public string Tagline;
        public string Description;
        public string Color;
        public string Glow;
        public RiderModel Model;
        public double GravityScale = 1;
        public double FrictionScale = 1;
        public double EnduranceScale = 1;
        public double StartVelocity = Constants.StartVelocity;
        /// <summary>Level id that unlocks the rider, or null if available from the start.</summary>
        public string Unlock;
    }

    public static class Riders
    {
        public static readonly RiderDef[] All =
        {
            new RiderDef { Id = "bosh", Name = "Bosh", Tagline = "The original", Description = "The classic rig on a hoverboard. Balanced weight, honest momentum, breaks like you remember.", Color = "#39f6ff", Glow = "#00c8ff", Model = RiderModel.Sled },
            new RiderDef { Id = "vex", Name = "Vex", Tagline = "Featherweight", Description = "Floats. Lower effective gravity means longer airtime and slower, softer landings, but the frame is fragile.", Color = "#c6ff4a", Glow = "#8cff00", Model = RiderModel.Sled, GravityScale = 0.78, EnduranceScale = 0.85, Unlock = "peaks-3" },
            new RiderDef { Id = "tank", Name = "Tank", Tagline = "Juggernaut", Description = "Heavy and stubborn. Carries momentum through mud and bumps, shrugs off crashes, but drops like a stone.", Color = "#ffa640", Glow = "#ff7a00", Model = RiderModel.Sled, GravityScale = 1.18, FrictionScale = 0.45, EnduranceScale = 1.9, StartVelocity = Constants.StartVelocity * 1.5, Unlock = "glacier-2" },
            new RiderDef { Id = "nova", Name = "Nova", Tagline = "Boarder", Description = "Rides a long deck with a tall standing rig. Longer contact patch, higher centre of mass: stable on rails, wild in the air.", Color = "#ff2bd6", Glow = "#ff00c8", Model = RiderModel.Board, FrictionScale = 0.7, EnduranceScale = 1.25, Unlock = "roof-2" },
        };

        public static RiderDef Get(string id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            return All[0];
        }
    }
}
