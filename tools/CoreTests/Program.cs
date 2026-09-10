using System;
using System.Collections.Generic;
using System.IO;
using CyberRider.Core;

/// <summary>
/// Console harness for the engine-agnostic core. Replays the reference traces dumped from the
/// TypeScript build and checks the C# simulation reproduces them, then runs behavioural checks.
/// </summary>
internal static class Program
{
    private static int _failures;
    private static int _checks;

    private static void Check(bool ok, string message)
    {
        _checks++;
        if (ok) return;
        _failures++;
        Console.WriteLine("  FAIL: " + message);
    }

    private static string DataPath(string file)
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(dir, "data", file);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
            if (dir == null) break;
        }
        return Path.Combine("data", file);
    }

    private sealed class Scenario
    {
        public string Name;
        public string Model = "sled";
        public double StartX, StartY;
        public Action<World> Setup;
    }

    private static Line L(World w, int id, double x1, double y1, double x2, double y2, string material = "normal", bool leftExt = false, bool flipped = false)
    {
        return w.AddLine(id, x1, y1, x2, y2, Materials.Get(material), flipped, leftExt);
    }

    private static List<Scenario> Scenarios()
    {
        return new List<Scenario>
        {
            new Scenario { Name = "slope", StartY = -2, Setup = w => L(w, 1, -50, 10, 1500, 400) },
            new Scenario { Name = "joint", StartY = -2, Setup = w => { L(w, 1, -50, 10, 500, 200); L(w, 2, 500, 200, 900, 150, "normal", true); } },
            new Scenario { Name = "wall-crash", StartY = -2, Setup = w => { L(w, 1, -50, 10, 400, 120); L(w, 2, 300, 200, 300, -200); } },
            new Scenario { Name = "accel", Setup = w => L(w, 1, -50, 5, 800, 5, "accel") },
            new Scenario { Name = "spring", Setup = w => L(w, 1, -100, 150, 600, 150, "spring") },
            new Scenario { Name = "ice-mud-sticky", StartY = -2, Setup = w => { L(w, 1, -50, 10, 300, 80, "ice"); L(w, 2, 300, 80, 600, 140, "mud"); L(w, 3, 600, 140, 900, 200, "sticky"); } },
            new Scenario { Name = "conveyor-oneway", StartY = -2, Setup = w => { L(w, 1, -50, 10, 300, 60); L(w, 2, 300, 60, 700, 60, "conveyor"); L(w, 3, 500, 0, 500, 60, "oneway"); } },
            new Scenario { Name = "crumble", Setup = w => L(w, 1, -100, 8, 600, 8, "crumble") },
            new Scenario { Name = "board", Model = "board", StartY = -1, Setup = w => L(w, 1, -50, 10, 1500, 300) },
            new Scenario { Name = "flipped", Setup = w => L(w, 1, 600, 60, -100, 60, "normal", false, true) },
            new Scenario
            {
                Name = "boulder", StartY = -2, Setup = w =>
                {
                    L(w, 1, -50, 10, 400, 60);
                    L(w, 2, 400, 60, 1200, 60, "normal", true);
                    w.AddProp(new Prop(w.NextDynamicId++, new PropDef { Kind = "boulder", X = 0, Y = -40, Radius = 12, Mass = 5 }));
                    w.AddProp(new Prop(w.NextDynamicId++, new PropDef { Kind = "domino", X = 520, Y = 50, Width = 4, Height = 20, Mass = 0.5 }));
                    w.AddProp(new Prop(w.NextDynamicId++, new PropDef { Kind = "crate", X = 600, Y = 50, Width = 20, Height = 20, Mass = 1.5 }));
                },
            },
            new Scenario
            {
                Name = "wind-lowg", StartY = -2, Setup = w =>
                {
                    L(w, 1, -50, 10, 400, 120);
                    L(w, 2, 700, 300, 1200, 350);
                    w.GravityScale = 0.4;
                    w.Wind = (double x, double y, int frame, out double fx, out double fy) =>
                    {
                        fx = 0.02 + Math.Sin((frame / 140.0 + x * 0.0015) * Math.PI * 2) * 0.05;
                        fy = 0;
                    };
                },
            },
        };
    }

    private static void PhysicsReference()
    {
        Console.WriteLine("physics reference traces");
        var reference = Json.List(Json.Parse(File.ReadAllText(DataPath("physics-reference.json"))));
        var scenarios = Scenarios();
        foreach (object item in reference)
        {
            var entry = Json.Obj(item);
            string name = Json.Str(entry, "name");
            Scenario sc = scenarios.Find(s => s.Name == name);
            if (sc == null)
            {
                Check(false, "missing scenario " + name);
                continue;
            }
            var world = new World();
            sc.Setup(world);
            var rider = new Rider(RiderModel.ById(sc.Model), sc.StartX, sc.StartY, Constants.StartVelocity, 0);
            world.AddRider(rider);
            var samples = Json.List(Json.Get(entry, "samples"));
            int frame = 0;
            double maxErr = 0;
            bool deadMismatch = false;
            foreach (object so in samples)
            {
                var sample = Json.Obj(so);
                int target = Json.Int(sample, "frame");
                while (frame < target)
                {
                    world.Step();
                    frame++;
                }
                if (Json.Bool(sample, "dead") != rider.Dead) deadMismatch = true;
                var pts = Json.List(Json.Get(sample, "points"));
                for (int i = 0; i < pts.Count; i++)
                {
                    var xy = Json.List(pts[i]);
                    double ex = Json.Num(xy[0]);
                    double ey = Json.Num(xy[1]);
                    double err = Math.Max(Math.Abs(ex - rider.Points[i].X), Math.Abs(ey - rider.Points[i].Y));
                    if (err > maxErr) maxErr = err;
                }
                var props = Json.List(Json.Get(sample, "props"));
                for (int i = 0; i < props.Count && i < world.Props.Count; i++)
                {
                    var pr = Json.List(props[i]);
                    Vec2d c = world.Props[i].Center();
                    double err = Math.Max(Math.Abs(Json.Num(pr[0]) - c.X), Math.Abs(Json.Num(pr[1]) - c.Y));
                    if (err > maxErr) maxErr = err;
                    bool active = Json.Num(pr[2]) != 0;
                    if (active != world.Props[i].Active) deadMismatch = true;
                }
            }
            Console.WriteLine($"  {name,-18} max position error {maxErr:0.000e0}{(deadMismatch ? "  STATE MISMATCH" : "")}");
            Check(maxErr < 1e-6 && !deadMismatch, $"{name}: trace diverged (max error {maxErr})");
        }
    }

    private static void LevelReference()
    {
        Console.WriteLine("level runs (naive straight-line solve on every level)");
        var reference = Json.List(Json.Parse(File.ReadAllText(DataPath("level-reference.json"))));
        int matched = 0;
        foreach (object item in reference)
        {
            var entry = Json.Obj(item);
            string id = Json.Str(entry, "id");
            LevelDef level = Levels.ById(id);
            if (level == null)
            {
                Check(false, "missing level " + id);
                continue;
            }
            Track track = LevelLoader.TrackFromLevel(level);
            if (level.Finish.HasValue)
            {
                LevelLine first = level.Lines[0];
                Zone fz = level.Finish.Value;
                track.AddLine(first.X2, first.Y2, fz.X + fz.W / 2, fz.Y + fz.H, MaterialId.Normal, false, 1, LineLayer.Player);
            }
            var run = new Run(new RunOptions { Track = track, Level = level, RiderDef = Riders.Get("bosh"), Environment = Environments.Get(level.Environment) });
            for (int i = 0; i < 1600 && !run.Done; i++) run.Step();
            RunSummary s = run.Summary();
            Vec2d c = run.Rider.Center();
            bool ok = s.Finished == Json.Bool(entry, "finished") && s.Died == Json.Bool(entry, "died") && run.Frame == Json.Int(entry, "frames")
                      && Math.Abs(c.X - Json.Num(entry, "x")) < 1e-3 && Math.Abs(c.Y - Json.Num(entry, "y")) < 1e-3
                      && s.TrickScore == Json.Int(entry, "trick") && s.FlagsCollected == Json.Int(entry, "flags") && s.Chaos == Json.Int(entry, "chaos");
            if (ok) matched++;
            else Console.WriteLine($"  {id}: finished={s.Finished}/{Json.Bool(entry, "finished")} died={s.Died}/{Json.Bool(entry, "died")} frames={run.Frame}/{Json.Int(entry, "frames")} x={c.X:0.##}/{Json.Num(entry, "x"):0.##} trick={s.TrickScore}/{Json.Int(entry, "trick")} chaos={s.Chaos}/{Json.Int(entry, "chaos")} fail={s.FailReason}");
            Check(ok, id + " diverged from the web build");
        }
        Console.WriteLine($"  {matched}/{reference.Count} levels reproduce the web build exactly");
    }

    private static void Behaviour()
    {
        Console.WriteLine("behaviour checks");
        // Track JSON round trip keeps lines, objects and props.
        var track = new Track { Start = new Vec2d(0, 0), Finish = new Zone(400, 40, 44, 70) };
        track.AddLine(-40, 6, 200, 60, MaterialId.Accel, false, 1, LineLayer.Level);
        track.AddLine(200, 60, 500, 140, MaterialId.Normal, true);
        track.AddObject(new EntityDef { Type = "gear", X = 100, Y = -50, Radius = 60, Teeth = 8, Speed = 0.02 });
        track.AddProp(new PropDef { Kind = "tnt", X = 300, Y = 90, Width = 18, Height = 18, Mass = 1, Explosive = true });
        string json = Json.Stringify(track.ToJson());
        Track back = Track.FromJson(Json.Obj(Json.Parse(json)));
        Check(back.Lines.Count == 2 && back.Objects.Count == 1 && back.Props.Count == 1, "track JSON round trip");
        Check(back.Lines[2].Flipped && back.Lines[1].Material == MaterialId.Accel && back.Lines[1].Layer == LineLayer.Level, "track JSON preserves flags and materials");
        Check(Math.Abs(back.Finish.Value.X - 400) < 1e-9, "track JSON preserves finish");
        Check(back.Lines[1].RightExt && back.Lines[2].LeftExt, "joined lines get extensions");

        // Share code round trip (web-compatible format).
        var pub = new PublishedTrack { Title = "Round trip", Author = "Harness", Description = "d", Difficulty = 3, Environment = "moon", Budget = 55, TrackJson = track.ToJson() };
        pub.Tags.Add("test");
        string code = ShareCodes.Encode(pub);
        Check(code.StartsWith("CYR1."), "share code prefix");
        PublishedTrack decoded = ShareCodes.Decode(code);
        Check(decoded != null && decoded.Title == "Round trip" && decoded.Budget == 55 && decoded.Tags.Count == 1 && decoded.LoadTrack().Lines.Count == 2, "share code decode");
        Check(ShareCodes.Decode("garbage") == null, "share code rejects garbage");

        // Web-built share code decodes here too.
        string webCode = "CYR1." + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"t\":\"Web\",\"a\":\"Tester\",\"d\":\"\",\"g\":[\"speed\"],\"f\":2,\"e\":\"moon\",\"b\":null,\"k\":{\"version\":1,\"start\":{\"x\":0,\"y\":0},\"lines\":[{\"id\":1,\"x1\":-40,\"y1\":6,\"x2\":200,\"y2\":60,\"material\":\"normal\",\"flipped\":false,\"leftExt\":false,\"rightExt\":false,\"multiplier\":1,\"layer\":\"player\",\"player\":0}],\"nextId\":2}}"));
        PublishedTrack web = ShareCodes.Decode(webCode);
        Check(web != null && web.Title == "Web" && !web.Budget.HasValue && web.LoadTrack().Lines.Count == 1, "decodes a web share code");

        // Daily challenge is deterministic per key and matches the web's seed derivation.
        uint seed = Daily.SeedFor("2026-09-10");
        LevelDef a = Daily.Generate(seed, "2026-09-10");
        LevelDef b = Daily.Generate(seed, "2026-09-10");
        Check(a.Lines.Count == b.Lines.Count && a.Budget == b.Budget && a.Environment == b.Environment, "daily generation deterministic");
        Check(a.Lines.Count >= 6 && a.Finish.HasValue, "daily level has terrain and a finish");
        Check(Rng.HashString("neon-daily:2026-09-10") == seed, "daily seed hash");
        Check(a.Environment == "forest" && a.Budget == 118 && a.Flags.Count == 2, $"daily 2026-09-10 matches the web build (got {a.Environment}, {a.Budget}, {a.Flags.Count} flags)");

        // Progress round trip.
        var storage = new MemoryStorage();
        var progress = new Progress(storage);
        var ghost = new GhostRecord { TrackJson = track.ToJson(), Rider = "bosh", Frames = 186 };
        Improvements imp = progress.RecordResult("peaks-1", Medal.Gold, 186, 21.9, 0, ghost);
        Check(imp.NewMedal && imp.NewTime && imp.NewInk, "progress records improvements");
        var reloaded = new Progress(storage);
        Check(reloaded.Level("peaks-1").Medal == Medal.Gold && reloaded.Level("peaks-1").BestFrames == 186 && reloaded.Level("peaks-1").Ghost != null, "progress JSON round trip");
        Check(reloaded.IsComplete("peaks-1") && !reloaded.IsComplete("peaks-2"), "progress completion");

        // Editor: budget truncation, snapping, undo/redo, object placement.
        LevelDef peaks1 = Levels.ById("peaks-1");
        Track lt = LevelLoader.TrackFromLevel(peaks1);
        var editor = new Editor(lt) { Zoom = 2 };
        editor.Constraints = new EditorConstraints { Budget = 10, Materials = new List<MaterialId> { MaterialId.Normal }, CanPlaceObjects = false };
        LineData added = editor.AddLine(70, 12, 400, 200);
        Check(added != null && Math.Abs(added.Length - 100) < 1e-6, "budget truncates a line to the remaining ink");
        Check(editor.AddLine(70, 12, 90, 20) == null, "no ink left rejects lines");
        editor.Undo();
        Check(lt.InkUsed() == 0, "undo removes the line");
        editor.Redo();
        Check(Math.Abs(lt.InkUsed() - 10) < 1e-6, "redo restores the line");
        var free = new Track();
        free.AddLine(-40, 6, 200, 40);
        var fe = new Editor(free) { Zoom = 2, ObjectKind = "crate" };
        fe.Constraints = new EditorConstraints { Budget = null, Materials = new List<MaterialId> { MaterialId.Normal }, CanPlaceObjects = true, CanEraseLevel = true };
        Check(fe.PlaceObject(100, 10), "object placement");
        Check(free.Props.Count == 1 && Math.Abs(free.Props[2].Def.Y - (26 - 10.5)) < 1.5, $"crate snaps onto the surface (y={free.Props[2].Def.Y})");
        fe.ObjectKind = "gear";
        fe.PlaceObject(300, -40);
        Check(free.Objects.Count == 1, "entity placement");
        fe.Undo();
        Check(free.Objects.Count == 0, "undo removes an entity");

        // Run: pickups, finish, results and a solved level.
        LevelDef peaks3 = Levels.ById("peaks-3");
        Check(peaks3 != null && peaks3.Flags.Count == 3, "peaks-3 has three flags");
        Track solve = LevelLoader.TrackFromLevel(peaks1);
        solve.AddLine(70, 12, 240, 150);
        var run = new Run(new RunOptions { Track = solve, Level = peaks1, RiderDef = Riders.Get("bosh"), Environment = Environments.Get(peaks1.Environment) });
        for (int i = 0; i < 600 && !run.Done; i++) run.Step();
        RunSummary summary = run.Summary();
        var results = Objectives.EvaluateLevel(peaks1, summary, out Medal medal, out bool complete);
        Check(summary.Finished && complete && medal == Medal.Gold && results.Count == 4, $"peaks-1 solve earns gold (finished={summary.Finished}, medal={medal})");
        Check(summary.Frames == 186, $"peaks-1 finish frame matches the web build (got {summary.Frames})");

        // Arcade director generates ahead and grows the budget.
        var arcadeTrack = new Track();
        var director = new ArcadeDirector(arcadeTrack, new Rng(12345));
        var arun = new Run(new RunOptions { Track = arcadeTrack, Level = null, RiderDef = Riders.Get("bosh"), Environment = Environments.Get("rooftops"), Endless = true });
        for (int i = 0; i < 200; i++)
        {
            arun.Step();
            director.Update(arun);
        }
        Check(arcadeTrack.Lines.Count > 7 && director.Budget >= 40, "arcade generates terrain and budget");

        // Stall detection.
        var stall = new Track();
        stall.AddLine(-40, 6, 100, 6, MaterialId.Normal, false, 1, LineLayer.Level);
        stall.AddLine(100, 6, 100, -40, MaterialId.Normal, false, 1, LineLayer.Level);
        var stallLevel = new LevelDef { Id = "t", Name = "t", Region = "peaks", Environment = "mountain", Mode = "reach", Budget = 100, Start = new Vec2d(0, 0), Finish = new Zone(900, 0, 10, 10) };
        stallLevel.Objectives.Add(new Objective("finish"));
        var srun = new Run(new RunOptions { Track = stall, Level = stallLevel, RiderDef = Riders.Get("bosh"), Environment = Environments.Get("mountain") });
        for (int i = 0; i < 1000 && !srun.Done; i++) srun.Step();
        Check(srun.FailReason == "Stalled", "stalled rider ends the run");

        // Built-in tracks parse.
        Check(BuiltinTracks.All.Count == 3 && BuiltinTracks.All[0].LoadTrack().Lines.Count > 0, "built-in tracks parse");
        Check(Levels.All.Count == 32 && Levels.Regions.Length == 8, "level content present");
    }

    private static int Main()
    {
        try
        {
            PhysicsReference();
            LevelReference();
            Behaviour();
        }
        catch (Exception ex)
        {
            Console.WriteLine("EXCEPTION: " + ex);
            _failures++;
        }
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed");
        return _failures == 0 ? 0 : 1;
    }
}
