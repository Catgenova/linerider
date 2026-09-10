using System;
using System.Collections.Generic;
using System.Text;

namespace NeonLineRider.Core
{
    /// <summary>Key/value persistence supplied by the host (PlayerPrefs, files, memory).</summary>
    public interface IStorage
    {
        string Load(string key);
        void Save(string key, string value);
    }

    public sealed class MemoryStorage : IStorage
    {
        private readonly Dictionary<string, string> _data = new Dictionary<string, string>();

        public string Load(string key) => _data.TryGetValue(key, out var v) ? v : null;

        public void Save(string key, string value) => _data[key] = value;
    }

    public sealed class TrackRecords
    {
        public int? BestFrames;
        public double? BestInk;
        public int BestTrick;
    }

    public sealed class PublishedTrack
    {
        public string Id;
        public string Title;
        public string Author;
        public string Description;
        public List<string> Tags = new List<string>();
        public int Difficulty = 2;
        public string Environment = "mountain";
        public double? Budget;
        public string Thumbnail;
        public long Created;
        public int Likes;
        public int Plays;
        public bool Liked;
        public TrackRecords Records = new TrackRecords();
        public Dictionary<string, object> TrackJson;
        public bool Builtin;

        public Dictionary<string, object> ToJson()
        {
            var tags = new List<object>();
            foreach (string t in Tags) tags.Add(t);
            var o = new Dictionary<string, object>
            {
                ["id"] = Id,
                ["title"] = Title,
                ["author"] = Author,
                ["description"] = Description,
                ["tags"] = tags,
                ["difficulty"] = (double)Difficulty,
                ["environment"] = Environment,
                ["budget"] = Budget.HasValue ? (object)Budget.Value : null,
                ["thumbnail"] = Thumbnail ?? "",
                ["created"] = (double)Created,
                ["likes"] = (double)Likes,
                ["plays"] = (double)Plays,
                ["liked"] = Liked,
                ["records"] = new Dictionary<string, object>
                {
                    ["bestFrames"] = Records.BestFrames.HasValue ? (object)(double)Records.BestFrames.Value : null,
                    ["bestInk"] = Records.BestInk.HasValue ? (object)Records.BestInk.Value : null,
                    ["bestTrick"] = (double)Records.BestTrick,
                },
                ["track"] = TrackJson,
            };
            if (Builtin) o["builtin"] = true;
            return o;
        }

        public static PublishedTrack FromJson(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var t = new PublishedTrack
            {
                Id = Json.Str(o, "id"),
                Title = Json.Str(o, "title", "Untitled"),
                Author = Json.Str(o, "author", "Anonymous"),
                Description = Json.Str(o, "description", ""),
                Difficulty = Json.Int(o, "difficulty", 2),
                Environment = Json.Str(o, "environment", "mountain"),
                Budget = Json.Has(o, "budget") ? Json.Num(o, "budget") : (double?)null,
                Thumbnail = Json.Str(o, "thumbnail"),
                Created = (long)Json.Num(o, "created"),
                Likes = Json.Int(o, "likes"),
                Plays = Json.Int(o, "plays"),
                Liked = Json.Bool(o, "liked"),
                TrackJson = Json.Obj(Json.Get(o, "track")),
                Builtin = Json.Bool(o, "builtin"),
            };
            var tags = Json.List(Json.Get(o, "tags"));
            if (tags != null) foreach (object tag in tags) t.Tags.Add(Json.Str(tag, ""));
            var rec = Json.Obj(Json.Get(o, "records"));
            if (rec != null)
            {
                t.Records.BestFrames = Json.Has(rec, "bestFrames") ? Json.Int(rec, "bestFrames") : (int?)null;
                t.Records.BestInk = Json.Has(rec, "bestInk") ? Json.Num(rec, "bestInk") : (double?)null;
                t.Records.BestTrick = Json.Int(rec, "bestTrick");
            }
            return t;
        }

        public Track LoadTrack() => Track.FromJson(TrackJson);
    }

    /// <summary>
    /// Track sharing store backed by IStorage; tracks travel between players through share codes. A
    /// server-backed implementation can replace it for global leaderboards.
    /// </summary>
    public sealed class LocalTrackStore
    {
        private const string Key = "neon-linerider-library-v1";
        private readonly IStorage _storage;
        private readonly List<PublishedTrack> _builtins;
        private List<PublishedTrack> _items;

        public LocalTrackStore(IStorage storage, List<PublishedTrack> builtins)
        {
            _storage = storage;
            _builtins = builtins ?? new List<PublishedTrack>();
            _items = Load();
        }

        private List<PublishedTrack> Load()
        {
            var list = new List<PublishedTrack>();
            try
            {
                string raw = _storage.Load(Key);
                if (string.IsNullOrEmpty(raw)) return list;
                var arr = Json.List(Json.Parse(raw));
                if (arr == null) return list;
                foreach (object item in arr)
                {
                    var t = PublishedTrack.FromJson(Json.Obj(item));
                    if (t != null) list.Add(t);
                }
            }
            catch
            {
                // Corrupt store: start fresh rather than crash.
            }
            return list;
        }

        private void Save()
        {
            var arr = new List<object>();
            foreach (var t in _items) arr.Add(t.ToJson());
            _storage.Save(Key, Json.Stringify(arr));
        }

        public List<PublishedTrack> List()
        {
            var all = new List<PublishedTrack>(_items);
            foreach (var b in _builtins)
            {
                bool present = false;
                foreach (var i in _items) if (i.Id == b.Id) present = true;
                if (!present) all.Add(b);
            }
            all.Sort((a, b) => b.Created.CompareTo(a.Created));
            return all;
        }

        public PublishedTrack Get(string id)
        {
            foreach (var t in _items) if (t.Id == id) return t;
            foreach (var t in _builtins) if (t.Id == id) return t;
            return null;
        }

        public PublishedTrack Publish(PublishedTrack input)
        {
            input.Id = "t_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x") + "_" + Guid.NewGuid().ToString("N").Substring(0, 5);
            input.Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            input.Likes = 0;
            input.Plays = 0;
            input.Liked = false;
            input.Records = new TrackRecords();
            _items.Insert(0, input);
            Save();
            return input;
        }

        public void Update(PublishedTrack track)
        {
            int i = _items.FindIndex(t => t.Id == track.Id);
            if (i >= 0) _items[i] = track;
            else _items.Insert(0, track);
            Save();
        }

        public void Remove(string id)
        {
            _items.RemoveAll(t => t.Id == id);
            Save();
        }
    }

    public static class ShareCodes
    {
        /// <summary>Encode a published track as a shareable string (NLR1. + base64 JSON), compatible with the web build.</summary>
        public static string Encode(PublishedTrack track)
        {
            var tags = new List<object>();
            foreach (string t in track.Tags) tags.Add(t);
            var payload = new Dictionary<string, object>
            {
                ["t"] = track.Title,
                ["a"] = track.Author,
                ["d"] = track.Description,
                ["g"] = tags,
                ["f"] = (double)track.Difficulty,
                ["e"] = track.Environment,
                ["b"] = track.Budget.HasValue ? (object)track.Budget.Value : null,
                ["k"] = track.TrackJson,
            };
            string json = Json.Stringify(payload);
            return "NLR1." + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public static PublishedTrack Decode(string code)
        {
            try
            {
                string trimmed = code.Trim();
                if (!trimmed.StartsWith("NLR1.")) return null;
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(trimmed.Substring(5)));
                var payload = Json.Obj(Json.Parse(json));
                var track = Json.Obj(Json.Get(payload, "k"));
                if (track == null || Json.List(Json.Get(track, "lines")) == null) return null;
                var t = new PublishedTrack
                {
                    Title = Json.Str(payload, "t", "Untitled"),
                    Author = Json.Str(payload, "a", "Anonymous"),
                    Description = Json.Str(payload, "d", ""),
                    Difficulty = Json.Int(payload, "f", 2),
                    Environment = Json.Str(payload, "e", "mountain"),
                    Budget = Json.Has(payload, "b") ? Json.Num(payload, "b") : (double?)null,
                    TrackJson = track,
                };
                var tags = Json.List(Json.Get(payload, "g"));
                if (tags != null) foreach (object tag in tags) t.Tags.Add(Json.Str(tag, ""));
                return t;
            }
            catch
            {
                return null;
            }
        }
    }
}
