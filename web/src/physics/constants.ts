/** Simulation runs at the classic 40 frames per second. */
export const SIM_FPS = 40;
export const FRAME_MS = 1000 / SIM_FPS;
/** Gravity in px per frame squared (screen y points down). */
export const GRAVITY = 0.175;
/** Constraint + collision passes per frame. */
export const ITERATIONS = 6;
/** Initial rightward push given to the rider. */
export const START_VELOCITY = 0.4;
/** Depth of the one-sided collision zone behind a line's surface. */
export const LINE_ZONE = 10;
/** Mount bones snap when stretched past this fraction (scaled by rest length). */
export const ENDURANCE = 0.057;
/** Velocity nudge per contact iteration on acceleration lines. */
export const ACCELERATION = 0.1;
/** Endpoint extension: up to 10 px, capped at a quarter of the line length. */
export const EXTENSION_PX = 10;
export const MAX_EXTENSION_RATIO = 0.25;
/** Spatial hash cell size. */
export const GRID_CELL = 14;
/** World scale used for track budgets and HUD readouts. */
export const PX_PER_METER = 10;
