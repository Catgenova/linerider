import { angleDiff } from '../core/vec';
import { SIM_FPS } from '../physics/constants';
import type { Line } from '../physics/line';
import type { Rider } from '../physics/rider';
import type { World } from '../physics/world';

export interface TrickEvent {
  name: string;
  points: number;
  x: number;
  y: number;
  combo: number;
}

/** Detects flips, airtime, drops, near misses, grinds, manuals and clean landings for one rider. */
export class TrickTracker {
  score = 0;
  combo = 0;
  comboTimer = 0;
  airtimeFrames = 0;
  flips = 0;
  backflips = 0;
  frontflips = 0;
  nearMisses = 0;
  cleanLandings = 0;
  hugeDrops = 0;
  grindFrames = 0;
  manuals = 0;
  bestAir = 0;
  private airborne = false;
  private airFrames = 0;
  private rotation = 0;
  private lastAngle = 0;
  private takeoffY = 0;
  private groundedFrames = 0;
  private grindRun = 0;
  private manualRun = 0;
  private readonly nearMissSeen = new Map<number, number>();
  private readonly scratch: Line[] = [];
  private bailed = false;

  constructor(private readonly rider: Rider) {
    this.lastAngle = rider.angle;
  }

  /** Call once per simulation frame after the world has stepped. */
  update(world: World, out: TrickEvent[]): void {
    const rider = this.rider;
    if (rider.dead) {
      if (!this.bailed) {
        this.bailed = true;
        this.combo = 0;
        this.comboTimer = 0;
        if (this.airborne) {
          const c = rider.center();
          out.push({ name: 'BAIL', points: 0, x: c.x, y: c.y - 14, combo: 0 });
        }
        this.airborne = false;
      }
      return;
    }
    if (this.comboTimer > 0) {
      this.comboTimer--;
      if (this.comboTimer === 0) this.combo = 0;
    }
    const angle = rider.angle;
    const delta = angleDiff(this.lastAngle, angle);
    this.lastAngle = angle;
    let grounded = false;
    let grindContact = false;
    for (const p of rider.points) {
      if (p.contact) {
        grounded = true;
        if (p.contact.material.grind) grindContact = true;
      }
    }
    const c = rider.center();
    const v = rider.velocity();

    if (!grounded) {
      this.groundedFrames = 0;
      this.airFrames++;
      if (this.airFrames === 5) {
        this.airborne = true;
        this.rotation = 0;
        this.takeoffY = c.y;
      }
      if (this.airborne) {
        this.rotation += delta;
        this.airtimeFrames++;
        this.checkNearMiss(world, out);
      }
      this.endGrind(out);
      this.endManual(out);
    } else {
      this.groundedFrames++;
      if (this.airborne) this.land(c, v, out);
      this.airborne = false;
      this.airFrames = 0;
      if (grindContact) {
        this.grindRun++;
        this.grindFrames++;
        if (this.grindRun % 40 === 0) this.award('GRIND', 60, c.x, c.y - 14, out);
      } else this.endGrind(out);
      this.checkManual(c, out);
    }
  }

  private land(c: { x: number; y: number }, v: { x: number; y: number }, out: TrickEvent[]): void {
    const air = this.airFrames;
    const rider = this.rider;
    const spins = Math.floor((Math.abs(this.rotation) + 0.6) / (Math.PI * 2));
    if (spins >= 1) {
      const back = (v.x >= 0 && this.rotation < 0) || (v.x < 0 && this.rotation > 0);
      const name = spins > 1 ? `${spins}x ${back ? 'BACKFLIP' : 'FRONTFLIP'}` : back ? 'BACKFLIP' : 'FRONTFLIP';
      this.flips += spins;
      if (back) this.backflips += spins;
      else this.frontflips += spins;
      this.award(name, 500 * spins + (spins > 1 ? 250 * (spins - 1) : 0), c.x, c.y - 18, out);
    }
    const seconds = air / SIM_FPS;
    if (seconds > this.bestAir) this.bestAir = seconds;
    if (seconds >= 1) this.award(`AIRTIME ${seconds.toFixed(1)}s`, Math.round(seconds * 60), c.x, c.y - 10, out);
    const drop = c.y - this.takeoffY;
    if (drop >= 150) {
      this.hugeDrops++;
      this.award('HUGE DROP', Math.round(drop / 2.5), c.x, c.y - 26, out);
    }
    // Clean landing: sled parallel to the surface and not dead.
    let contact: Line | null = null;
    for (const i of rider.model.vehicle) {
      const p = rider.points[i];
      if (p.contact) contact = p.contact;
    }
    if (contact && !rider.dead && air >= 12) {
      const lineAngle = Math.atan2(contact.dy, contact.dx);
      let diff = Math.abs(angleDiff(lineAngle, rider.angle));
      if (diff > Math.PI / 2) diff = Math.PI - diff;
      if (diff < 0.2) {
        this.cleanLandings++;
        this.award('CLEAN LANDING', 150, c.x, c.y - 34, out);
      }
    }
  }

  private checkNearMiss(world: World, out: TrickEvent[]): void {
    const rider = this.rider;
    const sh = rider.points[rider.model.anchor.shoulder];
    const hip = rider.points[rider.model.anchor.hip];
    const hx = sh.x + (sh.x - hip.x) * 0.6;
    const hy = sh.y + (sh.y - hip.y) * 0.6;
    const lines = world.linesNear(hx, hy, 9, this.scratch);
    for (const line of lines) {
      if (line.dead || !line.material.solid) continue;
      const seen = this.nearMissSeen.get(line.id);
      if (seen !== undefined && world.frame - seen < 120) continue;
      const dx = line.x2 - line.x1;
      const dy = line.y2 - line.y1;
      const len2 = dx * dx + dy * dy;
      if (len2 === 0) continue;
      let t = ((hx - line.x1) * dx + (hy - line.y1) * dy) / len2;
      t = t < 0 ? 0 : t > 1 ? 1 : t;
      const cx = line.x1 + dx * t;
      const cy = line.y1 + dy * t;
      const d = Math.hypot(hx - cx, hy - cy);
      if (d < 7 && d > 0.5) {
        this.nearMissSeen.set(line.id, world.frame);
        this.nearMisses++;
        this.award('NEAR MISS', 100, hx, hy - 12, out);
      }
    }
  }

  private checkManual(c: { x: number; y: number }, out: TrickEvent[]): void {
    const rider = this.rider;
    if (rider.model.id !== 'sled') return;
    const tail = rider.points[rider.model.anchor.tail].contact;
    const nose = rider.points[rider.model.anchor.nose].contact;
    const body = rider.bodyTouching();
    if (tail && !nose && !body && Math.abs(rider.velocity().x) > 2) {
      this.manualRun++;
      if (this.manualRun === 30) {
        this.manuals++;
        this.award('MANUAL', 120, c.x, c.y - 16, out);
      }
    } else this.endManual(out);
  }

  private endGrind(out: TrickEvent[]): void {
    void out;
    this.grindRun = 0;
  }

  private endManual(out: TrickEvent[]): void {
    void out;
    this.manualRun = 0;
  }

  private award(name: string, base: number, x: number, y: number, out: TrickEvent[]): void {
    this.combo++;
    this.comboTimer = 100;
    const mult = Math.min(3, 1 + (this.combo - 1) * 0.25);
    const points = Math.round(base * mult);
    this.score += points;
    out.push({ name, points, x, y, combo: this.combo });
  }
}
