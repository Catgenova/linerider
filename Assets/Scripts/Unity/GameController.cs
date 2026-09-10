using System;
using System.Collections.Generic;
using CyberRider.Core;
using Env = CyberRider.Core.Environment;
using TrackEditor = CyberRider.Core.Editor;

namespace CyberRider.Unity
{
    public enum GameMode
    {
        Campaign,
        Free,
        Arcade,
        Daily,
        Coop,
        Library,
    }

    public enum PlayState
    {
        Edit,
        Play,
        Pause,
    }

    public sealed class ResultsInfo
    {
        public GameMode Mode;
        public LevelDef Level;
        public RunSummary Summary;
        public List<ObjectiveResult> Results = new List<ObjectiveResult>();
        public Medal Medal;
        public bool Complete;
        public Improvements Improvements;
        public int? ArcadeBest;
        public LevelDef Next;
    }

    public interface IGameAudio
    {
        bool Enabled { get; set; }
        bool Riding { set; }
        void Trick(int points);
        void Pickup();
        void Crash();
        void Finish();
        void Explosion();
    }

    public sealed class Marker
    {
        public double X, Y;
        /// <summary>"flag" or "rescue".</summary>
        public string Kind;
        public bool Done;
    }

    /// <summary>Top-level game state: current mode, level, track, editor and playback. Engine-agnostic apart from timing.</summary>
    public sealed class GameController
    {
        public readonly CameraModel Camera = new CameraModel();
        public readonly Effects Effects = new Effects();
        public readonly Progress Progress;
        public readonly LocalTrackStore Store;
        public Track Track = new Track();
        public TrackEditor Editor;
        public Run Run;
        public Run GhostRun;
        public bool GhostEnabled = true;
        public LevelDef Level;
        public GameMode Mode = GameMode.Free;
        public Env Environment = Environments.Get("mountain");
        public RiderDef RiderDef = Riders.Get("bosh");
        public PlayState PlayState = PlayState.Edit;
        public double Speed = 1;
        public int FlagFrame = -1;
        public double Time;
        public bool Following = true;
        public bool CoopActive;
        public int CoopPlayer = 1;
        public readonly double[] CoopBudgets = { 100, 100 };
        public ArcadeDirector Arcade;
        public string DailyKey;
        public PublishedTrack LibraryTrack;
        public IGameAudio Audio;
        public Action<ResultsInfo> OnResults;
        public Action OnStateChange;
        /// <summary>(text, colour hex, big)</summary>
        public Action<string, string, bool> OnMessage;
        public World PreviewWorld;
        public readonly List<Marker> Markers = new List<Marker>();
        private double _accumulator;
        private bool _resultsPending;
        private int _resultsTimer;
        private int _previewRevision = -1;
        private string _placement = "none";

        public GameController(IStorage storage)
        {
            Progress = new Progress(storage);
            Store = new LocalTrackStore(storage, BuiltinTracks.All);
            Editor = new TrackEditor(Track);
            BindEditorEvents();
            RiderDef saved = Riders.Get(Progress.RiderSetting);
            if (saved != null) RiderDef = saved;
        }

        private void BindEditorEvents()
        {
            Editor.OnInkExhausted = () => OnMessage?.Invoke("OUT OF INK", "#ff3d7f", false);
            Editor.OnEdit = () => OnStateChange?.Invoke();
            Editor.Zoom = Camera.Zoom;
            Track.OnChange = null;
            _previewRevision = -1;
        }

        // ---------------------------------------------------------------- mode setup

        private void ResetTrack(Track track)
        {
            Stop();
            Track = track;
            Editor = new TrackEditor(track);
            BindEditorEvents();
            FlagFrame = -1;
            Effects.Clear();
            GhostRun = null;
            LibraryTrack = null;
            DailyKey = null;
            Arcade = null;
            CoopActive = false;
            _placement = "none";
        }

        public void LoadLevel(LevelDef level, string riderId = null, bool coop = false)
        {
            ResetTrack(LevelLoader.TrackFromLevel(level));
            Level = level;
            Mode = coop ? GameMode.Coop : GameMode.Campaign;
            Environment = Environments.Get(level.Environment);
            if (level.Rider != null) RiderDef = Riders.Get(level.Rider);
            else if (riderId != null) SetRider(riderId, false);
            Editor.Constraints = new EditorConstraints
            {
                Budget = coop ? level.Budget * 0.6 : level.Budget,
                Materials = new List<MaterialId>(level.Materials),
                CanEraseLevel = false,
                Player = coop ? 1 : 0,
                Locked = false,
                CanPlaceObjects = false,
            };
            if (coop)
            {
                CoopActive = true;
                CoopPlayer = 1;
                CoopBudgets[0] = CoopBudgets[1] = level.Budget * 0.6;
            }
            Editor.Material = level.Materials.Count > 0 ? level.Materials[0] : MaterialId.Normal;
            Editor.Tool = ToolId.Pencil;
            Camera.Zoom = level.Zoom > 0 ? level.Zoom : 2;
            Editor.Zoom = Camera.Zoom;
            FocusLevelCamera();
            LoadGhost(level.Id);
            Progress.RecordAttempt(level.Id);
            OnStateChange?.Invoke();
        }

        /// <summary>Centre on the level's focus point while keeping the start clear of the toolbar.</summary>
        public void FocusLevelCamera()
        {
            LevelDef level = Level;
            if (level == null) return;
            Vec2d focus = level.Focus ?? level.Start;
            double maxCx = level.Start.X + (Camera.Width / 2.0 - 290) / Camera.Zoom;
            Camera.SnapTo(Math.Min(focus.X, maxCx), focus.Y);
        }

        private void LoadGhost(string levelId)
        {
            LevelRecord rec = Progress.Level(levelId);
            if (rec.Ghost == null || rec.Ghost.TrackJson == null) return;
            try
            {
                Track ghostTrack = Track.FromJson(rec.Ghost.TrackJson);
                RiderDef rider = Riders.Get(rec.Ghost.Rider);
                GhostRun = new Run(new RunOptions { Track = ghostTrack, Level = Level, RiderDef = rider, Environment = Environment });
            }
            catch
            {
                GhostRun = null;
            }
        }

        public void StartFree(string environment = "mountain")
        {
            var track = new Track { Start = new Vec2d(0, 0) };
            ResetTrack(track);
            Level = null;
            Mode = GameMode.Free;
            Environment = Environments.Get(environment);
            Editor.Constraints = new EditorConstraints { Budget = null, Materials = AllMaterials(), CanEraseLevel = true, Player = 0, Locked = false, CanPlaceObjects = true };
            Camera.Zoom = 2.5;
            Editor.Zoom = Camera.Zoom;
            Camera.SnapTo(60, 20);
            OnStateChange?.Invoke();
        }

        private static List<MaterialId> AllMaterials()
        {
            var list = new List<MaterialId>();
            foreach (var m in Materials.All) list.Add(m.Id);
            return list;
        }

        public void StartCoopFree(string environment = "rooftops", double budget = 120)
        {
            StartFree(environment);
            Mode = GameMode.Coop;
            CoopActive = true;
            CoopPlayer = 1;
            CoopBudgets[0] = CoopBudgets[1] = budget;
            Track.Finish = new Zone(700, 120, 40, 70);
            Track.AddLine(-30, 10, 60, 20, MaterialId.Normal, false, 1, LineLayer.Level);
            Track.AddLine(700, 190, 760, 190, MaterialId.Normal, false, 1, LineLayer.Level);
            Editor.Constraints = new EditorConstraints { Budget = budget, Materials = AllMaterials(), CanEraseLevel = false, Player = 1, Locked = false, CanPlaceObjects = false };
            OnStateChange?.Invoke();
        }

        public void StartArcade()
        {
            var track = new Track { Start = new Vec2d(0, 0) };
            ResetTrack(track);
            Level = null;
            Mode = GameMode.Arcade;
            Environment = Environments.Get("rooftops");
            Arcade = new ArcadeDirector(track, new Rng((uint)DateTime.UtcNow.Ticks));
            Editor.Constraints = new EditorConstraints { Budget = Arcade.Budget, Materials = new List<MaterialId> { MaterialId.Normal, MaterialId.Accel, MaterialId.Spring }, CanEraseLevel = false, Player = 0, Locked = false, CanPlaceObjects = false };
            Editor.Tool = ToolId.Pencil;
            Camera.Zoom = 2;
            Editor.Zoom = Camera.Zoom;
            Camera.SnapTo(120, 0);
            Play();
            OnStateChange?.Invoke();
        }

        public void StartDaily()
        {
            string key = Daily.TodayKey();
            LevelDef level = Daily.Generate(Daily.SeedFor(key), key);
            LoadLevel(level);
            Mode = GameMode.Daily;
            DailyKey = key;
            GhostRun = null;
            OnStateChange?.Invoke();
        }

        public void PlayLibrary(PublishedTrack pub)
        {
            Track track = pub.LoadTrack();
            ResetTrack(track);
            Level = null;
            Mode = GameMode.Library;
            LibraryTrack = pub;
            Environment = Environments.Get(pub.Environment);
            bool freeRide = !pub.Budget.HasValue;
            Editor.Constraints = new EditorConstraints { Budget = pub.Budget, Materials = AllMaterials(), CanEraseLevel = freeRide, Player = 0, Locked = false, CanPlaceObjects = freeRide };
            foreach (LineData l in track.Lines.Values) l.Layer = freeRide ? LineLayer.Player : LineLayer.Level;
            Camera.Zoom = 2;
            Editor.Zoom = Camera.Zoom;
            Camera.SnapTo(track.Start.X + 80, track.Start.Y);
            pub.Plays++;
            Store.Update(pub);
            OnStateChange?.Invoke();
        }

        public void SetRider(string id, bool restart = true)
        {
            RiderDef = Riders.Get(id);
            Progress.RiderSetting = id;
            Progress.Save();
            if (Run != null && restart) Restart(true);
            OnStateChange?.Invoke();
        }

        public bool RiderUnlocked(string id)
        {
            RiderDef def = Riders.Get(id);
            return def.Unlock == null || Progress.IsComplete(def.Unlock);
        }

        // ---------------------------------------------------------------- playback

        public void Play()
        {
            if (PlayState == PlayState.Play) return;
            if (Run == null) StartRun(FlagFrame > 0 ? FlagFrame : 0);
            PlayState = PlayState.Play;
            Following = true;
            OnStateChange?.Invoke();
        }

        private void StartRun(int skipFrames)
        {
            Run?.Dispose();
            Run = new Run(new RunOptions { Track = Track, Level = Level, RiderDef = RiderDef, Environment = Environment, SkipFrames = skipFrames, Endless = Mode == GameMode.Arcade });
            WatchObjectChanges();
            if (GhostRun != null)
            {
                Run g = GhostRun;
                GhostRun = new Run(new RunOptions { Track = g.Track, Level = Level, RiderDef = g.RiderDef, Environment = Environment, SkipFrames = skipFrames });
            }
            Effects.Clear();
            _resultsPending = false;
            _resultsTimer = 0;
            _accumulator = 0;
        }

        /// <summary>Objects cannot be hot-swapped into a running world, so placing one rewinds the run.</summary>
        private void WatchObjectChanges()
        {
            Action<TrackChange> prev = Track.OnChange;
            Track.OnChange = c =>
            {
                prev?.Invoke(c);
                if (c.Type == "objects" && Run != null) Stop();
            };
        }

        public void Pause()
        {
            if (PlayState != PlayState.Play) return;
            PlayState = PlayState.Pause;
            OnStateChange?.Invoke();
        }

        public void TogglePlay()
        {
            if (PlayState == PlayState.Play) Pause();
            else Play();
        }

        /// <summary>Stop playback and return to editing at frame zero.</summary>
        public void Stop()
        {
            Run?.Dispose();
            Run = null;
            PlayState = PlayState.Edit;
            _resultsPending = false;
            Effects.Clear();
            if (GhostRun != null)
            {
                Run g = GhostRun;
                GhostRun = new Run(new RunOptions { Track = g.Track, Level = Level, RiderDef = g.RiderDef, Environment = Environment });
            }
            OnStateChange?.Invoke();
        }

        /// <summary>Restart the run. With keepLines=false the player's drawing is wiped.</summary>
        public void Restart(bool keepLines)
        {
            Stop();
            if (!keepLines)
            {
                Editor.ClearPlayerLines();
                Editor.ClearHistory();
                FlagFrame = -1;
            }
            if (Mode == GameMode.Arcade)
            {
                StartArcade();
                return;
            }
            Play();
        }

        public void SetFlag()
        {
            if (Run == null) return;
            FlagFrame = Run.Frame;
            OnMessage?.Invoke("FLAG SET AT " + U.F(FlagFrame / 40.0) + "s", "#39f6ff", false);
            OnStateChange?.Invoke();
        }

        public void ClearFlag()
        {
            FlagFrame = -1;
            OnStateChange?.Invoke();
        }

        public void SetSpeed(double s)
        {
            Speed = s;
            OnStateChange?.Invoke();
        }

        public void SetTool(ToolId tool)
        {
            Editor.Tool = tool;
            _placement = "none";
            OnStateChange?.Invoke();
        }

        public void SetMaterial(MaterialId m)
        {
            if (!Editor.CanUseMaterial(m)) return;
            Editor.Material = m;
            OnStateChange?.Invoke();
        }

        public void SetObjectKind(string kind)
        {
            Editor.ObjectKind = kind;
            Editor.Tool = ToolId.Object;
            _placement = "none";
            OnStateChange?.Invoke();
        }

        public void BeginPlacement(string kind)
        {
            _placement = kind;
            OnMessage?.Invoke(kind == "start" ? "CLICK TO PLACE START" : "CLICK TO PLACE FINISH", "#4dff9d", false);
        }

        /// <summary>Returns true if the click was consumed by a placement action.</summary>
        public bool HandlePlacementClick(double wx, double wy)
        {
            if (_placement == "none") return false;
            if (_placement == "start")
            {
                Track.Start = new Vec2d(wx, wy);
                Stop();
            }
            else
            {
                Track.Finish = new Zone(wx - 20, wy - 60, 40, 70);
            }
            Track.Revision++;
            _placement = "none";
            OnStateChange?.Invoke();
            return true;
        }

        public void SwitchCoopPlayer()
        {
            if (!CoopActive) return;
            CoopPlayer = CoopPlayer == 1 ? 2 : 1;
            Editor.Constraints.Player = CoopPlayer;
            Editor.Constraints.Budget = CoopBudgets[CoopPlayer - 1];
            OnMessage?.Invoke("PLAYER " + CoopPlayer + " DRAWING", CoopPlayer == 1 ? "#39f6ff" : "#ff2bd6", false);
            OnStateChange?.Invoke();
        }

        public void ToggleGhost()
        {
            GhostEnabled = !GhostEnabled;
            OnStateChange?.Invoke();
        }

        public void ToggleMusic()
        {
            if (Audio == null) return;
            bool v = !Audio.Enabled;
            Audio.Enabled = v;
            Progress.Music = v;
            Progress.Save();
            OnStateChange?.Invoke();
        }

        // ---------------------------------------------------------------- loop

        /// <summary>Advance simulation and effects by dt seconds.</summary>
        public void Tick(double dt)
        {
            if (dt > 0.25) dt = 0.25;
            Time += dt;
            Editor.Zoom = Camera.Zoom;
            if (PlayState == PlayState.Play && Run != null)
            {
                _accumulator += dt * 1000 * Speed;
                int steps = 0;
                while (_accumulator >= Constants.FrameMs && steps < 12)
                {
                    SimStep();
                    _accumulator -= Constants.FrameMs;
                    steps++;
                }
                if (steps == 12) _accumulator = 0;
            }
            if (Run == null)
            {
                RefreshPreview();
                World pw = PreviewWorld;
                if (pw != null)
                {
                    pw.Frame++;
                    foreach (Entity e in pw.Entities) if (e.Active) e.Update(pw);
                }
            }
            if (Audio != null) Audio.Riding = PlayState == PlayState.Play && Run != null && !Run.Done;
            Effects.Update(dt * 1000 * (PlayState == PlayState.Play ? Speed : 1) / Constants.FrameMs);
            UpdateCamera();
            Camera.Update(dt);
            UpdateMarkers();
        }

        private void RefreshPreview()
        {
            if (_previewRevision == Track.Revision && (PreviewWorld != null || (Track.Objects.Count == 0 && Track.Props.Count == 0))) return;
            _previewRevision = Track.Revision;
            if (Track.Objects.Count == 0 && Track.Props.Count == 0)
            {
                PreviewWorld = null;
                return;
            }
            var world = new World();
            foreach (ObjectData o in Track.Objects.Values)
            {
                Entity e = EntityFactory.Build(o.Def, world);
                if (e != null) world.AddEntity(e);
            }
            foreach (PropData o in Track.Props.Values) world.AddProp(new Prop(world.NextDynamicId++, o.Def));
            PreviewWorld = world;
        }

        private void SimStep()
        {
            Run run = Run;
            if (run.Done && !_resultsPending) return;
            run.Step();
            if (GhostRun != null && GhostEnabled) GhostRun.Step();
            if (Arcade != null) Editor.Constraints.Budget = Arcade.Update(run);
            ConsumeRunEvents(run);
            SpawnContactSparks(run);
            if (run.Done && !_resultsPending)
            {
                _resultsPending = true;
                _resultsTimer = run.Finished ? 30 : 45;
            }
            if (_resultsPending)
            {
                _resultsTimer--;
                if (_resultsTimer <= 0) FinishRun();
            }
        }

        private void ConsumeRunEvents(Run run)
        {
            Effects fx = Effects;
            foreach (RunEvent ev in run.Events)
            {
                switch (ev.Type)
                {
                    case "trick":
                        fx.AddPopup(ev.X, ev.Y, ev.Text, ev.Color, ev.Text.StartsWith("BAIL") ? 1 : 1.1);
                        if (ev.Points > 0) Audio?.Trick(ev.Points);
                        break;
                    case "flag":
                        Audio?.Pickup();
                        fx.AddPopup(ev.X, ev.Y, "FLAG", "#ffe93a", 1.2);
                        fx.AddRing(ev.X, ev.Y + 16, 30, "#ffe93a");
                        fx.Spark(ev.X, ev.Y + 16, 20, "#ffe93a", 2.5);
                        break;
                    case "rescue":
                        Audio?.Pickup();
                        fx.AddPopup(ev.X, ev.Y, "RESCUED", "#ff7ae8", 1.2);
                        fx.AddRing(ev.X, ev.Y + 16, 30, "#ff7ae8");
                        break;
                    case "finish":
                        fx.AddPopup(ev.X, ev.Y, "FINISH", "#4dff9d", 1.6);
                        fx.AddRing(ev.X, ev.Y + 16, 60, "#4dff9d", 30);
                        fx.Spark(ev.X, ev.Y + 16, 60, "#4dff9d", 4);
                        fx.Flash = 0.35;
                        fx.FlashColor = "#4dff9d";
                        Audio?.Finish();
                        OnMessage?.Invoke("FINISH", "#4dff9d", true);
                        break;
                    case "death":
                        fx.AddPopup(ev.X, ev.Y, "WIPEOUT", "#ff4d4d", 1.4);
                        fx.Spark(ev.X, ev.Y + 12, 40, "#ff4d4d", 3);
                        fx.Shake = 8;
                        Audio?.Crash();
                        OnMessage?.Invoke("WIPEOUT", "#ff4d4d", true);
                        break;
                    case "message":
                        fx.AddPopup(ev.X, ev.Y, ev.Text, ev.Color, 1);
                        break;
                }
            }
            run.Events.Clear();
            foreach (WorldEvent ev in run.World.Events)
            {
                if (ev.Type == WorldEventType.Crumble && ev.Line != null)
                {
                    fx.Shards(ev.Line.X1, ev.Line.Y1, ev.Line.X2, ev.Line.Y2, "#ff7a45");
                }
                else if (ev.Type == WorldEventType.Break && ev.Line != null)
                {
                    fx.Shards(ev.Line.X1, ev.Line.Y1, ev.Line.X2, ev.Line.Y2, "#ffb347");
                    fx.AddPopup(ev.X, ev.Y - 10, "SMASH", "#ffb347", 1.2);
                    fx.Shake = Math.Max(fx.Shake, 5);
                }
                else if (ev.Type == WorldEventType.Explode)
                {
                    Audio?.Explosion();
                    fx.Explosion(ev.X, ev.Y, ev.Radius);
                    fx.AddPopup(ev.X, ev.Y - 30, "BOOM", "#ffb347", 1.5);
                }
                else if (ev.Type == WorldEventType.Impact && ev.Speed > 4)
                {
                    fx.Spark(ev.X, ev.Y, 6, "#ffffff", 1.5);
                }
            }
        }

        private readonly Random _sparkRng = new Random();

        private void SpawnContactSparks(Run run)
        {
            Rider rider = run.Rider;
            if (run.Frame % 2 != 0) return;
            foreach (int i in rider.Model.Vehicle)
            {
                Point p = rider.Points[i];
                if (p.Contact == null) continue;
                double s = Math.Sqrt((p.X - p.Px) * (p.X - p.Px) + (p.Y - p.Py) * (p.Y - p.Py));
                if (s > 4 && _sparkRng.NextDouble() < 0.5)
                {
                    Effects.Spark(p.X, p.Y, 1, p.Contact.Material.Color, s * 0.25, 1.2, Math.Atan2(-(p.Y - p.Py), -(p.X - p.Px)), 0.05);
                }
                if (p.Contact.Material.Accel != 0 && _sparkRng.NextDouble() < 0.6)
                {
                    Effects.Spark(p.X, p.Y, 1, p.Contact.Material.Color, 2, 0.8, Math.Atan2(-p.Contact.Uy, -p.Contact.Ux), 0);
                }
            }
        }

        private void UpdateCamera()
        {
            if (Run != null && PlayState != PlayState.Edit && Following)
            {
                Rider rider = Run.Rider;
                Vec2d c = rider.Center();
                Vec2d v = rider.Velocity();
                double lead = Mode == GameMode.Arcade ? 90 / Camera.Zoom : 12;
                Camera.Follow(c.X + v.X * 6 + lead, c.Y + v.Y * 3);
            }
        }

        private void UpdateMarkers()
        {
            Markers.Clear();
            if (Level == null) return;
            for (int i = 0; i < Level.Flags.Count; i++)
            {
                Markers.Add(new Marker { X = Level.Flags[i].X, Y = Level.Flags[i].Y, Kind = "flag", Done = Run != null && Run.Flags[i] });
            }
            for (int i = 0; i < Level.Rescues.Count; i++)
            {
                Markers.Add(new Marker { X = Level.Rescues[i].X, Y = Level.Rescues[i].Y, Kind = "rescue", Done = Run != null && Run.Rescues[i] });
            }
        }

        private void FinishRun()
        {
            Run run = Run;
            RunSummary summary = run.Summary();
            _resultsPending = false;
            PlayState = PlayState.Pause;
            LevelDef level = Level;
            var info = new ResultsInfo { Mode = Mode, Level = level, Summary = summary, Complete = summary.Finished };
            if (level != null)
            {
                info.Results = Objectives.EvaluateLevel(level, summary, out Medal medal, out bool complete);
                info.Medal = medal;
                info.Complete = complete;
                if (Mode == GameMode.Campaign && complete)
                {
                    GhostRecord ghost = summary.Finished ? new GhostRecord { TrackJson = Track.ToJson(), Rider = RiderDef.Id, Frames = summary.Frames } : null;
                    info.Improvements = Progress.RecordResult(level.Id, medal, summary.Finished ? summary.Frames : (int?)null, summary.InkUsed, summary.TrickScore, ghost);
                }
                else if (Mode == GameMode.Daily && DailyKey != null)
                {
                    if (!Progress.Daily.TryGetValue(DailyKey, out DailyRecord rec))
                    {
                        rec = new DailyRecord();
                        Progress.Daily[DailyKey] = rec;
                    }
                    var imp = new Improvements();
                    if (summary.Finished && (!rec.Time.HasValue || summary.Frames < rec.Time.Value))
                    {
                        rec.Time = summary.Frames;
                        imp.NewTime = true;
                    }
                    if (summary.Finished && (!rec.Ink.HasValue || summary.InkUsed < rec.Ink.Value))
                    {
                        rec.Ink = summary.InkUsed;
                        imp.NewInk = true;
                    }
                    if (summary.TrickScore > rec.Trick)
                    {
                        rec.Trick = summary.TrickScore;
                        imp.NewTrick = true;
                    }
                    info.Improvements = imp;
                    Progress.Save();
                }
            }
            else if (Mode == GameMode.Arcade)
            {
                info.ArcadeBest = Progress.ArcadeBest;
                if (summary.Distance > Progress.ArcadeBest)
                {
                    Progress.ArcadeBest = (int)Math.Round(summary.Distance);
                    Progress.Save();
                    info.Improvements = new Improvements { NewTrick = true };
                }
            }
            else if (Mode == GameMode.Library && LibraryTrack != null && summary.Finished)
            {
                TrackRecords rec = LibraryTrack.Records;
                var imp = new Improvements();
                if (!rec.BestFrames.HasValue || summary.Frames < rec.BestFrames.Value)
                {
                    rec.BestFrames = summary.Frames;
                    imp.NewTime = true;
                }
                if (!rec.BestInk.HasValue || summary.InkUsed < rec.BestInk.Value)
                {
                    rec.BestInk = summary.InkUsed;
                    imp.NewInk = true;
                }
                if (summary.TrickScore > rec.BestTrick)
                {
                    rec.BestTrick = summary.TrickScore;
                    imp.NewTrick = true;
                }
                info.Improvements = imp;
                Store.Update(LibraryTrack);
            }
            info.Next = level != null && Mode == GameMode.Campaign ? Levels.Next(level.Id) : null;
            OnResults?.Invoke(info);
        }

        // ---------------------------------------------------------------- helpers

        public double? InkBudget => Editor.Constraints.Budget;

        public double InkUsed => Editor.InkUsed;

        public double MetersTravelled()
        {
            if (Run == null) return 0;
            return (Run.MaxX - Track.Start.X) / Constants.PxPerMeter;
        }

        public bool RegionUnlocked(int regionIndex)
        {
            if (regionIndex == 0) return true;
            RegionDef prev = Levels.Regions[regionIndex - 1];
            List<LevelDef> levels = Levels.InRegion(prev.Id);
            int done = 0;
            foreach (LevelDef l in levels) if (Progress.IsComplete(l.Id)) done++;
            return done >= Math.Min(2, levels.Count);
        }

        public bool LevelUnlocked(LevelDef level)
        {
            int regionIndex = Array.FindIndex(Levels.Regions, r => r.Id == level.Region);
            if (!RegionUnlocked(regionIndex)) return false;
            List<LevelDef> levels = Levels.InRegion(level.Region);
            int idx = levels.IndexOf(level);
            if (idx <= 0) return true;
            return Progress.IsComplete(levels[idx - 1].Id);
        }

        /// <summary>World point the cave halo follows: the rider while running, else the cursor.</summary>
        public Vec2d FocusPoint()
        {
            if (Run != null) return Run.Rider.Center();
            return Editor.CursorWorld;
        }

        public Zone? CurrentFinish => Level?.Finish ?? Track.Finish;
    }
}
