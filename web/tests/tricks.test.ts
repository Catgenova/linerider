import { describe, expect, it } from 'vitest';
import { ENVIRONMENTS } from '../src/game/environments';
import { RIDERS } from '../src/game/riders';
import { Run } from '../src/game/run';
import { Track } from '../src/game/track';
import type { LevelDef } from '../src/game/level';

function level(partial: Partial<LevelDef>): LevelDef {
  return {
    id: 't',
    name: 't',
    region: 'peaks',
    environment: 'mountain',
    mode: 'reach',
    tagline: '',
    briefing: '',
    budget: 100,
    materials: ['normal'],
    rider: null,
    start: { x: 0, y: 0 },
    lines: [],
    objectives: [{ kind: 'finish' }],
    ...partial,
  };
}

describe('trick tracker', () => {
  it('awards airtime and a huge drop for a long fall onto a landing', () => {
    const track = new Track();
    track.addLine({ x1: -40, y1: 6, x2: 200, y2: 30, layer: 'level' });
    // Landing slope far below so the rider survives the fall.
    track.addLine({ x1: 320, y1: 260, x2: 900, y2: 420, layer: 'level' });
    track.finish = { x: 820, y: 330, w: 60, h: 80 };
    const run = new Run({ track, level: level({ finish: track.finish }), riderDef: RIDERS.bosh, environment: ENVIRONMENTS.mountain });
    const seen: string[] = [];
    for (let i = 0; i < 600 && !run.done; i++) {
      run.step();
      for (const e of run.events) if (e.type === 'trick') seen.push(e.text);
      run.events.length = 0;
    }
    expect(run.tricks.airtimeFrames).toBeGreaterThan(40);
    expect(seen.some((t) => t.startsWith('AIRTIME'))).toBe(true);
    expect(seen.some((t) => t.startsWith('HUGE DROP'))).toBe(true);
    expect(run.tricks.score).toBeGreaterThan(0);
  });

  it('counts flags and finishes a level', () => {
    const track = new Track();
    track.addLine({ x1: -40, y1: 6, x2: 600, y2: 120, layer: 'level' });
    const lv = level({ flags: [{ x: 200, y: 44 }], finish: { x: 500, y: 40, w: 60, h: 70 } });
    track.finish = lv.finish;
    const run = new Run({ track, level: lv, riderDef: RIDERS.bosh, environment: ENVIRONMENTS.mountain });
    for (let i = 0; i < 600 && !run.done; i++) run.step();
    expect(run.flags[0]).toBe(true);
    expect(run.finished).toBe(true);
    const s = run.summary();
    expect(s.flagsCollected).toBe(1);
  });

  it('stalls a rider that stops moving', () => {
    const track = new Track();
    track.addLine({ x1: -40, y1: 6, x2: 100, y2: 6, layer: 'level' });
    track.addLine({ x1: 100, y1: 6, x2: 100, y2: -40, layer: 'level' });
    const run = new Run({ track, level: level({ finish: { x: 900, y: 0, w: 10, h: 10 } }), riderDef: RIDERS.bosh, environment: ENVIRONMENTS.mountain });
    for (let i = 0; i < 1000 && !run.done; i++) run.step();
    expect(run.failReason).toBe('Stalled');
  });

  it('settles destruction props so chaos starts at zero', () => {
    const track = new Track();
    track.addLine({ x1: -40, y1: 6, x2: 100, y2: 20, layer: 'level' });
    track.addLine({ x1: 100, y1: 20, x2: 800, y2: 20, layer: 'level' });
    const lv = level({
      mode: 'destruction',
      props: [
        { kind: 'crate', x: 500, y: 10, width: 20, height: 20, mass: 1.5 },
        { kind: 'crate', x: 500, y: -10, width: 20, height: 20, mass: 1.5 },
        { kind: 'crate', x: 500, y: -30, width: 20, height: 20, mass: 1.5 },
        { kind: 'domino', x: 600, y: 8, width: 4, height: 24, mass: 0.5 },
      ],
      objectives: [{ kind: 'chaos', value: 500 }],
    });
    const run = new Run({ track, level: lv, riderDef: RIDERS.bosh, environment: ENVIRONMENTS.mountain });
    expect(run.chaosScore()).toBe(0);
    for (let i = 0; i < 20; i++) run.step();
    expect(run.chaosScore()).toBeLessThan(50);
  });
});
