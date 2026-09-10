using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    /// <summary>
    /// Menu attract mode: an endless rolling ride generated ahead of the rider. Terrain is a drifting
    /// sum of cosines whose downhill bias adapts to the rider's speed, with the odd kicker for airtime.
    /// Crashes are part of the show; the game restarts the demo with the next environment.
    /// </summary>
    public sealed class AttractDirector
    {
        private double _frontier;
        private double _lastY;
        private double _drift = 0.1;
        private readonly double _p1;
        private readonly double _p2;
        private int _lastPrune;
        private readonly Track _track;
        private readonly Rng _rng;

        public AttractDirector(Track track, Rng rng)
        {
            _track = track;
            _rng = rng;
            _p1 = rng.Range(0, Math.PI * 2);
            _p2 = rng.Range(0, Math.PI * 2);
            track.Start = new Vec2d(0, 0);
            track.AddLine(-80, 6, 60, 14, MaterialId.Normal, false, 1, LineLayer.Level);
            _frontier = 60;
            _lastY = 14;
            for (int i = 0; i < 8; i++) Chunk(6);
        }

        private void Chunk(double speed)
        {
            const double seg = 40;
            double target = speed > 9.5 ? -0.05 : speed < 4 ? 0.22 : 0.1;
            _drift += (target - _drift) * 0.5;
            bool kicker = speed >= 5 && speed <= 9 && _rng.Chance(0.2);
            double x = _frontier;
            double y = _lastY;
            for (int i = 0; i < 8; i++)
            {
                double nx = x + seg;
                double slope = _drift + 0.17 * Math.Cos(nx / 260 + _p1) + 0.16 * Math.Cos(nx / 95 + _p2);
                double ny = y + slope * seg;
                MaterialId material = slope > 0.22 && _rng.Chance(0.2) ? MaterialId.Accel : MaterialId.Normal;
                _track.AddLine(x, y, nx, ny, material, false, 1, LineLayer.Level);
                x = nx;
                y = ny;
            }
            if (kicker)
            {
                double rx = x + 36;
                double ry = y - 12;
                _track.AddLine(x, y, rx, ry, MaterialId.Normal, false, 1, LineLayer.Level);
                x = rx + 120;
                y = ry + 50;
                _track.AddLine(x, y, x + 240, y + 62, MaterialId.Normal, false, 1, LineLayer.Level);
                x += 240;
                y += 62;
            }
            _frontier = x;
            _lastY = y;
        }

        /// <summary>Extend terrain ahead of the rider and drop lines far behind.</summary>
        public void Update(Run run)
        {
            Vec2d c = run.Rider.Center();
            Vec2d v = run.Rider.Velocity();
            double speed = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            while (_frontier < c.X + 1600) Chunk(speed);
            if (run.Frame - _lastPrune >= 40)
            {
                _lastPrune = run.Frame;
                foreach (LineData l in new List<LineData>(_track.Lines.Values))
                {
                    if (Math.Max(l.X1, l.X2) < c.X - 2500) _track.RemoveLine(l.Id);
                }
            }
        }
    }
}
