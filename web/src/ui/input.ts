import type { Game } from '../game/game';
import { MATERIALS, MATERIAL_ORDER } from '../physics/materials';
import type { Screens } from './screens';

/**
 * Pointer, touch and keyboard wiring for the canvas. A mouse works as before; on a touchscreen one
 * finger drives the current tool and two fingers pan and pinch-zoom.
 */
export function bindInput(canvas: HTMLCanvasElement, game: Game, screens: Screens): void {
  const pos = (ev: PointerEvent | WheelEvent) => {
    const r = canvas.getBoundingClientRect();
    return { x: ev.clientX - r.left, y: ev.clientY - r.top };
  };
  const touches = new Map<number, { x: number; y: number }>();
  /** The pointer currently driving the editor (mouse button or the first finger). */
  let drawId: number | null = null;
  let pinch: { cx: number; cy: number; d: number } | null = null;
  /** After a two-finger gesture, ignore new strokes until every finger has lifted. */
  let gestureLock = false;
  const pinchState = () => {
    const [a, b] = [...touches.values()];
    return { cx: (a.x + b.x) / 2, cy: (a.y + b.y) / 2, d: Math.hypot(b.x - a.x, b.y - a.y) };
  };

  canvas.addEventListener('contextmenu', (ev) => ev.preventDefault());
  window.addEventListener('pointerdown', () => game.audio.unlock(), { capture: true });
  window.addEventListener('keydown', () => game.audio.unlock(), { capture: true });

  canvas.addEventListener('pointerdown', (ev) => {
    if (screens.open) return;
    canvas.setPointerCapture(ev.pointerId);
    const { x, y } = pos(ev);
    if (ev.pointerType === 'touch') {
      touches.set(ev.pointerId, { x, y });
      if (touches.size >= 2) {
        // A second finger turns the stroke into a pan/zoom gesture; the stroke is discarded.
        if (drawId !== null) {
          game.editor.discardDrag();
          drawId = null;
        }
        pinch = pinchState();
        gestureLock = true;
        return;
      }
      if (gestureLock) return;
    }
    if (ev.button === 0) {
      const w = game.camera.toWorld(x, y);
      if (game.handlePlacementClick(w.x, w.y)) return;
    }
    drawId = ev.pointerId;
    game.editor.pointerDown(x, y, ev.button, ev.shiftKey);
    if (ev.button === 1) ev.preventDefault();
  });
  canvas.addEventListener('pointermove', (ev) => {
    if (screens.open) return;
    const { x, y } = pos(ev);
    if (ev.pointerType === 'touch') {
      if (!touches.has(ev.pointerId)) return;
      touches.set(ev.pointerId, { x, y });
      if (pinch && touches.size >= 2) {
        const p = pinchState();
        if (pinch.d > 1 && p.d > 1) game.camera.zoomAt(p.cx, p.cy, p.d / pinch.d);
        game.camera.panBy(p.cx - pinch.cx, p.cy - pinch.cy);
        game.following = false;
        pinch = p;
        return;
      }
      if (ev.pointerId !== drawId) return;
    }
    game.editor.pointerMove(x, y, ev.shiftKey);
  });
  const up = (ev: PointerEvent) => {
    if (ev.pointerType === 'touch') {
      touches.delete(ev.pointerId);
      if (touches.size < 2) pinch = null;
      if (touches.size === 0) gestureLock = false;
      if (ev.pointerId !== drawId) return;
    }
    drawId = null;
    game.editor.pointerUp();
  };
  canvas.addEventListener('pointerup', up);
  canvas.addEventListener('pointercancel', up);
  canvas.addEventListener(
    'wheel',
    (ev) => {
      if (screens.open) return;
      ev.preventDefault();
      const { x, y } = pos(ev);
      game.editor.wheel(x, y, ev.deltaY);
    },
    { passive: false },
  );

  window.addEventListener('keydown', (ev) => {
    const target = ev.target as HTMLElement | null;
    if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA')) return;
    if (ev.key === 'Escape') {
      ev.preventDefault();
      if (screens.open) {
        if (screens.current === 'pause') screens.hide();
        return;
      }
      screens.pause();
      return;
    }
    if (screens.open) return;
    const editor = game.editor;
    const ctrl = ev.ctrlKey || ev.metaKey;
    if (ctrl && ev.key.toLowerCase() === 'z') {
      ev.preventDefault();
      if (ev.shiftKey) editor.redo();
      else editor.undo();
      return;
    }
    if (ctrl && ev.key.toLowerCase() === 'y') {
      ev.preventDefault();
      editor.redo();
      return;
    }
    if (ev.repeat) return;
    switch (ev.key) {
      case ' ':
        ev.preventDefault();
        game.togglePlay();
        break;
      case 'Backspace':
        ev.preventDefault();
        game.stop();
        break;
      case 'r':
        game.restart(true);
        break;
      case 'R':
        game.restart(false);
        break;
      case 'f':
        game.setFlag();
        break;
      case 'F':
        game.clearFlag();
        break;
      case 'p':
      case 'P':
        game.setTool('pencil');
        break;
      case 'l':
      case 'L':
        game.setTool('line');
        break;
      case 'e':
      case 'E':
        game.setTool('eraser');
        break;
      case 'h':
      case 'H':
        game.setTool('pan');
        break;
      case 'v':
      case 'V':
        game.setTool('flip');
        break;
      case 'o':
      case 'O':
        if (game.editor.constraints.canPlaceObjects) game.setTool('object');
        break;
      case 'g':
      case 'G':
        game.toggleGhost();
        break;
      case 'c':
      case 'C':
        game.following = true;
        if (!game.run) game.camera.follow(game.track.start.x + 80, game.track.start.y);
        break;
      case 'Tab':
        ev.preventDefault();
        game.switchCoopPlayer();
        break;
      case ',':
        game.setSpeed(Math.max(0.25, game.speed / 2));
        break;
      case '.':
        game.setSpeed(Math.min(8, game.speed * 2));
        break;
      default: {
        const mat = MATERIAL_ORDER.find((m) => MATERIALS[m].hotkey === ev.key);
        if (mat) game.setMaterial(mat);
      }
    }
  });
}
