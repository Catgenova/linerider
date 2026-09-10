import type { WindFn } from '../physics/world';

export type EnvironmentId =
  | 'mountain'
  | 'glacier'
  | 'desert'
  | 'forest'
  | 'rooftops'
  | 'cave'
  | 'moon'
  | 'machine';

export type SkylineStyle = 'peaks' | 'crystals' | 'mesas' | 'trees' | 'city' | 'stalactites' | 'craters' | 'gears';

export interface EnvironmentVisuals {
  /** Background gradient top/bottom. */
  bg: [string, string];
  accent: string;
  accent2: string;
  grid: string;
  fog: string;
  skyline: SkylineStyle;
  /** Optional celestial body drawn on the far layer. */
  orb?: { color: string; size: number; x: number; y: number };
}

export interface Environment {
  id: EnvironmentId;
  name: string;
  blurb: string;
  gimmick: string;
  gravityScale: number;
  frictionScale: number;
  /** Constant force plus gusts, px/frame^2. */
  wind?: { x: number; y: number; gust: number; period: number };
  /** Light radius around the rider in px; 0 = fully lit. */
  darkness: number;
  visuals: EnvironmentVisuals;
}

const env = (e: Environment) => e;

export const ENVIRONMENTS: Record<EnvironmentId, Environment> = {
  mountain: env({
    id: 'mountain',
    name: 'Neon Peaks',
    blurb: 'Jagged synth ridges above a sleepless city.',
    gimmick: 'Pure speed. Nothing but gravity and your line.',
    gravityScale: 1,
    frictionScale: 1,
    darkness: 0,
    visuals: {
      bg: ['#070317', '#1a0838'],
      accent: '#39f6ff',
      accent2: '#ff2bd6',
      grid: 'rgba(80, 60, 200, 0.16)',
      fog: 'rgba(120, 40, 220, 0.18)',
      skyline: 'peaks',
      orb: { color: '#ff2bd6', size: 90, x: 0.72, y: 0.36 },
    },
  }),
  glacier: env({
    id: 'glacier',
    name: 'Glacier Grid',
    blurb: 'Frozen data shelves that never thaw.',
    gimmick: 'Every surface is ice. Friction is switched off, so speed only leaves through crashes.',
    gravityScale: 1,
    frictionScale: 0,
    darkness: 0,
    visuals: {
      bg: ['#03111c', '#0a2a45'],
      accent: '#a8f4ff',
      accent2: '#5cc8ff',
      grid: 'rgba(90, 200, 255, 0.14)',
      fog: 'rgba(120, 220, 255, 0.16)',
      skyline: 'crystals',
      orb: { color: '#b8f0ff', size: 60, x: 0.2, y: 0.3 },
    },
  }),
  desert: env({
    id: 'desert',
    name: 'Dune Circuit',
    blurb: 'Red mesas under a burnt orange sky.',
    gimmick: 'Crosswinds gust across the whole level and shove the rider mid-air.',
    gravityScale: 1,
    frictionScale: 1,
    wind: { x: 0.02, y: 0, gust: 0.05, period: 140 },
    darkness: 0,
    visuals: {
      bg: ['#1a0507', '#4a1608'],
      accent: '#ffa640',
      accent2: '#ff3d7f',
      grid: 'rgba(255, 140, 60, 0.13)',
      fog: 'rgba(255, 110, 40, 0.16)',
      skyline: 'mesas',
      orb: { color: '#ff7a1a', size: 120, x: 0.6, y: 0.42 },
    },
  }),
  forest: env({
    id: 'forest',
    name: 'Data Forest',
    blurb: 'Fibre-optic trunks and bioluminescent canopies.',
    gimmick: 'Springy canopy lines and low visibility. Bounce your way through.',
    gravityScale: 1,
    frictionScale: 1,
    darkness: 0,
    visuals: {
      bg: ['#020f0a', '#08301c'],
      accent: '#4dff9d',
      accent2: '#c6ff4a',
      grid: 'rgba(60, 220, 120, 0.13)',
      fog: 'rgba(40, 200, 120, 0.18)',
      skyline: 'trees',
    },
  }),
  rooftops: env({
    id: 'rooftops',
    name: 'Skyline District',
    blurb: 'Rooftop to rooftop across the megacity.',
    gimmick: 'Precision. Tight ink budgets, narrow ledges, long drops.',
    gravityScale: 1,
    frictionScale: 1,
    wind: { x: 0, y: 0, gust: 0.03, period: 90 },
    darkness: 0,
    visuals: {
      bg: ['#05061a', '#151040'],
      accent: '#ff2bd6',
      accent2: '#39f6ff',
      grid: 'rgba(255, 60, 200, 0.12)',
      fog: 'rgba(200, 40, 255, 0.16)',
      skyline: 'city',
    },
  }),
  cave: env({
    id: 'cave',
    name: 'Undercity Caves',
    blurb: 'Beneath the grid, where the neon never reached.',
    gimmick: 'Darkness. Only a small halo around the rider is lit, so plan from memory.',
    gravityScale: 1,
    frictionScale: 1,
    darkness: 170,
    visuals: {
      bg: ['#020106', '#0a0416'],
      accent: '#c65cff',
      accent2: '#39f6ff',
      grid: 'rgba(120, 60, 220, 0.1)',
      fog: 'rgba(80, 20, 160, 0.2)',
      skyline: 'stalactites',
    },
  }),
  moon: env({
    id: 'moon',
    name: 'Lunar Relay',
    blurb: 'A silent regolith bowl under a huge blue Earth.',
    gimmick: 'Low gravity. Jumps go forever, landings come slow.',
    gravityScale: 0.4,
    frictionScale: 1,
    darkness: 0,
    visuals: {
      bg: ['#000005', '#0b0b26'],
      accent: '#f4f4ff',
      accent2: '#5cc8ff',
      grid: 'rgba(140, 140, 255, 0.1)',
      fog: 'rgba(60, 60, 160, 0.16)',
      skyline: 'craters',
      orb: { color: '#3d8bff', size: 140, x: 0.78, y: 0.28 },
    },
  }),
  machine: env({
    id: 'machine',
    name: 'The Machine',
    blurb: 'Inside the engine that runs the city.',
    gimmick: 'Everything moves. Gears, pistons, and conveyors decide your timing.',
    gravityScale: 1,
    frictionScale: 1,
    darkness: 0,
    visuals: {
      bg: ['#0a0705', '#2a1508'],
      accent: '#fff04a',
      accent2: '#ff7a45',
      grid: 'rgba(255, 200, 60, 0.12)',
      fog: 'rgba(255, 150, 40, 0.15)',
      skyline: 'gears',
    },
  }),
};

export const ENVIRONMENT_ORDER: EnvironmentId[] = [
  'mountain',
  'glacier',
  'desert',
  'forest',
  'rooftops',
  'cave',
  'moon',
  'machine',
];

/** Build the wind function for an environment, or null if it has no wind. */
export function makeWind(e: Environment): WindFn | null {
  const w = e.wind;
  if (!w) return null;
  return (x, _y, frame, out) => {
    const phase = frame / w.period + x * 0.0015;
    const gust = Math.sin(phase * Math.PI * 2) * Math.sin(phase * 0.37 * Math.PI * 2 + 1.3);
    out.x = w.x + gust * w.gust;
    out.y = w.y + Math.sin(phase * 1.7) * w.gust * 0.25;
  };
}
