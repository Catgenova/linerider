using System;
using System.Collections.Generic;
using System.Globalization;

namespace NeonLineRider.Core
{
    public enum LineLayer
    {
        Level,
        Player,
    }

    public sealed class LineData
    {
        public int Id;
        public double X1, Y1, X2, Y2;
        public MaterialId Material;
        public bool Flipped;
        public bool LeftExt;
        public bool RightExt;
        public double Multiplier = 1;
        public LineLayer Layer = LineLayer.Player;
        public int Player;

        public LineData Clone() => (LineData)MemberwiseClone();

        public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));

        /// <summary>Ink cost in metres.</summary>
        public double Cost => (Length / Constants.PxPerMeter) * Materials.Get(Material).Cost;
    }

    public sealed class ObjectData
    {
        public int Id;
        public EntityDef Def;
    }

    public sealed class PropData
    {
        public int Id;
        public PropDef Def;
    }

    public sealed class TrackChange
    {
        /// <summary>"add", "remove", "update" or "objects".</summary>
        public string Type;
        public LineData Line;
        public List<int> Touched = new List<int>();
    }

    /// <summary>
    /// Editable track: lines plus start/finish plus placed objects. Keeps an endpoint index so joined
    /// lines get their collision extensions, and tracks ink spend for the player-drawn layer.
    /// </summary>
    public sealed class Track
    {
        public readonly Dictionary<int, LineData> Lines = new Dictionary<int, LineData>();
        public readonly Dictionary<int, ObjectData> Objects = new Dictionary<int, ObjectData>();
        public readonly Dictionary<int, PropData> Props = new Dictionary<int, PropData>();
        private readonly Dictionary<string, HashSet<int>> _endpoints = new Dictionary<string, HashSet<int>>();
        public int NextId = 1;
        public Vec2d Start;
        public bool HasStartVelocity;
        public Vec2d StartVelocity;
        public Zone? Finish;
        /// <summary>Bumped on every change so caches can invalidate.</summary>
        public int Revision;
        public Action<TrackChange> OnChange;

        private static string KeyOf(double x, double y)
        {
            double kx = Math.Floor(x * 100 + 0.5);
            double ky = Math.Floor(y * 100 + 0.5);
            return kx.ToString(CultureInfo.InvariantCulture) + "," + ky.ToString(CultureInfo.InvariantCulture);
        }

        public LineData AddLine(double x1, double y1, double x2, double y2, MaterialId material = MaterialId.Normal, bool flipped = false, double multiplier = 1, LineLayer layer = LineLayer.Player, int player = 0, int? id = null)
        {
            int lineId = id ?? NextId++;
            if (lineId >= NextId) NextId = lineId + 1;
            var line = new LineData
            {
                Id = lineId,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Material = material,
                Flipped = flipped,
                Multiplier = multiplier,
                Layer = layer,
                Player = player,
            };
            Lines[line.Id] = line;
            var touched = new HashSet<int>();
            IndexEndpoint(line.X1, line.Y1, line.Id, touched);
            IndexEndpoint(line.X2, line.Y2, line.Id, touched);
            RefreshExtensions(line);
            foreach (int other in touched) if (other != line.Id) RefreshExtensions(Lines[other]);
            Revision++;
            if (OnChange != null)
            {
                var change = new TrackChange { Type = "add", Line = line };
                foreach (int t in touched) if (t != line.Id) change.Touched.Add(t);
                OnChange(change);
            }
            return line;
        }

        public LineData AddLine(LineData data)
        {
            return AddLine(data.X1, data.Y1, data.X2, data.Y2, data.Material, data.Flipped, data.Multiplier, data.Layer, data.Player, data.Id);
        }

        public LineData RemoveLine(int id)
        {
            if (!Lines.TryGetValue(id, out var line)) return null;
            Lines.Remove(id);
            var touched = new HashSet<int>();
            UnindexEndpoint(line.X1, line.Y1, id, touched);
            UnindexEndpoint(line.X2, line.Y2, id, touched);
            foreach (int other in touched) RefreshExtensions(Lines[other]);
            Revision++;
            if (OnChange != null)
            {
                var change = new TrackChange { Type = "remove", Line = line };
                change.Touched.AddRange(touched);
                OnChange(change);
            }
            return line;
        }

        public void FlipLine(int id)
        {
            if (!Lines.TryGetValue(id, out var line)) return;
            line.Flipped = !line.Flipped;
            Revision++;
            OnChange?.Invoke(new TrackChange { Type = "update", Line = line });
        }

        public void SetMultiplier(int id, double multiplier)
        {
            if (!Lines.TryGetValue(id, out var line)) return;
            line.Multiplier = multiplier;
            Revision++;
            OnChange?.Invoke(new TrackChange { Type = "update", Line = line });
        }

        public ObjectData AddObject(EntityDef def, int? id = null)
        {
            int objId = id ?? NextId++;
            if (objId >= NextId) NextId = objId + 1;
            var data = new ObjectData { Id = objId, Def = def.Clone() };
            Objects[objId] = data;
            Revision++;
            OnChange?.Invoke(new TrackChange { Type = "objects" });
            return data;
        }

        public ObjectData RemoveObject(int id)
        {
            if (!Objects.TryGetValue(id, out var data)) return null;
            Objects.Remove(id);
            Revision++;
            OnChange?.Invoke(new TrackChange { Type = "objects" });
            return data;
        }

        public PropData AddProp(PropDef def, int? id = null)
        {
            int propId = id ?? NextId++;
            if (propId >= NextId) NextId = propId + 1;
            var data = new PropData { Id = propId, Def = def.Clone() };
            Props[propId] = data;
            Revision++;
            OnChange?.Invoke(new TrackChange { Type = "objects" });
            return data;
        }

        public PropData RemoveProp(int id)
        {
            if (!Props.TryGetValue(id, out var data)) return null;
            Props.Remove(id);
            Revision++;
            OnChange?.Invoke(new TrackChange { Type = "objects" });
            return data;
        }

        public void Clear()
        {
            foreach (int id in new List<int>(Lines.Keys)) RemoveLine(id);
            foreach (int id in new List<int>(Objects.Keys)) RemoveObject(id);
            foreach (int id in new List<int>(Props.Keys)) RemoveProp(id);
        }

        /// <summary>Metres of ink spent on player lines (optionally for one co-op player).</summary>
        public double InkUsed(int? player = null)
        {
            double total = 0;
            foreach (LineData l in Lines.Values)
            {
                if (l.Layer != LineLayer.Player) continue;
                if (player.HasValue && l.Player != player.Value) continue;
                total += l.Cost;
            }
            return total;
        }

        /// <summary>Nearest endpoint of any line within radius of (x,y).</summary>
        public bool SnapPoint(double x, double y, double radius, out Vec2d snapped, int ignoreId = -1)
        {
            snapped = new Vec2d(x, y);
            bool found = false;
            double bestD = radius;
            foreach (LineData l in Lines.Values)
            {
                if (l.Id == ignoreId) continue;
                double d1 = Math.Sqrt((l.X1 - x) * (l.X1 - x) + (l.Y1 - y) * (l.Y1 - y));
                if (d1 < bestD)
                {
                    bestD = d1;
                    snapped = new Vec2d(l.X1, l.Y1);
                    found = true;
                }
                double d2 = Math.Sqrt((l.X2 - x) * (l.X2 - x) + (l.Y2 - y) * (l.Y2 - y));
                if (d2 < bestD)
                {
                    bestD = d2;
                    snapped = new Vec2d(l.X2, l.Y2);
                    found = true;
                }
            }
            return found;
        }

        public bool Bounds(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = minY = double.PositiveInfinity;
            maxX = maxY = double.NegativeInfinity;
            if (Lines.Count == 0) return false;
            foreach (LineData l in Lines.Values)
            {
                minX = Math.Min(minX, Math.Min(l.X1, l.X2));
                maxX = Math.Max(maxX, Math.Max(l.X1, l.X2));
                minY = Math.Min(minY, Math.Min(l.Y1, l.Y2));
                maxY = Math.Max(maxY, Math.Max(l.Y1, l.Y2));
            }
            return true;
        }

        // ------------------------------------------------------------------ JSON

        public Dictionary<string, object> ToJson()
        {
            var o = new Dictionary<string, object>
            {
                ["version"] = 1.0,
                ["start"] = new Dictionary<string, object> { ["x"] = Start.X, ["y"] = Start.Y },
            };
            if (HasStartVelocity) o["startVelocity"] = new Dictionary<string, object> { ["x"] = StartVelocity.X, ["y"] = StartVelocity.Y };
            if (Finish.HasValue)
            {
                Zone z = Finish.Value;
                o["finish"] = new Dictionary<string, object> { ["x"] = z.X, ["y"] = z.Y, ["w"] = z.W, ["h"] = z.H };
            }
            var lines = new List<object>();
            foreach (LineData l in SortedLines())
            {
                lines.Add(new Dictionary<string, object>
                {
                    ["id"] = (double)l.Id,
                    ["x1"] = l.X1,
                    ["y1"] = l.Y1,
                    ["x2"] = l.X2,
                    ["y2"] = l.Y2,
                    ["material"] = Materials.KeyOf(l.Material),
                    ["flipped"] = l.Flipped,
                    ["leftExt"] = l.LeftExt,
                    ["rightExt"] = l.RightExt,
                    ["multiplier"] = l.Multiplier,
                    ["layer"] = l.Layer == LineLayer.Level ? "level" : "player",
                    ["player"] = (double)l.Player,
                });
            }
            o["lines"] = lines;
            var objects = new List<object>();
            foreach (ObjectData od in Objects.Values)
            {
                objects.Add(new Dictionary<string, object> { ["id"] = (double)od.Id, ["def"] = EntityDefJson.ToJson(od.Def) });
            }
            o["objects"] = objects;
            var props = new List<object>();
            foreach (PropData pd in Props.Values)
            {
                props.Add(new Dictionary<string, object> { ["id"] = (double)pd.Id, ["def"] = PropDefJson.ToJson(pd.Def) });
            }
            o["props"] = props;
            o["nextId"] = (double)NextId;
            return o;
        }

        public List<LineData> SortedLines()
        {
            var list = new List<LineData>(Lines.Values);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        public static Track FromJson(Dictionary<string, object> json)
        {
            var t = new Track();
            var start = Json.Obj(Json.Get(json, "start"));
            t.Start = new Vec2d(Json.Num(start, "x"), Json.Num(start, "y"));
            var sv = Json.Obj(Json.Get(json, "startVelocity"));
            if (sv != null)
            {
                t.HasStartVelocity = true;
                t.StartVelocity = new Vec2d(Json.Num(sv, "x"), Json.Num(sv, "y"));
            }
            var fz = Json.Obj(Json.Get(json, "finish"));
            if (fz != null) t.Finish = new Zone(Json.Num(fz, "x"), Json.Num(fz, "y"), Json.Num(fz, "w"), Json.Num(fz, "h"));
            var lines = Json.List(Json.Get(json, "lines"));
            if (lines != null)
            {
                foreach (object item in lines)
                {
                    var l = Json.Obj(item);
                    if (l == null) continue;
                    t.AddLine(
                        Json.Num(l, "x1"), Json.Num(l, "y1"), Json.Num(l, "x2"), Json.Num(l, "y2"),
                        Materials.ParseId(Json.Str(l, "material", "normal")),
                        Json.Bool(l, "flipped"),
                        Json.Num(l, "multiplier", 1),
                        Json.Str(l, "layer", "player") == "level" ? LineLayer.Level : LineLayer.Player,
                        Json.Int(l, "player"),
                        Json.Has(l, "id") ? Json.Int(l, "id") : (int?)null);
                }
            }
            var objects = Json.List(Json.Get(json, "objects"));
            if (objects != null)
            {
                foreach (object item in objects)
                {
                    var o = Json.Obj(item);
                    var def = EntityDefJson.FromJson(Json.Obj(Json.Get(o, "def")));
                    if (def != null) t.AddObject(def, Json.Has(o, "id") ? Json.Int(o, "id") : (int?)null);
                }
            }
            var props = Json.List(Json.Get(json, "props"));
            if (props != null)
            {
                foreach (object item in props)
                {
                    var o = Json.Obj(item);
                    var def = PropDefJson.FromJson(Json.Obj(Json.Get(o, "def")));
                    if (def != null) t.AddProp(def, Json.Has(o, "id") ? Json.Int(o, "id") : (int?)null);
                }
            }
            t.NextId = Math.Max(t.NextId, Json.Int(json, "nextId", 1));
            return t;
        }

        public Track Clone() => FromJson(ToJson());

        // ------------------------------------------------------------------ endpoints

        private void IndexEndpoint(double x, double y, int id, HashSet<int> touched)
        {
            string k = KeyOf(x, y);
            if (!_endpoints.TryGetValue(k, out var set))
            {
                set = new HashSet<int>();
                _endpoints[k] = set;
            }
            foreach (int other in set) touched.Add(other);
            set.Add(id);
        }

        private void UnindexEndpoint(double x, double y, int id, HashSet<int> touched)
        {
            string k = KeyOf(x, y);
            if (!_endpoints.TryGetValue(k, out var set)) return;
            set.Remove(id);
            foreach (int other in set) touched.Add(other);
            if (set.Count == 0) _endpoints.Remove(k);
        }

        /// <summary>
        /// A joint extends a line's collision past its endpoint only when another line continues on
        /// from that point in roughly the same direction (sharp corners get no extension).
        /// </summary>
        private void RefreshExtensions(LineData line)
        {
            line.LeftExt = HasContinuation(line, line.X1, line.Y1, line.X1 - line.X2, line.Y1 - line.Y2);
            line.RightExt = HasContinuation(line, line.X2, line.Y2, line.X2 - line.X1, line.Y2 - line.Y1);
        }

        private bool HasContinuation(LineData line, double px, double py, double outX, double outY)
        {
            if (!_endpoints.TryGetValue(KeyOf(px, py), out var set) || set.Count < 2) return false;
            double outLen = Math.Sqrt(outX * outX + outY * outY);
            if (outLen == 0) outLen = 1;
            string pk = KeyOf(px, py);
            foreach (int id in set)
            {
                if (id == line.Id) continue;
                if (!Lines.TryGetValue(id, out var other)) continue;
                bool atStart = KeyOf(other.X1, other.Y1) == pk;
                double dx = atStart ? other.X2 - other.X1 : other.X1 - other.X2;
                double dy = atStart ? other.Y2 - other.Y1 : other.Y1 - other.Y2;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len == 0) len = 1;
                double cos = (dx * outX + dy * outY) / (len * outLen);
                if (cos > 0.5) return true;
            }
            return false;
        }
    }

    /// <summary>JSON mapping for entity definitions (keys match the web build).</summary>
    public static class EntityDefJson
    {
        public static Dictionary<string, object> ToJson(EntityDef d)
        {
            var o = new Dictionary<string, object> { ["type"] = d.Type };
            switch (d.Type)
            {
                case "platform":
                    o["x"] = d.X; o["y"] = d.Y; o["w"] = d.W; o["dx"] = d.Dx; o["dy"] = d.Dy; o["period"] = d.Period; o["phase"] = d.Phase;
                    if (d.Material != null) o["material"] = d.Material;
                    o["thickness"] = d.Thickness;
                    break;
                case "gear":
                    o["x"] = d.X; o["y"] = d.Y; o["radius"] = d.Radius; o["teeth"] = (double)d.Teeth; o["speed"] = d.Speed;
                    break;
                case "pendulum":
                    o["x"] = d.X; o["y"] = d.Y; o["length"] = d.Length; o["width"] = d.Width; o["amplitude"] = d.Amplitude; o["period"] = d.Period; o["phase"] = d.Phase;
                    break;
                case "fan":
                    o["x"] = d.X; o["y"] = d.Y; o["w"] = d.W; o["h"] = d.H; o["fx"] = d.Fx; o["fy"] = d.Fy;
                    break;
                case "magnet":
                    o["x"] = d.X; o["y"] = d.Y; o["radius"] = d.Radius; o["strength"] = d.Strength;
                    break;
                case "cannon":
                    o["x"] = d.X; o["y"] = d.Y; o["angle"] = d.Angle; o["power"] = d.Power;
                    break;
                case "wall":
                    o["x1"] = d.X1; o["y1"] = d.Y1; o["x2"] = d.X2; o["y2"] = d.Y2; o["threshold"] = d.Threshold;
                    break;
                case "balloon":
                    o["x"] = d.X; o["y"] = d.Y; o["lift"] = d.Lift; o["duration"] = (double)d.Duration;
                    break;
                case "seesaw":
                    o["x"] = d.X; o["y"] = d.Y; o["length"] = d.Length; o["maxAngle"] = d.MaxAngle;
                    break;
                case "train":
                    o["x"] = d.X; o["y"] = d.Y; o["w"] = d.W; o["h"] = d.H; o["speed"] = d.Speed; o["range"] = d.Range;
                    break;
                case "rockfall":
                    o["x"] = d.X; o["y"] = d.Y; o["count"] = (double)d.Count; o["spread"] = d.Spread; o["trigger"] = d.Trigger; o["radius"] = d.Radius;
                    break;
                case "bridge":
                    o["x"] = d.X; o["y"] = d.Y; o["w"] = d.W; o["segments"] = (double)d.Segments; o["delay"] = (double)d.Delay;
                    break;
                case "collapse":
                    o["x1"] = d.X1; o["y1"] = d.Y1; o["x2"] = d.X2; o["y2"] = d.Y2; o["at"] = (double)d.At;
                    break;
                case "avalanche":
                    o["startX"] = d.StartX; o["speed"] = d.Speed; o["delay"] = (double)d.Delay; o["accel"] = d.Accel;
                    if (d.Label != null) o["label"] = d.Label;
                    break;
                case "snowball":
                    o["x"] = d.X; o["y"] = d.Y; o["radius"] = d.Radius; o["trigger"] = d.Trigger; o["push"] = d.Push;
                    break;
                case "spring":
                    o["x"] = d.X; o["y"] = d.Y; o["w"] = d.W; o["angle"] = d.Angle;
                    break;
                case "ramp":
                    o["x"] = d.X; o["y"] = d.Y; o["w"] = d.W; o["h"] = d.H;
                    break;
            }
            return o;
        }

        public static EntityDef FromJson(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var d = new EntityDef { Type = Json.Str(o, "type", "platform") };
            d.X = Json.Num(o, "x");
            d.Y = Json.Num(o, "y");
            d.W = Json.Num(o, "w");
            d.H = Json.Num(o, "h");
            d.Dx = Json.Num(o, "dx");
            d.Dy = Json.Num(o, "dy");
            d.Period = Json.Num(o, "period", 100);
            d.Phase = Json.Num(o, "phase");
            d.Material = Json.Str(o, "material");
            d.Thickness = Json.Num(o, "thickness", 3);
            d.Radius = Json.Num(o, "radius");
            d.Teeth = Json.Int(o, "teeth");
            d.Speed = Json.Num(o, "speed");
            d.Length = Json.Num(o, "length");
            d.Width = Json.Num(o, "width");
            d.Amplitude = Json.Num(o, "amplitude");
            d.Fx = Json.Num(o, "fx");
            d.Fy = Json.Num(o, "fy");
            d.Strength = Json.Num(o, "strength");
            d.Angle = Json.Num(o, "angle");
            d.Power = Json.Num(o, "power");
            d.X1 = Json.Num(o, "x1");
            d.Y1 = Json.Num(o, "y1");
            d.X2 = Json.Num(o, "x2");
            d.Y2 = Json.Num(o, "y2");
            d.Threshold = Json.Num(o, "threshold");
            d.Lift = Json.Num(o, "lift");
            d.Duration = Json.Int(o, "duration");
            d.MaxAngle = Json.Num(o, "maxAngle", 0.45);
            d.Range = Json.Num(o, "range");
            d.Count = Json.Int(o, "count");
            d.Spread = Json.Num(o, "spread");
            d.Trigger = Json.Num(o, "trigger");
            d.Segments = Json.Int(o, "segments");
            d.Delay = Json.Int(o, "delay");
            d.At = Json.Int(o, "at");
            d.StartX = Json.Num(o, "startX");
            d.Accel = Json.Num(o, "accel");
            d.Push = Json.Num(o, "push");
            d.Label = Json.Str(o, "label");
            return d;
        }
    }

    public static class PropDefJson
    {
        public static Dictionary<string, object> ToJson(PropDef d)
        {
            var o = new Dictionary<string, object> { ["kind"] = d.Kind, ["x"] = d.X, ["y"] = d.Y, ["mass"] = d.Mass };
            if (d.Radius > 0) o["radius"] = d.Radius;
            else
            {
                o["width"] = d.Width;
                o["height"] = d.Height;
            }
            if (d.Angle != 0) o["angle"] = d.Angle;
            if (d.Friction >= 0) o["friction"] = d.Friction;
            if (d.GravityScale != 1) o["gravityScale"] = d.GravityScale;
            if (d.Explosive) o["explosive"] = true;
            if (d.Dormant) o["dormant"] = true;
            return o;
        }

        public static PropDef FromJson(Dictionary<string, object> o)
        {
            if (o == null) return null;
            return new PropDef
            {
                Kind = Json.Str(o, "kind", "crate"),
                X = Json.Num(o, "x"),
                Y = Json.Num(o, "y"),
                Width = Json.Num(o, "width", 10),
                Height = Json.Num(o, "height", 10),
                Angle = Json.Num(o, "angle"),
                Radius = Json.Num(o, "radius"),
                Friction = Json.Has(o, "friction") ? Json.Num(o, "friction") : -1,
                Mass = Json.Num(o, "mass", 1),
                GravityScale = Json.Num(o, "gravityScale", 1),
                Explosive = Json.Bool(o, "explosive"),
                Dormant = Json.Bool(o, "dormant"),
            };
        }
    }
}
