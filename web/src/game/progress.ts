import type { Medal } from './level';
import { MEDAL_RANK } from './level';
import type { TrackJSON } from './track';

export interface LevelRecord {
  medal: Medal;
  bestFrames: number | null;
  bestInk: number | null;
  bestTrick: number;
  attempts: number;
  /** Track snapshot of the best (fastest finishing) run, used for ghost racing. */
  ghost?: { track: TrackJSON; rider: string; frames: number };
}

export interface SaveData {
  version: 1;
  levels: Record<string, LevelRecord>;
  arcadeBest: number;
  daily: Record<string, { time: number | null; ink: number | null; trick: number }>;
  settings: { rider: string; showTicks: boolean; music: boolean };
}

const KEY = 'neon-linerider-save-v1';

function blank(): SaveData {
  return { version: 1, levels: {}, arcadeBest: 0, daily: {}, settings: { rider: 'bosh', showTicks: true, music: true } };
}

/** Local persistence of campaign progress, records and ghosts. */
export class Progress {
  data: SaveData;

  constructor() {
    this.data = Progress.load();
  }

  static load(): SaveData {
    try {
      const raw = localStorage.getItem(KEY);
      if (!raw) return blank();
      const parsed = JSON.parse(raw) as SaveData;
      if (parsed.version !== 1) return blank();
      return { ...blank(), ...parsed, settings: { ...blank().settings, ...parsed.settings } };
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

  level(id: string): LevelRecord {
    let rec = this.data.levels[id];
    if (!rec) {
      rec = { medal: 'none', bestFrames: null, bestInk: null, bestTrick: 0, attempts: 0 };
      this.data.levels[id] = rec;
    }
    return rec;
  }

  isComplete(id: string): boolean {
    return MEDAL_RANK[this.level(id).medal] > 0;
  }

  recordAttempt(id: string): void {
    this.level(id).attempts++;
    this.save();
  }

  /** Merge a finished run into the record. Returns which records improved. */
  recordResult(
    id: string,
    medal: Medal,
    frames: number | null,
    ink: number,
    trick: number,
    ghost: { track: TrackJSON; rider: string; frames: number } | null,
  ): { newMedal: boolean; newTime: boolean; newInk: boolean; newTrick: boolean } {
    const rec = this.level(id);
    const out = { newMedal: false, newTime: false, newInk: false, newTrick: false };
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

  reset(): void {
    this.data = blank();
    this.save();
  }
}
