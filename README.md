# Cyber Rider

A from-scratch rebuild of the classic Flash toy as a pulsing neon sci-fi browser game. Under the
glow is a faithful re-implementation of the original engine (40 fps Verlet points, one-sided lines,
breakable mount bones), wrapped in a campaign with ink budgets, track materials, tricks, riders,
interactive objects, bosses, ghosts, daily challenges, a track library, an arcade mode and hotseat
co-op. It runs in any modern browser, on desktop and on phones.

**Play it online:** <https://catgenova.github.io/linerider/>

**Play it offline:** open `web/cyber-rider.html`. It is a self-contained build of the whole game,
no server and no install.

No backend is required. Progress, records, ghosts and published tracks live in `localStorage`;
tracks travel between players as share codes (`CYR1.…`).

## Development

```
cd web
npm install
npm run dev               # http://localhost:5173 with live reload
npm test                  # physics behaviour tests + a sweep over every campaign level
npm run build             # static bundle in dist/
npm run build:standalone  # regenerate cyber-rider.html from dist/
npm run pages             # the same, plus the root index.html that GitHub Pages publishes
```

The site is GitHub Pages serving the `index.html` at the root of `main`. After changing the game,
run `npm run pages` and commit the root `index.html`; the site updates a minute after the push.
The root `.nojekyll` keeps GitHub from post-processing the file.

Stack: TypeScript, Vite, Canvas 2D (additive glow strokes), plain DOM for the HUD and menus,
Vitest for tests. There are no runtime dependencies.

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

### Phones and tablets

Everything works by touch. One finger draws with the current tool (or pans with the Pan tool), two
fingers pan and pinch-zoom, and a second finger landing mid-stroke cancels that stroke instead of
leaving a mark. The HUD packs into the corners in landscape, keeps clear of notches, and every
keyboard shortcut has a button. Open the site in the phone's browser, or add it to the home
screen for a full-screen app.

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
- **Title demo** – the menu plays an endless generated ride behind itself, cycling through the
  environments and riders.

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
airtime, fragile), Tank (heavy, low friction, tough), Nova (a long deck with a tall standing rig).
Riders unlock by completing specific levels.

## Layout

```
index.html         the published game (generated by `npm run pages`, do not edit by hand)
web/src/physics    point, line (materials + collision), grid, rider models, props, world
web/src/game       track model, editor constraints, levels, run controller, tricks, progress, library
web/src/editor     camera and drawing tools
web/src/render     neon renderer, parallax backgrounds, effects
web/src/modes      arcade director, daily generator, title demo, built-in tracks
web/src/ui         HUD, menu screens, input (mouse, touch, keyboard)
web/tests          physics behaviour and a sweep over every campaign level
```

An earlier Unity 2D port of the same game is kept in git history at the `unity-port` tag. It is
not maintained; the browser build is the game.
