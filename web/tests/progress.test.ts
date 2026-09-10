import { describe, expect, it } from 'vitest';
import { medalFromObjectives } from '../src/game/level';
import { LEVELS } from '../src/game/levels';
import { Progress } from '../src/game/progress';

const level = LEVELS.find((l) => l.objectives.filter((o) => o.optional).length >= 2)!;
const results = (done: boolean[]) => level.objectives.map((objective, i) => ({ objective, done: !!done[i] }));
const required = level.objectives.map((o) => !o.optional);
const everything = level.objectives.map(() => true);

describe('objective progress', () => {
  it('accumulates objectives across completed runs and derives the medal from them', () => {
    const p = new Progress(LEVELS);
    let imp = p.recordResult(level, results(required), 100, 10, 0, null);
    expect(p.level(level.id).medal).toBe('bronze');
    expect(imp.newObjectives.length).toBe(required.filter(Boolean).length);
    expect(imp.newMedal).toBe(true);

    imp = p.recordResult(level, results(everything), 120, 12, 0, null);
    expect(p.level(level.id).medal).toBe('gold');
    expect(imp.newMedal).toBe(true);
    expect(p.objectivesDone(level)).toBe(level.objectives.length);

    // A weaker run later never takes anything away.
    imp = p.recordResult(level, results(required), 130, 14, 0, null);
    expect(imp.newObjectives).toEqual([]);
    expect(imp.newMedal).toBe(false);
    expect(p.level(level.id).medal).toBe('gold');
    expect(p.objectiveTotals([level])).toEqual({ done: level.objectives.length, total: level.objectives.length });
  });

  it('earns optional objectives one run at a time', () => {
    const p = new Progress();
    const optional = level.objectives.map((o, i) => (o.optional ? i : -1)).filter((i) => i >= 0);
    const first = required.slice();
    first[optional[0]] = true;
    p.recordResult(level, results(first), 100, 10, 0, null);
    expect(p.earned(level)[optional[0]]).toBe(true);
    expect(p.earned(level)[optional[1]]).toBe(false);
    const second = required.slice();
    second[optional[1]] = true;
    const imp = p.recordResult(level, results(second), 100, 10, 0, null);
    expect(imp.newObjectives).toEqual([optional[1]]);
    expect(p.earned(level)[optional[0]]).toBe(true);
  });

  it('seeds objectives for saves that only stored a medal', () => {
    const p = new Progress();
    const rec = p.level(level.id);
    rec.medal = 'silver';
    delete (rec as { objectives?: number[] }).objectives;
    p.migrate([level]);
    expect(medalFromObjectives(level, p.earned(level)).medal).toBe('silver');
  });

  it('bumps lifetime statistics', () => {
    const p = new Progress();
    expect(p.stats.sessions).toBe(1);
    p.bump('runs');
    p.bump('distance', 12.5);
    p.bumpMax('topSpeed', 9);
    p.bumpMax('topSpeed', 4);
    p.bumpMap('runsByRider', 'bosh');
    p.bumpMap('runsByRider', 'bosh');
    expect(p.stats.runs).toBe(1);
    expect(p.stats.distance).toBe(12.5);
    expect(p.stats.topSpeed).toBe(9);
    expect(p.stats.runsByRider.bosh).toBe(2);
  });
});
