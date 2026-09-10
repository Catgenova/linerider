import { distToSegment, type Vec } from '../core/vec';
import { PX_PER_METER } from '../physics/constants';
import { MATERIALS, type MaterialId } from '../physics/materials';
import type { LineData, ObjectData, PropData, Track } from '../game/track';
import { entityAnchor, objectKind, type ObjectKind } from '../game/objectKinds';
import type { Camera } from './camera';

export type ToolId = 'pencil' | 'line' | 'eraser' | 'pan' | 'flip' | 'object';

export interface EditorConstraints {
  /** Ink budget in metres, or null for unlimited. */
  budget: number | null;
  /** Materials the player may draw with. */
  materials: MaterialId[];
  /** Whether level geometry can be erased. */
  canEraseLevel: boolean;
  /** Co-op player id stamped on new lines (0 = single player). */
  player: number;
  /** Disables all editing. */
  locked: boolean;
  /** Whether interactive objects and props may be placed. */
  canPlaceObjects: boolean;
}

interface Command {
  added: LineData[];
  removed: LineData[];
  addedObjects?: ObjectData[];
  removedObjects?: ObjectData[];
  addedProps?: PropData[];
  removedProps?: PropData[];
}

export interface EditorEvents {
  onInkExhausted?: () => void;
  onEdit?: () => void;
  /** A line was drawn: its length in metres and material. */
  onLineDrawn?: (metres: number, material: MaterialId) => void;
  /** The eraser removed lines and/or objects. */
  onErased?: (lines: number, objects: number) => void;
  onObjectPlaced?: (kind: string) => void;
  onFlip?: () => void;
  onUndo?: () => void;
  onRedo?: () => void;
}

/** Mouse/touch driven track editing with undo/redo and budget enforcement. */
export class Editor {
  tool: ToolId = 'pencil';
  material: MaterialId = 'normal';
  objectKind: ObjectKind = 'spring';
  constraints: EditorConstraints = {
    budget: null,
    materials: ['normal', 'accel', 'scenery'],
    canEraseLevel: false,
    player: 0,
    locked: false,
    canPlaceObjects: false,
  };
  events: EditorEvents = {};
  /** Straight-line preview (world coords) while dragging. */
  preview: { x1: number; y1: number; x2: number; y2: number } | null = null;
  /** Line under the cursor for highlight. */
  hoverId = -1;
  cursorWorld: Vec = { x: 0, y: 0 };
  eraserRadiusScreen = 9;
  private undoStack: Command[] = [];
  private redoStack: Command[] = [];
  private current: Command | null = null;
  private dragging = false;
  private panning = false;
  private lastScreen: Vec = { x: 0, y: 0 };
  private anchor: Vec | null = null;
  private lastPencil: Vec | null = null;
  private exhaustedNotified = false;
  private spaceHeld = false;

  constructor(
    public readonly track: Track,
    public readonly camera: Camera,
  ) {}

  setSpaceHeld(v: boolean): void {
    this.spaceHeld = v;
  }

  get inkUsed(): number {
    return this.track.inkUsed(this.constraints.player || undefined);
  }

  get inkRemaining(): number {
    const b = this.constraints.budget;
    return b === null ? Infinity : Math.max(0, b - this.inkUsed);
  }

  canUseMaterial(m: MaterialId): boolean {
    return this.constraints.materials.includes(m);
  }

  private snapRadiusWorld(): number {
    return 8 / this.camera.zoom;
  }

  private snap(x: number, y: number): Vec {
    const s = this.track.snapPoint(x, y, this.snapRadiusWorld());
    return s ?? { x, y };
  }

  pointerDown(sx: number, sy: number, button: number, shift: boolean): void {
    this.lastScreen = { x: sx, y: sy };
    const w = this.camera.toWorld(sx, sy);
    this.cursorWorld = w;
    if (button === 1 || this.spaceHeld || this.tool === 'pan') {
      this.panning = true;
      return;
    }
    if (button === 2) {
      // Right-click flips the nearest line.
      const id = this.findNearest(w.x, w.y, 10 / this.camera.zoom);
      if (id >= 0 && !this.constraints.locked) this.flip(id);
      return;
    }
    if (this.constraints.locked) return;
    this.dragging = true;
    switch (this.tool) {
      case 'pencil': {
        const p = this.snap(w.x, w.y);
        this.lastPencil = p;
        this.current = { added: [], removed: [] };
        this.exhaustedNotified = false;
        break;
      }
      case 'line': {
        const p = this.snap(w.x, w.y);
        this.anchor = p;
        this.preview = { x1: p.x, y1: p.y, x2: p.x, y2: p.y };
        break;
      }
      case 'eraser': {
        this.current = { added: [], removed: [] };
        this.eraseAt(w.x, w.y);
        break;
      }
      case 'flip': {
        const id = this.findNearest(w.x, w.y, 10 / this.camera.zoom);
        if (id >= 0) this.flip(id);
        break;
      }
      case 'object':
        this.placeObject(w.x, w.y);
        break;
      default:
        break;
    }
    void shift;
  }

  /** Surface height of the nearest rideable line directly below (or just above) a point. */
  private surfaceBelow(x: number, y: number, reach = 80): number | null {
    let best: number | null = null;
    for (const l of this.track.lines.values()) {
      if (!MATERIALS[l.material].solid) continue;
      const minX = Math.min(l.x1, l.x2);
      const maxX = Math.max(l.x1, l.x2);
      if (x < minX || x > maxX || maxX === minX) continue;
      const t = (x - l.x1) / (l.x2 - l.x1);
      const ly = l.y1 + (l.y2 - l.y1) * t;
      if (ly < y - 20 || ly > y + reach) continue;
      if (best === null || ly < best) best = ly;
    }
    // Stacking: the top of a prop already placed under the cursor counts as a surface too.
    for (const o of this.track.props.values()) {
      const d = o.def;
      const halfW = d.radius ? d.radius : (d.width ?? 10) / 2;
      const top = d.radius ? d.y - d.radius : d.y - (d.height ?? 10) / 2;
      if (x < d.x - halfW || x > d.x + halfW) continue;
      if (top < y - 20 || top > y + reach) continue;
      if (best === null || top < best) best = top;
    }
    return best;
  }

  /** Drop the selected object kind at a world position. Props snap onto the surface beneath. */
  placeObject(x: number, y: number): boolean {
    if (!this.constraints.canPlaceObjects || this.constraints.locked) return false;
    const kind = objectKind(this.objectKind);
    let py = y;
    if (kind.group === 'props' || kind.id === 'spring' || kind.id === 'ramp') {
      const surface = this.surfaceBelow(x, y);
      if (surface !== null) py = surface - 0.5;
    }
    const placement = kind.make(Math.round(x), Math.round(py));
    const cmd: Command = { added: [], removed: [], addedObjects: [], addedProps: [] };
    if (placement.lines) {
      for (const l of placement.lines) {
        const line = this.track.addLine({ ...l, layer: 'player', player: this.constraints.player });
        cmd.added.push({ ...line });
      }
    }
    if (placement.entity) cmd.addedObjects!.push(this.track.addObject(placement.entity));
    if (placement.prop) cmd.addedProps!.push(this.track.addProp(placement.prop));
    this.undoStack.push(cmd);
    this.redoStack.length = 0;
    this.events.onObjectPlaced?.(kind.id);
    this.events.onEdit?.();
    return true;
  }

  pointerMove(sx: number, sy: number, shift: boolean): void {
    const w = this.camera.toWorld(sx, sy);
    this.cursorWorld = w;
    if (this.panning) {
      this.camera.panBy(sx - this.lastScreen.x, sy - this.lastScreen.y);
      this.lastScreen = { x: sx, y: sy };
      return;
    }
    this.lastScreen = { x: sx, y: sy };
    if (!this.dragging) {
      if (this.tool === 'eraser' || this.tool === 'flip') {
        this.hoverId = this.findNearest(w.x, w.y, 10 / this.camera.zoom);
      } else this.hoverId = -1;
      return;
    }
    switch (this.tool) {
      case 'pencil': {
        if (!this.lastPencil) break;
        const minSeg = Math.max(2, 5 / this.camera.zoom);
        const dx = w.x - this.lastPencil.x;
        const dy = w.y - this.lastPencil.y;
        if (dx * dx + dy * dy < minSeg * minSeg) break;
        const added = this.addLine(this.lastPencil.x, this.lastPencil.y, w.x, w.y);
        if (added) {
          this.lastPencil = { x: added.x2, y: added.y2 };
          if (added.x2 !== w.x || added.y2 !== w.y) {
            // Budget truncated the segment: stop the stroke here.
            this.lastPencil = null;
          }
        } else this.lastPencil = null;
        break;
      }
      case 'line': {
        if (!this.anchor) break;
        let end = this.snap(w.x, w.y);
        if (shift) {
          const ang = Math.atan2(end.y - this.anchor.y, end.x - this.anchor.x);
          const step = Math.PI / 12;
          const snapped = Math.round(ang / step) * step;
          const len = Math.sqrt((end.x - this.anchor.x) ** 2 + (end.y - this.anchor.y) ** 2);
          end = { x: this.anchor.x + Math.cos(snapped) * len, y: this.anchor.y + Math.sin(snapped) * len };
        }
        this.preview = { x1: this.anchor.x, y1: this.anchor.y, x2: end.x, y2: end.y };
        break;
      }
      case 'eraser':
        this.eraseAt(w.x, w.y);
        break;
      default:
        break;
    }
  }

  pointerUp(): void {
    if (this.panning) {
      this.panning = false;
      return;
    }
    if (!this.dragging) return;
    this.dragging = false;
    switch (this.tool) {
      case 'pencil':
        this.commit();
        this.lastPencil = null;
        break;
      case 'line': {
        if (this.preview) {
          const p = this.preview;
          const len = Math.sqrt((p.x2 - p.x1) ** 2 + (p.y2 - p.y1) ** 2);
          if (len >= 1) {
            this.current = { added: [], removed: [] };
            this.addLine(p.x1, p.y1, p.x2, p.y2);
            this.commit();
          }
        }
        this.preview = null;
        this.anchor = null;
        break;
      }
      case 'eraser':
        this.commit();
        break;
      default:
        break;
    }
  }

  cancel(): void {
    this.dragging = false;
    this.panning = false;
    this.preview = null;
    this.anchor = null;
    this.lastPencil = null;
    this.commit();
  }

  /** Abandon the drag in progress and revert what it changed (a second finger turned it into a gesture). */
  discardDrag(): void {
    const c = this.current;
    this.dragging = false;
    this.panning = false;
    this.preview = null;
    this.anchor = null;
    this.lastPencil = null;
    this.current = null;
    if (!c) return;
    for (const l of c.added) this.track.removeLine(l.id);
    for (const l of c.removed) this.track.addLine(l, l.id);
    for (const o of c.addedObjects ?? []) this.track.removeObject(o.id);
    for (const o of c.removedObjects ?? []) this.track.addObject(o.def, o.id);
    for (const o of c.addedProps ?? []) this.track.removeProp(o.id);
    for (const o of c.removedProps ?? []) this.track.addProp(o.def, o.id);
    this.events.onEdit?.();
  }

  wheel(sx: number, sy: number, deltaY: number): void {
    const factor = Math.exp(-deltaY * 0.0012);
    this.camera.zoomAt(sx, sy, factor);
  }

  /** Add a line respecting the ink budget. Returns the line actually added (possibly truncated). */
  addLine(x1: number, y1: number, x2: number, y2: number): LineData | null {
    if (this.constraints.locked) return null;
    const material = this.canUseMaterial(this.material) ? this.material : this.constraints.materials[0];
    if (!material) return null;
    const costPerPx = MATERIALS[material].cost / PX_PER_METER;
    let ex = x2;
    let ey = y2;
    const len = Math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2);
    if (len < 0.5) return null;
    if (costPerPx > 0) {
      const remaining = this.inkRemaining;
      const cost = len * costPerPx;
      if (cost > remaining) {
        const allowed = remaining / costPerPx;
        if (allowed < 2) {
          if (!this.exhaustedNotified) {
            this.exhaustedNotified = true;
            this.events.onInkExhausted?.();
          }
          return null;
        }
        ex = x1 + ((x2 - x1) * allowed) / len;
        ey = y1 + ((y2 - y1) * allowed) / len;
        this.exhaustedNotified = true;
        this.events.onInkExhausted?.();
      }
    }
    const line = this.track.addLine({
      x1,
      y1,
      x2: ex,
      y2: ey,
      material,
      layer: 'player',
      player: this.constraints.player,
    });
    if (!this.current) this.current = { added: [], removed: [] };
    this.current.added.push({ ...line });
    this.events.onLineDrawn?.(Math.hypot(line.x2 - line.x1, line.y2 - line.y1) / PX_PER_METER, material);
    this.events.onEdit?.();
    return line;
  }

  private eraseAt(x: number, y: number): void {
    const r = this.eraserRadiusScreen / this.camera.zoom;
    const hits: LineData[] = [];
    for (const l of this.track.lines.values()) {
      if (l.layer === 'level' && !this.constraints.canEraseLevel) continue;
      if (this.constraints.player && l.player !== this.constraints.player && l.layer === 'player') continue;
      if (distToSegment(x, y, l.x1, l.y1, l.x2, l.y2) <= r) hits.push(l);
    }
    for (const l of hits) {
      this.track.removeLine(l.id);
      this.current?.removed.push({ ...l });
    }
    let hitObjects = 0;
    if (this.constraints.canPlaceObjects) {
      const rr = Math.max(r, 14 / this.camera.zoom);
      for (const o of [...this.track.objects.values()]) {
        const a = entityAnchor(o.def);
        if (Math.hypot(a.x - x, a.y - y) <= rr) {
          this.track.removeObject(o.id);
          if (this.current) (this.current.removedObjects ??= []).push(o);
          hitObjects++;
        }
      }
      for (const o of [...this.track.props.values()]) {
        if (Math.hypot(o.def.x - x, o.def.y - y) <= rr) {
          this.track.removeProp(o.id);
          if (this.current) (this.current.removedProps ??= []).push(o);
          hitObjects++;
        }
      }
    }
    if (hits.length || hitObjects) {
      this.events.onErased?.(hits.length, hitObjects);
      this.events.onEdit?.();
    }
  }

  private flip(id: number): void {
    const l = this.track.lines.get(id);
    if (!l) return;
    if (l.layer === 'level' && !this.constraints.canEraseLevel) return;
    this.track.flipLine(id);
    this.events.onFlip?.();
    this.events.onEdit?.();
  }

  private findNearest(x: number, y: number, radius: number): number {
    let best = -1;
    let bestD = radius;
    for (const l of this.track.lines.values()) {
      const d = distToSegment(x, y, l.x1, l.y1, l.x2, l.y2);
      if (d < bestD) {
        bestD = d;
        best = l.id;
      }
    }
    return best;
  }

  private commit(): void {
    const c = this.current;
    if (c && (c.added.length || c.removed.length || c.removedObjects?.length || c.removedProps?.length)) {
      this.undoStack.push(c);
      if (this.undoStack.length > 200) this.undoStack.shift();
      this.redoStack.length = 0;
    }
    this.current = null;
  }

  undo(): void {
    const cmd = this.undoStack.pop();
    if (!cmd) return;
    for (const l of cmd.added) this.track.removeLine(l.id);
    for (const l of cmd.removed) this.track.addLine(l, l.id);
    for (const o of cmd.addedObjects ?? []) this.track.removeObject(o.id);
    for (const o of cmd.removedObjects ?? []) this.track.addObject(o.def, o.id);
    for (const o of cmd.addedProps ?? []) this.track.removeProp(o.id);
    for (const o of cmd.removedProps ?? []) this.track.addProp(o.def, o.id);
    this.redoStack.push(cmd);
    this.events.onUndo?.();
    this.events.onEdit?.();
  }

  redo(): void {
    const cmd = this.redoStack.pop();
    if (!cmd) return;
    for (const l of cmd.removed) this.track.removeLine(l.id);
    for (const l of cmd.added) this.track.addLine(l, l.id);
    for (const o of cmd.removedObjects ?? []) this.track.removeObject(o.id);
    for (const o of cmd.addedObjects ?? []) this.track.addObject(o.def, o.id);
    for (const o of cmd.removedProps ?? []) this.track.removeProp(o.id);
    for (const o of cmd.addedProps ?? []) this.track.addProp(o.def, o.id);
    this.undoStack.push(cmd);
    this.events.onRedo?.();
    this.events.onEdit?.();
  }

  clearHistory(): void {
    this.undoStack.length = 0;
    this.redoStack.length = 0;
    this.current = null;
  }

  /** Remove every player-drawn line and placed object (used by "clear my track"). */
  clearPlayerLines(): void {
    const cmd: Command = { added: [], removed: [], removedObjects: [], removedProps: [] };
    for (const l of [...this.track.lines.values()]) {
      if (l.layer !== 'player') continue;
      if (this.constraints.player && l.player !== this.constraints.player) continue;
      this.track.removeLine(l.id);
      cmd.removed.push({ ...l });
    }
    if (this.constraints.canPlaceObjects) {
      for (const o of [...this.track.objects.values()]) {
        this.track.removeObject(o.id);
        cmd.removedObjects!.push(o);
      }
      for (const o of [...this.track.props.values()]) {
        this.track.removeProp(o.id);
        cmd.removedProps!.push(o);
      }
    }
    if (cmd.removed.length || cmd.removedObjects!.length || cmd.removedProps!.length) {
      this.undoStack.push(cmd);
      this.redoStack.length = 0;
      this.events.onEdit?.();
    }
  }
}
