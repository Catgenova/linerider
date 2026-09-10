import { GRAVITY, ITERATIONS, LINE_ZONE } from './constants';
import type { Entity } from './entity';
import { LineGrid } from './grid';
import { Line, type CollideContext, type LineInit } from './line';
import type { Point } from './point';
import { Prop } from './prop';
import type { Rider } from './rider';

export type WorldEvent =
  | { type: 'death'; rider: number }
  | { type: 'crumble'; line: Line }
  | { type: 'break'; line: Line; x: number; y: number }
  | { type: 'explode'; x: number; y: number; radius: number }
  | { type: 'impact'; x: number; y: number; speed: number; prop?: Prop }
  | { type: 'pickup'; entity: Entity; x: number; y: number }
  | { type: 'trigger'; entity: Entity; x: number; y: number; label: string };

export type WindFn = (x: number, y: number, frame: number, out: { x: number; y: number }) => void;

/** A distance constraint between points of different bodies (e.g. cargo strapped to a sled). */
export interface Link {
  a: Point;
  b: Point;
  rest: number;
  /** Break when stretched past this fraction of rest length; Infinity never breaks. */
  endurance: number;
  broken: boolean;
  /** Fraction of the correction applied to `a` (0..1). */
  bias: number;
}

/** The simulation: static lines in a grid, riders, entities and props stepped in lockstep. */
export class World {
  frame = 0;
  readonly lines = new Map<number, Line>();
  readonly grid = new LineGrid();
  readonly riders: Rider[] = [];
  readonly entities: Entity[] = [];
  readonly props: Prop[] = [];
  readonly links: Link[] = [];
  gravityX = 0;
  gravityY = GRAVITY;
  gravityScale = 1;
  frictionScale = 1;
  wind: WindFn | null = null;
  events: WorldEvent[] = [];
  readonly crumbling = new Set<Line>();
  private readonly near: Line[] = [];
  private readonly ctx: CollideContext = { frame: 0, frictionScale: 1, riderFriction: 1 };
  private readonly windOut = { x: 0, y: 0 };
  nextDynamicId = 1_000_000;

  addLine(init: LineInit): Line {
    const line = new Line(init);
    this.lines.set(line.id, line);
    if (line.material.solid) this.grid.add(line);
    return line;
  }

  removeLine(id: number): Line | undefined {
    const line = this.lines.get(id);
    if (!line) return undefined;
    this.lines.delete(id);
    if (line.material.solid && !line.dead) this.grid.remove(line);
    this.crumbling.delete(line);
    return line;
  }

  /** Remove a line from collision but keep it in the map (renders as debris / gone). */
  killLine(line: Line): void {
    if (line.dead) return;
    line.dead = true;
    if (line.material.solid) this.grid.remove(line);
    this.crumbling.delete(line);
  }

  addRider(rider: Rider): number {
    this.riders.push(rider);
    return this.riders.length - 1;
  }

  addEntity(entity: Entity): void {
    this.entities.push(entity);
  }

  addProp(prop: Prop): void {
    this.props.push(prop);
  }

  addLink(a: Point, b: Point, endurance = Infinity, bias = 0.5): Link {
    const rest = Math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2);
    const link: Link = { a, b, rest, endurance, broken: false, bias };
    this.links.push(link);
    return link;
  }

  private satisfyLinks(): void {
    for (const link of this.links) {
      if (link.broken) continue;
      const { a, b } = link;
      const dx = a.x - b.x;
      const dy = a.y - b.y;
      const d = Math.sqrt(dx * dx + dy * dy);
      if (d === 0) continue;
      const scalar = (d - link.rest) / d;
      if (scalar > link.endurance) {
        link.broken = true;
        continue;
      }
      a.x -= dx * scalar * link.bias;
      a.y -= dy * scalar * link.bias;
      b.x += dx * scalar * (1 - link.bias);
      b.y += dy * scalar * (1 - link.bias);
    }
  }

  /** Advance the world by one simulation frame. */
  step(): void {
    this.frame++;
    this.ctx.frame = this.frame;
    this.ctx.frictionScale = this.frictionScale;
    this.events = [];

    for (const e of this.entities) if (e.active) e.update(this);
    for (const e of this.entities) if (e.active && e.applyForces) e.applyForces(this);

    const gx = this.gravityX * this.gravityScale;
    const gy = this.gravityY * this.gravityScale;
    for (const rider of this.riders) {
      if (this.wind) this.applyWind(rider.points, rider.forces);
      rider.step(gx, gy);
    }
    for (const prop of this.props) prop.step(gx, gy);

    for (let it = 0; it < ITERATIONS; it++) {
      for (const rider of this.riders) rider.satisfyBones();
      for (const prop of this.props) prop.satisfy();
      if (this.links.length) this.satisfyLinks();
      for (const rider of this.riders) {
        this.ctx.riderFriction = rider.frictionScale;
        for (const p of rider.points) this.collidePoint(p);
      }
      this.ctx.riderFriction = 1;
      for (const prop of this.props) {
        if (!prop.active || prop.dormant) continue;
        if (prop.isCircle) this.collideCircle(prop);
        else for (const p of prop.points) this.collidePoint(p);
      }
      for (const rider of this.riders) {
        for (const prop of this.props) {
          if (!prop.active) continue;
          this.collidePointsWithProp(rider.points, 1, prop, true);
        }
      }
      for (let i = 0; i < this.props.length; i++) {
        const a = this.props[i];
        if (!a.active) continue;
        for (let j = i + 1; j < this.props.length; j++) {
          const b = this.props[j];
          if (!b.active) continue;
          this.collideProps(a, b);
        }
      }
    }

    for (let i = 0; i < this.riders.length; i++) {
      const rider = this.riders[i];
      rider.checkFlip();
      if (rider.dead && rider.deathFrame < 0) {
        rider.deathFrame = this.frame;
        this.events.push({ type: 'death', rider: i });
      }
      rider.stepScarf(gy);
      const hip = rider.points[rider.model.anchor.hip];
      rider.trail.push(hip.x, hip.y);
      if (rider.trail.length > 64) rider.trail.splice(0, 2);
    }

    for (const line of this.crumbling) {
      if (this.frame >= line.crumbleAt) {
        this.killLine(line);
        this.events.push({ type: 'crumble', line });
      }
    }

    for (const prop of this.props) {
      if (!prop.active) continue;
      if (prop.fuse > 0) prop.fuse--;
      if (prop.fuse === 0) this.detonate(prop);
    }

    for (const e of this.entities) if (e.active && e.afterStep) e.afterStep(this);
  }

  private applyWind(points: Point[], forces: Float64Array): void {
    const w = this.wind!;
    for (let i = 0; i < points.length; i++) {
      w(points[i].x, points[i].y, this.frame, this.windOut);
      forces[i * 2] += this.windOut.x;
      forces[i * 2 + 1] += this.windOut.y;
    }
  }

  /** Collide a point against static lines then kinematic entity lines. */
  private collidePoint(p: Point): void {
    const near = this.grid.near(p.x, p.y, this.near);
    for (const line of near) {
      if (line.collide(p, this.ctx) && line.crumbleAt >= 0) this.crumbling.add(line);
    }
    for (const e of this.entities) {
      if (!e.active) continue;
      for (const line of e.lines) line.collide(p, this.ctx);
    }
  }

  /** Rolling circle against lines: treat the contact point as the rim. */
  private collideCircle(prop: Prop): void {
    const p = prop.points[0];
    const r = prop.radius;
    const check = (line: Line) => {
      if (line.dead || !line.material.solid) return;
      const rvx = p.vx - line.vx;
      const rvy = p.vy - line.vy;
      if (rvx * line.nx + rvy * line.ny <= 0) return;
      const sx = p.x - line.x1;
      const sy = p.y - line.y1;
      const doty = sx * line.nx + sy * line.ny;
      if (doty <= -r || doty >= LINE_ZONE) return;
      const dotx = (sx * line.dx + sy * line.dy) * line.invLenSq;
      const margin = r * (line.length > 0 ? 1 / line.length : 0);
      if (dotx < line.limitL - margin || dotx > line.limitR + margin) return;
      const push = doty + r;
      p.x -= push * line.nx;
      p.y -= push * line.ny;
      const f = p.friction * line.material.frictionScale * this.frictionScale;
      if (f !== 0) {
        let fx = Math.abs(line.ny) * f * push;
        let fy = Math.abs(line.nx) * f * push;
        if (p.px > p.x) fx = -fx;
        if (p.py > p.y) fy = -fy;
        if (Math.abs(fx) > Math.abs(p.x - p.px)) fx = p.x - p.px;
        if (Math.abs(fy) > Math.abs(p.y - p.py)) fy = p.y - p.py;
        p.px += fx;
        p.py += fy;
      }
      if (line.material.accel) {
        const a = line.material.accel * line.multiplier;
        p.px -= line.ux * a;
        p.py -= line.uy * a;
      }
      if (line.material.crumbleFrames && line.crumbleAt < 0) {
        line.crumbleAt = this.frame + line.material.crumbleFrames;
        this.crumbling.add(line);
      }
      p.contact = line;
    };
    // Query a slightly wider neighbourhood than the point grid guarantees.
    const near = this.grid.inRect(p.x - r - 2, p.y - r - 2, p.x + r + 2, p.y + r + 2, this.near);
    for (const line of near) check(line);
    for (const e of this.entities) {
      if (!e.active) continue;
      for (const line of e.lines) check(line);
    }
  }

  /** Push points out of a prop's outline (box edges) or radius (circle). */
  private collidePointsWithProp(points: Point[], pointMass: number, prop: Prop, wakeProp: boolean): void {
    const share = prop.mass / (prop.mass + pointMass); // how much the point moves
    if (prop.isCircle) {
      const c = prop.points[0];
      const r = prop.radius;
      for (const p of points) {
        const dx = p.x - c.x;
        const dy = p.y - c.y;
        const d2 = dx * dx + dy * dy;
        if (d2 >= r * r || d2 === 0) continue;
        const d = Math.sqrt(d2);
        const pen = r - d;
        const nx = dx / d;
        const ny = dy / d;
        p.x += nx * pen * share;
        p.y += ny * pen * share;
        if (!prop.dormant) {
          c.x -= nx * pen * (1 - share);
          c.y -= ny * pen * (1 - share);
        } else if (wakeProp) prop.wake();
        this.noteImpact(p, prop);
      }
      return;
    }
    const margin = 1.2;
    for (const [ia, ib] of prop.edges) {
      const a = prop.points[ia];
      const b = prop.points[ib];
      const ex = b.x - a.x;
      const ey = b.y - a.y;
      const len2 = ex * ex + ey * ey;
      if (len2 === 0) continue;
      const len = Math.sqrt(len2);
      const nx = -ey / len;
      const ny = ex / len;
      for (const p of points) {
        const t = ((p.x - a.x) * ex + (p.y - a.y) * ey) / len2;
        if (t < -0.05 || t > 1.05) continue;
        const d = (p.x - a.x) * nx + (p.y - a.y) * ny;
        if (Math.abs(d) >= margin) {
          // Did the point cross the edge this frame?
          const dp = (p.px - a.x) * nx + (p.py - a.y) * ny;
          if ((dp >= 0) === (d >= 0)) continue;
          const side = dp >= 0 ? 1 : -1;
          this.resolveEdge(p, a, b, nx, ny, d, side * margin, t, share, prop, wakeProp);
        } else {
          const dp = (p.px - a.x) * nx + (p.py - a.y) * ny;
          const side = dp >= 0 ? 1 : -1;
          this.resolveEdge(p, a, b, nx, ny, d, side * margin, t, share, prop, wakeProp);
        }
      }
    }
  }

  private resolveEdge(
    p: Point,
    a: Point,
    b: Point,
    nx: number,
    ny: number,
    d: number,
    target: number,
    t: number,
    share: number,
    prop: Prop,
    wakeProp: boolean,
  ): void {
    const delta = target - d;
    p.x += nx * delta * share;
    p.y += ny * delta * share;
    if (prop.dormant) {
      if (wakeProp) prop.wake();
    } else {
      const tt = t < 0 ? 0 : t > 1 ? 1 : t;
      const wa = 1 - tt;
      const wb = tt;
      const norm = wa * wa + wb * wb || 1;
      const k = (delta * (1 - share)) / norm;
      a.x -= nx * k * wa;
      a.y -= ny * k * wa;
      b.x -= nx * k * wb;
      b.y -= ny * k * wb;
    }
    // Some friction between point and prop surface.
    const rvx = p.x - p.px - (a.x - a.px);
    const rvy = p.y - p.py - (a.y - a.py);
    const tx = ny;
    const ty = -nx;
    const vt = rvx * tx + rvy * ty;
    const f = 0.3 * vt;
    p.px += tx * f * share;
    p.py += ty * f * share;
    this.noteImpact(p, prop);
  }

  private noteImpact(p: Point, prop: Prop): void {
    const v = prop.velocity();
    const rvx = p.x - p.px - v.x;
    const rvy = p.y - p.py - v.y;
    const s = Math.sqrt(rvx * rvx + rvy * rvy);
    if (s > prop.lastImpact) prop.lastImpact = s;
    if (s > 2.5) {
      this.events.push({ type: 'impact', x: p.x, y: p.y, speed: s, prop });
      if (prop.explosive && prop.fuse < 0 && s > 4) prop.fuse = 3;
    }
    if (prop.kind === 'cargo' && s > 1.5) prop.damage += (s - 1.5) * 6;
  }

  private collideProps(a: Prop, b: Prop): void {
    if (a.dormant && b.dormant) return;
    if (a.isCircle && b.isCircle) {
      const pa = a.points[0];
      const pb = b.points[0];
      const dx = pb.x - pa.x;
      const dy = pb.y - pa.y;
      const rr = a.radius + b.radius;
      const d2 = dx * dx + dy * dy;
      if (d2 >= rr * rr || d2 === 0) return;
      const d = Math.sqrt(d2);
      const pen = rr - d;
      const nx = dx / d;
      const ny = dy / d;
      const shareA = a.dormant ? 0 : b.mass / (a.mass + b.mass);
      const shareB = b.dormant ? 0 : a.mass / (a.mass + b.mass);
      pa.x -= nx * pen * shareA;
      pa.y -= ny * pen * shareA;
      pb.x += nx * pen * shareB;
      pb.y += ny * pen * shareB;
      if (a.dormant) a.wake();
      if (b.dormant) b.wake();
      return;
    }
    if (a.isCircle) {
      this.collidePointsWithProp(a.points, a.mass, b, true);
      return;
    }
    if (b.isCircle) {
      this.collidePointsWithProp(b.points, b.mass, a, true);
      return;
    }
    this.collidePointsWithProp(a.points, a.mass, b, true);
    this.collidePointsWithProp(b.points, b.mass, a, true);
  }

  /** Radial impulse to everything nearby, detonating explosives in range. */
  explode(x: number, y: number, radius: number, strength: number): void {
    this.events.push({ type: 'explode', x, y, radius });
    const hit = (p: Point, mass: number) => {
      const dx = p.x - x;
      const dy = p.y - y;
      const d = Math.sqrt(dx * dx + dy * dy);
      if (d >= radius) return;
      const k = ((1 - d / radius) * strength) / mass;
      const nx = d === 0 ? 0 : dx / d;
      const ny = d === 0 ? -1 : dy / d;
      p.px -= nx * k;
      p.py -= ny * k;
    };
    for (const rider of this.riders) for (const p of rider.points) hit(p, 1);
    for (const prop of this.props) {
      if (!prop.active) continue;
      const c = prop.center();
      const d = Math.sqrt((c.x - x) ** 2 + (c.y - y) ** 2);
      if (d < radius) {
        if (prop.dormant) prop.wake();
        if (prop.explosive && prop.fuse < 0) prop.fuse = 4 + Math.floor(d / 20);
        prop.disturbed = true;
      }
      for (const p of prop.points) hit(p, prop.mass);
    }
    for (const e of this.entities) {
      if (!e.active) continue;
      for (const line of e.lines) {
        if (line.dead) continue;
        const mx = (line.x1 + line.x2) / 2;
        const my = (line.y1 + line.y2) / 2;
        if (e.kind === 'wall' && Math.sqrt((mx - x) ** 2 + (my - y) ** 2) < radius) {
          this.killLine(line);
          this.events.push({ type: 'break', line, x: mx, y: my });
        }
      }
    }
  }

  detonate(prop: Prop): void {
    if (!prop.active) return;
    prop.active = false;
    prop.fuse = -1;
    const c = prop.center();
    this.explode(c.x, c.y, 90, 14);
  }

  /** Solid lines within `radius` of a point (rendering/near-miss helper). */
  linesNear(x: number, y: number, radius: number, out: Line[]): Line[] {
    return this.grid.inRect(x - radius, y - radius, x + radius, y + radius, out);
  }
}
