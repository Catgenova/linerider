import type { Game } from '../game/game';
import { MATERIALS, MATERIAL_ORDER } from '../physics/materials';
import type { Screens } from './screens';

/** Pointer and keyboard wiring for the canvas. */
export function bindInput(canvas: HTMLCanvasElement, game: Game, screens: Screens): void {
  const pos = (ev: PointerEvent | WheelEvent) => {
    const r = canvas.getBoundingClientRect();
    return { x: ev.clientX - r.left, y: ev.clientY - r.top };
  };
  canvas.addEventListener('contextmenu', (ev) => ev.preventDefault());
  window.addEventListener('pointerdown', () => game.audio.unlock(), { capture: true });
  window.addEventListener('keydown', () => game.audio.unlock(), { capture: true });
  canvas.addEventListener('pointerdown', (ev) => {
    if (screens.open) return;
    canvas.setPointerCapture(ev.pointerId);
    const { x, y } = pos(ev);
    if (ev.button === 0) {
      const w = game.camera.toWorld(x, y);
      if (game.handlePlacementClick(w.x, w.y)) return;
    }
    game.editor.pointerDown(x, y, ev.button, ev.shiftKey);
    if (ev.button === 1) ev.preventDefault();
  });
  canvas.addEventListener('pointermove', (ev) => {
    if (screens.open) return;
    const { x, y } = pos(ev);
    game.editor.pointerMove(x, y, ev.shiftKey);
  });
  const up = () => {
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
