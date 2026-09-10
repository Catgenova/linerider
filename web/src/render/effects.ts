interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  life: number;
  max: number;
  size: number;
  color: string;
  gravity: number;
  drag: number;
}

interface Popup {
  x: number;
  y: number;
  text: string;
  life: number;
  max: number;
  color: string;
  scale: number;
  vy: number;
}

interface Ring {
  x: number;
  y: number;
  life: number;
  max: number;
  radius: number;
  color: string;
}

/** Transient visual effects in world space: sparks, shards, explosion rings, score popups. */
export class Effects {
  readonly particles: Particle[] = [];
  readonly popups: Popup[] = [];
  readonly rings: Ring[] = [];
  /** Screen shake amplitude, decays each frame. */
  shake = 0;
  /** Camera flash intensity 0..1. */
  flash = 0;
  flashColor = '#ffffff';

  spark(x: number, y: number, count: number, color: string, speed = 2, spread = Math.PI * 2, dir = 0, gravity = 0.08): void {
    for (let i = 0; i < count; i++) {
      const a = dir + (Math.random() - 0.5) * spread;
      const s = speed * (0.3 + Math.random());
      this.particles.push({
        x,
        y,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s,
        life: 0,
        max: 18 + Math.random() * 22,
        size: 0.8 + Math.random() * 1.4,
        color,
        gravity,
        drag: 0.96,
      });
      if (this.particles.length > 1500) this.particles.shift();
    }
  }

  shards(x1: number, y1: number, x2: number, y2: number, color: string): void {
    const len = Math.hypot(x2 - x1, y2 - y1);
    const n = Math.max(3, Math.min(30, Math.round(len / 6)));
    for (let i = 0; i < n; i++) {
      const t = (i + Math.random()) / n;
      this.spark(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t, 2, color, 1.2, Math.PI * 2, 0, 0.12);
    }
  }

  ring(x: number, y: number, radius: number, color: string, frames = 24): void {
    this.rings.push({ x, y, life: 0, max: frames, radius, color });
  }

  popup(x: number, y: number, text: string, color: string, scale = 1): void {
    this.popups.push({ x, y, text, life: 0, max: 70, color, scale, vy: -0.5 });
    if (this.popups.length > 30) this.popups.shift();
  }

  explosion(x: number, y: number, radius: number): void {
    this.spark(x, y, 60, '#ffb347', 5, Math.PI * 2, 0, 0.05);
    this.spark(x, y, 40, '#ff3d7f', 3.5, Math.PI * 2, 0, 0.05);
    this.ring(x, y, radius, '#ffb347', 20);
    this.ring(x, y, radius * 0.6, '#ffffff', 12);
    this.shake = Math.max(this.shake, 10);
    this.flash = Math.max(this.flash, 0.5);
    this.flashColor = '#ffb347';
  }

  clear(): void {
    this.particles.length = 0;
    this.popups.length = 0;
    this.rings.length = 0;
    this.shake = 0;
    this.flash = 0;
  }

  /** Advance by `frames` simulation frames (fractional allowed). */
  update(frames: number): void {
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.life += frames;
      p.vy += p.gravity * frames;
      p.vx *= p.drag;
      p.vy *= p.drag;
      p.x += p.vx * frames;
      p.y += p.vy * frames;
      if (p.life >= p.max) this.particles.splice(i, 1);
    }
    for (let i = this.popups.length - 1; i >= 0; i--) {
      const p = this.popups[i];
      p.life += frames;
      p.y += p.vy * frames;
      if (p.life >= p.max) this.popups.splice(i, 1);
    }
    for (let i = this.rings.length - 1; i >= 0; i--) {
      const r = this.rings[i];
      r.life += frames;
      if (r.life >= r.max) this.rings.splice(i, 1);
    }
    this.shake *= Math.pow(0.85, frames);
    if (this.shake < 0.2) this.shake = 0;
    this.flash *= Math.pow(0.8, frames);
    if (this.flash < 0.01) this.flash = 0;
  }
}
