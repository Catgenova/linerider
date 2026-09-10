namespace CyberRider.Core
{
    /// <summary>
    /// Draw-while-riding: the rider is always moving, terrain is generated ahead as ledges with gaps
    /// that widen with distance, a tailwind ramps the pace up, and ink regenerates with distance.
    /// </summary>
    public sealed class ArcadeDirector
    {
        public double Budget = 40;
        private double _frontier;
        private double _lastY;
        private int _chunk;
        private readonly Track _track;
        private readonly Rng _rng;

        public ArcadeDirector(Track track, Rng rng)
        {
            _track = track;
            _rng = rng;
            track.AddLine(-40, 6, 140, 12, MaterialId.Normal, false, 1, LineLayer.Level);
            _frontier = 140;
            _lastY = 12;
            for (int i = 0; i < 6; i++) GenerateChunk();
        }

        private void GenerateChunk()
        {
            Rng rng = _rng;
            double difficulty = System.Math.Min(1, _chunk / 40.0);
            double gap = 40 + difficulty * 130 + rng.Range(0, 40);
            double drop = rng.Range(10, 40 + difficulty * 40);
            double width = rng.Range(120, 260);
            double x0 = _frontier + gap;
            double y0 = _lastY + drop;
            double slope = rng.Range(-0.05, 0.2);
            double y1 = y0 + width * slope;
            MaterialId material = rng.Chance(0.18 + difficulty * 0.2) ? MaterialId.Accel : MaterialId.Normal;
            _track.AddLine(x0, y0, x0 + width, y1, material, false, 1, LineLayer.Level);
            if (rng.Chance(0.25))
            {
                double bx = x0 + width * rng.Range(0.3, 0.7);
                double by = y0 + (bx - x0) * slope;
                _track.AddLine(bx - 14, by, bx, by - 8, MaterialId.Normal, false, 1, LineLayer.Level);
                _track.AddLine(bx, by - 8, bx + 14, by, MaterialId.Normal, false, 1, LineLayer.Level);
            }
            _frontier = x0 + width;
            _lastY = y1;
            _chunk++;
        }

        /// <summary>Extend terrain, grow the budget and raise the tailwind. Returns the new budget.</summary>
        public double Update(Run run)
        {
            Vec2d c = run.Rider.Center();
            while (_frontier < c.X + 1400) GenerateChunk();
            double distance = (run.MaxX - _track.Start.X) / Constants.PxPerMeter;
            Budget = 40 + distance * 0.55;
            double push = System.Math.Min(0.06, 0.008 + run.Frame * 0.00002);
            run.World.Wind = (double x, double y, int f, out double fx, out double fy) =>
            {
                fx = push;
                fy = 0;
            };
            return Budget;
        }
    }
}
