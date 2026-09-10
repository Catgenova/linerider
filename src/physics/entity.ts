import type { Line } from './line';
import type { World } from './world';

/**
 * Anything that lives in the world besides static lines and riders: moving platforms, gears,
 * fans, magnets, cannons, breakable walls, hazards, pickups. Entities may expose kinematic
 * collision lines, apply forces to rider points, and react to contacts.
 */
export interface Entity {
  readonly id: number;
  readonly kind: string;
  /** Kinematic collision lines. Their vx/vy must be kept up to date in update(). */
  lines: Line[];
  /** Whether the entity still participates in the simulation. */
  active: boolean;
  /** Called once per frame before integration. */
  update(world: World): void;
  /** Add forces to rider points (into rider.forces). Optional. */
  applyForces?(world: World): void;
  /** Called after the collision passes each frame. Optional. */
  afterStep?(world: World): void;
  /** Custom drawing in world space (the context is already transformed). Optional. */
  render?(ctx: CanvasRenderingContext2D, time: number, pulse: number): void;
}
