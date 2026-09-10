import type { Rng } from '../core/rng';
import type { Editor } from '../editor/editor';
import type { Run } from '../game/run';
import type { Track } from '../game/track';
import { PX_PER_METER } from '../physics/constants';

/**
 * Draw-while-riding: the rider is always moving, terrain is generated ahead as ledges with gaps
 * that widen with distance, a tailwind ramps the pace up, and ink regenerates with distance.
 */
export class ArcadeDirector {
  /** Current ink allowance in metres (grows with distance). */
  budget = 40;
  private frontier = 0;
  private lastY = 0;
  private chunk = 0;

  constructor(
    private readonly track: Track,
    private readonly rng: Rng,
  ) {
    track.addLine({ x1: -40, y1: 6, x2: 140, y2: 12, layer: 'level' });
    this.frontier = 140;
    this.lastY = 12;
    for (let i = 0; i < 6; i++) this.generateChunk();
  }

  private generateChunk(): void {
    const rng = this.rng;
    const difficulty = Math.min(1, this.chunk / 40);
    const gap = 40 + difficulty * 130 + rng.range(0, 40);
    const drop = rng.range(10, 40 + difficulty * 40);
    const width = rng.range(120, 260);
    const x0 = this.frontier + gap;
    const y0 = this.lastY + drop;
    const slope = rng.range(-0.05, 0.2);
    const y1 = y0 + width * slope;
    const material = rng.chance(0.18 + difficulty * 0.2) ? 'accel' : 'normal';
    this.track.addLine({ x1: x0, y1: y0, x2: x0 + width, y2: y1, layer: 'level', material });
    if (rng.chance(0.25)) {
      // Small bump in the middle of the ledge.
      const bx = x0 + width * rng.range(0.3, 0.7);
      const by = y0 + (bx - x0) * slope;
      this.track.addLine({ x1: bx - 14, y1: by, x2: bx, y2: by - 8, layer: 'level' });
      this.track.addLine({ x1: bx, y1: by - 8, x2: bx + 14, y2: by, layer: 'level' });
    }
    this.frontier = x0 + width;
    this.lastY = y1;
    this.chunk++;
  }

  update(run: Run, editor: Editor): void {
    const c = run.rider.center();
    while (this.frontier < c.x + 1400) this.generateChunk();
    const distance = (run.maxX - this.track.start.x) / PX_PER_METER;
    this.budget = 40 + distance * 0.55;
    editor.constraints.budget = this.budget;
    // Tailwind grows with time so the pace keeps climbing.
    const push = Math.min(0.06, 0.008 + run.frame * 0.00002);
    run.world.wind = (_x, _y, _f, out) => {
      out.x = push;
      out.y = 0;
    };
  }
}
