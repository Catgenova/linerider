using System;
using System.Collections.Generic;

namespace CyberRider.Core
{
    public enum ToolId
    {
        Pencil,
        Line,
        Eraser,
        Pan,
        Flip,
        Object,
    }

    public sealed class EditorConstraints
    {
        /// <summary>Ink budget in metres, or null for unlimited.</summary>
        public double? Budget;
        public List<MaterialId> Materials = new List<MaterialId> { MaterialId.Normal, MaterialId.Accel, MaterialId.Scenery };
        public bool CanEraseLevel;
        public int Player;
        public bool Locked;
        public bool CanPlaceObjects;
    }

    /// <summary>
    /// Track editing in world coordinates: pencil, straight line, eraser, flip and object placement
    /// with snapping, budget truncation and undo/redo. Panning and zoom belong to the view layer.
    /// </summary>
    public sealed class Editor
    {
        private sealed class Command
        {
            public readonly List<LineData> Added = new List<LineData>();
            public readonly List<LineData> Removed = new List<LineData>();
            public readonly List<ObjectData> AddedObjects = new List<ObjectData>();
            public readonly List<ObjectData> RemovedObjects = new List<ObjectData>();
            public readonly List<PropData> AddedProps = new List<PropData>();
            public readonly List<PropData> RemovedProps = new List<PropData>();

            public bool Empty => Added.Count == 0 && Removed.Count == 0 && AddedObjects.Count == 0 && RemovedObjects.Count == 0 && AddedProps.Count == 0 && RemovedProps.Count == 0;
        }

        public ToolId Tool = ToolId.Pencil;
        public MaterialId Material = MaterialId.Normal;
        public string ObjectKind = "spring";
        public EditorConstraints Constraints = new EditorConstraints();
        public Action OnInkExhausted;
        public Action OnEdit;
        /// <summary>Straight-line preview while dragging (world coords), or null.</summary>
        public double[] Preview;
        public int HoverId = -1;
        public Vec2d CursorWorld;
        /// <summary>Screen radius of the eraser in pixels; divided by zoom for world radius.</summary>
        public double EraserRadiusScreen = 9;
        /// <summary>Current view zoom (screen px per world px) supplied by the view layer.</summary>
        public double Zoom = 2;
        private readonly List<Command> _undo = new List<Command>();
        private readonly List<Command> _redo = new List<Command>();
        private Command _current;
        private bool _dragging;
        private Vec2d? _anchor;
        private Vec2d? _lastPencil;
        private bool _exhaustedNotified;
        public readonly Track Track;

        public Editor(Track track)
        {
            Track = track;
        }

        public double InkUsed => Track.InkUsed(Constraints.Player > 0 ? Constraints.Player : (int?)null);

        public double InkRemaining => Constraints.Budget.HasValue ? Math.Max(0, Constraints.Budget.Value - InkUsed) : double.PositiveInfinity;

        public bool CanUseMaterial(MaterialId m) => Constraints.Materials.Contains(m);

        private double SnapRadiusWorld => 8 / Zoom;

        private Vec2d Snap(double x, double y)
        {
            return Track.SnapPoint(x, y, SnapRadiusWorld, out Vec2d s) ? s : new Vec2d(x, y);
        }

        /// <summary>Primary button pressed at a world position.</summary>
        public void PointerDown(double wx, double wy, bool shift)
        {
            CursorWorld = new Vec2d(wx, wy);
            if (Constraints.Locked) return;
            _dragging = true;
            switch (Tool)
            {
                case ToolId.Pencil:
                    _lastPencil = Snap(wx, wy);
                    _current = new Command();
                    _exhaustedNotified = false;
                    break;
                case ToolId.Line:
                {
                    Vec2d p = Snap(wx, wy);
                    _anchor = p;
                    Preview = new[] { p.X, p.Y, p.X, p.Y };
                    break;
                }
                case ToolId.Eraser:
                    _current = new Command();
                    EraseAt(wx, wy);
                    break;
                case ToolId.Flip:
                {
                    int id = FindNearest(wx, wy, 10 / Zoom);
                    if (id >= 0) Flip(id);
                    break;
                }
                case ToolId.Object:
                    PlaceObject(wx, wy);
                    break;
            }
        }

        /// <summary>Secondary button: flip the nearest line.</summary>
        public void SecondaryClick(double wx, double wy)
        {
            int id = FindNearest(wx, wy, 10 / Zoom);
            if (id >= 0 && !Constraints.Locked) Flip(id);
        }

        public void PointerMove(double wx, double wy, bool shift)
        {
            CursorWorld = new Vec2d(wx, wy);
            if (!_dragging)
            {
                if (Tool == ToolId.Eraser || Tool == ToolId.Flip) HoverId = FindNearest(wx, wy, 10 / Zoom);
                else HoverId = -1;
                return;
            }
            switch (Tool)
            {
                case ToolId.Pencil:
                {
                    if (!_lastPencil.HasValue) break;
                    Vec2d last = _lastPencil.Value;
                    double minSeg = Math.Max(2, 5 / Zoom);
                    double dx = wx - last.X;
                    double dy = wy - last.Y;
                    if (dx * dx + dy * dy < minSeg * minSeg) break;
                    LineData added = AddLine(last.X, last.Y, wx, wy);
                    if (added != null)
                    {
                        _lastPencil = new Vec2d(added.X2, added.Y2);
                        if (added.X2 != wx || added.Y2 != wy) _lastPencil = null;
                    }
                    else _lastPencil = null;
                    break;
                }
                case ToolId.Line:
                {
                    if (!_anchor.HasValue) break;
                    Vec2d a = _anchor.Value;
                    Vec2d end = Snap(wx, wy);
                    if (shift)
                    {
                        double ang = Math.Atan2(end.Y - a.Y, end.X - a.X);
                        double step = Math.PI / 12;
                        double snapped = Math.Round(ang / step) * step;
                        double len = Math.Sqrt((end.X - a.X) * (end.X - a.X) + (end.Y - a.Y) * (end.Y - a.Y));
                        end = new Vec2d(a.X + Math.Cos(snapped) * len, a.Y + Math.Sin(snapped) * len);
                    }
                    Preview = new[] { a.X, a.Y, end.X, end.Y };
                    break;
                }
                case ToolId.Eraser:
                    EraseAt(wx, wy);
                    break;
            }
        }

        public void PointerUp()
        {
            if (!_dragging) return;
            _dragging = false;
            switch (Tool)
            {
                case ToolId.Pencil:
                    Commit();
                    _lastPencil = null;
                    break;
                case ToolId.Line:
                    if (Preview != null)
                    {
                        double[] p = Preview;
                        double len = Math.Sqrt((p[2] - p[0]) * (p[2] - p[0]) + (p[3] - p[1]) * (p[3] - p[1]));
                        if (len >= 1)
                        {
                            _current = new Command();
                            AddLine(p[0], p[1], p[2], p[3]);
                            Commit();
                        }
                    }
                    Preview = null;
                    _anchor = null;
                    break;
                case ToolId.Eraser:
                    Commit();
                    break;
            }
        }

        public void Cancel()
        {
            _dragging = false;
            Preview = null;
            _anchor = null;
            _lastPencil = null;
            Commit();
        }

        /// <summary>Abandon the drag in progress and revert what it changed (a second finger turned it into a gesture).</summary>
        public void DiscardDrag()
        {
            Command c = _current;
            _dragging = false;
            Preview = null;
            _anchor = null;
            _lastPencil = null;
            _current = null;
            if (c == null || c.Empty) return;
            foreach (LineData l in c.Added) Track.RemoveLine(l.Id);
            foreach (LineData l in c.Removed) Track.AddLine(l);
            foreach (ObjectData o in c.AddedObjects) Track.RemoveObject(o.Id);
            foreach (ObjectData o in c.RemovedObjects) Track.AddObject(o.Def, o.Id);
            foreach (PropData o in c.AddedProps) Track.RemoveProp(o.Id);
            foreach (PropData o in c.RemovedProps) Track.AddProp(o.Def, o.Id);
            OnEdit?.Invoke();
        }

        /// <summary>Add a line respecting the ink budget. Returns the line actually added (possibly truncated).</summary>
        public LineData AddLine(double x1, double y1, double x2, double y2)
        {
            if (Constraints.Locked) return null;
            if (Constraints.Materials.Count == 0) return null;
            MaterialId material = CanUseMaterial(Material) ? Material : Constraints.Materials[0];
            double costPerPx = Materials.Get(material).Cost / Constants.PxPerMeter;
            double ex = x2;
            double ey = y2;
            double len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            if (len < 0.5) return null;
            if (costPerPx > 0)
            {
                double remaining = InkRemaining;
                double cost = len * costPerPx;
                if (cost > remaining)
                {
                    double allowed = remaining / costPerPx;
                    if (allowed < 2)
                    {
                        if (!_exhaustedNotified)
                        {
                            _exhaustedNotified = true;
                            OnInkExhausted?.Invoke();
                        }
                        return null;
                    }
                    ex = x1 + ((x2 - x1) * allowed) / len;
                    ey = y1 + ((y2 - y1) * allowed) / len;
                    _exhaustedNotified = true;
                    OnInkExhausted?.Invoke();
                }
            }
            LineData line = Track.AddLine(x1, y1, ex, ey, material, false, 1, LineLayer.Player, Constraints.Player);
            if (_current == null) _current = new Command();
            _current.Added.Add(line.Clone());
            // Outside a drag (scripted adds) each line is its own undo step.
            if (!_dragging) Commit();
            OnEdit?.Invoke();
            return line;
        }

        /// <summary>Surface height of the nearest rideable line or prop top directly below (or just above) a point.</summary>
        private double? SurfaceBelow(double x, double y, double reach = 80)
        {
            double? best = null;
            foreach (LineData l in Track.Lines.Values)
            {
                if (!Materials.Get(l.Material).Solid) continue;
                double minX = Math.Min(l.X1, l.X2);
                double maxX = Math.Max(l.X1, l.X2);
                if (x < minX || x > maxX || maxX == minX) continue;
                double t = (x - l.X1) / (l.X2 - l.X1);
                double ly = l.Y1 + (l.Y2 - l.Y1) * t;
                if (ly < y - 20 || ly > y + reach) continue;
                if (!best.HasValue || ly < best.Value) best = ly;
            }
            foreach (PropData o in Track.Props.Values)
            {
                PropDef d = o.Def;
                double halfW = d.Radius > 0 ? d.Radius : d.Width / 2;
                double top = d.Radius > 0 ? d.Y - d.Radius : d.Y - d.Height / 2;
                if (x < d.X - halfW || x > d.X + halfW) continue;
                if (top < y - 20 || top > y + reach) continue;
                if (!best.HasValue || top < best.Value) best = top;
            }
            return best;
        }

        /// <summary>Drop the selected object kind at a world position. Props snap onto the surface beneath.</summary>
        public bool PlaceObject(double x, double y)
        {
            if (!Constraints.CanPlaceObjects || Constraints.Locked) return false;
            ObjectKindDef kind = ObjectKinds.Get(ObjectKind);
            if (kind == null) return false;
            double py = y;
            if (kind.Group == "props" || kind.Id == "spring" || kind.Id == "ramp")
            {
                double? surface = SurfaceBelow(x, y);
                if (surface.HasValue) py = surface.Value - 0.5;
            }
            Placement placement = kind.Make(Math.Round(x), Math.Round(py));
            var cmd = new Command();
            if (placement.Lines != null)
            {
                foreach (LevelLine l in placement.Lines)
                {
                    LineData line = Track.AddLine(l.X1, l.Y1, l.X2, l.Y2, Materials.ParseId(l.Material ?? "normal"), l.Flipped, l.Multiplier, LineLayer.Player, Constraints.Player);
                    cmd.Added.Add(line.Clone());
                }
            }
            if (placement.Entity != null) cmd.AddedObjects.Add(Track.AddObject(placement.Entity));
            if (placement.Prop != null) cmd.AddedProps.Add(Track.AddProp(placement.Prop));
            _undo.Add(cmd);
            _redo.Clear();
            OnEdit?.Invoke();
            return true;
        }

        private void EraseAt(double x, double y)
        {
            double r = EraserRadiusScreen / Zoom;
            var hits = new List<LineData>();
            foreach (LineData l in Track.Lines.Values)
            {
                if (l.Layer == LineLayer.Level && !Constraints.CanEraseLevel) continue;
                if (Constraints.Player > 0 && l.Player != Constraints.Player && l.Layer == LineLayer.Player) continue;
                if (MathUtil.DistToSegment(x, y, l.X1, l.Y1, l.X2, l.Y2) <= r) hits.Add(l);
            }
            foreach (LineData l in hits)
            {
                Track.RemoveLine(l.Id);
                _current?.Removed.Add(l.Clone());
            }
            int hitObjects = 0;
            if (Constraints.CanPlaceObjects)
            {
                double rr = Math.Max(r, 14 / Zoom);
                foreach (ObjectData o in new List<ObjectData>(Track.Objects.Values))
                {
                    Vec2d a = o.Def.Anchor();
                    if (Math.Sqrt((a.X - x) * (a.X - x) + (a.Y - y) * (a.Y - y)) <= rr)
                    {
                        Track.RemoveObject(o.Id);
                        _current?.RemovedObjects.Add(o);
                        hitObjects++;
                    }
                }
                foreach (PropData o in new List<PropData>(Track.Props.Values))
                {
                    if (Math.Sqrt((o.Def.X - x) * (o.Def.X - x) + (o.Def.Y - y) * (o.Def.Y - y)) <= rr)
                    {
                        Track.RemoveProp(o.Id);
                        _current?.RemovedProps.Add(o);
                        hitObjects++;
                    }
                }
            }
            if (hits.Count > 0 || hitObjects > 0) OnEdit?.Invoke();
        }

        private void Flip(int id)
        {
            if (!Track.Lines.TryGetValue(id, out var l)) return;
            if (l.Layer == LineLayer.Level && !Constraints.CanEraseLevel) return;
            Track.FlipLine(id);
            OnEdit?.Invoke();
        }

        private int FindNearest(double x, double y, double radius)
        {
            int best = -1;
            double bestD = radius;
            foreach (LineData l in Track.Lines.Values)
            {
                double d = MathUtil.DistToSegment(x, y, l.X1, l.Y1, l.X2, l.Y2);
                if (d < bestD)
                {
                    bestD = d;
                    best = l.Id;
                }
            }
            return best;
        }

        private void Commit()
        {
            Command c = _current;
            if (c != null && !c.Empty)
            {
                _undo.Add(c);
                if (_undo.Count > 200) _undo.RemoveAt(0);
                _redo.Clear();
            }
            _current = null;
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            Command cmd = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            foreach (LineData l in cmd.Added) Track.RemoveLine(l.Id);
            foreach (LineData l in cmd.Removed) Track.AddLine(l);
            foreach (ObjectData o in cmd.AddedObjects) Track.RemoveObject(o.Id);
            foreach (ObjectData o in cmd.RemovedObjects) Track.AddObject(o.Def, o.Id);
            foreach (PropData o in cmd.AddedProps) Track.RemoveProp(o.Id);
            foreach (PropData o in cmd.RemovedProps) Track.AddProp(o.Def, o.Id);
            _redo.Add(cmd);
            OnEdit?.Invoke();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            Command cmd = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            foreach (LineData l in cmd.Removed) Track.RemoveLine(l.Id);
            foreach (LineData l in cmd.Added) Track.AddLine(l);
            foreach (ObjectData o in cmd.RemovedObjects) Track.RemoveObject(o.Id);
            foreach (ObjectData o in cmd.AddedObjects) Track.AddObject(o.Def, o.Id);
            foreach (PropData o in cmd.RemovedProps) Track.RemoveProp(o.Id);
            foreach (PropData o in cmd.AddedProps) Track.AddProp(o.Def, o.Id);
            _undo.Add(cmd);
            OnEdit?.Invoke();
        }

        public void ClearHistory()
        {
            _undo.Clear();
            _redo.Clear();
            _current = null;
        }

        /// <summary>Remove every player-drawn line and placed object (used by "clear my track").</summary>
        public void ClearPlayerLines()
        {
            var cmd = new Command();
            foreach (LineData l in new List<LineData>(Track.Lines.Values))
            {
                if (l.Layer != LineLayer.Player) continue;
                if (Constraints.Player > 0 && l.Player != Constraints.Player) continue;
                Track.RemoveLine(l.Id);
                cmd.Removed.Add(l.Clone());
            }
            if (Constraints.CanPlaceObjects)
            {
                foreach (ObjectData o in new List<ObjectData>(Track.Objects.Values))
                {
                    Track.RemoveObject(o.Id);
                    cmd.RemovedObjects.Add(o);
                }
                foreach (PropData o in new List<PropData>(Track.Props.Values))
                {
                    Track.RemoveProp(o.Id);
                    cmd.RemovedProps.Add(o);
                }
            }
            if (!cmd.Empty)
            {
                _undo.Add(cmd);
                _redo.Clear();
                OnEdit?.Invoke();
            }
        }
    }
}
