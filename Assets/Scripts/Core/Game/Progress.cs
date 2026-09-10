using System.Collections.Generic;

namespace NeonLineRider.Core
{
    public sealed class GhostRecord
    {
        public Dictionary<string, object> TrackJson;
        public string Rider;
        public int Frames;
    }

    public sealed class LevelRecord
    {
        public Medal Medal = Medal.None;
        public int? BestFrames;
        public double? BestInk;
        public int BestTrick;
        public int Attempts;
        public GhostRecord Ghost;
    }

    public sealed class DailyRecord
    {
        public int? Time;
        public double? Ink;
        public int Trick;
    }

    public sealed class Improvements
    {
        public bool NewMedal, NewTime, NewInk, NewTrick;
    }

    /// <summary>Campaign progress, records, ghosts and settings, persisted as JSON through IStorage.</summary>
    public sealed class Progress
    {
        private const string Key = "neon-linerider-save-v1";
        public readonly Dictionary<string, LevelRecord> Levels = new Dictionary<string, LevelRecord>();
        public int ArcadeBest;
        public readonly Dictionary<string, DailyRecord> Daily = new Dictionary<string, DailyRecord>();
        public string RiderSetting = "bosh";
        public bool ShowTicks = true;
        public bool Music = true;
        private readonly IStorage _storage;

        public Progress(IStorage storage)
        {
            _storage = storage;
            Load();
        }

        private void Load()
        {
            try
            {
                string raw = _storage.Load(Key);
                if (string.IsNullOrEmpty(raw)) return;
                var o = Json.Obj(Json.Parse(raw));
                if (o == null || Json.Int(o, "version", 1) != 1) return;
                var levels = Json.Obj(Json.Get(o, "levels"));
                if (levels != null)
                {
                    foreach (var kv in levels)
                    {
                        var r = Json.Obj(kv.Value);
                        if (r == null) continue;
                        var rec = new LevelRecord
                        {
                            Medal = Objectives.ParseMedal(Json.Str(r, "medal", "none")),
                            BestFrames = Json.Has(r, "bestFrames") ? Json.Int(r, "bestFrames") : (int?)null,
                            BestInk = Json.Has(r, "bestInk") ? Json.Num(r, "bestInk") : (double?)null,
                            BestTrick = Json.Int(r, "bestTrick"),
                            Attempts = Json.Int(r, "attempts"),
                        };
                        var g = Json.Obj(Json.Get(r, "ghost"));
                        if (g != null)
                        {
                            rec.Ghost = new GhostRecord { TrackJson = Json.Obj(Json.Get(g, "track")), Rider = Json.Str(g, "rider", "bosh"), Frames = Json.Int(g, "frames") };
                        }
                        Levels[kv.Key] = rec;
                    }
                }
                ArcadeBest = Json.Int(o, "arcadeBest");
                var daily = Json.Obj(Json.Get(o, "daily"));
                if (daily != null)
                {
                    foreach (var kv in daily)
                    {
                        var d = Json.Obj(kv.Value);
                        if (d == null) continue;
                        Daily[kv.Key] = new DailyRecord
                        {
                            Time = Json.Has(d, "time") ? Json.Int(d, "time") : (int?)null,
                            Ink = Json.Has(d, "ink") ? Json.Num(d, "ink") : (double?)null,
                            Trick = Json.Int(d, "trick"),
                        };
                    }
                }
                var settings = Json.Obj(Json.Get(o, "settings"));
                if (settings != null)
                {
                    RiderSetting = Json.Str(settings, "rider", "bosh");
                    ShowTicks = Json.Bool(settings, "showTicks", true);
                    Music = Json.Bool(settings, "music", true);
                }
            }
            catch
            {
                // Corrupt save: keep defaults.
            }
        }

        public Dictionary<string, object> ToJson()
        {
            var levels = new Dictionary<string, object>();
            foreach (var kv in Levels)
            {
                LevelRecord r = kv.Value;
                var o = new Dictionary<string, object>
                {
                    ["medal"] = Objectives.MedalName(r.Medal),
                    ["bestFrames"] = r.BestFrames.HasValue ? (object)(double)r.BestFrames.Value : null,
                    ["bestInk"] = r.BestInk.HasValue ? (object)r.BestInk.Value : null,
                    ["bestTrick"] = (double)r.BestTrick,
                    ["attempts"] = (double)r.Attempts,
                };
                if (r.Ghost != null)
                {
                    o["ghost"] = new Dictionary<string, object> { ["track"] = r.Ghost.TrackJson, ["rider"] = r.Ghost.Rider, ["frames"] = (double)r.Ghost.Frames };
                }
                levels[kv.Key] = o;
            }
            var daily = new Dictionary<string, object>();
            foreach (var kv in Daily)
            {
                daily[kv.Key] = new Dictionary<string, object>
                {
                    ["time"] = kv.Value.Time.HasValue ? (object)(double)kv.Value.Time.Value : null,
                    ["ink"] = kv.Value.Ink.HasValue ? (object)kv.Value.Ink.Value : null,
                    ["trick"] = (double)kv.Value.Trick,
                };
            }
            return new Dictionary<string, object>
            {
                ["version"] = 1.0,
                ["levels"] = levels,
                ["arcadeBest"] = (double)ArcadeBest,
                ["daily"] = daily,
                ["settings"] = new Dictionary<string, object> { ["rider"] = RiderSetting, ["showTicks"] = ShowTicks, ["music"] = Music },
            };
        }

        public void Save()
        {
            try
            {
                _storage.Save(Key, Json.Stringify(ToJson()));
            }
            catch
            {
                // Storage unavailable.
            }
        }

        public LevelRecord Level(string id)
        {
            if (!Levels.TryGetValue(id, out var rec))
            {
                rec = new LevelRecord();
                Levels[id] = rec;
            }
            return rec;
        }

        public bool IsComplete(string id) => Objectives.Rank(Level(id).Medal) > 0;

        public void RecordAttempt(string id)
        {
            Level(id).Attempts++;
            Save();
        }

        /// <summary>Merge a finished run into the record. Returns which records improved.</summary>
        public Improvements RecordResult(string id, Medal medal, int? frames, double ink, int trick, GhostRecord ghost)
        {
            LevelRecord rec = Level(id);
            var o = new Improvements();
            if (Objectives.Rank(medal) > Objectives.Rank(rec.Medal))
            {
                rec.Medal = medal;
                o.NewMedal = true;
            }
            if (frames.HasValue && (!rec.BestFrames.HasValue || frames.Value < rec.BestFrames.Value))
            {
                rec.BestFrames = frames;
                o.NewTime = true;
                if (ghost != null) rec.Ghost = ghost;
            }
            if (frames.HasValue && (!rec.BestInk.HasValue || ink < rec.BestInk.Value))
            {
                rec.BestInk = ink;
                o.NewInk = true;
            }
            if (trick > rec.BestTrick)
            {
                rec.BestTrick = trick;
                o.NewTrick = true;
            }
            Save();
            return o;
        }

        public void TotalMedals(out int bronze, out int silver, out int gold)
        {
            bronze = silver = gold = 0;
            foreach (var rec in Levels.Values)
            {
                if (rec.Medal == Medal.Bronze) bronze++;
                else if (rec.Medal == Medal.Silver) silver++;
                else if (rec.Medal == Medal.Gold) gold++;
            }
        }

        public void Reset()
        {
            Levels.Clear();
            Daily.Clear();
            ArcadeBest = 0;
            RiderSetting = "bosh";
            ShowTicks = true;
            Music = true;
            Save();
        }
    }
}
