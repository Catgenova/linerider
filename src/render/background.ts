import type { Camera } from '../editor/camera';
import type { Environment, SkylineStyle } from '../game/environments';

/** Deterministic pseudo-noise for skyline heights. */
function noise(x: number, seed: number): number {
  const s = Math.sin(x * 12.9898 + seed * 78.233) * 43758.5453;
  return s - Math.floor(s);
}

function heightAt(x: number, style: SkylineStyle, seed: number): number {
  switch (style) {
    case 'peaks': {
      const a = Math.sin(x * 0.9 + seed) * 0.5 + Math.sin(x * 2.3 + seed * 2) * 0.25 + Math.sin(x * 5.1) * 0.12;
      return 0.35 + a * 0.35;
    }
    case 'crystals': {
      const cell = Math.floor(x * 3);
      const h = noise(cell, seed);
      const frac = x * 3 - cell;
      return 0.25 + h * 0.5 * (1 - Math.abs(frac - 0.5) * 2);
    }
    case 'mesas': {
      const cell = Math.floor(x * 1.2);
      const h = noise(cell, seed);
      const frac = x * 1.2 - cell;
      const edge = Math.min(frac, 1 - frac) * 8;
      return 0.2 + (h > 0.45 ? 0.35 * Math.min(1, edge) : 0.05);
    }
    case 'trees': {
      const cell = Math.floor(x * 6);
      const h = noise(cell, seed);
      const frac = x * 6 - cell;
      const trunk = Math.abs(frac - 0.5) < 0.08 ? 1 : 0;
      return 0.15 + h * 0.5 * trunk + (Math.abs(frac - 0.5) < 0.3 ? 0.18 * h : 0);
    }
    case 'city': {
      const cell = Math.floor(x * 4);
      const h = noise(cell, seed);
      return 0.15 + h * h * 0.65;
    }
    case 'stalactites': {
      const a = Math.sin(x * 3.1 + seed) * 0.5 + Math.sin(x * 7.7) * 0.3;
      return 0.2 + Math.abs(a) * 0.35;
    }
    case 'craters': {
      const a = Math.sin(x * 1.3 + seed) * 0.5 + Math.sin(x * 3.9 + seed) * 0.2;
      return 0.22 + a * 0.12;
    }
    case 'gears': {
      const a = Math.sin(x * 6) > 0.3 ? 0.1 : 0;
      return 0.3 + a + Math.sin(x * 0.7 + seed) * 0.1;
    }
    default:
      return 0.3;
  }
}

/** Multi-layer parallax backdrop: gradient sky, orb, two silhouette layers, grid, fog. */
export function drawBackground(
  ctx: CanvasRenderingContext2D,
  cam: Camera,
  env: Environment,
  time: number,
  pulse: number,
): void {
  const { width: w, height: h } = cam;
  const vis = env.visuals;
  const grad = ctx.createLinearGradient(0, 0, 0, h);
  grad.addColorStop(0, vis.bg[0]);
  grad.addColorStop(1, vis.bg[1]);
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, w, h);

  // Faint stars / floating motes.
  ctx.save();
  ctx.globalAlpha = 0.5;
  ctx.fillStyle = vis.accent;
  for (let i = 0; i < 60; i++) {
    const sx = ((noise(i, 3) * 4000 - cam.x * 0.05) % (w + 40)) - 20;
    const sy = ((noise(i, 7) * 3000 - cam.y * 0.05) % (h + 40)) - 20;
    const x = ((sx % (w + 40)) + w + 40) % (w + 40) - 20;
    const y = ((sy % (h + 40)) + h + 40) % (h + 40) - 20;
    const tw = 0.5 + 0.5 * Math.sin(time * 2 + i);
    ctx.globalAlpha = 0.15 + 0.35 * tw;
    ctx.fillRect(x, y, 1.5, 1.5);
  }
  ctx.restore();

  if (vis.orb) {
    const ox = vis.orb.x * w - cam.x * 0.02;
    const oy = vis.orb.y * h - cam.y * 0.02;
    const r = vis.orb.size;
    const g = ctx.createRadialGradient(ox, oy, r * 0.2, ox, oy, r * 2.2);
    g.addColorStop(0, vis.orb.color);
    g.addColorStop(0.45, vis.orb.color + '33');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.save();
    ctx.globalAlpha = 0.35 + 0.1 * pulse;
    ctx.fillStyle = g;
    ctx.fillRect(ox - r * 2.2, oy - r * 2.2, r * 4.4, r * 4.4);
    ctx.globalAlpha = 0.9;
    ctx.fillStyle = vis.orb.color;
    ctx.beginPath();
    ctx.arc(ox, oy, r * 0.5, 0, Math.PI * 2);
    ctx.fill();
    // Retro sun stripes.
    ctx.fillStyle = vis.bg[0];
    for (let i = 0; i < 5; i++) {
      const yy = oy + r * 0.05 + i * r * 0.09;
      ctx.fillRect(ox - r, yy, r * 2, r * 0.02 + i * r * 0.008);
    }
    ctx.restore();
  }

  // Silhouette layers.
  const layers: [number, number, string, number][] = [
    [0.12, 0.62, vis.fog, 1],
    [0.25, 0.5, vis.bg[0], 2],
  ];
  for (const [parallax, base, color, seed] of layers) {
    ctx.save();
    ctx.fillStyle = color;
    ctx.globalAlpha = 0.9;
    ctx.beginPath();
    ctx.moveTo(0, h);
    const step = 6;
    const horizon = h * base - cam.y * parallax * 0.4;
    for (let sx = 0; sx <= w + step; sx += step) {
      const wx = (sx + cam.x * parallax) * 0.004;
      const hh = heightAt(wx, vis.skyline, seed) * h * 0.5;
      ctx.lineTo(sx, horizon + h * 0.5 - hh);
    }
    ctx.lineTo(w, h);
    ctx.closePath();
    ctx.fill();
    // Neon edge on the silhouette.
    ctx.globalAlpha = 0.25 + 0.15 * pulse;
    ctx.strokeStyle = vis.accent2;
    ctx.lineWidth = 1;
    ctx.beginPath();
    for (let sx = 0; sx <= w + step; sx += step) {
      const wx = (sx + cam.x * parallax) * 0.004;
      const hh = heightAt(wx, vis.skyline, seed) * h * 0.5;
      const y = horizon + h * 0.5 - hh;
      if (sx === 0) ctx.moveTo(sx, y);
      else ctx.lineTo(sx, y);
    }
    ctx.stroke();
    if (vis.skyline === 'city') {
      // Lit windows.
      ctx.globalAlpha = 0.35;
      ctx.fillStyle = vis.accent;
      for (let sx = 0; sx <= w; sx += 14) {
        const wx = (sx + cam.x * parallax) * 0.004;
        const hh = heightAt(wx, vis.skyline, seed) * h * 0.5;
        const top = horizon + h * 0.5 - hh;
        for (let y = top + 10; y < h; y += 16) {
          if (noise(Math.floor(sx / 14) * 31 + Math.floor(y / 16), seed) > 0.6) ctx.fillRect(sx + 4, y, 3, 5);
        }
      }
    }
    ctx.restore();
  }

  // World-space grid.
  ctx.save();
  ctx.strokeStyle = vis.grid;
  ctx.lineWidth = 1;
  const minor = cam.zoom >= 1.5 ? 25 : cam.zoom >= 0.6 ? 100 : 400;
  const vp = cam.viewport();
  const x0 = Math.floor(vp.left / minor) * minor;
  const y0 = Math.floor(vp.top / minor) * minor;
  ctx.beginPath();
  for (let x = x0; x <= vp.right; x += minor) {
    const s = cam.toScreen(x, 0).x;
    ctx.moveTo(s, 0);
    ctx.lineTo(s, h);
  }
  for (let y = y0; y <= vp.bottom; y += minor) {
    const s = cam.toScreen(0, y).y;
    ctx.moveTo(0, s);
    ctx.lineTo(w, s);
  }
  ctx.stroke();
  ctx.restore();

  // Horizon glow band pulsing with the beat.
  ctx.save();
  ctx.globalCompositeOperation = 'lighter';
  const band = ctx.createLinearGradient(0, h * 0.55, 0, h);
  band.addColorStop(0, 'rgba(0,0,0,0)');
  band.addColorStop(1, vis.fog);
  ctx.globalAlpha = 0.5 + 0.5 * pulse;
  ctx.fillStyle = band;
  ctx.fillRect(0, h * 0.55, w, h * 0.45);
  ctx.restore();
}
