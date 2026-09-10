import { SIM_FPS } from '../physics/constants';
import { Prop } from '../physics/prop';
import { Rider } from '../physics/rider';
import { World, type Link } from '../physics/world';
import { buildEntity } from './entities';
import { makeWind, type Environment } from './environments';
import type { LevelDef, RunSummary } from './level';
import type { RiderDef } from './riders';
import { TrickTracker, type TrickEvent } from './tricks';
import type { Track, TrackChange } from './track';

export interface RunOptions {
  track: Track;
  level: LevelDef | null;
  riderDef: RiderDef;
  environment: Environment;
  /** Frames to fast-forward silently (flag playback). */
  skipFrames?: number;
  /** Arcade mode: keep going forever, no finish. */
  endless?: boolean;
}

export interface RunEvent {
  type: 'trick' | 'flag' | 'rescue' | 'finish' | 'death' | 'message';
  x: number;
  y: number;
  text: string;
  color: string;
  points?: number;
}

/** One playback of a track: owns the world, the rider, pickups, cargo and the objective tallies. */
export class Run {
  readonly world = new World();
  readonly rider: Rider;
  readonly tricks: TrickTracker;
  readonly level: LevelDef | null;
  readonly track: Track;
  readonly riderDef: RiderDef;
  readonly endless: boolean;
  finished = false;
  finishFrame = -1;
  died = false;
  failReason: string | null = null;
  readonly flags: boolean[];
  readonly rescues: boolean[];
  cargo: Prop | null = null;
  cargoLinks: Link[] = [];
  cargoIntegrity = 100;
  cargoLost = false;
  detonations = 0;
  maxX: number;
  readonly events: RunEvent[] = [];
  private readonly trickEvents: TrickEvent[] = [];
  private lastCargoV = { x: 0, y: 0 };
  private stalledFrames = 0;
  private visited = { minX: Infinity, minY: Infinity, maxX: -Infinity, maxY: -Infinity };
  private lastProgressFrame = 0;
  private readonly changeHandler: (c: TrackChange) => void;

  constructor(opts: RunOptions) {
    this.track = opts.track;
    this.level = opts.level;
    this.riderDef = opts.riderDef;
    this.endless = opts.endless ?? false;
    const env = opts.environment;
    const world = this.world;
    world.gravityScale = env.gravityScale;
    world.frictionScale = env.frictionScale;
    world.wind = makeWind(env);
    for (const l of opts.track.lines.values()) {
      const line = world.addLine(l);
      line.player = l.player;
    }
    const level = opts.level;
    if (level) {
      for (const def of level.entities ?? []) {
        const e = buildEntity(def, world);
        if (e) world.addEntity(e);
      }
      for (const pd of level.props ?? []) {
        world.addProp(new Prop({ ...pd, id: world.nextDynamicId++ }));
      }
      world.settleProps(200);
    }
    const start = opts.track.start;
    const rd = opts.riderDef;
    const vel = opts.track.startVelocity ?? { x: rd.startVelocity, y: 0 };
    this.rider = new Rider(rd.model, start.x, start.y, vel.x, vel.y, {
      gravityScale: rd.gravityScale,
      frictionScale: rd.frictionScale,
      enduranceScale: rd.enduranceScale,
    });
    world.addRider(this.rider);
    this.tricks = new TrickTracker(this.rider);
    this.maxX = start.x;
    this.flags = (level?.flags ?? []).map(() => false);
    this.rescues = (level?.rescues ?? []).map(() => false);
    if (level?.mode === 'delivery') this.attachCargo();
    this.changeHandler = (c) => this.applyChange(c);
    opts.track.onChange = this.changeHandler;
    const skip = opts.skipFrames ?? 0;
    for (let i = 0; i < skip; i++) this.step();
    this.events.length = 0;
  }

  dispose(): void {
    if (this.track.onChange === this.changeHandler) this.track.onChange = null;
  }

  /** Keep the world in sync with live edits (draw-while-riding). */
  private applyChange(c: TrackChange): void {
    const world = this.world;
    if (c.type === 'remove') world.removeLine(c.line.id);
    else if (c.type === 'add') {
      const line = world.addLine(c.line);
      line.player = c.line.player;
    } else {
      world.removeLine(c.line.id);
      const line = world.addLine(c.line);
      line.player = c.line.player;
    }
    for (const id of c.touched) {
      const data = this.track.lines.get(id);
      if (!data) continue;
      world.removeLine(id);
      const line = world.addLine(data);
      line.player = data.player;
    }
  }

  private attachCargo(): void {
    const rider = this.rider;
    const peg = rider.points[rider.model.anchor.tail];
    const box = new Prop({
      id: this.world.nextDynamicId++,
      kind: 'cargo',
      x: peg.x - 2,
      y: peg.y - 9,
      width: 6,
      height: 6,
      mass: 0.8,
      friction: 0.5,
    });
    for (const p of box.points) p.setVelocity(rider.points[0].x - rider.points[0].px, 0);
    this.world.addProp(box);
    this.cargo = box;
    const anchors = [rider.model.anchor.tail, rider.model.anchor.hip, rider.model.anchor.nose];
    for (const bi of [2, 3]) {
      for (const ai of anchors) {
        this.cargoLinks.push(this.world.addLink(box.points[bi], rider.points[ai], 0.22, 0.85));
      }
    }
    this.cargoLinks.push(this.world.addLink(box.points[0], rider.points[rider.model.anchor.shoulder], 0.3, 0.85));
  }

  get frame(): number {
    return this.world.frame;
  }

  get seconds(): number {
    return this.world.frame / SIM_FPS;
  }

  get done(): boolean {
    return this.finished || this.failReason !== null;
  }

  /** Advance one frame, collecting events for the HUD/effects. */
  step(): void {
    if (this.done) return;
    const world = this.world;
    const rider = this.rider;
    world.step();
    this.trickEvents.length = 0;
    this.tricks.update(world, this.trickEvents);
    for (const t of this.trickEvents) {
      this.events.push({
        type: 'trick',
        x: t.x,
        y: t.y,
        text: t.points > 0 ? `${t.name} +${t.points}${t.combo > 1 ? ` x${t.combo}` : ''}` : t.name,
        color: t.points > 0 ? (t.combo > 2 ? '#ff2bd6' : '#ffe93a') : '#ff4d4d',
        points: t.points,
      });
    }
    const c = rider.center();
    if (c.x > this.maxX) this.maxX = c.x;

    for (const ev of world.events) {
      if (ev.type === 'explode') this.detonations++;
      if (ev.type === 'death' && !this.died) {
        this.died = true;
        this.events.push({ type: 'death', x: c.x, y: c.y - 12, text: 'WIPEOUT', color: '#ff4d4d' });
      }
      if (ev.type === 'trigger') {
        this.events.push({ type: 'message', x: ev.x, y: ev.y - 20, text: ev.label, color: '#ffe93a' });
      }
    }
    if (rider.dead && !this.died) {
      this.died = true;
      this.events.push({ type: 'death', x: c.x, y: c.y - 12, text: 'WIPEOUT', color: '#ff4d4d' });
    }

    const level = this.level;
    if (level) {
      this.collectPickups(c, level);
      if (this.cargo) this.updateCargo();
      if (level.finish && !this.finished && !rider.dead) {
        const z = level.finish;
        if (c.x >= z.x && c.x <= z.x + z.w && c.y >= z.y && c.y <= z.y + z.h) {
          this.finished = true;
          this.finishFrame = world.frame;
          this.events.push({ type: 'finish', x: c.x, y: c.y - 16, text: 'FINISH', color: '#4dff9d' });
        }
      }
      if (level.timeLimit && world.frame > level.timeLimit * SIM_FPS && !this.finished) {
        this.failReason = 'Out of time';
      }
    } else if (this.track.finish && !this.finished && !rider.dead) {
      const z = this.track.finish;
      if (c.x >= z.x && c.x <= z.x + z.w && c.y >= z.y && c.y <= z.y + z.h) {
        this.finished = true;
        this.finishFrame = world.frame;
        this.events.push({ type: 'finish', x: c.x, y: c.y - 16, text: 'FINISH', color: '#4dff9d' });
      }
    }
    if (this.died && !this.failReason && this.deadFor() > 90 && !this.finished) {
      this.failReason = 'Crashed';
    }
    // A rider that has come to rest, or is just rocking back and forth, is not going anywhere.
    if (!this.finished && !this.failReason && !this.died && !this.endless && world.frame > 40) {
      const v = rider.velocity();
      if (Math.abs(v.x) + Math.abs(v.y) < 0.25) this.stalledFrames++;
      else this.stalledFrames = 0;
      const vis = this.visited;
      let grew = false;
      if (c.x < vis.minX - 4) { vis.minX = c.x; grew = true; }
      if (c.x > vis.maxX + 4) { vis.maxX = c.x; grew = true; }
      if (c.y < vis.minY - 4) { vis.minY = c.y; grew = true; }
      if (c.y > vis.maxY + 4) { vis.maxY = c.y; grew = true; }
      if (grew) this.lastProgressFrame = world.frame;
      if (this.stalledFrames > 120 || world.frame - this.lastProgressFrame > 320) this.failReason = 'Stalled';
    }
    // Fell into the void.
    if (!this.failReason && !this.finished && c.y > (this.track.bounds()?.maxY ?? 0) + 1500) {
      this.failReason = 'Lost in the void';
    }
  }

  private deadFor(): number {
    return this.rider.deathFrame >= 0 ? this.world.frame - this.rider.deathFrame : 0;
  }

  private collectPickups(c: { x: number; y: number }, level: LevelDef): void {
    const flags = level.flags ?? [];
    for (let i = 0; i < flags.length; i++) {
      if (this.flags[i]) continue;
      const f = flags[i];
      if (Math.hypot(c.x - f.x, c.y - (f.y - 8)) < 16) {
        this.flags[i] = true;
        this.events.push({ type: 'flag', x: f.x, y: f.y - 24, text: 'FLAG', color: '#ffe93a', points: 0 });
      }
    }
    const rescues = level.rescues ?? [];
    for (let i = 0; i < rescues.length; i++) {
      if (this.rescues[i]) continue;
      const r = rescues[i];
      if (!this.rider.dead && Math.hypot(c.x - r.x, c.y - (r.y - 5)) < 16) {
        this.rescues[i] = true;
        this.rider.passengers++;
        this.events.push({ type: 'rescue', x: r.x, y: r.y - 24, text: 'RESCUED', color: '#ff7ae8' });
      }
    }
  }

  private updateCargo(): void {
    const cargo = this.cargo!;
    if (!cargo.active) return;
    const v = cargo.velocity();
    const dvx = v.x - this.lastCargoV.x;
    const dvy = v.y - this.lastCargoV.y;
    this.lastCargoV = v;
    const jerk = Math.sqrt(dvx * dvx + dvy * dvy);
    if (this.world.frame > 5 && jerk > 1.6) {
      const dmg = (jerk - 1.6) * 9;
      this.cargoIntegrity = Math.max(0, this.cargoIntegrity - dmg);
      const c = cargo.center();
      this.events.push({ type: 'message', x: c.x, y: c.y - 12, text: `-${Math.round(dmg)}%`, color: '#ff7ae8' });
    }
    if (!this.cargoLost && this.cargoLinks.some((l) => l.broken)) {
      this.cargoLost = true;
      for (const l of this.cargoLinks) l.broken = true;
      const c = cargo.center();
      this.events.push({ type: 'message', x: c.x, y: c.y - 12, text: 'CARGO LOST', color: '#ff4d4d' });
    }
    if (this.cargoIntegrity <= 0 && !this.failReason) this.failReason = 'Cargo destroyed';
  }

  chaosScore(): number {
    let score = this.detonations * 400;
    for (const prop of this.world.props) {
      if (prop.kind === 'cargo') continue;
      if (prop.disturbed) score += 100;
      const c = prop.center();
      const d = Math.hypot(c.x - prop.originX, c.y - prop.originY);
      score += Math.min(300, d / 2);
      if (!prop.active) score += 200;
    }
    return Math.round(score);
  }

  summary(): RunSummary {
    const t = this.tricks;
    return {
      finished: this.finished,
      died: this.died,
      frames: this.finished ? this.finishFrame : this.world.frame,
      inkUsed: this.track.inkUsed(),
      airtimeFrames: t.airtimeFrames,
      flips: t.flips,
      trickScore: t.score,
      flagsCollected: this.flags.filter(Boolean).length,
      flagsTotal: this.flags.length,
      rescued: this.rescues.filter(Boolean).length,
      rescueTotal: this.rescues.length,
      cargoIntegrity: Math.round(this.cargoIntegrity),
      cargoLost: this.cargoLost,
      chaos: this.chaosScore(),
      nearMisses: t.nearMisses,
      hugeDrops: t.hugeDrops,
      distance: (this.maxX - this.track.start.x) / 10,
      survivedFrames: this.died ? this.rider.deathFrame : this.world.frame,
      failReason: this.failReason,
    };
  }
}
