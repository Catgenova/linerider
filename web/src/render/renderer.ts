import type { Camera } from '../editor/camera';
import type { Editor } from '../editor/editor';
import type { Environment } from '../game/environments';
import type { LineData, Track, Zone } from '../game/track';
import { MATERIALS, type Material, type MaterialId } from '../physics/materials';
import type { Line } from '../physics/line';
import type { Prop } from '../physics/prop';
import type { Rider } from '../physics/rider';
import type { World } from '../physics/world';
import { drawBackground } from './background';
import type { Effects } from './effects';

export interface RiderStyle {
  color: string;
  glow: string;
}

export interface Marker {
  x: number;
  y: number;
  kind: 'flag' | 'rescue' | 'checkpoint';
  done: boolean;
  label?: string;
}

export interface Scene {
  camera: Camera;
  track: Track;
  world: World | null;
  /** Objects shown while editing, when there is no live world. */
  previewWorld: World | null;
  ghostWorld: World | null;
  environment: Environment;
  editor: Editor | null;
  riderStyles: RiderStyle[];
  effects: Effects;
  time: number;
  start: { x: number; y: number };
  finish: Zone | null;
  markers: Marker[];
  /** World-space rect that bounds the playable area, drawn as a faint fence. Optional. */
  bounds?: { minX: number; minY: number; maxX: number; maxY: number } | null;
  /** Rider index the darkness halo follows. */
  focusRider: number;
  /** World point the darkness halo follows when no rider is running (the cursor). */
  focusPoint: { x: number; y: number } | null;
  showEditorOverlay: boolean;
}

type AnyLine = Line | LineData;

const FONT = '"Orbitron", "Rajdhani", "Segoe UI", system-ui, sans-serif';

function materialOf(l: AnyLine): Material {
  return typeof l.material === 'string' ? MATERIALS[l.material as MaterialId] : (l.material as Material);
}

function hexToRgba(hex: string, alpha: number): string {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map((c) => c + c).join('') : h, 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  return `rgba(${r},${g},${b},${alpha})`;
}

/** Canvas 2D renderer for the neon world. All world drawing happens under a camera transform. */
export class Renderer {
  readonly ctx: CanvasRenderingContext2D;
  private dpr = 1;
  private paths = new Map<string, Path2D>();
  private crumblePath = new Path2D();
  private arrowPath = new Path2D();
  private tickPath = new Path2D();
  private visible: AnyLine[] = [];

  constructor(public readonly canvas: HTMLCanvasElement) {
    const ctx = canvas.getContext('2d', { alpha: false });
    if (!ctx) throw new Error('Canvas 2D not supported');
    this.ctx = ctx;
  }

  resize(camera: Camera): void {
    this.dpr = Math.min(2, window.devicePixelRatio || 1);
    const w = this.canvas.clientWidth;
    const h = this.canvas.clientHeight;
    if (this.canvas.width !== Math.round(w * this.dpr) || this.canvas.height !== Math.round(h * this.dpr)) {
      this.canvas.width = Math.round(w * this.dpr);
      this.canvas.height = Math.round(h * this.dpr);
    }
    camera.resize(w, h);
  }

  render(scene: Scene): void {
    const { ctx } = this;
    const cam = scene.camera;
    const fx = scene.effects;
    const pulse = 0.5 + 0.5 * Math.sin(scene.time * Math.PI * 2 * 1.6);
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    drawBackground(ctx, cam, scene.environment, scene.time, pulse);

    // World transform (with screen shake).
    const shakeX = fx.shake ? (Math.random() - 0.5) * fx.shake : 0;
    const shakeY = fx.shake ? (Math.random() - 0.5) * fx.shake : 0;
    ctx.save();
    ctx.translate(cam.width / 2 + shakeX, cam.height / 2 + shakeY);
    ctx.scale(cam.zoom, cam.zoom);
    ctx.translate(-cam.x, -cam.y);

    this.drawZones(scene, pulse);
    this.drawLines(scene, pulse);
    if (scene.world) {
      for (const e of scene.world.entities) if (e.active && e.render) e.render(ctx, scene.time, pulse);
      this.drawEntityLines(scene.world, cam.zoom, pulse);
      for (const prop of scene.world.props) this.drawProp(prop, scene, pulse);
    }
    if (!scene.world && scene.previewWorld) {
      const pw = scene.previewWorld;
      for (const e of pw.entities) if (e.active && e.render) e.render(ctx, scene.time, pulse);
      this.drawEntityLines(pw, cam.zoom, pulse);
      for (const prop of pw.props) this.drawProp(prop, scene, pulse);
    }
    if (scene.ghostWorld) {
      for (const rider of scene.ghostWorld.riders) {
        this.drawRider(rider, { color: '#e0c8ff', glow: '#b48cff' }, cam.zoom, 0.38, false);
        const c = rider.center();
        ctx.save();
        ctx.globalAlpha = 0.5;
        ctx.fillStyle = '#e0c8ff';
        ctx.font = `700 ${4 / Math.min(1, cam.zoom / 2)}px ${FONT}`;
        ctx.textAlign = 'center';
        ctx.fillText('GHOST', c.x + 6, c.y - 14);
        ctx.restore();
      }
    }
    if (scene.world) {
      scene.world.riders.forEach((rider, i) => {
        const style = scene.riderStyles[i] ?? scene.riderStyles[0] ?? { color: '#39f6ff', glow: '#00c8ff' };
        this.drawRider(rider, style, cam.zoom, 1, true);
      });
    } else {
      this.drawStartRider(scene, pulse);
    }
    this.drawEffects(fx, cam.zoom);
    if (scene.showEditorOverlay && scene.editor) this.drawEditorOverlay(scene.editor, cam.zoom, pulse);
    ctx.restore();

    this.drawDarkness(scene);
    this.drawVignette(cam);
    if (fx.flash > 0) {
      ctx.globalAlpha = fx.flash;
      ctx.fillStyle = fx.flashColor;
      ctx.fillRect(0, 0, cam.width, cam.height);
      ctx.globalAlpha = 1;
    }
  }

  private collectVisible(scene: Scene): AnyLine[] {
    const vp = scene.camera.viewport();
    const pad = 20;
    const out = this.visible;
    out.length = 0;
    const source: Iterable<AnyLine> = scene.world ? scene.world.lines.values() : scene.track.lines.values();
    for (const l of source) {
      const minX = l.x1 < l.x2 ? l.x1 : l.x2;
      const maxX = l.x1 < l.x2 ? l.x2 : l.x1;
      const minY = l.y1 < l.y2 ? l.y1 : l.y2;
      const maxY = l.y1 < l.y2 ? l.y2 : l.y1;
      if (maxX < vp.left - pad || minX > vp.right + pad || maxY < vp.top - pad || minY > vp.bottom + pad) continue;
      out.push(l);
    }
    return out;
  }

  private drawLines(scene: Scene, pulse: number): void {
    const { ctx } = this;
    const zoom = scene.camera.zoom;
    const lines = this.collectVisible(scene);
    for (const p of this.paths.values()) void p;
    this.paths.clear();
    this.crumblePath = new Path2D();
    this.arrowPath = new Path2D();
    this.tickPath = new Path2D();
    const showArrows = zoom >= 0.7;
    const showTicks = zoom >= 2.2;
    const frame = scene.world?.frame ?? 0;
    for (const l of lines) {
      const dead = (l as Line).dead === true;
      if (dead) continue;
      const mat = materialOf(l);
      const crumbleAt = (l as Line).crumbleAt ?? -1;
      const key = mat.id + (l.player === 2 && mat.id === 'normal' ? ':p2' : '');
      let path = this.paths.get(key);
      if (!path) {
        path = new Path2D();
        this.paths.set(key, path);
      }
      if (crumbleAt >= 0) {
        this.crumblePath.moveTo(l.x1, l.y1);
        this.crumblePath.lineTo(l.x2, l.y2);
        // Flicker as the timer runs down.
        if (((frame - crumbleAt) & 2) === 0) continue;
      }
      path.moveTo(l.x1, l.y1);
      path.lineTo(l.x2, l.y2);
      const dx = l.x2 - l.x1;
      const dy = l.y2 - l.y1;
      const len = Math.sqrt(dx * dx + dy * dy);
      if (len < 1) continue;
      const s = l.flipped ? -1 : 1;
      const ux = (dx / len) * s;
      const uy = (dy / len) * s;
      const nx = -uy;
      const ny = ux;
      if (showArrows && mat.arrows && len > 14) {
        const spacing = 28;
        const count = Math.max(1, Math.floor(len / spacing));
        for (let i = 0; i < count; i++) {
          const t = (i + 0.5) / count;
          const cx = l.x1 + dx * t;
          const cy = l.y1 + dy * t;
          const a = 3.2;
          this.arrowPath.moveTo(cx - ux * a - nx * a, cy - uy * a - ny * a);
          this.arrowPath.lineTo(cx + ux * a, cy + uy * a);
          this.arrowPath.lineTo(cx - ux * a + nx * a, cy - uy * a + ny * a);
        }
      }
      if (showTicks && mat.solid) {
        const spacing = 10;
        const count = Math.floor(len / spacing);
        for (let i = 1; i <= count; i++) {
          const t = (i - 0.5) / Math.max(1, count);
          const cx = l.x1 + dx * t;
          const cy = l.y1 + dy * t;
          this.tickPath.moveTo(cx, cy);
          this.tickPath.lineTo(cx + nx * 2.2, cy + ny * 2.2);
        }
      }
    }

    const core = Math.min(3.2, Math.max(0.9, 1.6 / Math.sqrt(zoom))) * (zoom < 1 ? 1 / Math.max(zoom, 0.25) : 1);
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    // Glow passes (additive).
    ctx.globalCompositeOperation = 'lighter';
    for (const [key, path] of this.paths) {
      const matId = key.split(':')[0] as MaterialId;
      const mat = MATERIALS[matId];
      const glow = key.endsWith(':p2') ? '#ff00c8' : mat.glow;
      const isScenery = matId === 'scenery';
      ctx.strokeStyle = glow;
      ctx.globalAlpha = (isScenery ? 0.05 : 0.09 + 0.05 * pulse);
      ctx.lineWidth = core * 6.5;
      ctx.stroke(path);
      ctx.globalAlpha = isScenery ? 0.12 : 0.28;
      ctx.lineWidth = core * 2.8;
      ctx.stroke(path);
    }
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 1;
    for (const [key, path] of this.paths) {
      const matId = key.split(':')[0] as MaterialId;
      const mat = MATERIALS[matId];
      ctx.strokeStyle = key.endsWith(':p2') ? '#ff7ae8' : mat.color;
      ctx.lineWidth = core;
      ctx.stroke(path);
    }
    ctx.strokeStyle = '#ffffff';
    ctx.globalAlpha = 0.7;
    ctx.lineWidth = core * 0.9;
    ctx.setLineDash([3, 4]);
    ctx.stroke(this.crumblePath);
    ctx.setLineDash([]);
    ctx.globalAlpha = 0.85;
    ctx.lineWidth = core * 0.8;
    ctx.stroke(this.arrowPath);
    ctx.globalAlpha = 0.3;
    ctx.lineWidth = core * 0.6;
    ctx.stroke(this.tickPath);
    ctx.globalAlpha = 1;
  }

  private drawEntityLines(world: World, zoom: number, pulse: number): void {
    const { ctx } = this;
    const path = new Path2D();
    for (const e of world.entities) {
      if (!e.active) continue;
      for (const l of e.lines) {
        if (l.dead) continue;
        path.moveTo(l.x1, l.y1);
        path.lineTo(l.x2, l.y2);
      }
    }
    const core = Math.min(3.2, Math.max(0.9, 1.6 / Math.sqrt(zoom)));
    ctx.lineCap = 'round';
    ctx.globalCompositeOperation = 'lighter';
    ctx.strokeStyle = '#ffd000';
    ctx.globalAlpha = 0.1 + 0.05 * pulse;
    ctx.lineWidth = core * 6;
    ctx.stroke(path);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 1;
    ctx.strokeStyle = '#fff3a0';
    ctx.lineWidth = core;
    ctx.stroke(path);
  }

  private drawRider(rider: Rider, style: RiderStyle, zoom: number, alpha: number, trail: boolean): void {
    const { ctx } = this;
    const pts = rider.points;
    const model = rider.model;
    const dead = rider.dead;
    const color = dead ? '#ff4d4d' : style.color;
    const glow = dead ? '#ff2020' : style.glow;
    const core = Math.min(2.4, Math.max(0.8, 1.4 / Math.sqrt(zoom)));
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    if (trail && rider.trail.length >= 4) {
      ctx.globalCompositeOperation = 'lighter';
      const n = rider.trail.length / 2;
      for (let i = 1; i < n; i++) {
        const a = (i / n) * 0.35 * alpha;
        ctx.strokeStyle = hexToRgba(glow, a);
        ctx.lineWidth = core * 2.5 * (i / n);
        ctx.beginPath();
        ctx.moveTo(rider.trail[(i - 1) * 2], rider.trail[(i - 1) * 2 + 1]);
        ctx.lineTo(rider.trail[i * 2], rider.trail[i * 2 + 1]);
        ctx.stroke();
      }
      ctx.globalCompositeOperation = 'source-over';
    }

    // Scarf.
    const sc = rider.scarf;
    for (let i = 1; i < sc.length; i++) {
      ctx.strokeStyle = i % 2 === 0 ? '#ff2bd6' : '#ffffff';
      ctx.globalAlpha = alpha * (1 - (i / sc.length) * 0.6);
      ctx.lineWidth = core * 1.1;
      ctx.beginPath();
      ctx.moveTo(sc[i - 1].x, sc[i - 1].y);
      ctx.lineTo(sc[i].x, sc[i].y);
      ctx.stroke();
    }
    ctx.globalAlpha = alpha;

    const vehicle = new Path2D();
    for (const poly of model.draw.vehicle) {
      vehicle.moveTo(pts[poly[0]].x, pts[poly[0]].y);
      for (let i = 1; i < poly.length; i++) vehicle.lineTo(pts[poly[i]].x, pts[poly[i]].y);
      vehicle.closePath();
    }
    const body = new Path2D();
    for (const [a, b] of model.draw.body) {
      body.moveTo(pts[a].x, pts[a].y);
      body.lineTo(pts[b].x, pts[b].y);
    }
    const hip = pts[model.anchor.hip];
    const sh = pts[model.anchor.shoulder];
    let hx = sh.x - hip.x;
    let hy = sh.y - hip.y;
    const hl = Math.sqrt(hx * hx + hy * hy) || 1;
    hx /= hl;
    hy /= hl;
    const headX = sh.x + hx * model.draw.headOffset;
    const headY = sh.y + hy * model.draw.headOffset;

    ctx.globalCompositeOperation = 'lighter';
    ctx.strokeStyle = glow;
    ctx.globalAlpha = alpha * 0.35;
    ctx.lineWidth = core * 5;
    ctx.stroke(vehicle);
    ctx.stroke(body);
    ctx.beginPath();
    ctx.arc(headX, headY, model.draw.headRadius + core * 2, 0, Math.PI * 2);
    ctx.stroke();
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = alpha;

    ctx.fillStyle = 'rgba(5, 2, 20, 0.85)';
    ctx.fill(vehicle);
    ctx.strokeStyle = color;
    ctx.lineWidth = core * 1.3;
    ctx.stroke(vehicle);
    ctx.strokeStyle = '#f6f8ff';
    ctx.lineWidth = core * 1.2;
    ctx.stroke(body);
    ctx.beginPath();
    ctx.arc(headX, headY, model.draw.headRadius, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(5, 2, 20, 0.9)';
    ctx.fill();
    ctx.strokeStyle = '#f6f8ff';
    ctx.stroke();
    // Visor.
    ctx.strokeStyle = color;
    ctx.lineWidth = core;
    ctx.beginPath();
    ctx.arc(headX, headY, model.draw.headRadius * 0.6, -0.2 + Math.atan2(hy, hx) - Math.PI / 2 + Math.PI / 4, Math.atan2(hy, hx) - Math.PI / 2 + Math.PI * 0.95);
    ctx.stroke();
    ctx.restore();
  }

  private drawStartRider(scene: Scene, pulse: number): void {
    const { ctx } = this;
    const { x, y } = scene.start;
    ctx.save();
    ctx.strokeStyle = scene.riderStyles[0]?.color ?? '#39f6ff';
    ctx.globalAlpha = 0.5 + 0.3 * pulse;
    ctx.lineWidth = 1;
    ctx.setLineDash([2, 2]);
    ctx.strokeRect(x - 2, y - 8, 22, 14);
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.moveTo(x + 24, y - 1);
    ctx.lineTo(x + 30, y - 1);
    ctx.moveTo(x + 27, y - 4);
    ctx.lineTo(x + 30, y - 1);
    ctx.lineTo(x + 27, y + 2);
    ctx.stroke();
    ctx.font = `700 4px ${FONT}`;
    ctx.fillStyle = scene.riderStyles[0]?.color ?? '#39f6ff';
    ctx.fillText('START', x - 1, y - 10);
    ctx.restore();
  }

  private drawProp(prop: Prop, scene: Scene, pulse: number): void {
    const { ctx } = this;
    if (!prop.active) return;
    const colors: Record<string, [string, string]> = {
      domino: ['#e8f4ff', '#7ad0ff'],
      crate: ['#ffb347', '#ff7a00'],
      tnt: ['#ff3d5c', '#ff0033'],
      cart: ['#c6ff4a', '#8cff00'],
      cargo: ['#ff7ae8', '#ff2bd6'],
      boulder: ['#b8c0d8', '#7a86b0'],
      snowball: ['#ffffff', '#a8f4ff'],
      rock: ['#a09aa8', '#6a6478'],
      ball: ['#ffe93a', '#ffd000'],
    };
    const [color, glow] = colors[prop.kind] ?? ['#ffffff', '#ffffff'];
    const core = Math.min(2.4, Math.max(0.8, 1.4 / Math.sqrt(scene.camera.zoom)));
    ctx.save();
    ctx.lineJoin = 'round';
    if (prop.isCircle) {
      const p = prop.points[0];
      ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = glow;
      ctx.globalAlpha = 0.25 + 0.1 * pulse;
      ctx.lineWidth = core * 4;
      ctx.beginPath();
      ctx.arc(p.x, p.y, prop.radius, 0, Math.PI * 2);
      ctx.stroke();
      ctx.globalCompositeOperation = 'source-over';
      ctx.globalAlpha = 1;
      ctx.fillStyle = 'rgba(5, 2, 20, 0.8)';
      ctx.fill();
      ctx.strokeStyle = color;
      ctx.lineWidth = core * 1.2;
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(p.x, p.y);
      ctx.lineTo(p.x + Math.cos(prop.spin) * prop.radius, p.y + Math.sin(prop.spin) * prop.radius);
      ctx.moveTo(p.x, p.y);
      ctx.lineTo(p.x + Math.cos(prop.spin + 2.1) * prop.radius, p.y + Math.sin(prop.spin + 2.1) * prop.radius);
      ctx.moveTo(p.x, p.y);
      ctx.lineTo(p.x + Math.cos(prop.spin + 4.2) * prop.radius, p.y + Math.sin(prop.spin + 4.2) * prop.radius);
      ctx.globalAlpha = 0.6;
      ctx.stroke();
    } else {
      const path = new Path2D();
      path.moveTo(prop.points[0].x, prop.points[0].y);
      for (let i = 1; i < prop.points.length; i++) path.lineTo(prop.points[i].x, prop.points[i].y);
      path.closePath();
      ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = glow;
      ctx.globalAlpha = 0.25 + 0.1 * pulse;
      ctx.lineWidth = core * 4;
      ctx.stroke(path);
      ctx.globalCompositeOperation = 'source-over';
      ctx.globalAlpha = 1;
      ctx.fillStyle = prop.kind === 'tnt' && prop.fuse >= 0 ? 'rgba(255,60,80,0.6)' : 'rgba(5, 2, 20, 0.8)';
      ctx.fill(path);
      ctx.strokeStyle = color;
      ctx.lineWidth = core * 1.2;
      ctx.stroke(path);
      if (prop.kind === 'tnt' || prop.kind === 'cargo' || prop.kind === 'crate') {
        const c = prop.center();
        ctx.save();
        ctx.translate(c.x, c.y);
        ctx.rotate(prop.angle());
        ctx.fillStyle = color;
        ctx.font = `700 ${Math.max(3, prop.height * 0.35)}px ${FONT}`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(prop.kind === 'tnt' ? 'TNT' : prop.kind === 'cargo' ? 'FRAGILE' : 'X', 0, 0);
        ctx.restore();
      }
    }
    ctx.restore();
  }

  private drawZones(scene: Scene, pulse: number): void {
    const { ctx } = this;
    const t = scene.time;
    if (scene.finish) {
      const z = scene.finish;
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      const g = ctx.createLinearGradient(z.x, z.y, z.x, z.y + z.h);
      g.addColorStop(0, 'rgba(77,255,157,0.02)');
      g.addColorStop(1, 'rgba(77,255,157,0.18)');
      ctx.fillStyle = g;
      ctx.fillRect(z.x, z.y, z.w, z.h);
      // Scanning bar.
      const scan = z.y + ((t * 40) % z.h);
      ctx.fillStyle = `rgba(77,255,157,${0.25 + 0.2 * pulse})`;
      ctx.fillRect(z.x, scan, z.w, 1.5);
      ctx.globalCompositeOperation = 'source-over';
      ctx.strokeStyle = '#4dff9d';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 3]);
      ctx.strokeRect(z.x, z.y, z.w, z.h);
      ctx.setLineDash([]);
      ctx.fillStyle = '#4dff9d';
      ctx.font = `700 6px ${FONT}`;
      ctx.textAlign = 'center';
      ctx.fillText('FINISH', z.x + z.w / 2, z.y - 4);
      ctx.restore();
    }
    for (const m of scene.markers) {
      ctx.save();
      if (m.kind === 'flag') {
        const wave = Math.sin(t * 6 + m.x) * 1.5;
        ctx.globalAlpha = m.done ? 0.3 : 1;
        ctx.strokeStyle = m.done ? '#8080a0' : '#ffe93a';
        ctx.lineWidth = 1.2;
        ctx.beginPath();
        ctx.moveTo(m.x, m.y);
        ctx.lineTo(m.x, m.y - 18);
        ctx.stroke();
        ctx.fillStyle = m.done ? '#8080a0' : '#ffe93a';
        ctx.beginPath();
        ctx.moveTo(m.x, m.y - 18);
        ctx.lineTo(m.x + 9 + wave, m.y - 14);
        ctx.lineTo(m.x, m.y - 10);
        ctx.closePath();
        ctx.fill();
        if (!m.done) {
          ctx.globalCompositeOperation = 'lighter';
          ctx.strokeStyle = `rgba(255,233,58,${0.25 + 0.25 * pulse})`;
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.arc(m.x, m.y - 9, 12 + pulse * 3, 0, Math.PI * 2);
          ctx.stroke();
        }
      } else if (m.kind === 'rescue') {
        if (m.done) {
          ctx.restore();
          continue;
        }
        const bob = Math.sin(t * 4 + m.x) * 1;
        ctx.strokeStyle = '#ff7ae8';
        ctx.lineWidth = 1.2;
        ctx.beginPath();
        ctx.moveTo(m.x, m.y - 2 + bob);
        ctx.lineTo(m.x, m.y - 8 + bob);
        ctx.moveTo(m.x - 3, m.y - 4 + bob);
        ctx.lineTo(m.x + 3, m.y - 9 + bob);
        ctx.moveTo(m.x, m.y - 2 + bob);
        ctx.lineTo(m.x - 2.5, m.y + 2 + bob);
        ctx.moveTo(m.x, m.y - 2 + bob);
        ctx.lineTo(m.x + 2.5, m.y + 2 + bob);
        ctx.stroke();
        ctx.beginPath();
        ctx.arc(m.x, m.y - 10.5 + bob, 2.2, 0, Math.PI * 2);
        ctx.stroke();
        ctx.globalCompositeOperation = 'lighter';
        ctx.strokeStyle = `rgba(255,122,232,${0.3 + 0.3 * pulse})`;
        ctx.beginPath();
        ctx.arc(m.x, m.y - 5, 14 + pulse * 4, 0, Math.PI * 2);
        ctx.stroke();
        ctx.fillStyle = '#ff7ae8';
        ctx.font = `700 4px ${FONT}`;
        ctx.textAlign = 'center';
        ctx.fillText('HELP!', m.x, m.y - 18 + bob);
      } else {
        ctx.strokeStyle = m.done ? '#4dff9d' : '#39f6ff';
        ctx.globalAlpha = 0.6;
        ctx.setLineDash([2, 2]);
        ctx.beginPath();
        ctx.moveTo(m.x, m.y - 30);
        ctx.lineTo(m.x, m.y + 30);
        ctx.stroke();
      }
      ctx.restore();
    }
  }

  private drawEffects(fx: Effects, zoom: number): void {
    const { ctx } = this;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    for (const p of fx.particles) {
      const a = 1 - p.life / p.max;
      ctx.globalAlpha = a;
      ctx.fillStyle = p.color;
      ctx.fillRect(p.x - p.size / 2, p.y - p.size / 2, p.size, p.size);
    }
    for (const r of fx.rings) {
      const k = r.life / r.max;
      ctx.globalAlpha = (1 - k) * 0.8;
      ctx.strokeStyle = r.color;
      ctx.lineWidth = 2 / zoom + 0.5;
      ctx.beginPath();
      ctx.arc(r.x, r.y, r.radius * (0.2 + 0.8 * k), 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.globalCompositeOperation = 'source-over';
    ctx.textAlign = 'center';
    for (const p of fx.popups) {
      const k = p.life / p.max;
      const a = k < 0.8 ? 1 : 1 - (k - 0.8) / 0.2;
      const grow = 1 + Math.sin(Math.min(1, k * 4) * Math.PI) * 0.3;
      const size = (11 / zoom) * p.scale * grow;
      ctx.globalAlpha = a;
      ctx.font = `700 ${size}px ${FONT}`;
      ctx.lineWidth = 3 / zoom;
      ctx.strokeStyle = 'rgba(0,0,0,0.6)';
      ctx.strokeText(p.text, p.x, p.y);
      ctx.fillStyle = p.color;
      ctx.fillText(p.text, p.x, p.y);
    }
    ctx.restore();
  }

  private drawEditorOverlay(editor: Editor, zoom: number, pulse: number): void {
    const { ctx } = this;
    ctx.save();
    if (editor.preview) {
      const p = editor.preview;
      const mat = MATERIALS[editor.material];
      ctx.strokeStyle = mat.color;
      ctx.globalAlpha = 0.85;
      ctx.lineWidth = 1.5 / zoom + 0.4;
      ctx.setLineDash([4 / zoom, 3 / zoom]);
      ctx.beginPath();
      ctx.moveTo(p.x1, p.y1);
      ctx.lineTo(p.x2, p.y2);
      ctx.stroke();
      ctx.setLineDash([]);
      const len = Math.hypot(p.x2 - p.x1, p.y2 - p.y1) / 10;
      ctx.font = `600 ${10 / zoom}px ${FONT}`;
      ctx.fillStyle = mat.color;
      ctx.fillText(`${(len * mat.cost).toFixed(1)} m`, p.x2 + 6 / zoom, p.y2 - 6 / zoom);
    }
    if (editor.tool === 'eraser') {
      const c = editor.cursorWorld;
      ctx.strokeStyle = '#ff3d7f';
      ctx.globalAlpha = 0.8;
      ctx.lineWidth = 1 / zoom;
      ctx.beginPath();
      ctx.arc(c.x, c.y, editor.eraserRadiusScreen / zoom, 0, Math.PI * 2);
      ctx.stroke();
    }
    if (editor.hoverId >= 0) {
      const l = editor.track.lines.get(editor.hoverId);
      if (l) {
        ctx.strokeStyle = '#ffffff';
        ctx.globalAlpha = 0.5 + 0.4 * pulse;
        ctx.lineWidth = 3 / zoom;
        ctx.beginPath();
        ctx.moveTo(l.x1, l.y1);
        ctx.lineTo(l.x2, l.y2);
        ctx.stroke();
      }
    }
    ctx.restore();
  }

  private drawDarkness(scene: Scene): void {
    const radius = scene.environment.darkness;
    if (!radius) return;
    const { ctx } = this;
    const cam = scene.camera;
    let fx = cam.width / 2;
    let fy = cam.height / 2;
    const rider = scene.world?.riders[scene.focusRider];
    if (rider) {
      const c = rider.center();
      const s = cam.toScreen(c.x, c.y);
      fx = s.x;
      fy = s.y;
    } else {
      const p = scene.focusPoint ?? scene.start;
      const s = cam.toScreen(p.x, p.y);
      fx = s.x;
      fy = s.y;
    }
    const r = radius * cam.zoom * (rider ? 1 : 1.4);
    const g = ctx.createRadialGradient(fx, fy, r * 0.35, fx, fy, r);
    g.addColorStop(0, 'rgba(2,1,6,0)');
    g.addColorStop(0.7, 'rgba(2,1,6,0.75)');
    g.addColorStop(1, 'rgba(2,1,6,0.97)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, cam.width, cam.height);
  }

  private drawVignette(cam: Camera): void {
    const { ctx } = this;
    const g = ctx.createRadialGradient(
      cam.width / 2,
      cam.height / 2,
      Math.min(cam.width, cam.height) * 0.4,
      cam.width / 2,
      cam.height / 2,
      Math.max(cam.width, cam.height) * 0.75,
    );
    g.addColorStop(0, 'rgba(0,0,0,0)');
    g.addColorStop(1, 'rgba(0,0,0,0.45)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, cam.width, cam.height);
  }

  /** Render a small thumbnail of a track for the library. */
  static thumbnail(track: Track, width: number, height: number, env: Environment): string {
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d')!;
    const grad = ctx.createLinearGradient(0, 0, 0, height);
    grad.addColorStop(0, env.visuals.bg[0]);
    grad.addColorStop(1, env.visuals.bg[1]);
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, width, height);
    const b = track.bounds();
    if (!b) return canvas.toDataURL('image/png');
    const pad = 10;
    const sx = (width - pad * 2) / Math.max(1, b.maxX - b.minX);
    const sy = (height - pad * 2) / Math.max(1, b.maxY - b.minY);
    const s = Math.min(sx, sy);
    const ox = pad + ((width - pad * 2) - (b.maxX - b.minX) * s) / 2;
    const oy = pad + ((height - pad * 2) - (b.maxY - b.minY) * s) / 2;
    ctx.lineCap = 'round';
    for (const l of track.lines.values()) {
      const m = MATERIALS[l.material];
      ctx.strokeStyle = m.color;
      ctx.globalAlpha = m.solid ? 0.95 : 0.4;
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.moveTo(ox + (l.x1 - b.minX) * s, oy + (l.y1 - b.minY) * s);
      ctx.lineTo(ox + (l.x2 - b.minX) * s, oy + (l.y2 - b.minY) * s);
      ctx.stroke();
    }
    if (track.finish) {
      ctx.globalAlpha = 0.8;
      ctx.strokeStyle = '#4dff9d';
      ctx.strokeRect(ox + (track.finish.x - b.minX) * s, oy + (track.finish.y - b.minY) * s, track.finish.w * s, track.finish.h * s);
    }
    return canvas.toDataURL('image/png');
  }
}
