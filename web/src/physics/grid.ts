import { GRID_CELL } from './constants';
import type { Line } from './line';

function key(cx: number, cy: number): number {
  return (((cx & 0xffff) << 16) | (cy & 0xffff)) >>> 0;
}

/** Walk every grid cell a segment passes through (Amanatides–Woo traversal). */
export function cellsAlong(
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  cb: (cx: number, cy: number) => void,
): void {
  const S = GRID_CELL;
  let cx = Math.floor(x1 / S);
  let cy = Math.floor(y1 / S);
  const ex = Math.floor(x2 / S);
  const ey = Math.floor(y2 / S);
  const dx = x2 - x1;
  const dy = y2 - y1;
  const stepX = dx > 0 ? 1 : -1;
  const stepY = dy > 0 ? 1 : -1;
  let tMaxX = dx !== 0 ? ((dx > 0 ? (cx + 1) * S - x1 : x1 - cx * S) / Math.abs(dx)) : Infinity;
  let tMaxY = dy !== 0 ? ((dy > 0 ? (cy + 1) * S - y1 : y1 - cy * S) / Math.abs(dy)) : Infinity;
  const tDeltaX = dx !== 0 ? S / Math.abs(dx) : Infinity;
  const tDeltaY = dy !== 0 ? S / Math.abs(dy) : Infinity;
  cb(cx, cy);
  let guard = 0;
  while ((cx !== ex || cy !== ey) && guard++ < 100000) {
    if (tMaxX < tMaxY) {
      cx += stepX;
      tMaxX += tDeltaX;
    } else {
      cy += stepY;
      tMaxY += tDeltaY;
    }
    cb(cx, cy);
  }
}

/** Spatial hash of static lines. Queries return lines near a point in ascending id order. */
export class LineGrid {
  private cells = new Map<number, Line[]>();
  private stamp = new Map<number, number>();
  private queryStamp = 0;

  add(line: Line): void {
    const [x1, y1, x2, y2] = line.extendedBounds();
    cellsAlong(x1, y1, x2, y2, (cx, cy) => {
      const k = key(cx, cy);
      let bucket = this.cells.get(k);
      if (!bucket) {
        bucket = [];
        this.cells.set(k, bucket);
      }
      if (bucket[bucket.length - 1] !== line) bucket.push(line);
    });
  }

  remove(line: Line): void {
    const [x1, y1, x2, y2] = line.extendedBounds();
    cellsAlong(x1, y1, x2, y2, (cx, cy) => {
      const k = key(cx, cy);
      const bucket = this.cells.get(k);
      if (!bucket) return;
      const i = bucket.indexOf(line);
      if (i >= 0) bucket.splice(i, 1);
      if (bucket.length === 0) this.cells.delete(k);
    });
  }

  clear(): void {
    this.cells.clear();
    this.stamp.clear();
  }

  /** Lines in the 3x3 cells around (x,y), deduplicated and sorted by id for determinism. */
  near(x: number, y: number, out: Line[]): Line[] {
    out.length = 0;
    const cx = Math.floor(x / GRID_CELL);
    const cy = Math.floor(y / GRID_CELL);
    const qs = ++this.queryStamp;
    for (let j = -1; j <= 1; j++) {
      for (let i = -1; i <= 1; i++) {
        const bucket = this.cells.get(key(cx + i, cy + j));
        if (!bucket) continue;
        for (const line of bucket) {
          if (this.stamp.get(line.id) === qs) continue;
          this.stamp.set(line.id, qs);
          out.push(line);
        }
      }
    }
    if (out.length > 1) out.sort((a, b) => a.id - b.id);
    return out;
  }

  /** Lines whose extended bounds fall in a rect (for rendering/erasing). Conservative. */
  inRect(left: number, top: number, right: number, bottom: number, out: Line[]): Line[] {
    out.length = 0;
    const qs = ++this.queryStamp;
    const c0 = Math.floor(left / GRID_CELL) - 1;
    const c1 = Math.floor(right / GRID_CELL) + 1;
    const r0 = Math.floor(top / GRID_CELL) - 1;
    const r1 = Math.floor(bottom / GRID_CELL) + 1;
    // Guard against pathological rect sizes: fall back to scanning all buckets.
    if ((c1 - c0) * (r1 - r0) > 40000) {
      for (const bucket of this.cells.values()) {
        for (const line of bucket) {
          if (this.stamp.get(line.id) === qs) continue;
          this.stamp.set(line.id, qs);
          out.push(line);
        }
      }
      return out;
    }
    for (let cy = r0; cy <= r1; cy++) {
      for (let cx = c0; cx <= c1; cx++) {
        const bucket = this.cells.get(key(cx, cy));
        if (!bucket) continue;
        for (const line of bucket) {
          if (this.stamp.get(line.id) === qs) continue;
          this.stamp.set(line.id, qs);
          out.push(line);
        }
      }
    }
    return out;
  }
}
