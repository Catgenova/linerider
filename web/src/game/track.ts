import type { Vec } from '../core/vec';
import { PX_PER_METER } from '../physics/constants';
import { MATERIALS, type MaterialId } from '../physics/materials';
import type { EntityDef } from './entities';
import type { PropDef } from './level';

export type LineLayer = 'level' | 'player';

export interface LineData {
  id: number;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  material: MaterialId;
  flipped: boolean;
  leftExt: boolean;
  rightExt: boolean;
  multiplier: number;
  layer: LineLayer;
  /** Co-op owner (1 or 2). 0 for single player / level geometry. */
  player: number;
}

export interface Zone {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface LineInput {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  material?: MaterialId;
  flipped?: boolean;
  multiplier?: number;
  layer?: LineLayer;
  player?: number;
}

export interface ObjectData {
  id: number;
  def: EntityDef;
}

export interface PropData {
  id: number;
  def: PropDef;
}

export interface TrackJSON {
  version: 1;
  start: Vec;
  startVelocity?: Vec;
  finish?: Zone;
  lines: LineData[];
  objects?: ObjectData[];
  props?: PropData[];
  nextId: number;
}

const keyOf = (x: number, y: number) => `${Math.round(x * 100)},${Math.round(y * 100)}`;

export function lineLength(l: { x1: number; y1: number; x2: number; y2: number }): number {
  return Math.sqrt((l.x2 - l.x1) ** 2 + (l.y2 - l.y1) ** 2);
}

/** Ink cost of a line in metres. */
export function lineCost(l: LineData): number {
  return (lineLength(l) / PX_PER_METER) * MATERIALS[l.material].cost;
}

/**
 * Editable track: lines plus start/finish. Keeps an endpoint index so joined lines get their
 * collision extensions, and tracks ink spend for the player-drawn layer.
 */
export class Track {
  readonly lines = new Map<number, LineData>();
  /** Player-placed interactive objects (free ride / published tracks). */
  readonly objects = new Map<number, ObjectData>();
  /** Player-placed physics props. */
  readonly props = new Map<number, PropData>();
  private readonly endpoints = new Map<string, Set<number>>();
  nextId = 1;
  start: Vec = { x: 0, y: 0 };
  startVelocity?: Vec;
  finish?: Zone;
  /** Bumped on every change so caches (renderer batches, ink totals) can invalidate. */
  revision = 0;
  /** Listener for incremental world sync. */
  onChange: ((change: TrackChange) => void) | null = null;

  addLine(input: LineInput, id?: number): LineData {
    const lineId = id ?? this.nextId++;
    if (lineId >= this.nextId) this.nextId = lineId + 1;
    const line: LineData = {
      id: lineId,
      x1: input.x1,
      y1: input.y1,
      x2: input.x2,
      y2: input.y2,
      material: input.material ?? 'normal',
      flipped: input.flipped ?? false,
      leftExt: false,
      rightExt: false,
      multiplier: input.multiplier ?? 1,
      layer: input.layer ?? 'player',
      player: input.player ?? 0,
    };
    this.lines.set(line.id, line);
    const touched = new Set<number>();
    this.indexEndpoint(line.x1, line.y1, line.id, touched);
    this.indexEndpoint(line.x2, line.y2, line.id, touched);
    this.refreshExtensions(line);
    for (const other of touched) if (other !== line.id) this.refreshExtensions(this.lines.get(other)!);
    this.revision++;
    this.onChange?.({ type: 'add', line, touched: [...touched].filter((t) => t !== line.id) });
    return line;
  }

  removeLine(id: number): LineData | undefined {
    const line = this.lines.get(id);
    if (!line) return undefined;
    this.lines.delete(id);
    const touched = new Set<number>();
    this.unindexEndpoint(line.x1, line.y1, id, touched);
    this.unindexEndpoint(line.x2, line.y2, id, touched);
    for (const other of touched) this.refreshExtensions(this.lines.get(other)!);
    this.revision++;
    this.onChange?.({ type: 'remove', line, touched: [...touched] });
    return line;
  }

  flipLine(id: number): void {
    const line = this.lines.get(id);
    if (!line) return;
    line.flipped = !line.flipped;
    this.revision++;
    this.onChange?.({ type: 'update', line, touched: [] });
  }

  setMultiplier(id: number, multiplier: number): void {
    const line = this.lines.get(id);
    if (!line) return;
    line.multiplier = multiplier;
    this.revision++;
    this.onChange?.({ type: 'update', line, touched: [] });
  }

  addObject(def: EntityDef, id?: number): ObjectData {
    const objId = id ?? this.nextId++;
    if (objId >= this.nextId) this.nextId = objId + 1;
    const data = { id: objId, def: JSON.parse(JSON.stringify(def)) as EntityDef };
    this.objects.set(objId, data);
    this.revision++;
    this.onChange?.({ type: 'objects', line: null, touched: [] });
    return data;
  }

  removeObject(id: number): ObjectData | undefined {
    const data = this.objects.get(id);
    if (!data) return undefined;
    this.objects.delete(id);
    this.revision++;
    this.onChange?.({ type: 'objects', line: null, touched: [] });
    return data;
  }

  addProp(def: PropDef, id?: number): PropData {
    const propId = id ?? this.nextId++;
    if (propId >= this.nextId) this.nextId = propId + 1;
    const data = { id: propId, def: { ...def } };
    this.props.set(propId, data);
    this.revision++;
    this.onChange?.({ type: 'objects', line: null, touched: [] });
    return data;
  }

  removeProp(id: number): PropData | undefined {
    const data = this.props.get(id);
    if (!data) return undefined;
    this.props.delete(id);
    this.revision++;
    this.onChange?.({ type: 'objects', line: null, touched: [] });
    return data;
  }

  clear(): void {
    const ids = [...this.lines.keys()];
    for (const id of ids) this.removeLine(id);
    for (const id of [...this.objects.keys()]) this.removeObject(id);
    for (const id of [...this.props.keys()]) this.removeProp(id);
  }

  /** Metres of ink spent on player lines (optionally for one co-op player). */
  inkUsed(player?: number): number {
    let total = 0;
    for (const l of this.lines.values()) {
      if (l.layer !== 'player') continue;
      if (player !== undefined && l.player !== player) continue;
      total += lineCost(l);
    }
    return total;
  }

  /** Nearest endpoint of any line within `radius` of (x,y), or null. */
  snapPoint(x: number, y: number, radius: number, ignoreId = -1): Vec | null {
    let best: Vec | null = null;
    let bestD = radius;
    for (const l of this.lines.values()) {
      if (l.id === ignoreId) continue;
      for (const [px, py] of [
        [l.x1, l.y1],
        [l.x2, l.y2],
      ]) {
        const d = Math.sqrt((px - x) ** 2 + (py - y) ** 2);
        if (d < bestD) {
          bestD = d;
          best = { x: px, y: py };
        }
      }
    }
    return best;
  }

  bounds(): { minX: number; minY: number; maxX: number; maxY: number } | null {
    if (this.lines.size === 0) return null;
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (const l of this.lines.values()) {
      minX = Math.min(minX, l.x1, l.x2);
      maxX = Math.max(maxX, l.x1, l.x2);
      minY = Math.min(minY, l.y1, l.y2);
      maxY = Math.max(maxY, l.y1, l.y2);
    }
    return { minX, minY, maxX, maxY };
  }

  toJSON(): TrackJSON {
    return {
      version: 1,
      start: { ...this.start },
      startVelocity: this.startVelocity ? { ...this.startVelocity } : undefined,
      finish: this.finish ? { ...this.finish } : undefined,
      lines: [...this.lines.values()].map((l) => ({ ...l })),
      objects: [...this.objects.values()].map((o) => ({ id: o.id, def: JSON.parse(JSON.stringify(o.def)) })),
      props: [...this.props.values()].map((o) => ({ id: o.id, def: { ...o.def } })),
      nextId: this.nextId,
    };
  }

  static fromJSON(json: TrackJSON): Track {
    const t = new Track();
    t.start = { ...json.start };
    t.startVelocity = json.startVelocity ? { ...json.startVelocity } : undefined;
    t.finish = json.finish ? { ...json.finish } : undefined;
    for (const l of json.lines) t.addLine(l, l.id);
    for (const o of json.objects ?? []) t.addObject(o.def, o.id);
    for (const o of json.props ?? []) t.addProp(o.def, o.id);
    t.nextId = Math.max(t.nextId, json.nextId ?? 1);
    return t;
  }

  clone(): Track {
    return Track.fromJSON(this.toJSON());
  }

  private indexEndpoint(x: number, y: number, id: number, touched: Set<number>): void {
    const k = keyOf(x, y);
    let set = this.endpoints.get(k);
    if (!set) {
      set = new Set();
      this.endpoints.set(k, set);
    }
    for (const other of set) touched.add(other);
    set.add(id);
  }

  private unindexEndpoint(x: number, y: number, id: number, touched: Set<number>): void {
    const k = keyOf(x, y);
    const set = this.endpoints.get(k);
    if (!set) return;
    set.delete(id);
    for (const other of set) touched.add(other);
    if (set.size === 0) this.endpoints.delete(k);
  }

  /**
   * A joint extends a line's collision past its endpoint only when another line continues on
   * from that point in roughly the same direction. Sharp corners (a wall meeting a ledge) get no
   * extension, which stops the wall from poking up through the surface the rider is on.
   */
  private refreshExtensions(line: LineData): void {
    line.leftExt = this.hasContinuation(line, line.x1, line.y1, line.x1 - line.x2, line.y1 - line.y2);
    line.rightExt = this.hasContinuation(line, line.x2, line.y2, line.x2 - line.x1, line.y2 - line.y1);
  }

  private hasContinuation(line: LineData, px: number, py: number, outX: number, outY: number): boolean {
    const set = this.endpoints.get(keyOf(px, py));
    if (!set || set.size < 2) return false;
    const outLen = Math.sqrt(outX * outX + outY * outY) || 1;
    for (const id of set) {
      if (id === line.id) continue;
      const other = this.lines.get(id);
      if (!other) continue;
      // Direction of the other line leading away from the shared point.
      const atStart = keyOf(other.x1, other.y1) === keyOf(px, py);
      const dx = atStart ? other.x2 - other.x1 : other.x1 - other.x2;
      const dy = atStart ? other.y2 - other.y1 : other.y1 - other.y2;
      const len = Math.sqrt(dx * dx + dy * dy) || 1;
      const cos = (dx * outX + dy * outY) / (len * outLen);
      if (cos > 0.5) return true; // within 60 degrees of straight on
    }
    return false;
  }
}

export interface TrackChange {
  type: 'add' | 'remove' | 'update' | 'objects';
  line: LineData | null;
  /** Ids of other lines whose extension flags changed. */
  touched: number[];
}
