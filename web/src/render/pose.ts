import type { Vec } from '../core/vec';
import { PEG, SHOULDER, type Rider } from '../physics/rider';

export interface Seg {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}

/** Joint positions used to draw a rider as a stick figure on a hoverboard. */
export interface RiderPose {
  /** Contact points of the deck (the physics tail/nose). */
  tail: Vec;
  nose: Vec;
  /** Unit vector along the deck and the unit "up" normal (toward the rider). */
  ux: number;
  uy: number;
  nx: number;
  ny: number;
  length: number;
  speed: number;
  segments: Seg[];
  head: Vec;
  /** Unit vector pointing from the neck through the head. */
  headUp: Vec;
  /** Translate the physics scarf by this much so it hangs from the drawn shoulders. */
  scarfDx: number;
  scarfDy: number;
  standing: boolean;
}

/**
 * Derive a drawing pose from the physics rig. The classic sled rig keeps its exact collision
 * behaviour, but while the rider is alive it is drawn as a standing figure on the deck, leaning
 * with speed and flexing with the mount bones. Once dismounted the raw ragdoll points are drawn.
 */
export function computePose(rider: Rider, time: number): RiderPose {
  const pts = rider.points;
  const m = rider.model;
  const tail = pts[m.anchor.tail];
  const nose = pts[m.anchor.nose];
  let ux = nose.x - tail.x;
  let uy = nose.y - tail.y;
  const length = Math.sqrt(ux * ux + uy * uy) || 1;
  ux /= length;
  uy /= length;
  const nx = uy;
  const ny = -ux;
  const v = rider.velocity();
  const speed = Math.sqrt(v.x * v.x + v.y * v.y);
  const base = { tail: { x: tail.x, y: tail.y }, nose: { x: nose.x, y: nose.y }, ux, uy, nx, ny, length, speed };

  if (m.id === 'sled' && !rider.dead) {
    const cx = (tail.x + nose.x) / 2;
    const cy = (tail.y + nose.y) / 2;
    const P = (a: number, h: number): Vec => ({ x: cx + ux * a + nx * h, y: cy + uy * a + ny * h });
    // How far the physics shoulder has been shoved from its rest offset: mount-bone flex.
    const peg = pts[PEG];
    const sh = pts[SHOULDER];
    const restX = peg.x + ux * 5 + nx * 5.5;
    const restY = peg.y + uy * 5 + ny * 5.5;
    const dx = sh.x - restX;
    const dy = sh.y - restY;
    // Lean into the direction of travel along the deck.
    const along = v.x * ux + v.y * uy;
    const lean = Math.max(-0.28, Math.min(0.28, along * 0.035));
    const bob = Math.sin(time * 7) * 0.12;
    const rearFoot = P(-4.4, 0);
    const frontFoot = P(4.6, 0);
    const rearKnee = P(-3.2 + lean * 2, 5.2 + bob);
    const frontKnee = P(4.0 + lean * 2, 5.4 + bob);
    const hip: Vec = { x: P(0.8 + lean * 4, 9.4 + bob).x + dx * 0.6, y: P(0.8 + lean * 4, 9.4 + bob).y + dy * 0.6 };
    const shoulderBase = P(1.6 + lean * 9, 15.6 + bob);
    const shoulder: Vec = { x: shoulderBase.x + dx * 1.4, y: shoulderBase.y + dy * 1.4 };
    const rearHandBase = P(-6.2 + lean * 6, 13.2 + bob);
    const frontHandBase = P(8.6 + lean * 9, 16.4 + bob);
    const rearHand: Vec = { x: rearHandBase.x + dx * 1.2, y: rearHandBase.y + dy * 1.2 };
    const frontHand: Vec = { x: frontHandBase.x + dx * 1.2, y: frontHandBase.y + dy * 1.2 };
    let hx = shoulder.x - hip.x;
    let hy = shoulder.y - hip.y;
    const hl = Math.sqrt(hx * hx + hy * hy) || 1;
    hx /= hl;
    hy /= hl;
    const head: Vec = { x: shoulder.x + hx * 3.4, y: shoulder.y + hy * 3.4 };
    return {
      ...base,
      segments: [
        seg(rearFoot, rearKnee),
        seg(rearKnee, hip),
        seg(frontFoot, frontKnee),
        seg(frontKnee, hip),
        seg(hip, shoulder),
        seg(shoulder, rearHand),
        seg(shoulder, frontHand),
      ],
      head,
      headUp: { x: hx, y: hy },
      scarfDx: shoulder.x - sh.x,
      scarfDy: shoulder.y - sh.y,
      standing: true,
    };
  }

  // Ragdoll (or the standing board rig): draw the physics joints as they are.
  const segments: Seg[] = m.draw.body.map(([a, b]) => seg(pts[a], pts[b]));
  const hip = pts[m.anchor.hip];
  const sh = pts[m.anchor.shoulder];
  let hx = sh.x - hip.x;
  let hy = sh.y - hip.y;
  const hl = Math.sqrt(hx * hx + hy * hy) || 1;
  hx /= hl;
  hy /= hl;
  return {
    ...base,
    segments,
    head: { x: sh.x + hx * m.draw.headOffset, y: sh.y + hy * m.draw.headOffset },
    headUp: { x: hx, y: hy },
    scarfDx: 0,
    scarfDy: 0,
    standing: m.id === 'board',
  };
}

function seg(a: { x: number; y: number }, b: { x: number; y: number }): Seg {
  return { x1: a.x, y1: a.y, x2: b.x, y2: b.y };
}
