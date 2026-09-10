import type { EntityDef } from './entities';
import type { PropDef } from './level';
import type { LineInput } from './track';

export type ObjectKind =
  | 'spring'
  | 'ramp'
  | 'seesaw'
  | 'pendulum'
  | 'fanUp'
  | 'fanRight'
  | 'magnet'
  | 'cannon'
  | 'wall'
  | 'balloon'
  | 'platformV'
  | 'platformH'
  | 'gear'
  | 'train'
  | 'domino'
  | 'crate'
  | 'tnt'
  | 'boulder'
  | 'cart';

export interface Placement {
  entity?: EntityDef;
  prop?: PropDef;
  lines?: LineInput[];
}

export interface ObjectKindDef {
  id: ObjectKind;
  name: string;
  icon: string;
  group: 'toys' | 'hazards' | 'props';
  description: string;
  make: (x: number, y: number) => Placement;
}

/** Everything the player can drop into a free-ride track, with sensible default sizes. */
export const OBJECT_KINDS: ObjectKindDef[] = [
  { id: 'spring', name: 'Spring pad', icon: '⤒', group: 'toys', description: 'A bouncy surface.', make: (x, y) => ({ lines: [{ x1: x - 25, y1: y, x2: x + 25, y2: y, material: 'spring' }] }) },
  {
    id: 'ramp',
    name: 'Ramp',
    icon: '◢',
    group: 'toys',
    description: 'A kicker with a vertical back.',
    make: (x, y) => ({
      lines: [
        { x1: x - 40, y1: y, x2: x + 20, y2: y - 30, material: 'normal' },
        { x1: x + 20, y1: y - 30, x2: x + 20, y2: y, material: 'normal' },
      ],
    }),
  },
  { id: 'seesaw', name: 'Seesaw', icon: '⚖', group: 'toys', description: 'Tilts under weight.', make: (x, y) => ({ entity: { type: 'seesaw', x, y, length: 140 } }) },
  { id: 'pendulum', name: 'Swing', icon: '⟟', group: 'toys', description: 'A bar swinging from a pivot above.', make: (x, y) => ({ entity: { type: 'pendulum', x, y: y - 160, length: 160, width: 40, amplitude: 0.8, period: 180 } }) },
  { id: 'fanUp', name: 'Fan (up)', icon: '⇈', group: 'toys', description: 'Column of air pushing up.', make: (x, y) => ({ entity: { type: 'fan', x: x - 40, y: y - 260, w: 80, h: 260, fx: 0, fy: -0.4 } }) },
  { id: 'fanRight', name: 'Fan (right)', icon: '⇉', group: 'toys', description: 'Air pushing to the right.', make: (x, y) => ({ entity: { type: 'fan', x, y: y - 60, w: 220, h: 120, fx: 0.25, fy: -0.02 } }) },
  { id: 'magnet', name: 'Magnet', icon: '◉', group: 'toys', description: 'Pulls the rider in.', make: (x, y) => ({ entity: { type: 'magnet', x, y, radius: 160, strength: 0.08 } }) },
  { id: 'cannon', name: 'Cannon', icon: '➶', group: 'toys', description: 'Launches at 45 degrees.', make: (x, y) => ({ entity: { type: 'cannon', x, y, angle: -45, power: 11 } }) },
  { id: 'balloon', name: 'Balloon', icon: '🎈', group: 'toys', description: 'Lifts the rider for three seconds.', make: (x, y) => ({ entity: { type: 'balloon', x, y, lift: 0.3, duration: 120 } }) },
  { id: 'platformV', name: 'Lift', icon: '⇕', group: 'toys', description: 'Platform moving up and down.', make: (x, y) => ({ entity: { type: 'platform', x, y, w: 70, dx: 0, dy: 60, period: 200 } }) },
  { id: 'platformH', name: 'Shuttle', icon: '⇔', group: 'toys', description: 'Platform moving side to side.', make: (x, y) => ({ entity: { type: 'platform', x, y, w: 70, dx: 90, dy: 0, period: 220 } }) },
  { id: 'wall', name: 'Breakable wall', icon: '▮', group: 'hazards', description: 'Shatters when hit fast.', make: (x, y) => ({ entity: { type: 'wall', x1: x, y1: y - 40, x2: x, y2: y + 40, threshold: 4 } }) },
  { id: 'gear', name: 'Gear', icon: '⚙', group: 'hazards', description: 'Spinning toothed wheel.', make: (x, y) => ({ entity: { type: 'gear', x, y, radius: 60, teeth: 8, speed: 0.02 } }) },
  { id: 'train', name: 'Train', icon: '🚃', group: 'hazards', description: 'Shuttles back and forth.', make: (x, y) => ({ entity: { type: 'train', x, y, w: 110, h: 28, speed: 2, range: 300 } }) },
  { id: 'domino', name: 'Domino', icon: '▯', group: 'props', description: 'Knock it over.', make: (x, y) => ({ prop: { kind: 'domino', x, y: y - 12, width: 4, height: 24, mass: 0.5 } }) },
  { id: 'crate', name: 'Crate', icon: '▣', group: 'props', description: 'A pushable box.', make: (x, y) => ({ prop: { kind: 'crate', x, y: y - 10, width: 20, height: 20, mass: 1.5 } }) },
  { id: 'tnt', name: 'TNT', icon: '✸', group: 'props', description: 'Explodes on a hard hit.', make: (x, y) => ({ prop: { kind: 'tnt', x, y: y - 9, width: 18, height: 18, mass: 1, explosive: true } }) },
  { id: 'boulder', name: 'Boulder', icon: '●', group: 'props', description: 'Heavy and rolls.', make: (x, y) => ({ prop: { kind: 'boulder', x, y: y - 14, radius: 14, mass: 6 } }) },
  { id: 'cart', name: 'Cart', icon: '▭', group: 'props', description: 'Low, slippery, fast.', make: (x, y) => ({ prop: { kind: 'cart', x, y: y - 6, width: 34, height: 12, mass: 1.2, friction: 0.05 } }) },
];

export function objectKind(id: ObjectKind): ObjectKindDef {
  return OBJECT_KINDS.find((k) => k.id === id)!;
}

/** Anchor point used for hover/erase on a placed entity. */
export function entityAnchor(def: EntityDef): { x: number; y: number } {
  switch (def.type) {
    case 'wall':
      return { x: (def.x1 + def.x2) / 2, y: (def.y1 + def.y2) / 2 };
    case 'collapse':
      return { x: (def.x1 + def.x2) / 2, y: (def.y1 + def.y2) / 2 };
    case 'fan':
      return { x: def.x + def.w / 2, y: def.y + def.h / 2 };
    case 'avalanche':
      return { x: def.startX, y: 0 };
    case 'pendulum':
      return { x: def.x, y: def.y + def.length };
    default:
      return { x: def.x, y: def.y };
  }
}
