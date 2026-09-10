import type { Vec } from '../core/vec';
import type { MaterialId } from '../physics/materials';
import type { PropInit } from '../physics/prop';
import type { EntityDef } from './entities';
import type { EnvironmentId } from './environments';
import type { RiderId } from './riders';
import type { Zone } from './track';

export type ObjectiveKind =
  | 'finish'
  | 'flags'
  | 'rescue'
  | 'cargo'
  | 'survive'
  | 'chaos'
  | 'airtime'
  | 'flips'
  | 'inkUnder'
  | 'timeUnder'
  | 'trickScore'
  | 'noDeath'
  | 'nearMiss'
  | 'distance'
  | 'hugeDrop';

export interface Objective {
  kind: ObjectiveKind;
  value?: number;
  optional?: boolean;
  label?: string;
}

export type LevelMode = 'reach' | 'puzzle' | 'stunt' | 'delivery' | 'rescue' | 'destruction' | 'boss' | 'survive' | 'flags';

export type RegionId = 'peaks' | 'glacier' | 'dunes' | 'forest' | 'skyline' | 'undercity' | 'lunar' | 'machine';

export interface LevelLine {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  material?: MaterialId;
  flipped?: boolean;
  multiplier?: number;
}

export type PropDef = Omit<PropInit, 'id'>;

export interface LevelDef {
  id: string;
  name: string;
  region: RegionId;
  environment: EnvironmentId;
  mode: LevelMode;
  tagline: string;
  briefing: string;
  /** Ink budget in metres. */
  budget: number;
  materials: MaterialId[];
  /** Fixed rider, or null to let the player choose. */
  rider: RiderId | null;
  start: Vec;
  finish?: Zone;
  flags?: Vec[];
  rescues?: Vec[];
  lines: LevelLine[];
  entities?: EntityDef[];
  props?: PropDef[];
  objectives: Objective[];
  /** Seconds before the run fails, if set. */
  timeLimit?: number;
  /** Frames of survival needed for 'survive' objectives. */
  surviveFrames?: number;
  /** Suggested starting zoom. */
  zoom?: number;
  /** Optional camera hint: keep this world rect visible when editing. */
  focus?: Vec;
}

export interface RunSummary {
  finished: boolean;
  died: boolean;
  frames: number;
  inkUsed: number;
  airtimeFrames: number;
  flips: number;
  trickScore: number;
  flagsCollected: number;
  flagsTotal: number;
  rescued: number;
  rescueTotal: number;
  cargoIntegrity: number;
  cargoLost: boolean;
  chaos: number;
  nearMisses: number;
  hugeDrops: number;
  distance: number;
  survivedFrames: number;
  failReason: string | null;
}

export type Medal = 'none' | 'bronze' | 'silver' | 'gold';

export function objectiveLabel(o: Objective): string {
  if (o.label) return o.label;
  switch (o.kind) {
    case 'finish':
      return 'Reach the finish';
    case 'flags':
      return `Collect ${o.value ?? 'all'} flags`;
    case 'rescue':
      return `Rescue ${o.value ?? 'everyone'}`;
    case 'cargo':
      return `Deliver cargo at ${o.value ?? 50}%+ integrity`;
    case 'survive':
      return `Survive ${((o.value ?? 400) / 40).toFixed(0)}s`;
    case 'chaos':
      return `Cause ${o.value ?? 1000} chaos`;
    case 'airtime':
      return `${((o.value ?? 80) / 40).toFixed(1)}s total airtime`;
    case 'flips':
      return `Land ${o.value ?? 1} flip${(o.value ?? 1) > 1 ? 's' : ''}`;
    case 'inkUnder':
      return `Use under ${o.value ?? 50} m of ink`;
    case 'timeUnder':
      return `Finish under ${((o.value ?? 400) / 40).toFixed(1)}s`;
    case 'trickScore':
      return `Score ${o.value ?? 1000} trick points`;
    case 'noDeath':
      return 'Do not crash';
    case 'nearMiss':
      return `${o.value ?? 1} near miss${(o.value ?? 1) > 1 ? 'es' : ''}`;
    case 'distance':
      return `Travel ${o.value ?? 100} m`;
    case 'hugeDrop':
      return `${o.value ?? 1} huge drop${(o.value ?? 1) > 1 ? 's' : ''}`;
    default:
      return o.kind;
  }
}

export function evaluateObjective(o: Objective, s: RunSummary): boolean {
  switch (o.kind) {
    case 'finish':
      return s.finished;
    case 'flags':
      return s.flagsCollected >= (o.value ?? s.flagsTotal);
    case 'rescue':
      return s.finished && s.rescued >= (o.value ?? s.rescueTotal);
    case 'cargo':
      return s.finished && !s.cargoLost && s.cargoIntegrity >= (o.value ?? 50);
    case 'survive':
      return s.survivedFrames >= (o.value ?? 400) && !s.died;
    case 'chaos':
      return s.chaos >= (o.value ?? 1000);
    case 'airtime':
      return s.airtimeFrames >= (o.value ?? 80);
    case 'flips':
      return s.flips >= (o.value ?? 1);
    case 'inkUnder':
      return s.finished && s.inkUsed <= (o.value ?? 50) + 1e-6;
    case 'timeUnder':
      return s.finished && s.frames <= (o.value ?? 400);
    case 'trickScore':
      return s.trickScore >= (o.value ?? 1000);
    case 'noDeath':
      return s.finished && !s.died;
    case 'nearMiss':
      return s.nearMisses >= (o.value ?? 1);
    case 'distance':
      return s.distance >= (o.value ?? 100);
    case 'hugeDrop':
      return s.hugeDrops >= (o.value ?? 1);
    default:
      return false;
  }
}

export interface ObjectiveResult {
  objective: Objective;
  done: boolean;
}

/**
 * Medal for a set of earned objectives (one flag per objective, in level order): bronze once every
 * required objective is earned, silver with at least half of the optional ones, gold with all.
 */
export function medalFromObjectives(level: LevelDef, done: boolean[]): { medal: Medal; complete: boolean } {
  let complete = true;
  let optional = 0;
  let optionalDone = 0;
  level.objectives.forEach((o, i) => {
    if (o.optional) {
      optional++;
      if (done[i]) optionalDone++;
    } else if (!done[i]) complete = false;
  });
  let medal: Medal = 'none';
  if (complete) medal = optional === 0 || optionalDone === optional ? 'gold' : optionalDone * 2 >= optional ? 'silver' : 'bronze';
  return { medal, complete };
}

export function evaluateLevel(level: LevelDef, s: RunSummary): { results: ObjectiveResult[]; medal: Medal; complete: boolean } {
  const results = level.objectives.map((objective) => ({ objective, done: evaluateObjective(objective, s) }));
  const { medal, complete } = medalFromObjectives(level, results.map((r) => r.done));
  return { results, medal, complete };
}

export const MEDAL_RANK: Record<Medal, number> = { none: 0, bronze: 1, silver: 2, gold: 3 };
