import { describe, expect, it } from 'vitest';
import { ENVIRONMENTS } from '../src/game/environments';
import { LEVELS } from '../src/game/levels';
import { trackFromLevel } from '../src/game/loadLevel';
import { RIDERS } from '../src/game/riders';
import { Run } from '../src/game/run';
import { evaluateLevel } from '../src/game/level';

function runLevel(levelIndex: number, draw?: (track: ReturnType<typeof trackFromLevel>) => void, frames = 1600) {
  const level = LEVELS[levelIndex];
  const track = trackFromLevel(level);
  draw?.(track);
  const run = new Run({ track, level, riderDef: RIDERS.bosh, environment: ENVIRONMENTS[level.environment] });
  let aliveAt30 = true;
  for (let i = 0; i < frames && !run.done; i++) {
    run.step();
    if (i === 30 && run.rider.dead) aliveAt30 = false;
  }
  return { level, run, aliveAt30 };
}

describe('campaign levels', () => {
  it('all levels build, simulate without throwing, and the start ledge holds the rider', () => {
    for (let i = 0; i < LEVELS.length; i++) {
      const { level, aliveAt30 } = runLevel(i, undefined, 200);
      expect(aliveAt30, `${level.id}: rider died within 30 frames on the start ledge`).toBe(true);
    }
  });

  it('level ids are unique and objectives are well formed', () => {
    const ids = new Set<string>();
    for (const l of LEVELS) {
      expect(ids.has(l.id)).toBe(false);
      ids.add(l.id);
      expect(l.objectives.some((o) => !o.optional)).toBe(true);
      expect(l.materials.length).toBeGreaterThan(0);
      expect(l.budget).toBeGreaterThan(0);
    }
  });

  it('reports which reach levels a naive straight line solves (informational)', () => {
    const report: string[] = [];
    for (let i = 0; i < LEVELS.length; i++) {
      const level = LEVELS[i];
      if (!level.finish) continue;
      const first = level.lines[0];
      const fz = level.finish;
      const { run } = runLevel(i, (track) => {
        track.addLine({ x1: first.x2, y1: first.y2, x2: fz.x + fz.w / 2, y2: fz.y + fz.h, material: 'normal', layer: 'player' });
      });
      const s = run.summary();
      const ev = evaluateLevel(level, s);
      report.push(`${level.id.padEnd(12)} finished=${s.finished} died=${s.died} fail=${s.failReason ?? '-'} frames=${s.frames} medal=${ev.medal}`);
    }
    console.log(report.join('\n'));
    expect(report.length).toBeGreaterThan(0);
  });
});
