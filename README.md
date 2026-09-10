# Cyber Rider

A from-scratch rebuild of the classic Flash toy as a pulsing neon sci-fi game, available as a
**Unity 2D project** (this repository root) and as a **browser build** (`web/`). Both share the same
design: a faithful re-implementation of the original engine (40 fps Verlet points, one-sided lines,
breakable mount bones) wrapped in a campaign with ink budgets, track materials, tricks, riders,
interactive objects, bosses, ghosts, daily challenges, a track library, an arcade mode and hotseat
co-op. The two builds are simulation-identical: the C# core reproduces the TypeScript traces to the
last digit, share codes work across both, and the daily challenge is the same terrain on both.

## Unity (open this folder)

1. In Unity Hub choose **Add project from disk** and pick this repository folder. It was written
   for Unity 2022.3 LTS (`ProjectSettings/ProjectVersion.txt`); any 2022.3 or newer editor works.
2. Open `Assets/Scenes/Main.unity` and press Play. The scene holds a camera and one `GameBootstrap`
   component; everything else (renderer, UI, audio, input) is created in code at runtime.
   Input works with either backend: the project ships with **Active Input Handling = Both**, so the
   classic Input Manager is read; in a project where only the Input System package is active
   (`com.unity.inputsystem`, listed in `Packages/manifest.json`), the same scripts read its Mouse and
   Keyboard devices and drive the UI through `InputSystemUIInputModule`.
3. Build settings already list the scene, so **File > Build Settings > Build** produces a desktop
   player. Save data and published tracks are written to `Application.persistentDataPath`.

Layout:

```
Assets/Scenes/Main.unity           bootstrap scene
Assets/Scripts/Core/               engine-agnostic C# (no UnityEngine): physics, track, levels, run, editor,
                                   tricks, daily/arcade/attract directors, library, progress, JSON
Assets/Scripts/Unity/              MonoBehaviours and views: controller, neon mesh renderer, uGUI HUD/menus,
                                   procedural audio, input, bootstrap
Assets/Resources/Shaders/          additive and alpha vertex-colour shaders used by the renderer
tools/CoreTests/                   .NET console harness that replays the web build's reference traces
tools/UnityCheck, tools/UnityStubs .NET compile check of the Unity layer against a stub UnityEngine
tools/gen_levels.py                regenerates Levels.cs / BuiltinTracks.cs from the web level data
tools/gen_unity_meta.py            regenerates the scene, build settings and .meta files
```

The core is verified without the editor: `cd tools/CoreTests && dotnet run` replays 12 physics
scenarios and all 32 level runs recorded from the web build and checks positions match exactly,
then runs behaviour checks (JSON round trips, share codes, daily seeds, editor budgets, objects).

### Android

The project is set up for phones and tablets: landscape auto-rotation, ARM64 with IL2CPP, minimum
API 24 (Android 7.0), package id `com.catgenova.cyberrider`, full-screen rendering with a HUD that
keeps clear of the notch, and finger-sized controls (the UI scales with pixel density). To build:

1. In Unity Hub add the **Android Build Support** module, with OpenJDK and the Android SDK & NDK
   tools, to the editor you use for this project.
2. Open the project, go to **File > Build Settings**, select **Android** and press
   **Switch Platform**.
3. Enable USB debugging on the phone, plug it in and press **Build And Run**, or press **Build** for
   an APK to sideload. **Player Settings** already carry the settings above; change the package
   name there before publishing.

On a touchscreen one finger draws with the current tool (or pans with the Pan tool), two fingers
pan and pinch-zoom, a second finger cancels the stroke in progress, and every keyboard shortcut has
a button in the HUD. The browser build behaves the same way in Android Chrome: open
`web/cyber-rider.html` on the phone, or add the dev server's address to the home screen.

## Web (`web/`)

**Quickest way to play:** open `web/cyber-rider.html` in any modern browser. It is a
self-contained build of the whole game (no server, no install).

```
cd web
npm install
npm run dev               # http://localhost:5173 with live reload
npm test                  # physics + level sweep
npm run build             # static bundle in dist/
npm run build:standalone  # regenerate cyber-rider.html from dist/
```

No backend is required. Progress, records, ghosts and published tracks live in `localStorage`;
tracks travel between players as share codes (`CYR1.…`).

## Playing

| Key | Action |
| --- | --- |
| Space | Play / pause |
| Backspace | Stop and rewind |
| R / Shift+R | Restart keeping lines / restart and wipe your lines |
| F / Shift+F | Set a flag at the current moment / clear it |
| P L E H V O | Pencil, straight line, eraser, pan, flip, object placement (free ride) |
| 1-9 0 - = | Select a material (see palette) |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Scroll, middle-drag | Zoom, pan |
| Right-click | Flip a line's solid side |
| Tab | Co-op: hand the pen to the other player |
| G | Toggle ghost |
| , . | Playback speed |
| Touch | One finger uses the current tool; two fingers pan and pinch-zoom; buttons cover the rest |

Lines are one-sided, exactly like the original: draw left to right and the top face is solid.

## Modes

- **Adventure** – eight regions, 32 levels, an overworld map. Each level ships partially built
  terrain, a goal, an ink budget and a set of medal objectives (airtime, flips, ink used, time,
  no crash, near misses, huge drops…). Bronze completes it, silver and gold need the optional ones.
  Level types: reach, puzzle (tiny budgets), stunt, flags, delivery (fragile cargo), rescue
  (pick up stranded techs), destruction (chaos score), boss (avalanche, rockslide, collapsing
  clock tower, rolling moonball, the machine core).
- **Free Ride** – unlimited ink in any environment, place your own start and finish, drop in
  physics toys and props from the object palette (O), and publish the result.
- **Cyber Rush** – draw-while-riding arcade: the rider never stops, terrain generates ahead, ink
  regenerates with distance, a tailwind keeps raising the pace. Score is distance.
- **Daily Challenge** – seeded from the UTC date; everyone gets the same terrain, flags, finish and
  budget. Separate boards for fastest time, least ink and trick score.
- **Track Library** – publish with thumbnail, tags, difficulty and description; likes, plays and
  per-track records; import/export share codes. Built-in showcase tracks included. The store is an
  interface (`TrackStore`) so a server implementation can replace the local one.
- **Co-op** – two players, one rider, two ink colours and budgets, Tab to hand over.
- **Ghost racing** – your best finishing run on a level is stored as a track snapshot and replayed
  as a translucent rider in lockstep with your current attempt.

## Materials

Track, Accel, Scenery (free, no collision), Ice (no friction), Mud (drag), Spring (bounce), Sticky,
Conveyor (drives to a belt speed), Crumble (vanishes after a touch), Booster (hard shove), One-way
(solid only when moving along its arrow), Grind rail (scores while riding it). Each costs a
different amount of ink per metre.

## Objects and obstacles

Moving platforms, gears, pendulums, fans, magnets, cannons, breakable walls, balloons, seesaws,
trains, rockfalls, collapsing bridges, timed collapses, an avalanche wall, a chasing snowball, and
Verlet props (dominoes, crates, carts, TNT with chain reactions, boulders, fragile cargo). Levels
define them in data; in Free Ride the player places them from a palette, sees them animate while
editing, erases them with the eraser, and they travel with published tracks and share codes.

## Environments

Neon Peaks, Glacier Grid (global zero friction), Dune Circuit (gusting crosswind), Data Forest
(springs), Skyline District (precision budgets, light wind), Undercity (darkness halo), Lunar Relay
(0.4 g), The Machine (everything moves).

## Riders

Bosh (the classic rig, drawn as a stick figure standing on a hoverboard), Vex (featherweight, more
airtime, fragile), Tank (heavy, low friction, tough), Nova (a long deck with a tall standing rig). Riders unlock by completing specific levels.

## Web layout

```
web/src/physics    point, line (materials + collision), grid, rider models, props, world
web/src/game       track model, editor constraints, levels, run controller, tricks, progress, library
web/src/editor     camera and drawing tools
web/src/render     neon renderer, parallax backgrounds, effects
web/src/modes      arcade director, daily generator, built-in tracks
web/src/ui         HUD, menu screens, input
web/tests          physics behaviour and a sweep over every campaign level
```
