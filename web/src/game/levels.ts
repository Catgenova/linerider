import type { LevelDef, LevelLine, RegionId } from './level';
import type { EnvironmentId } from './environments';
import type { MaterialId } from '../physics/materials';

export interface RegionDef {
  id: RegionId;
  name: string;
  environment: EnvironmentId;
  blurb: string;
  /** Position on the adventure map, 0..1. */
  mapX: number;
  mapY: number;
}

export const REGIONS: RegionDef[] = [
  { id: 'peaks', name: 'Neon Peaks', environment: 'mountain', blurb: 'Downhill speed. Learn the line.', mapX: 0.08, mapY: 0.62 },
  { id: 'glacier', name: 'Glacier Grid', environment: 'glacier', blurb: 'Frictionless shelves and an avalanche.', mapX: 0.22, mapY: 0.32 },
  { id: 'dunes', name: 'Dune Circuit', environment: 'desert', blurb: 'Crosswinds, cannons and fragile cargo.', mapX: 0.36, mapY: 0.66 },
  { id: 'forest', name: 'Data Forest', environment: 'forest', blurb: 'Springy canopies and stranded techs.', mapX: 0.5, mapY: 0.38 },
  { id: 'skyline', name: 'Skyline District', environment: 'rooftops', blurb: 'Precision ledges, rails and demolition.', mapX: 0.62, mapY: 0.7 },
  { id: 'undercity', name: 'Undercity', environment: 'cave', blurb: 'Darkness, magnets and a collapsing tower.', mapX: 0.74, mapY: 0.4 },
  { id: 'lunar', name: 'Lunar Relay', environment: 'moon', blurb: 'Low gravity, big air, a rolling moonball.', mapX: 0.86, mapY: 0.66 },
  { id: 'machine', name: 'The Machine', environment: 'machine', blurb: 'Gears, belts, pendulums and the core.', mapX: 0.94, mapY: 0.3 },
];

// ------------------------------------------------------------------ helpers

const line = (x1: number, y1: number, x2: number, y2: number, material?: MaterialId, flipped?: boolean): LevelLine => ({
  x1,
  y1,
  x2,
  y2,
  material,
  flipped,
});

/** Connected polyline; draw floors left to right so the top face is solid. */
function poly(points: [number, number][], material?: MaterialId): LevelLine[] {
  const out: LevelLine[] = [];
  for (let i = 1; i < points.length; i++) {
    out.push(line(points[i - 1][0], points[i - 1][1], points[i][0], points[i][1], material));
  }
  return out;
}

/**
 * A pillar: rideable top, walls solid from the outside. The walls start a little below the top so
 * their collision zones never overlap the surface the rider travels along.
 */
function box(x: number, y: number, w: number, h: number, material?: MaterialId): LevelLine[] {
  const inset = 12;
  return [
    line(x, y, x + w, y, material),
    line(x + w, y + inset, x + w, y + h, material),
    line(x + w, y + h, x, y + h, material),
    line(x, y + h, x, y + inset, material),
  ];
}

/** Wall segment solid on its left face (drawn bottom to top). */
function wallLeftFace(x: number, yTop: number, yBottom: number, material?: MaterialId): LevelLine {
  return line(x, yBottom, x, yTop, material);
}

const START_LEDGE = poly([
  [-40, 6],
  [70, 12],
]);

const finish = (x: number, y: number, w = 44, h = 70) => ({ x, y, w, h });

// ------------------------------------------------------------------ levels

export const LEVELS: LevelDef[] = [
  // ---------------------------------------------------------- Neon Peaks
  {
    id: 'peaks-1',
    name: 'First Descent',
    region: 'peaks',
    environment: 'mountain',
    mode: 'reach',
    tagline: 'Bridge the gap. Reach the glow.',
    briefing:
      'Bosh starts on the ledge. The valley floor is down and to the right. Draw a line to carry him across the gap. Lines are one-sided: draw left to right so the top face is solid.',
    budget: 45,
    materials: ['normal'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(560, 156, 44, 70),
    lines: [
      ...START_LEDGE,
      ...poly([
        [240, 150],
        [470, 226],
        [700, 226],
        [760, 200],
      ]),
      wallLeftFace(760, 100, 200),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 30, optional: true },
      { kind: 'noDeath', optional: true },
      { kind: 'timeUnder', value: 300, optional: true },
    ],
    zoom: 1.6,
    focus: { x: 300, y: 100 },
  },
  {
    id: 'peaks-2',
    name: 'The Canyon',
    region: 'peaks',
    environment: 'mountain',
    mode: 'reach',
    tagline: 'Speed first. Then the jump.',
    briefing:
      'A steep chute feeds a plateau that ends at a canyon. Shape a kicker at the edge so Bosh clears the drop, and give him somewhere soft to land.',
    budget: 40,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(760, 20, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [60, 30],
        [200, 140],
        [360, 170],
        [420, 170],
      ]),
      ...poly([
        [640, 150],
        [700, 100],
        [760, 90],
        [900, 90],
      ]),
      wallLeftFace(900, -20, 90),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'airtime', value: 40, optional: true },
      { kind: 'inkUnder', value: 25, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.4,
    focus: { x: 400, y: 80 },
  },
  {
    id: 'peaks-3',
    name: 'Three Flags',
    region: 'peaks',
    environment: 'mountain',
    mode: 'flags',
    tagline: 'Detours pay.',
    briefing:
      'The main slope runs straight to the finish, but three flags hang off it. Build detours that grab every flag and still get Bosh home. Completing this unlocks Vex.',
    budget: 110,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1180, 250, 44, 70),
    flags: [
      { x: 330, y: 60 },
      { x: 700, y: 260 },
      { x: 980, y: 140 },
    ],
    lines: [
      ...START_LEDGE,
      ...poly([
        [140, 60],
        [260, 110],
        [400, 150],
      ]),
      ...poly([
        [520, 200],
        [620, 300],
        [820, 300],
        [900, 280],
      ]),
      ...poly([
        [1040, 300],
        [1100, 320],
        [1300, 320],
      ]),
      wallLeftFace(1300, 200, 320),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'flags', value: 3 },
      { kind: 'noDeath', optional: true },
      { kind: 'timeUnder', value: 700, optional: true },
    ],
    zoom: 1.1,
    focus: { x: 600, y: 160 },
  },
  {
    id: 'peaks-4',
    name: 'Big Air',
    region: 'peaks',
    environment: 'mountain',
    mode: 'stunt',
    tagline: 'Send it. Land it.',
    briefing:
      'The chute below is long and fast. Build a kicker, get Bosh upside down, and bring him back onto the landing. A flip only counts once he lands on the board.',
    budget: 70,
    materials: ['normal', 'accel', 'booster'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1100, 330, 50, 80),
    lines: [
      ...poly([
        [-40, 6],
        [80, 40],
        [260, 210],
        [420, 260],
        [520, 262],
      ]),
      ...poly([
        [760, 300],
        [900, 400],
        [1200, 410],
      ]),
      wallLeftFace(1200, 300, 410),
    ],
    objectives: [
      { kind: 'flips', value: 1 },
      { kind: 'finish' },
      { kind: 'airtime', value: 80, optional: true },
      { kind: 'trickScore', value: 1200, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.2,
    focus: { x: 550, y: 200 },
  },

  // ---------------------------------------------------------- Glacier Grid
  {
    id: 'glacier-1',
    name: 'Zero Friction',
    region: 'glacier',
    environment: 'glacier',
    mode: 'reach',
    tagline: 'Nothing slows down here.',
    briefing:
      'Every surface on the glacier is frictionless. Speed only ever grows. Get Bosh from the shelf to the finish without letting the speed turn into a crash.',
    budget: 60,
    materials: ['normal', 'ice', 'sticky'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(900, 260, 44, 70),
    lines: [
      ...poly(
        [
          [-40, 6],
          [120, 40],
          [300, 130],
          [380, 140],
        ],
        'ice',
      ),
      ...poly(
        [
          [560, 220],
          [720, 330],
          [1000, 330],
        ],
        'ice',
      ),
      wallLeftFace(1000, 230, 330, 'ice'),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 35, optional: true },
      { kind: 'timeUnder', value: 320, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.3,
    focus: { x: 450, y: 150 },
  },
  {
    id: 'glacier-2',
    name: 'Crevasse',
    region: 'glacier',
    environment: 'glacier',
    mode: 'puzzle',
    tagline: 'Fifty metres of ice. One crevasse.',
    briefing:
      'A deep crevasse splits the shelf. You have very little ink. Crumble lines are cheap but vanish after a touch; use them where Bosh only needs a moment of support. Completing this unlocks Tank.',
    budget: 50,
    materials: ['normal', 'crumble', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1020, 60, 44, 70),
    lines: [
      ...poly(
        [
          [-40, 6],
          [200, 40],
          [340, 60],
        ],
        'ice',
      ),
      wallLeftFace(340, 60, 400, 'ice'),
      ...poly(
        [
          [660, 400],
          [660, 120],
        ],
        'ice',
      ),
      ...poly(
        [
          [660, 120],
          [800, 130],
          [1100, 130],
        ],
        'ice',
      ),
      wallLeftFace(1100, 30, 130, 'ice'),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 38, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.2,
    focus: { x: 500, y: 100 },
  },
  {
    id: 'glacier-3',
    name: 'Sticky Situation',
    region: 'glacier',
    environment: 'glacier',
    mode: 'puzzle',
    tagline: 'Arrive slow or not at all.',
    briefing:
      'The finish sits on a narrow ledge at the bottom of a sheer drop with a wall behind it. Bosh will arrive screaming fast. Use sticky and spring surfaces to bleed speed before the ledge.',
    budget: 55,
    materials: ['normal', 'sticky', 'spring', 'mud'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(700, 350, 40, 60),
    lines: [
      ...poly(
        [
          [-40, 6],
          [200, 120],
          [420, 260],
          [480, 270],
        ],
        'ice',
      ),
      ...poly([
        [600, 410],
        [760, 410],
      ]),
      wallLeftFace(760, 300, 410),
      line(760, 300, 760, 410, 'normal', true),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'inkUnder', value: 40, optional: true },
    ],
    zoom: 1.3,
    focus: { x: 400, y: 200 },
  },
  {
    id: 'glacier-4',
    name: 'Avalanche',
    region: 'glacier',
    environment: 'glacier',
    mode: 'boss',
    tagline: 'It is faster than you think.',
    briefing:
      'The whole shelf is coming down behind you. Bridge the gaps before you press play, then keep Bosh moving. Accel lines are your friend; sticky patches are not. If the wall catches him, it is over.',
    budget: 120,
    materials: ['normal', 'accel', 'ice', 'booster'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1900, 380, 50, 90),
    flags: [
      { x: 620, y: 150 },
      { x: 1300, y: 300 },
    ],
    lines: [
      ...poly(
        [
          [-40, 6],
          [200, 60],
          [400, 110],
          [500, 120],
        ],
        'ice',
      ),
      ...poly(
        [
          [700, 180],
          [900, 220],
          [1000, 230],
        ],
        'ice',
      ),
      ...poly(
        [
          [1200, 320],
          [1450, 360],
          [1550, 370],
        ],
        'ice',
      ),
      ...poly(
        [
          [1750, 470],
          [2100, 470],
        ],
        'ice',
      ),
      wallLeftFace(2100, 360, 470, 'ice'),
    ],
    entities: [{ type: 'avalanche', startX: -900, speed: 6.2, delay: 30, accel: 0.004 }],
    objectives: [
      { kind: 'finish' },
      { kind: 'flags', value: 2, optional: true },
      { kind: 'inkUnder', value: 90, optional: true },
    ],
    zoom: 1,
    focus: { x: 900, y: 220 },
  },

  // ---------------------------------------------------------- Dune Circuit
  {
    id: 'dunes-1',
    name: 'Crosswind',
    region: 'dunes',
    environment: 'desert',
    mode: 'reach',
    tagline: 'The air has opinions.',
    briefing:
      'Gusts sweep across the dunes and shove the rider whenever he leaves the ground. Keep jumps short or aim them into the wind. Watch the streaks: they show which way it blows.',
    budget: 70,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1000, 210, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [120, 60],
        [260, 100],
        [340, 100],
      ]),
      ...poly([
        [480, 160],
        [600, 180],
        [640, 180],
      ]),
      ...poly([
        [800, 240],
        [960, 280],
        [1100, 280],
      ]),
      wallLeftFace(1100, 180, 280),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'inkUnder', value: 50, optional: true },
    ],
    zoom: 1.2,
    focus: { x: 500, y: 140 },
  },
  {
    id: 'dunes-2',
    name: 'Cannon Run',
    region: 'dunes',
    environment: 'desert',
    mode: 'reach',
    tagline: 'Ride in. Fly out.',
    briefing:
      'A cannon sits at the bottom of the first dune. Roll Bosh into it and it fires him at 40 degrees. Your ink goes on the far side: he needs a landing, and the finish is above the mesa.',
    budget: 60,
    materials: ['normal', 'accel', 'spring'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1040, 30, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [100, 60],
        [220, 120],
        [300, 130],
      ]),
      ...poly([
        [780, 100],
        [1000, 100],
        [1150, 100],
      ]),
      wallLeftFace(1150, 0, 100),
      ...poly([
        [300, 130],
        [400, 220],
        [700, 220],
      ]),
    ],
    entities: [{ type: 'cannon', x: 330, y: 122, angle: -40, power: 11 }],
    objectives: [
      { kind: 'finish' },
      { kind: 'airtime', value: 60, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.2,
    focus: { x: 550, y: 100 },
  },
  {
    id: 'dunes-3',
    name: 'Express Delivery',
    region: 'dunes',
    environment: 'desert',
    mode: 'delivery',
    tagline: 'Handle with care.',
    briefing:
      'A fragile crate is strapped to the back of the board. Every hard landing and jolt chips at it, and a violent enough hit tears it off entirely. Smooth curves beat fast drops.',
    budget: 120,
    materials: ['normal', 'mud', 'sticky'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1100, 300, 50, 80),
    lines: [
      ...poly([
        [-40, 6],
        [160, 40],
        [300, 60],
      ]),
      ...poly([
        [560, 200],
        [700, 240],
        [820, 250],
      ]),
      ...poly([
        [980, 370],
        [1220, 380],
      ]),
      wallLeftFace(1220, 280, 380),
    ],
    objectives: [
      { kind: 'cargo', value: 50 },
      { kind: 'cargo', value: 85, optional: true },
      { kind: 'timeUnder', value: 600, optional: true },
    ],
    zoom: 1.2,
    focus: { x: 600, y: 200 },
  },
  {
    id: 'dunes-4',
    name: 'Rockslide',
    region: 'dunes',
    environment: 'desert',
    mode: 'boss',
    tagline: 'The cliff is letting go.',
    briefing:
      'Crossing the marker triggers a rockfall from the cliff above. Rocks roll, bounce and follow gravity like everything else. Build your route so the rocks miss him, or so he is long gone.',
    budget: 100,
    materials: ['normal', 'accel', 'spring'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1200, 300, 50, 80),
    lines: [
      ...poly([
        [-40, 6],
        [120, 50],
        [260, 110],
        [380, 140],
        [500, 150],
      ]),
      ...poly([
        [500, 150],
        [700, 260],
        [900, 330],
        [1000, 350],
        [1300, 380],
      ]),
      wallLeftFace(1300, 280, 380),
      ...poly([
        [560, -80],
        [780, -40],
      ]),
    ],
    entities: [{ type: 'rockfall', x: 680, y: -70, count: 5, spread: 30, trigger: 420, radius: 8 }],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'timeUnder', value: 500, optional: true },
    ],
    zoom: 1.1,
    focus: { x: 650, y: 150 },
  },

  // ---------------------------------------------------------- Data Forest
  {
    id: 'forest-1',
    name: 'Canopy Bounce',
    region: 'forest',
    environment: 'forest',
    mode: 'reach',
    tagline: 'The trees push back.',
    briefing:
      'Spring canopies fling the rider back the way he came in. Angle your drop so the bounce sends Bosh up and to the right, and be ready for the second one.',
    budget: 60,
    materials: ['normal', 'spring'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(860, 40, 44, 70),
    lines: [
      ...START_LEDGE,
      ...poly(
        [
          [220, 240],
          [340, 200],
        ],
        'spring',
      ),
      ...poly(
        [
          [560, 260],
          [680, 220],
        ],
        'spring',
      ),
      ...poly([
        [800, 110],
        [960, 110],
      ]),
      wallLeftFace(960, 10, 110),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'airtime', value: 100, optional: true },
      { kind: 'inkUnder', value: 40, optional: true },
    ],
    zoom: 1.3,
    focus: { x: 450, y: 120 },
  },
  {
    id: 'forest-2',
    name: 'Search Party',
    region: 'forest',
    environment: 'forest',
    mode: 'rescue',
    tagline: 'Nobody gets left behind.',
    briefing:
      'Three technicians are stranded in the canopy. Bosh has to pass close enough to scoop each one up, then reach the finish with everyone aboard. Reaching them means leaving the main line and reconnecting later.',
    budget: 150,
    materials: ['normal', 'accel', 'spring'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1300, 320, 50, 80),
    rescues: [
      { x: 380, y: 40 },
      { x: 720, y: 330 },
      { x: 1040, y: 180 },
    ],
    lines: [
      ...START_LEDGE,
      ...poly([
        [200, 120],
        [320, 160],
        [480, 190],
      ]),
      ...poly([
        [640, 340],
        [800, 340],
      ]),
      ...poly([
        [960, 240],
        [1100, 280],
      ]),
      ...poly([
        [1240, 400],
        [1420, 400],
      ]),
      wallLeftFace(1420, 300, 400),
    ],
    objectives: [
      { kind: 'rescue', value: 3 },
      { kind: 'noDeath', optional: true },
      { kind: 'inkUnder', value: 110, optional: true },
    ],
    zoom: 1.05,
    focus: { x: 700, y: 200 },
  },
  {
    id: 'forest-3',
    name: 'Balance',
    region: 'forest',
    environment: 'forest',
    mode: 'puzzle',
    tagline: 'Weight tips the beam.',
    briefing:
      'A seesaw bridges the ravine and a balloon waits beyond it. The beam tilts under Bosh, so where he lands decides where he leaves. Grab the balloon to float up to the finish.',
    budget: 45,
    materials: ['normal'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(900, 60, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [120, 60],
        [200, 80],
      ]),
      ...poly([
        [560, 220],
        [680, 230],
      ]),
      ...poly([
        [860, 130],
        [1000, 130],
      ]),
      wallLeftFace(1000, 30, 130),
    ],
    entities: [
      { type: 'seesaw', x: 380, y: 140, length: 160 },
      { type: 'balloon', x: 760, y: 200, lift: 0.34, duration: 120 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 30, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.3,
    focus: { x: 500, y: 120 },
  },
  {
    id: 'forest-4',
    name: 'Updraft',
    region: 'forest',
    environment: 'forest',
    mode: 'reach',
    tagline: 'Ride the fans.',
    briefing:
      'Vent fans blow straight up through the canopy. Drop Bosh into a column and he rises; leave it and gravity takes over. Chain the columns to reach the finish high on the right.',
    budget: 70,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(940, -140, 44, 70),
    lines: [
      ...START_LEDGE,
      ...poly([
        [180, 160],
        [300, 200],
        [420, 210],
      ]),
      ...poly([
        [900, -70],
        [1060, -70],
      ]),
      wallLeftFace(1060, -170, -70),
    ],
    entities: [
      { type: 'fan', x: 430, y: -200, w: 90, h: 420, fx: 0.02, fy: -0.42 },
      { type: 'fan', x: 700, y: -260, w: 90, h: 420, fx: 0.03, fy: -0.42 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'airtime', value: 160, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.1,
    focus: { x: 550, y: 0 },
  },

  // ---------------------------------------------------------- Skyline
  {
    id: 'roof-1',
    name: 'Ledge Runner',
    region: 'skyline',
    environment: 'rooftops',
    mode: 'puzzle',
    tagline: 'Twenty-five metres. Make them count.',
    briefing:
      'Rooftop to rooftop with almost no ink. Each ledge is a little lower than the last, so momentum does most of the work if the exits are clean.',
    budget: 25,
    materials: ['normal'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(760, 130, 40, 60),
    lines: [
      ...poly([
        [-40, 6],
        [140, 30],
      ]),
      ...poly([
        [220, 70],
        [360, 80],
      ]),
      ...poly([
        [440, 120],
        [580, 130],
      ]),
      ...poly([
        [660, 190],
        [860, 190],
      ]),
      wallLeftFace(860, 90, 190),
      ...box(140, 30, 40, 400),
      ...box(360, 80, 40, 400),
      ...box(580, 130, 40, 400),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 18, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.4,
    focus: { x: 400, y: 100 },
  },
  {
    id: 'roof-2',
    name: 'Grind City',
    region: 'skyline',
    environment: 'rooftops',
    mode: 'stunt',
    tagline: 'Rails score. Ledges do not.',
    briefing:
      'Grind rails are frictionless and every second on one pays out. Link the rails with your ink, keep Bosh on them, and reach the finish with a trick score to match. Completing this unlocks Nova.',
    budget: 80,
    materials: ['normal', 'rail', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1200, 260, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [120, 40],
      ]),
      ...poly(
        [
          [200, 80],
          [420, 110],
        ],
        'rail',
      ),
      ...poly(
        [
          [520, 150],
          [780, 200],
        ],
        'rail',
      ),
      ...poly(
        [
          [860, 240],
          [1060, 300],
        ],
        'rail',
      ),
      ...poly([
        [1120, 330],
        [1300, 330],
      ]),
      wallLeftFace(1300, 230, 330),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'trickScore', value: 600 },
      { kind: 'trickScore', value: 1500, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.1,
    focus: { x: 600, y: 160 },
  },
  {
    id: 'roof-3',
    name: 'Train Hop',
    region: 'skyline',
    environment: 'rooftops',
    mode: 'reach',
    tagline: 'Timing is a material too.',
    briefing:
      'Elevated trains shuttle across the gap. They carry whatever lands on them. Drop Bosh onto a roof at the right moment and let the train do the travelling.',
    budget: 50,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1000, 120, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [160, 40],
        [240, 50],
      ]),
      ...poly([
        [960, 190],
        [1120, 190],
      ]),
      wallLeftFace(1120, 90, 190),
    ],
    entities: [
      { type: 'train', x: 380, y: 160, w: 120, h: 30, speed: 2.4, range: 460 },
      { type: 'train', x: 800, y: 260, w: 100, h: 30, speed: -1.8, range: 380 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 30, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.2,
    focus: { x: 550, y: 120 },
  },
  {
    id: 'roof-4',
    name: 'Demolition',
    region: 'skyline',
    environment: 'rooftops',
    mode: 'destruction',
    tagline: 'Maximum chaos.',
    briefing:
      'The rooftop is stacked with dominoes, crates and explosives. Draw a track, launch Bosh, and see how much of it you can bring down. Chaos is scored by everything you knock loose or blow up.',
    budget: 90,
    materials: ['normal', 'accel', 'booster'],
    rider: null,
    start: { x: 0, y: 0 },
    lines: [
      ...poly([
        [-40, 6],
        [100, 40],
        [220, 120],
      ]),
      ...poly([
        [220, 120],
        [1000, 120],
      ]),
      wallLeftFace(1000, -100, 120),
      ...poly([
        [1000, 120],
        [1300, 120],
      ]),
    ],
    props: [
      { kind: 'domino', x: 320, y: 108, width: 4, height: 24, mass: 0.5 },
      { kind: 'domino', x: 345, y: 108, width: 4, height: 24, mass: 0.5 },
      { kind: 'domino', x: 370, y: 108, width: 4, height: 24, mass: 0.5 },
      { kind: 'domino', x: 395, y: 108, width: 4, height: 24, mass: 0.5 },
      { kind: 'domino', x: 420, y: 108, width: 4, height: 24, mass: 0.5 },
      { kind: 'crate', x: 520, y: 110, width: 20, height: 20, mass: 1.5 },
      { kind: 'crate', x: 520, y: 90, width: 20, height: 20, mass: 1.5 },
      { kind: 'crate', x: 542, y: 110, width: 20, height: 20, mass: 1.5 },
      { kind: 'tnt', x: 640, y: 111, width: 18, height: 18, mass: 1, explosive: true },
      { kind: 'crate', x: 700, y: 110, width: 20, height: 20, mass: 1.5 },
      { kind: 'crate', x: 700, y: 90, width: 20, height: 20, mass: 1.5 },
      { kind: 'crate', x: 700, y: 70, width: 20, height: 20, mass: 1.5 },
      { kind: 'tnt', x: 800, y: 111, width: 18, height: 18, mass: 1, explosive: true },
      { kind: 'boulder', x: 900, y: 106, radius: 14, mass: 6 },
      { kind: 'domino', x: 960, y: 108, width: 4, height: 24, mass: 0.5 },
    ],
    objectives: [
      { kind: 'chaos', value: 1500 },
      { kind: 'chaos', value: 3500, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    surviveFrames: 0,
    timeLimit: 30,
    zoom: 1.1,
    focus: { x: 550, y: 60 },
  },

  // ---------------------------------------------------------- Undercity
  {
    id: 'cave-1',
    name: 'Blackout',
    region: 'undercity',
    environment: 'cave',
    mode: 'reach',
    tagline: 'Draw from memory.',
    briefing:
      'Only a small halo of light follows the rider. Scout the cave while editing, then trust the lines you drew. The finish glows, faintly, down and to the right.',
    budget: 80,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(900, 300, 44, 70),
    lines: [
      ...START_LEDGE,
      ...poly([
        [200, 120],
        [340, 160],
        [420, 170],
      ]),
      ...poly([
        [600, 250],
        [740, 300],
      ]),
      ...poly([
        [860, 370],
        [1000, 370],
      ]),
      wallLeftFace(1000, 270, 370),
      // Stalactite hazards drawn top-down (solid underneath).
      line(500, 60, 520, 200),
      line(520, 200, 540, 60),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'inkUnder', value: 55, optional: true },
    ],
    zoom: 1.2,
    focus: { x: 450, y: 180 },
  },
  {
    id: 'cave-2',
    name: 'Crumbling Depths',
    region: 'undercity',
    environment: 'cave',
    mode: 'reach',
    tagline: 'Everything here is temporary.',
    briefing:
      'The old bridges give way segment by segment once touched. Keep moving, or draw your own way over them. Crumble ink is cheap if you only need a moment.',
    budget: 90,
    materials: ['normal', 'crumble', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1100, 260, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [120, 50],
        [200, 70],
      ]),
      ...poly([
        [620, 200],
        [720, 220],
      ]),
      ...poly([
        [1080, 330],
        [1200, 330],
      ]),
      wallLeftFace(1200, 230, 330),
    ],
    entities: [
      { type: 'bridge', x: 200, y: 70, w: 420, segments: 7, delay: 8 },
      { type: 'bridge', x: 720, y: 220, w: 360, segments: 6, delay: 6 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 40, optional: true },
      { kind: 'timeUnder', value: 420, optional: true },
    ],
    zoom: 1.1,
    focus: { x: 600, y: 180 },
  },
  {
    id: 'cave-3',
    name: 'Magnet Mine',
    region: 'undercity',
    environment: 'cave',
    mode: 'puzzle',
    tagline: 'Attraction is not optional.',
    briefing:
      'Magnetic cores pull the rider toward them within their field. One sits above the chasm: pass under it fast enough and it bends the arc instead of swallowing it.',
    budget: 40,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(860, 180, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [160, 80],
        [300, 140],
        [360, 150],
      ]),
      ...poly([
        [800, 250],
        [960, 250],
      ]),
      wallLeftFace(960, 150, 250),
    ],
    entities: [
      { type: 'magnet', x: 580, y: 40, radius: 190, strength: 0.09 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 25, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1.3,
    focus: { x: 500, y: 120 },
  },
  {
    id: 'cave-4',
    name: 'Clock Tower',
    region: 'undercity',
    environment: 'cave',
    mode: 'boss',
    tagline: 'Descend before it falls.',
    briefing:
      'The tower is coming apart on a timer. Floors drop in order from the top, gears keep turning. Get Bosh from the belfry down to the base before his floor goes.',
    budget: 100,
    materials: ['normal', 'accel', 'crumble'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(560, 640, 50, 80),
    lines: [
      ...poly([
        [-40, 6],
        [80, 20],
        [160, 30],
      ]),
      wallLeftFace(700, 0, 720),
      line(-120, 0, -120, 720),
      ...poly([
        [-120, 720],
        [700, 720],
      ]),
    ],
    entities: [
      { type: 'collapse', x1: 160, y1: 30, x2: 560, y2: 60, at: 140 },
      { type: 'collapse', x1: 460, y1: 200, x2: -20, y2: 230, at: 260 },
      { type: 'collapse', x1: 60, y1: 380, x2: 560, y2: 400, at: 400 },
      { type: 'gear', x: 300, y: 300, radius: 50, teeth: 8, speed: 0.02 },
      { type: 'gear', x: 460, y: 520, radius: 40, teeth: 7, speed: -0.03 },
      { type: 'collapse', x1: 560, y1: 560, x2: 200, y2: 580, at: 560 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'timeUnder', value: 560, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 0.9,
    focus: { x: 280, y: 360 },
  },

  // ---------------------------------------------------------- Lunar Relay
  {
    id: 'moon-1',
    name: 'One Small Jump',
    region: 'lunar',
    environment: 'moon',
    mode: 'reach',
    tagline: 'Gravity at forty percent.',
    briefing:
      'The moon pulls at less than half strength. Jumps go much further and everything floats, so lines that would be too short on Earth are suddenly generous.',
    budget: 40,
    materials: ['normal'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1100, 120, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [160, 60],
        [300, 100],
      ]),
      ...poly([
        [1000, 190],
        [1200, 190],
      ]),
      wallLeftFace(1200, 90, 190),
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'inkUnder', value: 20, optional: true },
      { kind: 'airtime', value: 100, optional: true },
    ],
    zoom: 1.1,
    focus: { x: 600, y: 100 },
  },
  {
    id: 'moon-2',
    name: 'Orbital',
    region: 'lunar',
    environment: 'moon',
    mode: 'stunt',
    tagline: 'Two flips. Take your time.',
    briefing:
      'With this much hang time a single kicker can produce a double. Build the ramp, land two rotations, and bring him to the finish with the score to prove it.',
    budget: 80,
    materials: ['normal', 'accel', 'booster'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1300, 260, 50, 80),
    lines: [
      ...poly([
        [-40, 6],
        [120, 60],
        [360, 220],
        [520, 250],
        [620, 252],
      ]),
      ...poly([
        [1000, 300],
        [1200, 340],
        [1400, 340],
      ]),
      wallLeftFace(1400, 240, 340),
    ],
    objectives: [
      { kind: 'flips', value: 2 },
      { kind: 'finish' },
      { kind: 'trickScore', value: 2500, optional: true },
      { kind: 'noDeath', optional: true },
    ],
    zoom: 1,
    focus: { x: 700, y: 200 },
  },
  {
    id: 'moon-3',
    name: 'Regolith Rescue',
    region: 'lunar',
    environment: 'moon',
    mode: 'rescue',
    tagline: 'Two survivors, one balloon.',
    briefing:
      'Two crew members are stranded on crater rims. Balloons drift near each; grab one to float up and collect the survivor, then find a way back down to the finish.',
    budget: 120,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1200, 260, 50, 80),
    rescues: [
      { x: 460, y: -60 },
      { x: 900, y: 60 },
    ],
    lines: [
      ...poly([
        [-40, 6],
        [140, 60],
        [300, 100],
      ]),
      ...poly([
        [420, -50],
        [520, -50],
      ]),
      ...poly([
        [600, 180],
        [760, 200],
      ]),
      ...poly([
        [860, 70],
        [960, 70],
      ]),
      ...poly([
        [1120, 340],
        [1320, 340],
      ]),
      wallLeftFace(1320, 240, 340),
    ],
    entities: [
      { type: 'balloon', x: 380, y: 60, lift: 0.14, duration: 110 },
      { type: 'balloon', x: 820, y: 170, lift: 0.14, duration: 110 },
    ],
    objectives: [
      { kind: 'rescue', value: 2 },
      { kind: 'noDeath', optional: true },
      { kind: 'inkUnder', value: 80, optional: true },
    ],
    zoom: 1,
    focus: { x: 650, y: 120 },
  },
  {
    id: 'moon-4',
    name: 'Moonball',
    region: 'lunar',
    environment: 'moon',
    mode: 'boss',
    tagline: 'It only ever rolls toward you.',
    briefing:
      'A colossal boulder is balanced above the crater rim. Cross the marker and it drops, then it keeps coming. Outrun it, jump it, or make it fall somewhere it cannot follow.',
    budget: 130,
    materials: ['normal', 'accel', 'booster', 'spring'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1800, 380, 50, 90),
    lines: [
      ...poly([
        [-40, 6],
        [160, 60],
        [400, 160],
        [600, 220],
        [800, 260],
        [1000, 300],
      ]),
      ...poly([
        [1200, 380],
        [1400, 430],
        [1500, 450],
      ]),
      ...poly([
        [1650, 470],
        [1950, 470],
      ]),
      wallLeftFace(1950, 370, 470),
      ...poly([
        [-140, -60],
        [-60, -40],
      ]),
    ],
    entities: [{ type: 'snowball', x: -100, y: -100, radius: 34, trigger: 200, push: 0.09 }],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'inkUnder', value: 100, optional: true },
    ],
    zoom: 0.9,
    focus: { x: 900, y: 250 },
  },

  // ---------------------------------------------------------- The Machine
  {
    id: 'machine-1',
    name: 'Gearbox',
    region: 'machine',
    environment: 'machine',
    mode: 'reach',
    tagline: 'Teeth carry. Teeth also bite.',
    briefing:
      'Spinning gears fill the chamber. Ride their rims in the direction they turn and they throw the board forward; land against the turn and they eat the speed.',
    budget: 70,
    materials: ['normal', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1000, 200, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [120, 60],
        [200, 80],
      ]),
      ...poly([
        [940, 270],
        [1100, 270],
      ]),
      wallLeftFace(1100, 170, 270),
    ],
    entities: [
      { type: 'gear', x: 380, y: 200, radius: 70, teeth: 10, speed: 0.025 },
      { type: 'gear', x: 660, y: 300, radius: 90, teeth: 12, speed: 0.02 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'inkUnder', value: 45, optional: true },
    ],
    zoom: 1.1,
    focus: { x: 550, y: 150 },
  },
  {
    id: 'machine-2',
    name: 'Belt Drive',
    region: 'machine',
    environment: 'machine',
    mode: 'reach',
    tagline: 'Some belts run the wrong way.',
    briefing:
      'Conveyor surfaces drive the board toward a set speed along their arrow. Pistons rise and fall between them. Use the belts that help, jump the ones that do not.',
    budget: 60,
    materials: ['normal', 'conveyor', 'accel'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1200, 90, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [120, 60],
      ]),
      ...poly(
        [
          [200, 100],
          [440, 100],
        ],
        'conveyor',
      ),
      ...poly(
        [
          [820, 150],
          [600, 150],
        ],
        'conveyor',
      ),
      ...poly([
        [1160, 160],
        [1300, 160],
      ]),
      wallLeftFace(1300, 60, 160),
    ],
    entities: [
      { type: 'platform', x: 520, y: 130, w: 60, dx: 0, dy: 40, period: 160 },
      { type: 'platform', x: 980, y: 200, w: 70, dx: 0, dy: 60, period: 200, phase: 1.5 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'timeUnder', value: 500, optional: true },
    ],
    zoom: 1.1,
    focus: { x: 650, y: 100 },
  },
  {
    id: 'machine-3',
    name: 'Pendulum Hall',
    region: 'machine',
    environment: 'machine',
    mode: 'reach',
    tagline: 'Swing through.',
    briefing:
      'Three hammers swing across the hall on different periods. Their heads are solid and moving; a hit from the side is a crash, a landing on top is a lift. Time your run.',
    budget: 80,
    materials: ['normal', 'accel', 'booster'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1200, 200, 44, 70),
    lines: [
      ...poly([
        [-40, 6],
        [140, 60],
        [260, 100],
      ]),
      ...poly([
        [1140, 270],
        [1300, 270],
      ]),
      wallLeftFace(1300, 170, 270),
    ],
    entities: [
      { type: 'pendulum', x: 480, y: -80, length: 220, width: 40, amplitude: 0.9, period: 180 },
      { type: 'pendulum', x: 760, y: -80, length: 260, width: 40, amplitude: 0.8, period: 220, phase: 2 },
      { type: 'pendulum', x: 1020, y: -60, length: 240, width: 40, amplitude: 0.7, period: 160, phase: 1 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'nearMiss', value: 2, optional: true },
    ],
    zoom: 1,
    focus: { x: 650, y: 100 },
  },
  {
    id: 'machine-4',
    name: 'The Core',
    region: 'machine',
    environment: 'machine',
    mode: 'boss',
    tagline: 'Everything at once.',
    briefing:
      'The core turns a wheel the size of a building. Gears, belts and a collapsing gantry surround it. Build a path around the machine, keep Bosh out of the teeth, and reach the exit before the gantry is gone.',
    budget: 160,
    materials: ['normal', 'accel', 'booster', 'conveyor', 'crumble'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: finish(1700, 420, 60, 100),
    lines: [
      ...poly([
        [-40, 6],
        [160, 60],
        [300, 90],
      ]),
      ...poly([
        [1400, 480],
        [1600, 520],
        [1900, 520],
      ]),
      wallLeftFace(1900, 400, 520),
      ...poly(
        [
          [1100, 380],
          [1300, 380],
        ],
        'conveyor',
      ),
    ],
    entities: [
      { type: 'gear', x: 800, y: 280, radius: 200, teeth: 20, speed: 0.008 },
      { type: 'gear', x: 420, y: 330, radius: 60, teeth: 8, speed: -0.03 },
      { type: 'gear', x: 1180, y: 120, radius: 70, teeth: 9, speed: 0.03 },
      { type: 'collapse', x1: 300, y1: 90, x2: 560, y2: 60, at: 200 },
      { type: 'platform', x: 1000, y: 470, w: 80, dx: 0, dy: 50, period: 180 },
      { type: 'bridge', x: 1300, y: 380, w: 100, segments: 4, delay: 10 },
    ],
    objectives: [
      { kind: 'finish' },
      { kind: 'noDeath', optional: true },
      { kind: 'timeUnder', value: 900, optional: true },
      { kind: 'inkUnder', value: 120, optional: true },
    ],
    zoom: 0.8,
    focus: { x: 850, y: 280 },
  },
];

export function levelById(id: string): LevelDef | undefined {
  return LEVELS.find((l) => l.id === id);
}

export function nextLevel(id: string): LevelDef | null {
  const i = LEVELS.findIndex((l) => l.id === id);
  if (i < 0 || i >= LEVELS.length - 1) return null;
  return LEVELS[i + 1];
}
