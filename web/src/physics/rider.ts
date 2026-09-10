import { ENDURANCE } from './constants';
import { Point } from './point';

export interface PointDef {
  x: number;
  y: number;
  friction: number;
}

export interface BoneDef {
  a: number;
  b: number;
  kind?: 'normal' | 'mount' | 'repel';
  /** Repel bones only push once closer than factor * natural distance. */
  factor?: number;
}

export interface RiderModel {
  id: string;
  points: PointDef[];
  bones: BoneDef[];
  /** Indices used for orientation, camera and trick detection. */
  anchor: { tail: number; nose: number; hip: number; shoulder: number };
  /** Points that count as the vehicle (sled or board) for airtime/landing detection. */
  vehicle: number[];
  draw: {
    /** Closed polygons for the vehicle. */
    vehicle: number[][];
    /** Body segments. */
    body: [number, number][];
    /** Head is projected past this point, away from the hip. */
    headRadius: number;
    headOffset: number;
  };
}

// Classic sled rider. Coordinates are relative to the sled's rear peg, y pointing down.
export const PEG = 0;
export const TAIL = 1;
export const NOSE = 2;
export const STRING = 3;
export const BUTT = 4;
export const SHOULDER = 5;
export const RHAND = 6;
export const LHAND = 7;
export const LFOOT = 8;
export const RFOOT = 9;

export const SLED_MODEL: RiderModel = {
  id: 'sled',
  points: [
    { x: 0, y: 0, friction: 0.8 }, // peg
    { x: 0, y: 5, friction: 0 }, // tail
    { x: 15, y: 5, friction: 0 }, // nose
    { x: 17.5, y: 0, friction: 0 }, // string
    { x: 5, y: 0, friction: 0.8 }, // butt
    { x: 5, y: -5.5, friction: 0.8 }, // shoulder
    { x: 11.5, y: -5, friction: 0.1 }, // right hand
    { x: 11.5, y: -5, friction: 0.1 }, // left hand
    { x: 10, y: 5, friction: 0 }, // left foot
    { x: 10, y: 5, friction: 0 }, // right foot
  ],
  bones: [
    { a: PEG, b: TAIL },
    { a: TAIL, b: NOSE },
    { a: NOSE, b: STRING },
    { a: STRING, b: PEG },
    { a: PEG, b: NOSE },
    { a: STRING, b: TAIL },
    { a: PEG, b: BUTT, kind: 'mount' },
    { a: TAIL, b: BUTT, kind: 'mount' },
    { a: NOSE, b: BUTT, kind: 'mount' },
    { a: SHOULDER, b: BUTT },
    { a: SHOULDER, b: RHAND },
    { a: SHOULDER, b: LHAND },
    { a: BUTT, b: LFOOT },
    { a: BUTT, b: RFOOT },
    { a: SHOULDER, b: RHAND },
    { a: SHOULDER, b: PEG, kind: 'mount' },
    { a: STRING, b: LHAND, kind: 'mount' },
    { a: STRING, b: RHAND, kind: 'mount' },
    { a: LFOOT, b: NOSE, kind: 'mount' },
    { a: RFOOT, b: NOSE, kind: 'mount' },
    { a: SHOULDER, b: LFOOT, kind: 'repel', factor: 0.5 },
    { a: SHOULDER, b: RFOOT, kind: 'repel', factor: 0.5 },
  ],
  anchor: { tail: TAIL, nose: NOSE, hip: BUTT, shoulder: SHOULDER },
  vehicle: [PEG, TAIL, NOSE, STRING],
  draw: {
    vehicle: [[PEG, TAIL, NOSE, STRING]],
    body: [
      [BUTT, SHOULDER],
      [SHOULDER, RHAND],
      [SHOULDER, LHAND],
      [BUTT, LFOOT],
      [BUTT, RFOOT],
    ],
    headRadius: 2.6,
    headOffset: 3.2,
  },
};

// Snowboarder. A rigid board with a standing rider mounted through the hips.
const BT = 0;
const BN = 1;
const BTT = 2;
const BNT = 3;
const LF = 4;
const RF = 5;
const HIP = 6;
const SH = 7;
const LH = 8;
const RH = 9;

export const BOARD_MODEL: RiderModel = {
  id: 'board',
  points: [
    { x: 0, y: 5, friction: 0 }, // board tail
    { x: 20, y: 5, friction: 0 }, // board nose
    { x: 2, y: 2.5, friction: 0 }, // tail top
    { x: 18, y: 2.5, friction: 0 }, // nose top
    { x: 6, y: 2.5, friction: 0.3 }, // left foot
    { x: 14, y: 2.5, friction: 0.3 }, // right foot
    { x: 10, y: -4, friction: 0.8 }, // hip
    { x: 10, y: -12, friction: 0.8 }, // shoulder
    { x: 3, y: -5, friction: 0.1 }, // left hand
    { x: 17, y: -5, friction: 0.1 }, // right hand
  ],
  bones: [
    { a: BT, b: BN },
    { a: BTT, b: BNT },
    { a: BT, b: BTT },
    { a: BN, b: BNT },
    { a: BT, b: BNT },
    { a: BN, b: BTT },
    { a: LF, b: BT, kind: 'mount' },
    { a: LF, b: BN, kind: 'mount' },
    { a: RF, b: BT, kind: 'mount' },
    { a: RF, b: BN, kind: 'mount' },
    { a: HIP, b: LF },
    { a: HIP, b: RF },
    { a: HIP, b: SH },
    { a: SH, b: LH },
    { a: SH, b: RH },
    { a: HIP, b: LH },
    { a: HIP, b: RH },
    { a: HIP, b: BT, kind: 'mount' },
    { a: HIP, b: BN, kind: 'mount' },
    { a: SH, b: BT, kind: 'mount' },
    { a: SH, b: BN, kind: 'mount' },
    { a: SH, b: BTT, kind: 'repel', factor: 0.5 },
    { a: SH, b: BNT, kind: 'repel', factor: 0.5 },
  ],
  anchor: { tail: BT, nose: BN, hip: HIP, shoulder: SH },
  vehicle: [BT, BN, BTT, BNT],
  draw: {
    vehicle: [[BT, BN, BNT, BTT]],
    body: [
      [LF, HIP],
      [RF, HIP],
      [HIP, SH],
      [SH, LH],
      [SH, RH],
    ],
    headRadius: 2.6,
    headOffset: 3.4,
  },
};

export class Bone {
  readonly a: number;
  readonly b: number;
  readonly rest: number;
  readonly kind: 'normal' | 'mount' | 'repel';
  readonly endurance: number;

  constructor(a: number, b: number, rest: number, kind: Bone['kind'], enduranceScale: number) {
    this.a = a;
    this.b = b;
    this.rest = rest;
    this.kind = kind;
    this.endurance = ENDURANCE * rest * 0.5 * enduranceScale;
  }
}

export interface ScarfNode {
  x: number;
  y: number;
  px: number;
  py: number;
}

export interface RiderOptions {
  gravityScale?: number;
  frictionScale?: number;
  enduranceScale?: number;
  scarfLength?: number;
}

/** A rider: point masses, bones, and a cosmetic scarf. Dies when a mount bone snaps. */
export class Rider {
  readonly model: RiderModel;
  readonly points: Point[];
  readonly bones: Bone[];
  readonly forces: Float64Array;
  readonly scarf: ScarfNode[] = [];
  gravityScale: number;
  frictionScale: number;
  dead = false;
  deathFrame = -1;
  /** Frames since creation. */
  age = 0;
  /** Trail of recent hip positions for rendering. */
  readonly trail: number[] = [];
  /** Cosmetic rescued passengers riding along. */
  passengers = 0;

  constructor(model: RiderModel, x: number, y: number, vx: number, vy: number, opts: RiderOptions = {}) {
    this.model = model;
    this.gravityScale = opts.gravityScale ?? 1;
    this.frictionScale = opts.frictionScale ?? 1;
    const enduranceScale = opts.enduranceScale ?? 1;
    this.points = model.points.map((d, i) => {
      const p = new Point(x + d.x, y + d.y, d.friction);
      p.index = i;
      p.setVelocity(vx, vy);
      return p;
    });
    this.bones = model.bones.map((b) => {
      const pa = model.points[b.a];
      const pb = model.points[b.b];
      const natural = Math.sqrt((pa.x - pb.x) ** 2 + (pa.y - pb.y) ** 2);
      const rest = b.kind === 'repel' ? natural * (b.factor ?? 0.5) : natural;
      return new Bone(b.a, b.b, rest, b.kind ?? 'normal', enduranceScale);
    });
    this.forces = new Float64Array(this.points.length * 2);
    const sh = this.points[model.anchor.shoulder];
    const n = opts.scarfLength ?? 7;
    for (let i = 0; i < n; i++) {
      this.scarf.push({ x: sh.x - i * 2, y: sh.y, px: sh.x - i * 2, py: sh.y });
    }
  }

  /** Integrate all points with gravity plus any accumulated external forces. */
  step(gx: number, gy: number): void {
    const g = this.gravityScale;
    const f = this.forces;
    for (let i = 0; i < this.points.length; i++) {
      this.points[i].step(gx * g + f[i * 2], gy * g + f[i * 2 + 1]);
    }
    f.fill(0);
    this.age++;
  }

  /** One relaxation pass over every bone. Mount bones snap (and kill the rider) past their endurance. */
  satisfyBones(): void {
    const pts = this.points;
    for (const bone of this.bones) {
      const pa = pts[bone.a];
      const pb = pts[bone.b];
      const dx = pa.x - pb.x;
      const dy = pa.y - pb.y;
      const d = Math.sqrt(dx * dx + dy * dy);
      if (d === 0) continue;
      if (bone.kind === 'repel' && d >= bone.rest) continue;
      const scalar = ((d - bone.rest) / d) * 0.5;
      if (bone.kind === 'mount') {
        if (this.dead) continue;
        if (scalar > bone.endurance) {
          this.dead = true;
          continue;
        }
      }
      pa.x -= dx * scalar;
      pa.y -= dy * scalar;
      pb.x += dx * scalar;
      pb.y += dy * scalar;
    }
  }

  /** Dismount if the body has been pushed through the plane of the vehicle. */
  checkFlip(): void {
    if (this.dead) return;
    const a = this.model.anchor;
    const tail = this.points[a.tail];
    const nose = this.points[a.nose];
    const hip = this.points[a.hip];
    const sh = this.points[a.shoulder];
    const cross = (nose.x - tail.x) * (sh.y - hip.y) - (nose.y - tail.y) * (sh.x - hip.x);
    if (cross > 0) this.dead = true;
  }

  stepScarf(gy: number): void {
    const sh = this.points[this.model.anchor.shoulder];
    const s = this.scarf;
    s[0].px = s[0].x;
    s[0].py = s[0].y;
    s[0].x = sh.x;
    s[0].y = sh.y;
    for (let i = 1; i < s.length; i++) {
      const n = s[i];
      const vx = (n.x - n.px) * 0.86;
      const vy = (n.y - n.py) * 0.86 + gy * 0.15;
      n.px = n.x;
      n.py = n.y;
      n.x += vx;
      n.y += vy;
    }
    for (let i = 1; i < s.length; i++) {
      const a = s[i - 1];
      const b = s[i];
      const dx = b.x - a.x;
      const dy = b.y - a.y;
      const d = Math.sqrt(dx * dx + dy * dy) || 1;
      const rest = 2.2;
      b.x = a.x + (dx / d) * rest;
      b.y = a.y + (dy / d) * rest;
    }
  }

  /** Orientation of the vehicle in radians (screen coordinates). */
  get angle(): number {
    const t = this.points[this.model.anchor.tail];
    const n = this.points[this.model.anchor.nose];
    return Math.atan2(n.y - t.y, n.x - t.x);
  }

  /** Centre of the vehicle. */
  center(): { x: number; y: number } {
    let x = 0;
    let y = 0;
    for (const i of this.model.vehicle) {
      x += this.points[i].x;
      y += this.points[i].y;
    }
    const n = this.model.vehicle.length;
    return { x: x / n, y: y / n };
  }

  /** Average velocity of the vehicle points (px/frame). */
  velocity(): { x: number; y: number } {
    let x = 0;
    let y = 0;
    for (const i of this.model.vehicle) {
      const p = this.points[i];
      x += p.x - p.px;
      y += p.y - p.py;
    }
    const n = this.model.vehicle.length;
    return { x: x / n, y: y / n };
  }

  /** Whether any vehicle point touched a line this frame. */
  vehicleGrounded(): boolean {
    for (const i of this.model.vehicle) if (this.points[i].contact) return true;
    return false;
  }

  /** Whether any body (non-vehicle) point touched a line this frame. */
  bodyTouching(): boolean {
    const v = this.model.vehicle;
    for (const p of this.points) if (!v.includes(p.index) && p.contact) return true;
    return false;
  }
}
