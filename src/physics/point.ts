import type { Line } from './line';

/** A Verlet point mass. Velocity is implicit in (pos - prev); `vx/vy` caches the frame momentum. */
export class Point {
  x: number;
  y: number;
  px: number;
  py: number;
  vx = 0;
  vy = 0;
  friction: number;
  /** Line touched most recently this frame, or null. */
  contact: Line | null = null;
  /** Frame stamps used to apply once-per-frame material effects. */
  bounceFrame = -1;
  dragFrame = -1;
  carryFrame = -1;
  /** Extra per-point tag (e.g. index within its body). */
  index = 0;

  constructor(x: number, y: number, friction: number) {
    this.x = x;
    this.y = y;
    this.px = x;
    this.py = y;
    this.friction = friction;
  }

  /** Apply gravity/forces and advance: momentum = (pos - prev) + force; prev = pos; pos += momentum. */
  step(fx: number, fy: number): void {
    this.vx = this.x - this.px + fx;
    this.vy = this.y - this.py + fy;
    this.px = this.x;
    this.py = this.y;
    this.x += this.vx;
    this.y += this.vy;
    this.contact = null;
  }

  setVelocity(vx: number, vy: number): void {
    this.px = this.x - vx;
    this.py = this.y - vy;
    this.vx = vx;
    this.vy = vy;
  }

  get speed(): number {
    const dx = this.x - this.px;
    const dy = this.y - this.py;
    return Math.sqrt(dx * dx + dy * dy);
  }
}
