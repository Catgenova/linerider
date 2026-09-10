using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    /// <summary>Spatial hash of static lines. Queries return lines near a point in ascending id order.</summary>
    public sealed class LineGrid
    {
        private readonly Dictionary<uint, List<Line>> _cells = new Dictionary<uint, List<Line>>();
        private int _queryStamp;

        private static uint Key(int cx, int cy) => (uint)(((cx & 0xffff) << 16) | (cy & 0xffff));

        /// <summary>Walk every grid cell a segment passes through (Amanatides-Woo traversal).</summary>
        public static void CellsAlong(double x1, double y1, double x2, double y2, Action<int, int> cb)
        {
            double S = Constants.GridCell;
            int cx = (int)Math.Floor(x1 / S);
            int cy = (int)Math.Floor(y1 / S);
            int ex = (int)Math.Floor(x2 / S);
            int ey = (int)Math.Floor(y2 / S);
            double dx = x2 - x1;
            double dy = y2 - y1;
            int stepX = dx > 0 ? 1 : -1;
            int stepY = dy > 0 ? 1 : -1;
            double tMaxX = dx != 0 ? ((dx > 0 ? (cx + 1) * S - x1 : x1 - cx * S) / Math.Abs(dx)) : double.PositiveInfinity;
            double tMaxY = dy != 0 ? ((dy > 0 ? (cy + 1) * S - y1 : y1 - cy * S) / Math.Abs(dy)) : double.PositiveInfinity;
            double tDeltaX = dx != 0 ? S / Math.Abs(dx) : double.PositiveInfinity;
            double tDeltaY = dy != 0 ? S / Math.Abs(dy) : double.PositiveInfinity;
            cb(cx, cy);
            int guard = 0;
            while ((cx != ex || cy != ey) && guard++ < 100000)
            {
                if (tMaxX < tMaxY)
                {
                    cx += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    cy += stepY;
                    tMaxY += tDeltaY;
                }
                cb(cx, cy);
            }
        }

        public void Add(Line line)
        {
            line.ExtendedBounds(out double x1, out double y1, out double x2, out double y2);
            CellsAlong(x1, y1, x2, y2, (cx, cy) =>
            {
                uint k = Key(cx, cy);
                if (!_cells.TryGetValue(k, out var bucket))
                {
                    bucket = new List<Line>();
                    _cells[k] = bucket;
                }
                if (bucket.Count == 0 || bucket[bucket.Count - 1] != line) bucket.Add(line);
            });
        }

        public void Remove(Line line)
        {
            line.ExtendedBounds(out double x1, out double y1, out double x2, out double y2);
            CellsAlong(x1, y1, x2, y2, (cx, cy) =>
            {
                uint k = Key(cx, cy);
                if (!_cells.TryGetValue(k, out var bucket)) return;
                bucket.Remove(line);
                if (bucket.Count == 0) _cells.Remove(k);
            });
        }

        public void Clear()
        {
            _cells.Clear();
        }

        /// <summary>Lines in the 3x3 cells around (x,y), de-duplicated and sorted by id for determinism.</summary>
        public List<Line> Near(double x, double y, List<Line> output)
        {
            output.Clear();
            int cx = (int)Math.Floor(x / Constants.GridCell);
            int cy = (int)Math.Floor(y / Constants.GridCell);
            int qs = ++_queryStamp;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    if (!_cells.TryGetValue(Key(cx + i, cy + j), out var bucket)) continue;
                    for (int b = 0; b < bucket.Count; b++)
                    {
                        Line line = bucket[b];
                        if (line.GridStamp == qs) continue;
                        line.GridStamp = qs;
                        output.Add(line);
                    }
                }
            }
            if (output.Count > 1) output.Sort(CompareById);
            return output;
        }

        private static int CompareById(Line a, Line b) => a.Id.CompareTo(b.Id);

        /// <summary>Lines whose cells fall in a rect (for rendering/erasing). Conservative.</summary>
        public List<Line> InRect(double left, double top, double right, double bottom, List<Line> output)
        {
            output.Clear();
            int qs = ++_queryStamp;
            int c0 = (int)Math.Floor(left / Constants.GridCell) - 1;
            int c1 = (int)Math.Floor(right / Constants.GridCell) + 1;
            int r0 = (int)Math.Floor(top / Constants.GridCell) - 1;
            int r1 = (int)Math.Floor(bottom / Constants.GridCell) + 1;
            if ((long)(c1 - c0) * (r1 - r0) > 40000)
            {
                foreach (var bucket in _cells.Values)
                {
                    foreach (var line in bucket)
                    {
                        if (line.GridStamp == qs) continue;
                        line.GridStamp = qs;
                        output.Add(line);
                    }
                }
                return output;
            }
            for (int cy = r0; cy <= r1; cy++)
            {
                for (int cx = c0; cx <= c1; cx++)
                {
                    if (!_cells.TryGetValue(Key(cx, cy), out var bucket)) continue;
                    foreach (var line in bucket)
                    {
                        if (line.GridStamp == qs) continue;
                        line.GridStamp = qs;
                        output.Add(line);
                    }
                }
            }
            return output;
        }
    }
}
