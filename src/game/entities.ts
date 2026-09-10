import type { Entity } from '../physics/entity';
import { Line } from '../physics/line';
import type { MaterialId } from '../physics/materials';
import { Prop } from '../physics/prop';
import type { Rider } from '../physics/rider';
import type { World } from '../physics/world';

export type EntityDef =
  | {
      type: 'platform';
      x: number;
      y: number;
      w: number;
      dx: number;
      dy: number;
      period: number;
      phase?: number;
      material?: MaterialId;
      thickness?: number;
    }
  | { type: 'gear'; x: number; y: number; radius: number; teeth: number; speed: number }
  | { type: 'pendulum'; x: number; y: number; length: number; width: number; amplitude: number; period: number; phase?: number }
  | { type: 'fan'; x: number; y: number; w: number; h: number; fx: number; fy: number }
  | { type: 'magnet'; x: number; y: number; radius: number; strength: number }
  | { type: 'cannon'; x: number; y: number; angle: number; power: number }
  | { type: 'wall'; x1: number; y1: number; x2: number; y2: number; threshold: number }
  | { type: 'balloon'; x: number; y: number; lift: number; duration: number }
  | { type: 'seesaw'; x: number; y: number; length: number; maxAngle?: number }
  | { type: 'train'; x: number; y: number; w: number; h: number; speed: number; range: number }
  | { type: 'rockfall'; x: number; y: number; count: number; spread: number; trigger: number; radius?: number }
  | { type: 'bridge'; x: number; y: number; w: number; segments: number; delay: number }
  | { type: 'collapse'; x1: number; y1: number; x2: number; y2: number; at: number }
  | { type: 'avalanche'; startX: number; speed: number; delay: number; accel?: number; label?: string }
  | { type: 'snowball'; x: number; y: number; radius: number; trigger: number; push: number }
  | { type: 'spring'; x: number; y: number; w: number; angle?: number }
  | { type: 'ramp'; x: number; y: number; w: number; h: number };

let nextEntityId = 1;
const FONT = '"Orbitron", "Rajdhani", "Segoe UI", system-ui, sans-serif';

function newLine(world: World, x1: number, y1: number, x2: number, y2: number, material: MaterialId = 'normal', owner = -1): Line {
  const line = new Line({ id: world.nextDynamicId++, x1, y1, x2, y2, material });
  line.owner = owner;
  return line;
}

/** Base for entities whose lines are placed from local coordinates each frame. */
abstract class Kinematic implements Entity {
  readonly id = nextEntityId++;
  abstract readonly kind: string;
  lines: Line[] = [];
  active = true;
  /** Local segment coordinates, in order matching `lines`. */
  protected local: [number, number, number, number][] = [];
  protected originX = 0;
  protected originY = 0;

  /** Place all lines given a translation and rotation, computing velocities from the previous placement. */
  protected place(tx: number, ty: number, angle: number): void {
    const cos = Math.cos(angle);
    const sin = Math.sin(angle);
    for (let i = 0; i < this.lines.length; i++) {
      const [lx1, ly1, lx2, ly2] = this.local[i];
      const line = this.lines[i];
      const nx1 = tx + lx1 * cos - ly1 * sin;
      const ny1 = ty + lx1 * sin + ly1 * cos;
      const nx2 = tx + lx2 * cos - ly2 * sin;
      const ny2 = ty + lx2 * sin + ly2 * cos;
      const omx = (line.x1 + line.x2) / 2;
      const omy = (line.y1 + line.y2) / 2;
      line.x1 = nx1;
      line.y1 = ny1;
      line.x2 = nx2;
      line.y2 = ny2;
      line.recompute();
      line.vx = (nx1 + nx2) / 2 - omx;
      line.vy = (ny1 + ny2) / 2 - omy;
    }
  }

  abstract update(world: World): void;
}

/** A bar that oscillates along (dx,dy). Rideable on top and solid underneath. */
export class MovingPlatform extends Kinematic {
  readonly kind = 'platform';
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'platform' }>,
  ) {
    super();
    const t = def.thickness ?? 3;
    const w = def.w;
    this.local = [
      [-w / 2, 0, w / 2, 0],
      [w / 2, t, -w / 2, t],
      [w / 2, 0, w / 2, t],
      [-w / 2, t, -w / 2, 0],
    ];
    const mat = def.material ?? 'normal';
    this.lines = this.local.map(() => newLine(world, 0, 0, 0, 0, mat, this.id));
    this.place(def.x, def.y, 0);
    this.update(world);
  }

  update(world: World): void {
    const d = this.def;
    const phase = (world.frame / d.period) * Math.PI * 2 + (d.phase ?? 0);
    const s = Math.sin(phase);
    this.place(d.x + d.dx * s, d.y + d.dy * s, 0);
  }

  render(ctx: CanvasRenderingContext2D): void {
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = 'rgba(255,240,74,0.25)';
    ctx.setLineDash([3, 3]);
    ctx.lineWidth = 0.8;
    ctx.beginPath();
    ctx.moveTo(d.x - d.dx, d.y - d.dy);
    ctx.lineTo(d.x + d.dx, d.y + d.dy);
    ctx.stroke();
    ctx.restore();
  }
}

/** A rotating toothed wheel. */
export class Gear extends Kinematic {
  readonly kind = 'gear';
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'gear' }>,
  ) {
    super();
    const n = def.teeth * 4;
    const pts: [number, number][] = [];
    for (let i = 0; i < n; i++) {
      const a = (i / n) * Math.PI * 2;
      const r = i % 4 < 2 ? def.radius : def.radius * 0.82;
      pts.push([Math.cos(a) * r, Math.sin(a) * r]);
    }
    for (let i = 0; i < n; i++) {
      const [x1, y1] = pts[i];
      const [x2, y2] = pts[(i + 1) % n];
      this.local.push([x1, y1, x2, y2]);
    }
    this.lines = this.local.map(() => newLine(world, 0, 0, 0, 0, 'normal', this.id));
    this.place(def.x, def.y, 0);
  }

  update(world: World): void {
    this.place(this.def.x, this.def.y, world.frame * this.def.speed);
  }

  render(ctx: CanvasRenderingContext2D, _time: number, pulse: number): void {
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = `rgba(255,240,74,${0.25 + 0.15 * pulse})`;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(d.x, d.y, d.radius * 0.35, 0, Math.PI * 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(d.x, d.y, d.radius * 0.1, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  }
}

/** A swinging bar hanging from a pivot. */
export class Pendulum extends Kinematic {
  readonly kind = 'pendulum';
  private angle = 0;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'pendulum' }>,
  ) {
    super();
    const w = def.width;
    const L = def.length;
    this.local = [
      [-w / 2, L, w / 2, L],
      [w / 2, L + 3, -w / 2, L + 3],
      [w / 2, L, w / 2, L + 3],
      [-w / 2, L + 3, -w / 2, L],
    ];
    this.lines = this.local.map(() => newLine(world, 0, 0, 0, 0, 'normal', this.id));
    this.place(def.x, def.y, 0);
  }

  update(world: World): void {
    const d = this.def;
    this.angle = d.amplitude * Math.sin((world.frame / d.period) * Math.PI * 2 + (d.phase ?? 0));
    this.place(d.x, d.y, this.angle);
  }

  render(ctx: CanvasRenderingContext2D): void {
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = 'rgba(255,240,74,0.7)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(d.x, d.y);
    ctx.lineTo(d.x - Math.sin(this.angle) * d.length, d.y + Math.cos(this.angle) * d.length);
    ctx.stroke();
    ctx.fillStyle = '#fff04a';
    ctx.beginPath();
    ctx.arc(d.x, d.y, 2, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }
}

/** A rectangular zone that pushes rider points. */
export class Fan implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'fan';
  lines: Line[] = [];
  active = true;
  constructor(private def: Extract<EntityDef, { type: 'fan' }>) {}

  update(): void {}

  applyForces(world: World): void {
    const d = this.def;
    for (const rider of world.riders) {
      for (let i = 0; i < rider.points.length; i++) {
        const p = rider.points[i];
        if (p.x < d.x || p.x > d.x + d.w || p.y < d.y || p.y > d.y + d.h) continue;
        rider.forces[i * 2] += d.fx;
        rider.forces[i * 2 + 1] += d.fy;
      }
    }
    for (const prop of world.props) {
      if (!prop.active || prop.dormant) continue;
      for (const p of prop.points) {
        if (p.x < d.x || p.x > d.x + d.w || p.y < d.y || p.y > d.y + d.h) continue;
        p.px -= d.fx / prop.mass;
        p.py -= d.fy / prop.mass;
      }
    }
  }

  render(ctx: CanvasRenderingContext2D, time: number, pulse: number): void {
    const d = this.def;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = `rgba(57,246,255,${0.05 + 0.03 * pulse})`;
    ctx.fillRect(d.x, d.y, d.w, d.h);
    ctx.strokeStyle = 'rgba(57,246,255,0.45)';
    ctx.lineWidth = 0.8;
    const len = Math.hypot(d.fx, d.fy) || 1;
    const ux = d.fx / len;
    const uy = d.fy / len;
    const streaks = Math.max(3, Math.floor((d.w + d.h) / 30));
    for (let i = 0; i < streaks; i++) {
      const t = ((time * 60 + i * 37) % 100) / 100;
      const bx = d.x + ((i * 53) % Math.max(1, d.w));
      const by = d.y + ((i * 31) % Math.max(1, d.h));
      const sx = bx + ux * t * Math.max(d.w, d.h) * 0.5;
      const sy = by + uy * t * Math.max(d.w, d.h) * 0.5;
      if (sx < d.x || sx > d.x + d.w || sy < d.y || sy > d.y + d.h) continue;
      ctx.beginPath();
      ctx.moveTo(sx, sy);
      ctx.lineTo(sx + ux * 8, sy + uy * 8);
      ctx.stroke();
    }
    ctx.restore();
  }
}

/** Attracts rider points within a radius. */
export class Magnet implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'magnet';
  lines: Line[] = [];
  active = true;
  constructor(private def: Extract<EntityDef, { type: 'magnet' }>) {}

  update(): void {}

  applyForces(world: World): void {
    const d = this.def;
    for (const rider of world.riders) {
      for (let i = 0; i < rider.points.length; i++) {
        const p = rider.points[i];
        const dx = d.x - p.x;
        const dy = d.y - p.y;
        const dist = Math.sqrt(dx * dx + dy * dy);
        if (dist > d.radius || dist < 1) continue;
        const k = (d.strength * (1 - dist / d.radius)) / dist;
        rider.forces[i * 2] += dx * k;
        rider.forces[i * 2 + 1] += dy * k;
      }
    }
  }

  render(ctx: CanvasRenderingContext2D, time: number, pulse: number): void {
    const d = this.def;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    for (let i = 0; i < 3; i++) {
      const k = ((time * 0.6 + i / 3) % 1);
      ctx.strokeStyle = `rgba(198,92,255,${(1 - k) * 0.5})`;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.arc(d.x, d.y, d.radius * (1 - k), 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.globalCompositeOperation = 'source-over';
    ctx.fillStyle = `rgba(198,92,255,${0.7 + 0.3 * pulse})`;
    ctx.beginPath();
    ctx.arc(d.x, d.y, 4, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }
}

/** Grabs the rider and launches them along the barrel. */
export class Cannon implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'cannon';
  lines: Line[] = [];
  active = true;
  private cooldown = 0;
  fired = 0;
  constructor(private def: Extract<EntityDef, { type: 'cannon' }>) {}

  update(): void {
    if (this.cooldown > 0) this.cooldown--;
  }

  afterStep(world: World): void {
    if (this.cooldown > 0) return;
    const d = this.def;
    const a = (d.angle * Math.PI) / 180;
    for (const rider of world.riders) {
      if (rider.dead) continue;
      const c = rider.center();
      if (Math.hypot(c.x - d.x, c.y - d.y) > 14) continue;
      const mx = d.x + Math.cos(a) * 26;
      const my = d.y + Math.sin(a) * 26;
      const offX = mx - c.x;
      const offY = my - c.y;
      const vx = Math.cos(a) * d.power;
      const vy = Math.sin(a) * d.power;
      for (const p of rider.points) {
        p.x += offX;
        p.y += offY;
        p.setVelocity(vx, vy);
      }
      for (const s of rider.scarf) {
        s.x += offX;
        s.y += offY;
        s.px = s.x;
        s.py = s.y;
      }
      this.cooldown = 60;
      this.fired++;
      world.events.push({ type: 'trigger', entity: this, x: mx, y: my, label: 'LAUNCH' });
    }
  }

  render(ctx: CanvasRenderingContext2D, _time: number, pulse: number): void {
    const d = this.def;
    const a = (d.angle * Math.PI) / 180;
    ctx.save();
    ctx.translate(d.x, d.y);
    ctx.globalCompositeOperation = 'lighter';
    ctx.strokeStyle = `rgba(255,61,127,${0.2 + 0.2 * pulse})`;
    ctx.lineWidth = 6;
    ctx.beginPath();
    ctx.arc(0, 0, 12, 0, Math.PI * 2);
    ctx.stroke();
    ctx.globalCompositeOperation = 'source-over';
    ctx.rotate(a);
    ctx.fillStyle = 'rgba(5,2,20,0.85)';
    ctx.strokeStyle = '#ff3d7f';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.rect(-4, -7, 30, 14);
    ctx.fill();
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(0, 0, 10, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.restore();
  }
}

/** Lines that shatter when hit hard enough. */
export class BreakableWall implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'wall';
  lines: Line[];
  active = true;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'wall' }>,
  ) {
    // Two-sided: one line each way.
    this.lines = [
      newLine(world, def.x1, def.y1, def.x2, def.y2, 'normal', this.id),
      newLine(world, def.x2, def.y2, def.x1, def.y1, 'normal', this.id),
    ];
  }

  update(): void {}

  afterStep(world: World): void {
    if (!this.active) return;
    const hit = (speed: number) => speed >= this.def.threshold;
    let broke = false;
    for (const rider of world.riders) {
      for (const p of rider.points) {
        if (p.contact && p.contact.owner === this.id && hit(Math.hypot(p.vx, p.vy))) broke = true;
      }
    }
    for (const prop of world.props) {
      if (!prop.active) continue;
      for (const p of prop.points) {
        if (p.contact && p.contact.owner === this.id && hit(Math.hypot(p.vx, p.vy) * prop.mass)) broke = true;
      }
    }
    if (!this.lines.some((l) => !l.dead)) this.active = false;
    if (broke) {
      for (const l of this.lines) {
        if (l.dead) continue;
        world.killLine(l);
      }
      world.events.push({
        type: 'break',
        line: this.lines[0],
        x: (this.def.x1 + this.def.x2) / 2,
        y: (this.def.y1 + this.def.y2) / 2,
      });
      this.active = false;
    }
  }

  render(ctx: CanvasRenderingContext2D, _time: number, pulse: number): void {
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = `rgba(255,122,69,${0.5 + 0.3 * pulse})`;
    ctx.lineWidth = 0.7;
    const n = 6;
    for (let i = 1; i < n; i++) {
      const t = i / n;
      const x = d.x1 + (d.x2 - d.x1) * t;
      const y = d.y1 + (d.y2 - d.y1) * t;
      ctx.beginPath();
      ctx.moveTo(x - 2, y - 2);
      ctx.lineTo(x + 2, y + 2);
      ctx.stroke();
    }
    ctx.restore();
  }
}

/** Lifts the rider for a while once grabbed. */
export class Balloon implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'balloon';
  lines: Line[] = [];
  active = true;
  private holder = -1;
  private remaining = 0;
  constructor(private def: Extract<EntityDef, { type: 'balloon' }>) {}

  update(): void {}

  applyForces(world: World): void {
    if (this.holder < 0) return;
    const rider = world.riders[this.holder];
    if (!rider || rider.dead) {
      this.active = false;
      return;
    }
    const per = this.def.lift / rider.points.length;
    for (let i = 0; i < rider.points.length; i++) rider.forces[i * 2 + 1] -= per;
  }

  afterStep(world: World): void {
    if (this.holder >= 0) {
      this.remaining--;
      if (this.remaining <= 0) {
        const rider = world.riders[this.holder];
        const sh = rider.points[rider.model.anchor.shoulder];
        world.events.push({ type: 'trigger', entity: this, x: sh.x, y: sh.y - 16, label: 'POP' });
        this.active = false;
      }
      return;
    }
    const d = this.def;
    for (let i = 0; i < world.riders.length; i++) {
      const rider = world.riders[i];
      if (rider.dead) continue;
      const c = rider.center();
      if (Math.hypot(c.x - d.x, c.y - d.y) < 16) {
        this.holder = i;
        this.riderRef = rider;
        this.remaining = d.duration;
        world.events.push({ type: 'pickup', entity: this, x: d.x, y: d.y });
      }
    }
  }

  render(ctx: CanvasRenderingContext2D, time: number, pulse: number): void {
    const d = this.def;
    let x = d.x;
    let y = d.y;
    if (this.holder >= 0 && this.riderRef) {
      const sh = this.riderRef.points[this.riderRef.model.anchor.shoulder];
      x = sh.x + Math.sin(time * 3) * 2;
      y = sh.y - 18;
      ctx.save();
      ctx.strokeStyle = 'rgba(255,255,255,0.6)';
      ctx.lineWidth = 0.6;
      ctx.beginPath();
      ctx.moveTo(sh.x, sh.y);
      ctx.lineTo(x, y + 6);
      ctx.stroke();
      ctx.restore();
    } else {
      y += Math.sin(time * 2 + d.x) * 2;
    }
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = `rgba(255,122,232,${0.15 + 0.1 * pulse})`;
    ctx.beginPath();
    ctx.arc(x, y, 9, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalCompositeOperation = 'source-over';
    ctx.strokeStyle = '#ff7ae8';
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.ellipse(x, y, 5, 6, 0, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  }

  /** Renderer helper: the rider currently holding the balloon. */
  riderRef: Rider | null = null;
}

/** A beam that tilts under the rider's weight. */
export class Seesaw extends Kinematic {
  readonly kind = 'seesaw';
  private angle = 0;
  private angVel = 0;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'seesaw' }>,
  ) {
    super();
    const L = def.length;
    this.local = [
      [-L / 2, 0, L / 2, 0],
      [L / 2, 3, -L / 2, 3],
    ];
    this.lines = this.local.map(() => newLine(world, 0, 0, 0, 0, 'normal', this.id));
    this.place(def.x, def.y, 0);
  }

  update(): void {
    const max = this.def.maxAngle ?? 0.45;
    this.angVel *= 0.97;
    this.angle += this.angVel;
    if (this.angle > max) {
      this.angle = max;
      this.angVel *= -0.2;
    } else if (this.angle < -max) {
      this.angle = -max;
      this.angVel *= -0.2;
    }
    this.place(this.def.x, this.def.y, this.angle);
  }

  afterStep(world: World): void {
    let torque = 0;
    const cos = Math.cos(this.angle);
    const sin = Math.sin(this.angle);
    for (const rider of world.riders) {
      for (const p of rider.points) {
        if (!p.contact || p.contact.owner !== this.id) continue;
        const lever = (p.x - this.def.x) * cos + (p.y - this.def.y) * sin;
        torque += lever;
      }
    }
    for (const prop of world.props) {
      if (!prop.active) continue;
      for (const p of prop.points) {
        if (!p.contact || p.contact.owner !== this.id) continue;
        torque += ((p.x - this.def.x) * cos + (p.y - this.def.y) * sin) * prop.mass;
      }
    }
    this.angVel += torque * 0.00035;
  }

  render(ctx: CanvasRenderingContext2D): void {
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = '#fff04a';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(d.x, d.y + 2);
    ctx.lineTo(d.x - 8, d.y + 16);
    ctx.lineTo(d.x + 8, d.y + 16);
    ctx.closePath();
    ctx.stroke();
    ctx.restore();
  }
}

/** A box that shuttles back and forth along the x axis. */
export class Train extends Kinematic {
  readonly kind = 'train';
  private t = 0;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'train' }>,
  ) {
    super();
    const w = def.w;
    const h = def.h;
    this.local = [
      [-w / 2, -h, w / 2, -h],
      [w / 2, -h, w / 2, 0],
      [w / 2, 0, -w / 2, 0],
      [-w / 2, 0, -w / 2, -h],
    ];
    this.lines = this.local.map(() => newLine(world, 0, 0, 0, 0, 'normal', this.id));
    this.place(def.x, def.y, 0);
  }

  update(world: World): void {
    const d = this.def;
    this.t = (world.frame * Math.abs(d.speed)) % (d.range * 2);
    const offset = this.t < d.range ? this.t : d.range * 2 - this.t;
    this.place(d.x + (d.speed >= 0 ? offset : -offset), d.y, 0);
  }

  render(ctx: CanvasRenderingContext2D, _time: number, pulse: number): void {
    const l = this.lines[0];
    const x = (l.x1 + l.x2) / 2;
    const y = l.y1;
    const d = this.def;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = `rgba(255,240,74,${0.06 + 0.04 * pulse})`;
    ctx.fillRect(x - d.w / 2, y, d.w, d.h);
    ctx.fillStyle = `rgba(57,246,255,${0.5 + 0.3 * pulse})`;
    const wins = Math.max(1, Math.floor(d.w / 14));
    for (let i = 0; i < wins; i++) {
      ctx.fillRect(x - d.w / 2 + 4 + i * 14, y + d.h * 0.25, 7, d.h * 0.3);
    }
    ctx.restore();
  }
}

/** Dormant rocks that drop when the rider crosses a trigger line. */
export class Rockfall implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'rockfall';
  lines: Line[] = [];
  active = true;
  private rocks: Prop[] = [];
  private triggered = false;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'rockfall' }>,
  ) {
    const r = def.radius ?? 9;
    for (let i = 0; i < def.count; i++) {
      const prop = new Prop({
        id: world.nextDynamicId++,
        kind: 'rock',
        x: def.x + (i - (def.count - 1) / 2) * def.spread,
        y: def.y - (i % 2) * r * 2.2,
        radius: r * (0.8 + ((i * 7) % 5) * 0.1),
        mass: 3,
        dormant: true,
      });
      world.addProp(prop);
      this.rocks.push(prop);
    }
  }

  update(world: World): void {
    if (this.triggered) return;
    for (const rider of world.riders) {
      if (rider.center().x >= this.def.trigger) {
        this.triggered = true;
        for (const rock of this.rocks) rock.wake();
        world.events.push({ type: 'trigger', entity: this, x: this.def.x, y: this.def.y, label: 'ROCKFALL!' });
      }
    }
  }

  render(ctx: CanvasRenderingContext2D, _time: number, pulse: number): void {
    if (this.triggered) return;
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = `rgba(255,122,69,${0.2 + 0.2 * pulse})`;
    ctx.setLineDash([3, 5]);
    ctx.lineWidth = 0.8;
    ctx.beginPath();
    ctx.moveTo(d.trigger, d.y - 200);
    ctx.lineTo(d.trigger, d.y + 400);
    ctx.stroke();
    ctx.restore();
  }
}

/** A bridge whose segments drop one after another once touched. */
export class Bridge implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'bridge';
  lines: Line[] = [];
  active = true;
  private touchedAt = -1;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'bridge' }>,
  ) {
    const seg = def.w / def.segments;
    for (let i = 0; i < def.segments; i++) {
      const line = newLine(world, def.x + i * seg, def.y, def.x + (i + 1) * seg, def.y, 'normal', this.id);
      line.leftExt = true;
      line.rightExt = true;
      line.recompute();
      this.lines.push(line);
    }
  }

  update(world: World): void {
    if (this.touchedAt < 0) return;
    const elapsed = world.frame - this.touchedAt;
    for (let i = 0; i < this.lines.length; i++) {
      const line = this.lines[i];
      if (!line.dead && elapsed >= this.def.delay * (i + 1)) {
        world.killLine(line);
        world.events.push({ type: 'crumble', line });
      }
    }
    if (this.lines.every((l) => l.dead)) this.active = false;
  }

  afterStep(world: World): void {
    if (this.touchedAt >= 0) return;
    for (const rider of world.riders) {
      for (const p of rider.points) {
        if (p.contact && p.contact.owner === this.id) {
          this.touchedAt = world.frame;
          return;
        }
      }
    }
  }

  render(ctx: CanvasRenderingContext2D, _time: number, pulse: number): void {
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = `rgba(255,122,69,${this.touchedAt >= 0 ? 0.6 + 0.4 * pulse : 0.3})`;
    ctx.lineWidth = 0.7;
    const seg = d.w / d.segments;
    for (let i = 0; i <= d.segments; i++) {
      const x = d.x + i * seg;
      ctx.beginPath();
      ctx.moveTo(x, d.y);
      ctx.lineTo(x, d.y + 10);
      ctx.stroke();
    }
    ctx.restore();
  }
}

/** A line that disappears at a fixed frame (collapsing structures on a timer). */
export class TimedCollapse implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'collapse';
  lines: Line[];
  active = true;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'collapse' }>,
  ) {
    this.lines = [newLine(world, def.x1, def.y1, def.x2, def.y2, 'normal', this.id)];
  }

  update(world: World): void {
    const line = this.lines[0];
    if (!line.dead && world.frame >= this.def.at) {
      world.killLine(line);
      world.events.push({ type: 'crumble', line });
      this.active = false;
    }
  }

  render(ctx: CanvasRenderingContext2D, time: number): void {
    void time;
    const d = this.def;
    ctx.save();
    ctx.strokeStyle = 'rgba(255,122,69,0.5)';
    ctx.setLineDash([2, 3]);
    ctx.lineWidth = 0.6;
    ctx.beginPath();
    ctx.moveTo(d.x1, d.y1 + 2);
    ctx.lineTo(d.x2, d.y2 + 2);
    ctx.stroke();
    ctx.restore();
  }
}

/** A wall of destruction that sweeps left to right. Riders it catches are killed. */
export class Avalanche implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'avalanche';
  lines: Line[] = [];
  active = true;
  x: number;
  caught = false;
  constructor(private def: Extract<EntityDef, { type: 'avalanche' }>) {
    this.x = def.startX;
  }

  update(world: World): void {
    const d = this.def;
    const t = world.frame - d.delay;
    if (t <= 0) {
      this.x = d.startX;
      return;
    }
    this.x = d.startX + d.speed * t + 0.5 * (d.accel ?? 0) * t * t;
  }

  afterStep(world: World): void {
    for (const rider of world.riders) {
      if (rider.dead) continue;
      if (rider.center().x < this.x) {
        rider.dead = true;
        this.caught = true;
        world.events.push({ type: 'trigger', entity: this, x: this.x, y: rider.center().y, label: 'CAUGHT' });
      }
    }
  }

  render(ctx: CanvasRenderingContext2D, time: number, pulse: number): void {
    const x = this.x;
    ctx.save();
    const g = ctx.createLinearGradient(x - 600, 0, x, 0);
    g.addColorStop(0, 'rgba(255,255,255,0.75)');
    g.addColorStop(0.8, 'rgba(200,240,255,0.55)');
    g.addColorStop(1, 'rgba(120,220,255,0.05)');
    ctx.fillStyle = g;
    ctx.fillRect(x - 3000, -6000, 3000, 12000);
    ctx.globalCompositeOperation = 'lighter';
    for (let i = 0; i < 40; i++) {
      const yy = ((i * 173 + time * 240) % 1400) - 700;
      const xx = x - ((i * 97 + time * 90) % 260);
      ctx.fillStyle = `rgba(255,255,255,${0.25 + 0.4 * ((i % 3) / 2)})`;
      ctx.beginPath();
      ctx.arc(xx, yy, 6 + (i % 5) * 4 + pulse * 2, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }
}

/** A giant rolling ball released when the rider crosses a trigger, pushed along relentlessly. */
export class Snowball implements Entity {
  readonly id = nextEntityId++;
  readonly kind = 'snowball';
  lines: Line[] = [];
  active = true;
  readonly prop: Prop;
  private triggered = false;
  constructor(
    world: World,
    private def: Extract<EntityDef, { type: 'snowball' }>,
  ) {
    this.prop = new Prop({
      id: world.nextDynamicId++,
      kind: 'snowball',
      x: def.x,
      y: def.y,
      radius: def.radius,
      mass: 40,
      dormant: true,
      friction: 0.02,
    });
    world.addProp(this.prop);
  }

  update(world: World): void {
    if (!this.triggered) {
      for (const rider of world.riders) {
        if (rider.center().x >= this.def.trigger) {
          this.triggered = true;
          this.prop.wake();
          world.events.push({ type: 'trigger', entity: this, x: this.def.x, y: this.def.y, label: 'RUN!' });
        }
      }
      return;
    }
    const p = this.prop.points[0];
    // Only push while it has ground under it (touched something last frame).
    if (p.contact) p.px -= this.def.push;
  }

  afterStep(world: World): void {
    if (!this.triggered) return;
    const c = this.prop.points[0];
    for (const rider of world.riders) {
      if (rider.dead) continue;
      const rc = rider.center();
      if (Math.hypot(rc.x - c.x, rc.y - c.y) < this.def.radius + 4) rider.dead = true;
    }
  }
}

/** Build an entity from its definition and register any props it needs. */
export function buildEntity(def: EntityDef, world: World): Entity | null {
  switch (def.type) {
    case 'platform':
      return new MovingPlatform(world, def);
    case 'gear':
      return new Gear(world, def);
    case 'pendulum':
      return new Pendulum(world, def);
    case 'fan':
      return new Fan(def);
    case 'magnet':
      return new Magnet(def);
    case 'cannon':
      return new Cannon(def);
    case 'wall':
      return new BreakableWall(world, def);
    case 'balloon':
      return new Balloon(def);
    case 'seesaw':
      return new Seesaw(world, def);
    case 'train':
      return new Train(world, def);
    case 'rockfall':
      return new Rockfall(world, def);
    case 'bridge':
      return new Bridge(world, def);
    case 'collapse':
      return new TimedCollapse(world, def);
    case 'avalanche':
      return new Avalanche(def);
    case 'snowball':
      return new Snowball(world, def);
    case 'spring': {
      // Springs and ramps are plain static lines; return null and let the level add lines.
      return null;
    }
    case 'ramp':
      return null;
    default:
      return null;
  }
}

/** Static lines some "object" definitions expand into (springs, ramps). */
export function entityStaticLines(def: EntityDef): { x1: number; y1: number; x2: number; y2: number; material: MaterialId }[] {
  if (def.type === 'spring') {
    const a = ((def.angle ?? 0) * Math.PI) / 180;
    const hx = (Math.cos(a) * def.w) / 2;
    const hy = (Math.sin(a) * def.w) / 2;
    return [{ x1: def.x - hx, y1: def.y - hy, x2: def.x + hx, y2: def.y + hy, material: 'spring' }];
  }
  if (def.type === 'ramp') {
    return [
      { x1: def.x, y1: def.y, x2: def.x + def.w, y2: def.y - def.h, material: 'normal' },
      { x1: def.x + def.w, y1: def.y - def.h, x2: def.x + def.w, y2: def.y, material: 'normal' },
    ];
  }
  return [];
}

export { FONT as ENTITY_FONT };
