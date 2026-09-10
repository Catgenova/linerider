import { EXTENSION_PX, LINE_ZONE, MAX_EXTENSION_RATIO } from './constants';
import { MATERIALS, type Material, type MaterialId } from './materials';
import type { Point } from './point';

export interface LineInit {
  id: number;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  material?: MaterialId;
  flipped?: boolean;
  leftExt?: boolean;
  rightExt?: boolean;
  multiplier?: number;
}

export interface CollideContext {
  frame: number;
  /** Environment-wide friction multiplier (glacier levels use 0). */
  frictionScale: number;
  /** Per-rider friction multiplier. */
  riderFriction: number;
}

/**
 * A one-sided collision segment. The normal points into the solid side; points that end a frame
 * within LINE_ZONE px past the surface while moving into it are projected back onto the surface.
 */
export class Line {
  readonly id: number;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  material: Material;
  flipped: boolean;
  leftExt: boolean;
  rightExt: boolean;
  multiplier: number;

  dx = 0;
  dy = 0;
  length = 0;
  invLenSq = 0;
  /** Unit direction along the drawn line (reversed when flipped). */
  ux = 0;
  uy = 0;
  /** Unit normal pointing into the solid side. */
  nx = 0;
  ny = 0;
  limitL = 0;
  limitR = 1;

  /** Frame at which a crumbling line disappears, or -1. */
  crumbleAt = -1;
  /** Removed from simulation (crumbled, broken wall...). */
  dead = false;
  /** Velocity of the surface (kinematic platforms), px/frame. */
  vx = 0;
  vy = 0;
  /** Owning entity id for dynamic lines, else -1. */
  owner = -1;
  /** Which player drew it (co-op), 0 = level/default. */
  player = 0;

  constructor(init: LineInit) {
    this.id = init.id;
    this.x1 = init.x1;
    this.y1 = init.y1;
    this.x2 = init.x2;
    this.y2 = init.y2;
    this.material = MATERIALS[init.material ?? 'normal'];
    this.flipped = init.flipped ?? false;
    this.leftExt = init.leftExt ?? false;
    this.rightExt = init.rightExt ?? false;
    this.multiplier = init.multiplier ?? 1;
    this.recompute();
  }

  recompute(): void {
    this.dx = this.x2 - this.x1;
    this.dy = this.y2 - this.y1;
    const lenSq = this.dx * this.dx + this.dy * this.dy;
    this.length = Math.sqrt(lenSq);
    this.invLenSq = lenSq > 0 ? 1 / lenSq : 0;
    const inv = this.length > 0 ? 1 / this.length : 0;
    const s = this.flipped ? -1 : 1;
    this.ux = this.dx * inv * s;
    this.uy = this.dy * inv * s;
    // Drawn left-to-right => normal points down (+y), so the top face is rideable.
    this.nx = -this.dy * inv * s;
    this.ny = this.dx * inv * s;
    const ext = this.length > 0 ? Math.min(MAX_EXTENSION_RATIO, EXTENSION_PX / this.length) : 0;
    this.limitL = this.leftExt ? -ext : 0;
    this.limitR = this.rightExt ? 1 + ext : 1;
  }

  get solid(): boolean {
    return this.material.solid && !this.dead;
  }

  /** Extended endpoints (used for grid registration). */
  extendedBounds(): [number, number, number, number] {
    const el = this.leftExt ? Math.min(EXTENSION_PX, this.length * MAX_EXTENSION_RATIO) : 0;
    const er = this.rightExt ? Math.min(EXTENSION_PX, this.length * MAX_EXTENSION_RATIO) : 0;
    const inv = this.length > 0 ? 1 / this.length : 0;
    const dxu = this.dx * inv;
    const dyu = this.dy * inv;
    return [this.x1 - dxu * el, this.y1 - dyu * el, this.x2 + dxu * er, this.y2 + dyu * er];
  }

  /** Resolve a point against this line. Returns true on contact. */
  collide(p: Point, ctx: CollideContext): boolean {
    if (this.dead || !this.material.solid) return false;
    const rvx = p.vx - this.vx;
    const rvy = p.vy - this.vy;
    if (rvx * this.nx + rvy * this.ny <= 0) return false;
    const sx = p.x - this.x1;
    const sy = p.y - this.y1;
    const doty = sx * this.nx + sy * this.ny;
    if (doty <= 0 || doty >= LINE_ZONE) return false;
    const dotx = (sx * this.dx + sy * this.dy) * this.invLenSq;
    if (dotx < this.limitL || dotx > this.limitR) return false;
    const m = this.material;
    if (m.oneWay && rvx * this.ux + rvy * this.uy <= 0) return false;

    // Project back onto the surface.
    p.x -= doty * this.nx;
    p.y -= doty * this.ny;

    // Friction: proportional to the impact depth, opposing motion per axis (classic quirk kept).
    const f = p.friction * m.frictionScale * ctx.frictionScale * ctx.riderFriction;
    if (f !== 0) {
      let fx = Math.abs(this.ny) * f * doty;
      let fy = Math.abs(this.nx) * f * doty;
      if (p.px > p.x) fx = -fx;
      if (p.py > p.y) fy = -fy;
      // Never let friction reverse the direction of travel.
      if (Math.abs(fx) > Math.abs(p.x - p.px)) fx = p.x - p.px;
      if (Math.abs(fy) > Math.abs(p.y - p.py)) fy = p.y - p.py;
      p.px += fx;
      p.py += fy;
    }

    // A moving surface carries the point with it (once per frame).
    if ((this.vx !== 0 || this.vy !== 0) && p.carryFrame !== ctx.frame) {
      p.carryFrame = ctx.frame;
      p.px -= this.vx;
      p.py -= this.vy;
    }

    if (m.accel) {
      const a = m.accel * this.multiplier;
      p.px -= this.ux * a;
      p.py -= this.uy * a;
    }
    if (m.conveyorSpeed) {
      const cvx = p.x - p.px;
      const cvy = p.y - p.py;
      const vt = cvx * this.ux + cvy * this.uy;
      let delta = m.conveyorSpeed * this.multiplier - vt;
      if (delta > 0.15) delta = 0.15;
      else if (delta < -0.15) delta = -0.15;
      p.px -= this.ux * delta;
      p.py -= this.uy * delta;
    }
    if (m.restitution && p.bounceFrame !== ctx.frame) {
      p.bounceFrame = ctx.frame;
      const vn = p.vx * this.nx + p.vy * this.ny;
      const k = (1 + m.restitution) * vn;
      const nvx = p.vx - k * this.nx;
      const nvy = p.vy - k * this.ny;
      p.setVelocity(nvx, nvy);
    }
    if ((m.drag || m.stickiness) && p.dragFrame !== ctx.frame) {
      p.dragFrame = ctx.frame;
      let cvx = p.x - p.px;
      let cvy = p.y - p.py;
      if (m.drag) {
        cvx *= m.drag;
        cvy *= m.drag;
      }
      if (m.stickiness) {
        const vn = cvx * this.nx + cvy * this.ny;
        cvx = (cvx - vn * this.nx) * (1 - m.stickiness);
        cvy = (cvy - vn * this.ny) * (1 - m.stickiness);
      }
      p.px = p.x - cvx;
      p.py = p.y - cvy;
    }
    if (m.crumbleFrames && this.crumbleAt < 0) {
      this.crumbleAt = ctx.frame + m.crumbleFrames;
    }
    p.contact = this;
    return true;
  }
}
