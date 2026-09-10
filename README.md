# Neon Line Rider

A from-scratch rebuild of the classic Flash toy as a pulsing neon sci-fi game. The physics is a
faithful re-implementation of the original engine (40 fps Verlet points, one-sided lines, breakable
mount bones), wrapped in a campaign with ink budgets, track materials, tricks, riders, interactive
objects, bosses, ghosts, daily challenges, a track library, an arcade mode and hotseat co-op.

```
npm install
npm run dev        # http://localhost:5173
npm test           # physics + level sweep
npm run build      # static bundle in dist/
```

No backend is required. Progress, records, ghosts and published tracks live in `localStorage`;
tracks travel between players as share codes (`NLR1.…`).

## Playing

| Key | Action |
| --- | --- |
| Space | Play / pause |
| Backspace | Stop and rewind |
| R / Shift+R | Restart keeping lines / restart and wipe your lines |
| F / Shift+F | Set a flag at the current moment / clear it |
| P L E H V | Pencil, straight line, eraser, pan, flip tool |
| 1-9 0 - = | Select a material (see palette) |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Scroll, middle-drag | Zoom, pan |
| Right-click | Flip a line's solid side |
| Tab | Co-op: hand the pen to the other player |
| G | Toggle ghost |
| , . | Playback speed |

Lines are one-sided, exactly like the original: draw left to right and the top face is solid.

## Modes

- **Adventure** – eight regions, 32 levels, an overworld map. Each level ships partially built
  terrain, a goal, an ink budget and a set of medal objectives (airtime, flips, ink used, time,
  no crash, near misses, huge drops…). Bronze completes it, silver and gold need the optional ones.
  Level types: reach, puzzle (tiny budgets), stunt, flags, delivery (fragile cargo), rescue
  (pick up stranded techs), destruction (chaos score), boss (avalanche, rockslide, collapsing
  clock tower, rolling moonball, the machine core).
- **Free Ride** – unlimited ink in any environment, place your own start and finish, publish.
- **Neon Rush** – draw-while-riding arcade: the rider never stops, terrain generates ahead, ink
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
Verlet props (dominoes, crates, TNT with chain reactions, boulders, fragile cargo).

## Environments

Neon Peaks, Glacier Grid (global zero friction), Dune Circuit (gusting crosswind), Data Forest
(springs), Skyline District (precision budgets, light wind), Undercity (darkness halo), Lunar Relay
(0.4 g), The Machine (everything moves).

## Riders

Bosh (classic sled), Vex (featherweight, more airtime, fragile), Tank (heavy, low friction, tough),
Nova (snowboard model with a different point/bone rig). Riders unlock by completing specific levels.

## Layout

```
src/physics    point, line (materials + collision), grid, rider models, props, world
src/game       track model, editor constraints, levels, run controller, tricks, progress, library
src/editor     camera and drawing tools
src/render     neon renderer, parallax backgrounds, effects
src/modes      arcade director, daily generator, built-in tracks
src/ui         HUD, menu screens, input
tests          physics behaviour and a sweep over every campaign level
```
