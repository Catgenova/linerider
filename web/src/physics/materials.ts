export type MaterialId =
  | 'normal'
  | 'accel'
  | 'scenery'
  | 'ice'
  | 'mud'
  | 'spring'
  | 'sticky'
  | 'conveyor'
  | 'crumble'
  | 'booster'
  | 'oneway'
  | 'rail';

export interface Material {
  id: MaterialId;
  name: string;
  /** Core stroke colour. */
  color: string;
  /** Glow colour (usually the same hue, used for the halo). */
  glow: string;
  /** Ink cost per metre relative to a normal line. Scenery is free. */
  cost: number;
  /** Whether points collide with the line at all. */
  solid: boolean;
  /** Multiplies the point's own friction coefficient. */
  frictionScale: number;
  /** Velocity nudge along the line per contact iteration. */
  accel?: number;
  /** Target tangential speed the surface drives the point toward. */
  conveyorSpeed?: number;
  /** Bounce factor for spring surfaces. */
  restitution?: number;
  /** Per-frame velocity multiplier when touching (mud). */
  drag?: number;
  /** Fraction of tangential velocity removed per frame (sticky). */
  stickiness?: number;
  /** Frames after first contact until the line disappears. */
  crumbleFrames?: number;
  /** Only solid when the point moves along the line's arrow. */
  oneWay?: boolean;
  /** Counts as a grind surface for the trick system. */
  grind?: boolean;
  /** Draw direction arrows along the line. */
  arrows?: boolean;
  hotkey: string;
  description: string;
}

const M = (m: Material) => m;

export const MATERIALS: Record<MaterialId, Material> = {
  normal: M({
    id: 'normal',
    name: 'Track',
    color: '#39f6ff',
    glow: '#00c8ff',
    cost: 1,
    solid: true,
    frictionScale: 1,
    hotkey: '1',
    description: 'Standard neon rail. One-sided: solid on the side you drew from.',
  }),
  accel: M({
    id: 'accel',
    name: 'Accel',
    color: '#ff3d7f',
    glow: '#ff0055',
    cost: 1.5,
    solid: true,
    frictionScale: 1,
    accel: 0.1,
    arrows: true,
    hotkey: '2',
    description: 'Pushes the rider along the arrow on every contact.',
  }),
  scenery: M({
    id: 'scenery',
    name: 'Scenery',
    color: '#4a5080',
    glow: '#2a2f55',
    cost: 0,
    solid: false,
    frictionScale: 0,
    hotkey: '3',
    description: 'Decorative only. Free to draw, no collision.',
  }),
  ice: M({
    id: 'ice',
    name: 'Ice',
    color: '#c8fbff',
    glow: '#7ae8ff',
    cost: 1,
    solid: true,
    frictionScale: 0,
    hotkey: '4',
    description: 'Frictionless. Speed is kept but control is not.',
  }),
  mud: M({
    id: 'mud',
    name: 'Mud',
    color: '#ffa640',
    glow: '#ff7a00',
    cost: 1,
    solid: true,
    frictionScale: 3,
    drag: 0.9,
    hotkey: '5',
    description: 'Thick and slow. Bleeds speed on every touch.',
  }),
  spring: M({
    id: 'spring',
    name: 'Spring',
    color: '#c6ff4a',
    glow: '#8cff00',
    cost: 2,
    solid: true,
    frictionScale: 0.5,
    restitution: 0.9,
    hotkey: '6',
    description: 'Bounces the rider back off the surface.',
  }),
  sticky: M({
    id: 'sticky',
    name: 'Sticky',
    color: '#c65cff',
    glow: '#9b1cff',
    cost: 1.5,
    solid: true,
    frictionScale: 6,
    stickiness: 0.35,
    hotkey: '7',
    description: 'Grabs the board. Great for killing speed before a drop.',
  }),
  conveyor: M({
    id: 'conveyor',
    name: 'Conveyor',
    color: '#fff04a',
    glow: '#ffd000',
    cost: 2,
    solid: true,
    frictionScale: 1,
    conveyorSpeed: 6,
    arrows: true,
    hotkey: '8',
    description: 'Drives the rider toward a fixed belt speed, either direction.',
  }),
  crumble: M({
    id: 'crumble',
    name: 'Crumble',
    color: '#ff7a45',
    glow: '#ff3c00',
    cost: 0.75,
    solid: true,
    frictionScale: 1,
    crumbleFrames: 30,
    hotkey: '9',
    description: 'Shatters shortly after the first touch. Cheap but temporary.',
  }),
  booster: M({
    id: 'booster',
    name: 'Booster',
    color: '#ff2bd6',
    glow: '#ff00c8',
    cost: 3,
    solid: true,
    frictionScale: 0,
    accel: 0.35,
    arrows: true,
    hotkey: '0',
    description: 'A hard shove along the arrow. Expensive ink.',
  }),
  oneway: M({
    id: 'oneway',
    name: 'One-way',
    color: '#4dff9d',
    glow: '#00ff77',
    cost: 1.25,
    solid: true,
    frictionScale: 1,
    oneWay: true,
    arrows: true,
    hotkey: '-',
    description: 'Solid only while moving along the arrow. Pass through the other way.',
  }),
  rail: M({
    id: 'rail',
    name: 'Grind rail',
    color: '#f4f4ff',
    glow: '#b0b8ff',
    cost: 1.25,
    solid: true,
    frictionScale: 0,
    grind: true,
    hotkey: '=',
    description: 'Frictionless rail. Riding it scores grind points.',
  }),
};

export const MATERIAL_ORDER: MaterialId[] = [
  'normal',
  'accel',
  'scenery',
  'ice',
  'mud',
  'spring',
  'sticky',
  'conveyor',
  'crumble',
  'booster',
  'oneway',
  'rail',
];
