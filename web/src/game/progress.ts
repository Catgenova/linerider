import type { LevelDef, Medal, ObjectiveResult } from './level';
import { MEDAL_RANK, medalFromObjectives } from './level';
import type { TrackJSON } from './track';

export interface LevelRecord {
  medal: Medal;
  /** Indices of the level's objectives earned so far. They accumulate across completed runs. */
  objectives: number[];
  bestFrames: number | null;
  bestInk: number | null;
  bestTrick: number;
  attempts: number;
  /** Track snapshot of the best (fastest finishing) run, used for ghost racing. */
  ghost?: { track: TrackJSON; rider: string; frames: number };
}

/** What a finished campaign run improved. */
export interface Improvements {
  newMedal: boolean;
  /** Indices of objectives earned for the first time in this run. */
  newObjectives: number[];
  newTime: boolean;
  newInk: boolean;
  newTrick: boolean;
}

/** Everything counted across every run, in every mode, for as long as the save exists. */
export interface LifetimeStats {
  sessions: number;
  firstPlayed: string | null;
  lastPlayed: string | null;
  /** Wall-clock seconds with the game open. */
  playSeconds: number;
  runs: number;
  finished: number;
  crashes: number;
  /** Runs stopped or restarted before they ended. */
  aborted: number;
  failsByReason: Record<string, number>;
  /** Simulation frames the rider stayed on the board. */
  framesRidden: number;
  /** Metres travelled forward, summed over runs. */
  distance: number;
  longestRunFrames: number;
  /** Metres per second. */
  topSpeed: number;
  airtimeFrames: number;
  bestAirFrames: number;
  flips: number;
  backflips: number;
  frontflips: number;
  trickScore: number;
  bestTrickRun: number;
  bestCombo: number;
  nearMisses: number;
  hugeDrops: number;
  cleanLandings: number;
  manuals: number;
  grindFrames: number;
  flagsCollected: number;
  rescued: number;
  cargoDelivered: number;
  cargoLost: number;
  chaos: number;
  detonations: number;
  wallsSmashed: number;
  crumbles: number;
  /** Metres of line drawn by hand. */
  inkDrawn: number;
  linesDrawn: number;
  linesErased: number;
  linesFlipped: number;
  objectsPlaced: number;
  objectsErased: number;
  undos: number;
  redos: number;
  inkRunOuts: number;
  flagsSet: number;
  restarts: number;
  pauses: number;
  inkByMaterial: Record<string, number>;
  objectsByKind: Record<string, number>;
  runsByMode: Record<string, number>;
  finishedByMode: Record<string, number>;
  runsByRider: Record<string, number>;
  distanceByRider: Record<string, number>;
  runsByEnvironment: Record<string, number>;
  distanceByEnvironment: Record<string, number>;
  arcadeDistance: number;
  coopSwitches: number;
  tracksPublished: number;
  codesImported: number;
  codesShared: number;
  likesGiven: number;
  /** Personal bests beaten (time, ink, trick score). */
  recordsBroken: number;
}

type NumericStat = { [K in keyof LifetimeStats]: LifetimeStats[K] extends number ? K : never }[keyof LifetimeStats];
type MapStat = { [K in keyof LifetimeStats]: LifetimeStats[K] extends Record<string, number> ? K : never }[keyof LifetimeStats];

export function blankStats(): LifetimeStats {
  return {
    sessions: 0, firstPlayed: null, lastPlayed: null, playSeconds: 0,
    runs: 0, finished: 0, crashes: 0, aborted: 0, failsByReason: {},
    framesRidden: 0, distance: 0, longestRunFrames: 0, topSpeed: 0,
    airtimeFrames: 0, bestAirFrames: 0, flips: 0, backflips: 0, frontflips: 0,
    trickScore: 0, bestTrickRun: 0, bestCombo: 0, nearMisses: 0, hugeDrops: 0, cleanLandings: 0, manuals: 0, grindFrames: 0,
    flagsCollected: 0, rescued: 0, cargoDelivered: 0, cargoLost: 0, chaos: 0, detonations: 0, wallsSmashed: 0, crumbles: 0,
    inkDrawn: 0, linesDrawn: 0, linesErased: 0, linesFlipped: 0, objectsPlaced: 0, objectsErased: 0, undos: 0, redos: 0, inkRunOuts: 0, flagsSet: 0, restarts: 0, pauses: 0,
    inkByMaterial: {}, objectsByKind: {}, runsByMode: {}, finishedByMode: {}, runsByRider: {}, distanceByRider: {}, runsByEnvironment: {}, distanceByEnvironment: {},
    arcadeDistance: 0, coopSwitches: 0, tracksPublished: 0, codesImported: 0, codesShared: 0, likesGiven: 0, recordsBroken: 0,
  };
}

export interface SaveData {
  version: 1;
  levels: Record<string, LevelRecord>;
  arcadeBest: number;
  daily: Record<string, { time: number | null; ink: number | null; trick: number }>;
  settings: { rider: string; showTicks: boolean; music: boolean };
  stats: LifetimeStats;
}

const KEY = 'cyber-rider-save-v1';

function blank(): SaveData {
  return { version: 1, levels: {}, arcadeBest: 0, daily: {}, settings: { rider: 'bosh', showTicks: true, music: true }, stats: blankStats() };
}

/** Local persistence of campaign progress, records, ghosts and lifetime statistics. */
export class Progress {
  data: SaveData;

  constructor(levels: LevelDef[] = []) {
    this.data = Progress.load();
    this.migrate(levels);
    const now = new Date().toISOString();
    const st = this.data.stats;
    st.sessions++;
    st.firstPlayed ??= now;
    st.lastPlayed = now;
    this.save();
  }

  static load(): SaveData {
    try {
      const raw = localStorage.getItem(KEY);
      if (!raw) return blank();
      const parsed = JSON.parse(raw) as Partial<SaveData>;
      if (parsed.version !== 1) return blank();
      return {
        ...blank(),
        ...parsed,
        version: 1,
        settings: { ...blank().settings, ...parsed.settings },
        stats: { ...blankStats(), ...(parsed.stats ?? {}) },
      };
    } catch {
      return blank();
    }
  }

  save(): void {
    try {
      localStorage.setItem(KEY, JSON.stringify(this.data));
    } catch {
      /* storage unavailable */
    }
  }

  get stats(): LifetimeStats {
    return this.data.stats;
  }

  level(id: string): LevelRecord {
    let rec = this.data.levels[id];
    if (!rec) {
      rec = { medal: 'none', objectives: [], bestFrames: null, bestInk: null, bestTrick: 0, attempts: 0 };
      this.data.levels[id] = rec;
    }
    rec.objectives ??= [];
    return rec;
  }

  /**
   * Saves written before objectives were tracked only stored a medal; seed the earned set from it
   * (required objectives for bronze, half the optional ones for silver, everything for gold).
   */
  migrate(levels: LevelDef[]): void {
    let changed = false;
    for (const level of levels) {
      const rec = this.data.levels[level.id];
      if (!rec || rec.objectives) continue;
      const earned: number[] = [];
      if (rec.medal !== 'none') {
        const optional = level.objectives.map((o, i) => (o.optional ? i : -1)).filter((i) => i >= 0);
        const keep = rec.medal === 'gold' ? optional.length : rec.medal === 'silver' ? Math.ceil(optional.length / 2) : 0;
        level.objectives.forEach((o, i) => {
          if (!o.optional) earned.push(i);
        });
        earned.push(...optional.slice(0, keep));
        earned.sort((a, b) => a - b);
      }
      rec.objectives = earned;
      changed = true;
    }
    if (changed) this.save();
  }

  isComplete(id: string): boolean {
    return MEDAL_RANK[this.level(id).medal] > 0;
  }

  /** One flag per objective of the level, in level order: earned so far or not. */
  earned(level: LevelDef): boolean[] {
    const set = new Set(this.level(level.id).objectives);
    return level.objectives.map((_, i) => set.has(i));
  }

  objectivesDone(level: LevelDef): number {
    return this.earned(level).filter(Boolean).length;
  }

  objectiveTotals(levels: LevelDef[]): { done: number; total: number } {
    let done = 0;
    let total = 0;
    for (const level of levels) {
      total += level.objectives.length;
      done += this.objectivesDone(level);
    }
    return { done, total };
  }

  recordAttempt(id: string): void {
    this.level(id).attempts++;
    this.save();
  }

  /**
   * Merge a completed run into the record: every objective it satisfied stays earned, the medal
   * follows the earned set, and the time, ink and trick records update. Returns what improved.
   */
  recordResult(
    level: LevelDef,
    results: ObjectiveResult[],
    frames: number | null,
    ink: number,
    trick: number,
    ghost: { track: TrackJSON; rider: string; frames: number } | null,
  ): Improvements {
    const rec = this.level(level.id);
    const out: Improvements = { newMedal: false, newObjectives: [], newTime: false, newInk: false, newTrick: false };
    const earned = new Set(rec.objectives);
    results.forEach((r, i) => {
      if (r.done && !earned.has(i)) {
        earned.add(i);
        out.newObjectives.push(i);
      }
    });
    rec.objectives = [...earned].sort((a, b) => a - b);
    const { medal } = medalFromObjectives(level, this.earned(level));
    if (MEDAL_RANK[medal] > MEDAL_RANK[rec.medal]) {
      rec.medal = medal;
      out.newMedal = true;
    }
    if (frames !== null && (rec.bestFrames === null || frames < rec.bestFrames)) {
      rec.bestFrames = frames;
      out.newTime = true;
      if (ghost) rec.ghost = ghost;
    }
    if (frames !== null && (rec.bestInk === null || ink < rec.bestInk)) {
      rec.bestInk = ink;
      out.newInk = true;
    }
    if (trick > rec.bestTrick) {
      rec.bestTrick = trick;
      out.newTrick = true;
    }
    this.save();
    return out;
  }

  totalMedals(): { bronze: number; silver: number; gold: number } {
    const t = { bronze: 0, silver: 0, gold: 0 };
    for (const rec of Object.values(this.data.levels)) if (rec.medal !== 'none') t[rec.medal]++;
    return t;
  }

  // ---------------------------------------------------------------- lifetime statistics

  /** Add to a counter. Callers save at natural checkpoints (run end, periodic autosave). */
  bump(key: NumericStat, n = 1): void {
    this.data.stats[key] += n;
  }

  /** Raise a personal best if the value beats it. */
  bumpMax(key: NumericStat, value: number): void {
    if (value > this.data.stats[key]) this.data.stats[key] = value;
  }

  /** Add to one entry of a keyed counter (per rider, per material...). */
  bumpMap(map: MapStat, key: string, n = 1): void {
    const m = this.data.stats[map];
    m[key] = (m[key] ?? 0) + n;
  }

  reset(): void {
    this.data = blank();
    this.save();
  }
}
