import type { Vec } from '../core/vec';

/** World <-> screen mapping with smooth follow. */
export class Camera {
  x = 0;
  y = 0;
  zoom = 2;
  width = 1;
  height = 1;
  private targetX: number | null = null;
  private targetY: number | null = null;
  private targetZoom: number | null = null;

  resize(width: number, height: number): void {
    this.width = width;
    this.height = height;
  }

  toScreen(wx: number, wy: number): Vec {
    return {
      x: (wx - this.x) * this.zoom + this.width / 2,
      y: (wy - this.y) * this.zoom + this.height / 2,
    };
  }

  toWorld(sx: number, sy: number): Vec {
    return {
      x: (sx - this.width / 2) / this.zoom + this.x,
      y: (sy - this.height / 2) / this.zoom + this.y,
    };
  }

  /** Visible world rect. */
  viewport(): { left: number; top: number; right: number; bottom: number } {
    const hw = this.width / 2 / this.zoom;
    const hh = this.height / 2 / this.zoom;
    return { left: this.x - hw, top: this.y - hh, right: this.x + hw, bottom: this.y + hh };
  }

  panBy(dxScreen: number, dyScreen: number): void {
    this.x -= dxScreen / this.zoom;
    this.y -= dyScreen / this.zoom;
    this.targetX = this.targetY = null;
  }

  zoomAt(sx: number, sy: number, factor: number): void {
    const before = this.toWorld(sx, sy);
    this.zoom = Math.min(12, Math.max(0.15, this.zoom * factor));
    const after = this.toWorld(sx, sy);
    this.x += before.x - after.x;
    this.y += before.y - after.y;
    this.targetZoom = null;
  }

  follow(x: number, y: number): void {
    this.targetX = x;
    this.targetY = y;
  }

  stopFollowing(): void {
    this.targetX = this.targetY = null;
  }

  setZoomTarget(z: number): void {
    this.targetZoom = z;
  }

  snapTo(x: number, y: number): void {
    this.x = x;
    this.y = y;
    this.targetX = this.targetY = null;
  }

  /** Called once per render frame. */
  update(dt: number): void {
    const k = 1 - Math.exp(-dt * 8);
    if (this.targetX !== null && this.targetY !== null) {
      this.x += (this.targetX - this.x) * k;
      this.y += (this.targetY - this.y) * k;
    }
    if (this.targetZoom !== null) {
      this.zoom += (this.targetZoom - this.zoom) * k;
      if (Math.abs(this.targetZoom - this.zoom) < 0.001) this.targetZoom = null;
    }
  }
}
