import { describe, expect, it } from 'vitest';
import { GRAVITY, START_VELOCITY } from '../src/physics/constants';
import { Rider, SLED_MODEL, BOARD_MODEL, NOSE, TAIL } from '../src/physics/rider';
import { World } from '../src/physics/world';
import { Prop } from '../src/physics/prop';

function makeWorld(): World {
  return new World();
}

function addRider(world: World, x = 0, y = 0, model = SLED_MODEL): Rider {
  const rider = new Rider(model, x, y, START_VELOCITY, 0);
  world.addRider(rider);
  return rider;
}

function run(world: World, frames: number): void {
  for (let i = 0; i < frames; i++) world.step();
}

describe('rider physics', () => {
  it('falls under gravity with no lines', () => {
    const world = makeWorld();
    const rider = addRider(world);
    const y0 = rider.center().y;
    run(world, 40);
    const dy = rider.center().y - y0;
    // 40 frames of 0.175 px/frame^2: ~ 0.5 * g * t^2 + cumulative Verlet drift.
    expect(dy).toBeGreaterThan(GRAVITY * 40 * 40 * 0.45);
    expect(rider.dead).toBe(false);
  });

  it('rides a gentle slope without dying and gains speed', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: -50, y1: 10, x2: 1500, y2: 400 });
    const rider = addRider(world, 0, -2);
    run(world, 200);
    expect(rider.dead).toBe(false);
    const v = rider.velocity();
    expect(v.x).toBeGreaterThan(2);
    // Should be sitting on the line: nose and tail near the line.
    const nose = rider.points[NOSE];
    const slopeY = 10 + ((nose.x + 50) * 390) / 1550;
    expect(Math.abs(nose.y - slopeY)).toBeLessThan(2);
  });

  it('passes through lines drawn right-to-left from above (one-sided)', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: 600, y1: 200, x2: -100, y2: 200 });
    const rider = addRider(world, 0, 0);
    run(world, 120);
    expect(rider.center().y).toBeGreaterThan(250);
  });

  it('is stopped by lines drawn left-to-right from above', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: -100, y1: 60, x2: 600, y2: 60 });
    const rider = addRider(world, 0, 0);
    run(world, 120);
    expect(rider.points[TAIL].y).toBeLessThan(62);
  });

  it('dies when driving into a wall', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: -50, y1: 10, x2: 400, y2: 120 });
    // Vertical wall drawn bottom-to-top: solid on its left face.
    world.addLine({ id: 2, x1: 300, y1: 200, x2: 300, y2: -200 });
    const rider = addRider(world, 0, -2);
    run(world, 240);
    expect(rider.dead).toBe(true);
    expect(rider.deathFrame).toBeGreaterThan(0);
  });

  it('accelerates on booster lines', () => {
    const plain = makeWorld();
    plain.addLine({ id: 1, x1: -50, y1: 5, x2: 800, y2: 5 });
    const r1 = addRider(plain, 0, 0);
    run(plain, 80);
    const boosted = makeWorld();
    boosted.addLine({ id: 1, x1: -50, y1: 5, x2: 800, y2: 5, material: 'accel' });
    const r2 = addRider(boosted, 0, 0);
    run(boosted, 80);
    expect(r2.velocity().x).toBeGreaterThan(r1.velocity().x + 1);
    expect(r2.dead).toBe(false);
  });

  it('is deterministic', () => {
    const a = makeWorld();
    const b = makeWorld();
    for (const w of [a, b]) {
      w.addLine({ id: 1, x1: -50, y1: 10, x2: 500, y2: 200 });
      w.addLine({ id: 2, x1: 500, y1: 200, x2: 900, y2: 150, leftExt: true });
    }
    const ra = addRider(a, 0, -2);
    const rb = addRider(b, 0, -2);
    run(a, 300);
    run(b, 300);
    for (let i = 0; i < ra.points.length; i++) {
      expect(ra.points[i].x).toBe(rb.points[i].x);
      expect(ra.points[i].y).toBe(rb.points[i].y);
    }
  });

  it('crumbling lines vanish after contact', () => {
    const world = makeWorld();
    const line = world.addLine({ id: 1, x1: -100, y1: 8, x2: 600, y2: 8, material: 'crumble' });
    const rider = addRider(world, 0, 0);
    run(world, 20);
    expect(line.crumbleAt).toBeGreaterThan(0);
    run(world, 40);
    expect(line.dead).toBe(true);
    run(world, 60);
    expect(rider.center().y).toBeGreaterThan(100);
  });

  it('spring lines bounce the rider back up', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: -100, y1: 150, x2: 600, y2: 150, material: 'spring' });
    const rider = addRider(world, 0, 0);
    let minAfterBounce = Infinity;
    let touched = false;
    for (let i = 0; i < 200; i++) {
      world.step();
      if (rider.vehicleGrounded()) touched = true;
      if (touched) minAfterBounce = Math.min(minAfterBounce, rider.center().y);
    }
    expect(touched).toBe(true);
    expect(minAfterBounce).toBeLessThan(110);
  });

  it('snowboarder rides a slope', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: -50, y1: 10, x2: 1500, y2: 300 });
    const rider = addRider(world, 0, -1, BOARD_MODEL);
    run(world, 200);
    expect(rider.dead).toBe(false);
    expect(rider.velocity().x).toBeGreaterThan(1.5);
  });
});

describe('props', () => {
  it('boulders roll down slopes and rest on lines', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: -50, y1: 0, x2: 600, y2: 200 });
    const prop = new Prop({ id: 1, kind: 'boulder', x: 0, y: -30, radius: 12, mass: 5 });
    world.addProp(prop);
    run(world, 150);
    const c = prop.center();
    expect(c.x).toBeGreaterThan(100);
    const slopeY = (c.x + 50) * (200 / 650);
    expect(c.y).toBeLessThan(slopeY);
    expect(c.y).toBeGreaterThan(slopeY - 16);
  });

  it('dominoes get knocked over by the rider', () => {
    const world = makeWorld();
    world.addLine({ id: 1, x1: -50, y1: 10, x2: 400, y2: 60 });
    world.addLine({ id: 2, x1: 400, y1: 60, x2: 1200, y2: 60, leftExt: true });
    const domino = new Prop({ id: 1, kind: 'domino', x: 520, y: 50, width: 4, height: 20, mass: 0.5 });
    world.addProp(domino);
    addRider(world, 0, -2);
    run(world, 400);
    expect(domino.disturbed).toBe(true);
    // Upright dominoes have a horizontal top edge (angle 0); a toppled one is near vertical.
    const ang = Math.abs(Math.sin(domino.angle()));
    expect(ang).toBeGreaterThan(0.7);
  });
});
