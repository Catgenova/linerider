import { Rng, hashString } from '../core/rng';
import type { LevelDef, LevelLine } from '../game/level';
import type { EnvironmentId } from '../game/environments';

export function todayKey(): string {
  const d = new Date();
  const y = d.getUTCFullYear();
  const m = String(d.getUTCMonth() + 1).padStart(2, '0');
  const day = String(d.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

export function dailySeedFor(key: string): number {
  return hashString(`neon-daily:${key}`);
}

const ENVS: EnvironmentId[] = ['mountain', 'glacier', 'desert', 'forest', 'rooftops', 'moon', 'machine'];

/** Everyone gets the same terrain, finish and budget for a given date. */
export function generateDaily(seed: number, key: string): LevelDef {
  const rng = new Rng(seed);
  const environment = rng.pick(ENVS);
  const lines: LevelLine[] = [{ x1: -40, y1: 6, x2: 100, y2: 14 }];
  const flags: { x: number; y: number }[] = [];
  let x = 100;
  let y = 14;
  const segments = rng.int(4, 6);
  for (let i = 0; i < segments; i++) {
    const gap = rng.range(70, 190);
    const drop = rng.range(30, 110);
    const width = rng.range(120, 300);
    const x0 = x + gap;
    const y0 = y + drop;
    const slope = rng.range(-0.1, 0.35);
    const material = rng.chance(0.2) ? (environment === 'glacier' ? 'ice' : 'accel') : 'normal';
    lines.push({ x1: x0, y1: y0, x2: x0 + width, y2: y0 + width * slope, material });
    if (rng.chance(0.7) && flags.length < 3) {
      flags.push({ x: x + gap * rng.range(0.3, 0.7), y: y + drop * rng.range(0.1, 0.6) - 20 });
    }
    x = x0 + width;
    y = y0 + width * slope;
  }
  lines.push({ x1: x, y1: y, x2: x + 200, y2: y });
  lines.push({ x1: x + 200, y1: y, x2: x + 200, y2: y - 120 });
  const budget = Math.round(90 + rng.range(0, 80));
  return {
    id: `daily-${key}`,
    name: `Daily ${key}`,
    region: 'peaks',
    environment,
    mode: 'reach',
    tagline: 'Same terrain for everyone today.',
    briefing:
      'Today\'s seed builds the same terrain, flags, finish and ink budget for every player. Fastest time, least ink and highest trick score are ranked separately.',
    budget,
    materials: ['normal', 'accel', 'spring', 'crumble'],
    rider: null,
    start: { x: 0, y: 0 },
    finish: { x: x + 120, y: y - 80, w: 50, h: 80 },
    flags,
    lines,
    objectives: [
      { kind: 'finish' },
      { kind: 'flags', value: flags.length, optional: true },
      { kind: 'inkUnder', value: Math.round(budget * 0.6), optional: true },
      { kind: 'timeUnder', value: 40 * Math.round(6 + segments * 2.2), optional: true },
    ],
    zoom: 1,
    focus: { x: x / 2, y: y / 2 },
  };
}
