import type { Rng } from '../core/rng';
import type { Run } from '../game/run';
import type { Track } from '../game/track';

/**
 * Menu attract mode: an endless rolling ride generated ahead of the rider. Terrain is a drifting
 * sum of sines whose downhill bias adapts to the rider's speed, with the odd kicker for airtime.
 * Crashes are part of the show; the game restarts the demo with the next environment.
 */
export class AttractDirector {
  private frontier: number;
  private lastY: number;
  private drift = 0.1;
  private readonly p1: number;
  private readonly p2: number;
  private lastPrune = 0;

  constructor(
    private readonly track: Track,
    private readonly rng: Rng,
  ) {
    this.p1 = rng.range(0, Math.PI * 2);
    this.p2 = rng.range(0, Math.PI * 2);
    track.start = { x: 0, y: 0 };
    track.addLine({ x1: -80, y1: 6, x2: 60, y2: 14, layer: 'level' });
    this.frontier = 60;
    this.lastY = 14;
    for (let i = 0; i < 8; i++) this.chunk(6);
  }

  private chunk(speed: number): void {
    const seg = 40;
    const target = speed > 9.5 ? -0.05 : speed < 4 ? 0.22 : 0.1;
    this.drift += (target - this.drift) * 0.5;
    const kicker = speed >= 5 && speed <= 9 && this.rng.chance(0.2);
    let x = this.frontier;
    let y = this.lastY;
    for (let i = 0; i < 8; i++) {
      const nx = x + seg;
      const slope = this.drift + 0.17 * Math.cos(nx / 260 + this.p1) + 0.16 * Math.cos(nx / 95 + this.p2);
      const ny = y + slope * seg;
      const material = slope > 0.22 && this.rng.chance(0.2) ? 'accel' : 'normal';
      this.track.addLine({ x1: x, y1: y, x2: nx, y2: ny, layer: 'level', material });
      x = nx;
      y = ny;
    }
    if (kicker) {
      const rx = x + 36;
      const ry = y - 12;
      this.track.addLine({ x1: x, y1: y, x2: rx, y2: ry, layer: 'level' });
      x = rx + 120;
      y = ry + 50;
      this.track.addLine({ x1: x, y1: y, x2: x + 240, y2: y + 62, layer: 'level' });
      x += 240;
      y += 62;
    }
    this.frontier = x;
    this.lastY = y;
  }

  /** Extend terrain ahead of the rider and drop lines far behind. */
  update(run: Run): void {
    const c = run.rider.center();
    const v = run.rider.velocity();
    const speed = Math.sqrt(v.x * v.x + v.y * v.y);
    while (this.frontier < c.x + 1600) this.chunk(speed);
    if (run.frame - this.lastPrune >= 40) {
      this.lastPrune = run.frame;
      for (const l of [...this.track.lines.values()]) {
        if (Math.max(l.x1, l.x2) < c.x - 2500) this.track.removeLine(l.id);
      }
    }
  }
}
