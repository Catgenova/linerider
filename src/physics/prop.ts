import { Point } from './point';
import { Bone } from './rider';

export type PropKind = 'domino' | 'crate' | 'boulder' | 'tnt' | 'cart' | 'cargo' | 'snowball' | 'rock' | 'ball';

export interface PropInit {
  id: number;
  kind: PropKind;
  x: number;
  y: number;
  /** Box dimensions (ignored for circles). */
  width?: number;
  height?: number;
  angle?: number;
  /** Circle radius (makes the prop a single rolling point). */
  radius?: number;
  friction?: number;
  mass?: number;
  gravityScale?: number;
  /** Explosive props detonate on hard impact or when another explosion reaches them. */
  explosive?: boolean;
  /** Start asleep: no gravity until touched or triggered (used for falling rocks). */
  dormant?: boolean;
}

/**
 * A physics toy: either a rigid Verlet box (dominoes, crates, TNT, carts, cargo) or a rolling
 * circle (boulders, snowballs). Boxes collide through their edges; circles through their radius.
 */
export class Prop {
  readonly id: number;
  readonly kind: PropKind;
  readonly points: Point[] = [];
  readonly bones: Bone[] = [];
  /** Point index pairs forming the collision outline (boxes only). */
  readonly edges: [number, number][] = [];
  readonly radius: number;
  readonly mass: number;
  readonly explosive: boolean;
  readonly originX: number;
  readonly originY: number;
  readonly width: number;
  readonly height: number;
  gravityScale: number;
  dormant: boolean;
  active = true;
  /** Rolling angle for circles, radians. */
  spin = 0;
  /** Frames until detonation once lit, or -1. */
  fuse = -1;
  /** Peak impact speed seen (used for damage / triggering). */
  lastImpact = 0;
  /** Accumulated damage for cargo. */
  damage = 0;
  /** Whether the prop has been displaced from where it started. */
  disturbed = false;

  constructor(init: PropInit) {
    this.id = init.id;
    this.kind = init.kind;
    this.radius = init.radius ?? 0;
    this.mass = init.mass ?? 1;
    this.explosive = init.explosive ?? false;
    this.gravityScale = init.gravityScale ?? 1;
    this.dormant = init.dormant ?? false;
    this.originX = init.x;
    this.originY = init.y;
    const friction = init.friction ?? (this.radius > 0 ? 0.04 : 0.4);
    if (this.radius > 0) {
      this.width = this.height = this.radius * 2;
      const p = new Point(init.x, init.y, friction);
      this.points.push(p);
      return;
    }
    const w = init.width ?? 10;
    const h = init.height ?? 10;
    this.width = w;
    this.height = h;
    const a = init.angle ?? 0;
    const cos = Math.cos(a);
    const sin = Math.sin(a);
    const corners = [
      [-w / 2, -h / 2],
      [w / 2, -h / 2],
      [w / 2, h / 2],
      [-w / 2, h / 2],
    ];
    corners.forEach(([cx, cy], i) => {
      const p = new Point(init.x + cx * cos - cy * sin, init.y + cx * sin + cy * cos, friction);
      p.index = i;
      this.points.push(p);
    });
    const link = (i: number, j: number) => {
      const pa = this.points[i];
      const pb = this.points[j];
      this.bones.push(new Bone(i, j, Math.sqrt((pa.x - pb.x) ** 2 + (pa.y - pb.y) ** 2), 'normal', 1));
    };
    link(0, 1);
    link(1, 2);
    link(2, 3);
    link(3, 0);
    link(0, 2);
    link(1, 3);
    this.edges.push([0, 1], [1, 2], [2, 3], [3, 0]);
  }

  get isCircle(): boolean {
    return this.radius > 0;
  }

  center(): { x: number; y: number } {
    let x = 0;
    let y = 0;
    for (const p of this.points) {
      x += p.x;
      y += p.y;
    }
    return { x: x / this.points.length, y: y / this.points.length };
  }

  velocity(): { x: number; y: number } {
    let x = 0;
    let y = 0;
    for (const p of this.points) {
      x += p.x - p.px;
      y += p.y - p.py;
    }
    return { x: x / this.points.length, y: y / this.points.length };
  }

  angle(): number {
    if (this.isCircle) return this.spin;
    const a = this.points[0];
    const b = this.points[1];
    return Math.atan2(b.y - a.y, b.x - a.x);
  }

  wake(): void {
    this.dormant = false;
  }

  step(gx: number, gy: number): void {
    if (!this.active) return;
    if (this.dormant) {
      for (const p of this.points) {
        p.vx = 0;
        p.vy = 0;
        p.px = p.x;
        p.py = p.y;
        p.contact = null;
      }
      return;
    }
    const g = this.gravityScale;
    for (const p of this.points) {
      // Light air damping keeps stacks from jittering forever.
      const vx = (p.x - p.px) * 0.999;
      const vy = (p.y - p.py) * 0.999;
      p.px = p.x - vx;
      p.py = p.y - vy;
      p.step(gx * g, gy * g);
    }
    if (this.isCircle) {
      const p = this.points[0];
      this.spin += (p.x - p.px) / this.radius;
    }
    const c = this.center();
    if (!this.disturbed && Math.abs(c.x - this.originX) + Math.abs(c.y - this.originY) > 3) {
      this.disturbed = true;
    }
  }

  satisfy(): void {
    if (!this.active || this.dormant) return;
    const pts = this.points;
    for (const bone of this.bones) {
      const pa = pts[bone.a];
      const pb = pts[bone.b];
      const dx = pa.x - pb.x;
      const dy = pa.y - pb.y;
      const d = Math.sqrt(dx * dx + dy * dy);
      if (d === 0) continue;
      const scalar = ((d - bone.rest) / d) * 0.5;
      pa.x -= dx * scalar;
      pa.y -= dy * scalar;
      pb.x += dx * scalar;
      pb.y += dy * scalar;
    }
  }

  /** Approximate speed of the whole prop. */
  speed(): number {
    const v = this.velocity();
    return Math.sqrt(v.x * v.x + v.y * v.y);
  }
}
