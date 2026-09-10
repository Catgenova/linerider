import type { ToolId } from '../editor/editor';
import type { Game } from '../game/game';
import { objectiveLabel, evaluateObjective, type Objective } from '../game/level';
import { MATERIALS, type MaterialId } from '../physics/materials';
import { OBJECT_KINDS } from '../game/objectKinds';
import { $, clear, el, fmtTime } from './dom';

/** Coarse pointers (phones, tablets) get touch wording instead of keyboard hints. */
const TOUCH = typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(pointer: coarse)').matches;

const TOOLS: { id: ToolId; label: string; key: string; icon: string }[] = [
  { id: 'pencil', label: 'Pencil', key: 'P', icon: '✎' },
  { id: 'line', label: 'Line', key: 'L', icon: '╱' },
  { id: 'eraser', label: 'Eraser', key: 'E', icon: '⌫' },
  { id: 'flip', label: 'Flip side', key: 'V', icon: '⇅' },
  { id: 'pan', label: 'Pan', key: 'H', icon: '✥' },
  { id: 'object', label: 'Objects', key: 'O', icon: '⚙' },
];

/** In-game overlay: ink meter, timer, objectives, tools, materials, playback. */
export class Hud {
  private root = $('hud');
  private toolbar = el('div', { class: 'toolbar' });
  private palette = el('div', { class: 'palette' });
  private objectPalette = el('div', { class: 'palette objects' });
  private playbar = el('div', { class: 'playbar' });
  private objectives = el('div', { class: 'objectives' });
  private message = el('div', { class: 'hud-message' });
  private inkFill = el('div', { class: 'ink-fill' });
  private inkText = el('span', { class: 'ink-text' });
  private inkFill2 = el('div', { class: 'ink-fill p2' });
  private inkText2 = el('span', { class: 'ink-text' });
  private inkBox2 = el('div', { class: 'ink-box p2' });
  private title = el('div', { class: 'hud-title' });
  private stats = el('div', { class: 'hud-stats' });
  private hint = el('div', { class: 'hud-hint' });
  private messageTimer = 0;

  constructor(
    private readonly game: Game,
    private readonly onMenu: () => void,
  ) {
    const top = el('div', { class: 'hud-top' }, [
      el('div', { class: 'hud-left' }, [el('button', { class: 'btn icon', text: '☰', title: 'Menu (Esc)', onClick: () => this.onMenu() }), this.title]),
      el('div', { class: 'hud-center' }, [
        el('div', { class: 'ink-box' }, [el('div', { class: 'ink-bar' }, [this.inkFill]), this.inkText]),
        this.inkBox2,
      ]),
      el('div', { class: 'hud-right' }, [this.stats]),
    ]);
    this.inkBox2.append(el('div', { class: 'ink-bar' }, [this.inkFill2]), this.inkText2);
    this.root.append(top, this.objectives, this.toolbar, this.objectPalette, this.palette, this.playbar, this.message, this.hint);
    this.rebuild();
  }

  /** Rebuild tool/material/playback buttons (called on state changes). */
  rebuild(): void {
    const game = this.game;
    const editor = game.editor;
    clear(this.toolbar);
    for (const t of TOOLS) {
      if (t.id === 'object' && !editor.constraints.canPlaceObjects) continue;
      this.toolbar.append(
        el('button', {
          class: `btn tool${editor.tool === t.id ? ' active' : ''}`,
          title: `${t.label} (${t.key})`,
          onClick: () => game.setTool(t.id),
        }, [el('span', { class: 'icon', text: t.icon }), el('span', { class: 'label', text: t.label })]),
      );
    }
    this.toolbar.append(
      el('div', { class: 'sep' }),
      el('button', { class: 'btn tool', title: 'Undo (Ctrl+Z)', onClick: () => editor.undo() }, [el('span', { class: 'icon', text: '↶' }), el('span', { class: 'label', text: 'Undo' })]),
      el('button', { class: 'btn tool', title: 'Redo (Ctrl+Y)', onClick: () => editor.redo() }, [el('span', { class: 'icon', text: '↷' }), el('span', { class: 'label', text: 'Redo' })]),
      el('button', {
        class: 'btn tool',
        title: 'Erase all your lines',
        onClick: () => {
          editor.clearPlayerLines();
          game.stop();
        },
      }, [el('span', { class: 'icon', text: '✕' }), el('span', { class: 'label', text: 'Clear' })]),
    );
    if (game.mode === 'free' || (game.mode === 'library' && game.inkBudget === null)) {
      this.toolbar.append(
        el('div', { class: 'sep' }),
        el('button', { class: 'btn tool', title: 'Place the start point', onClick: () => game.beginPlacement('start') }, [el('span', { class: 'icon', text: '⚑' }), el('span', { class: 'label', text: 'Start' })]),
        el('button', { class: 'btn tool', title: 'Place a finish zone', onClick: () => game.beginPlacement('finish') }, [el('span', { class: 'icon', text: '◫' }), el('span', { class: 'label', text: 'Finish' })]),
      );
    }
    if (game.coop.active) {
      this.toolbar.append(
        el('div', { class: 'sep' }),
        el('button', { class: `btn tool p${game.coop.player}`, title: 'Switch player (Tab)', onClick: () => game.switchCoopPlayer() }, [
          el('span', { class: 'icon', text: '⇄' }),
          el('span', { class: 'label', text: `P${game.coop.player}` }),
        ]),
      );
    }

    clear(this.objectPalette);
    if (editor.constraints.canPlaceObjects && editor.tool === 'object') {
      for (const k of OBJECT_KINDS) {
        this.objectPalette.append(
          el('button', {
            class: `btn mat obj${editor.objectKind === k.id ? ' active' : ''}`,
            title: `${k.name}: ${k.description} Click on the track to place; the eraser removes it.`,
            onClick: () => game.setObjectKind(k.id),
          }, [el('span', { class: 'icon', text: k.icon }), el('span', { class: 'label', text: k.name })]),
        );
      }
    }
    clear(this.palette);
    for (const id of editor.constraints.materials) {
      const m = MATERIALS[id as MaterialId];
      this.palette.append(
        el('button', {
          class: `btn mat${editor.material === id ? ' active' : ''}`,
          title: `${m.name} (${m.hotkey}): ${m.description}${m.cost !== 1 ? ` Cost x${m.cost}.` : ''}`,
          style: `--mat:${m.color}`,
          onClick: () => game.setMaterial(id as MaterialId),
        }, [el('span', { class: 'swatch' }), el('span', { class: 'label', text: m.name }), el('span', { class: 'key', text: m.hotkey })]),
      );
    }

    clear(this.playbar);
    const playing = game.playState === 'play';
    this.playbar.append(
      el('button', { class: 'btn play', title: 'Play / pause (Space)', text: playing ? '❚❚' : '▶', onClick: () => game.togglePlay() }),
      el('button', { class: 'btn', title: 'Stop and rewind (Backspace)', text: '■', onClick: () => game.stop() }),
      el('button', { class: 'btn', title: 'Restart, keep lines (R)', text: '↻', onClick: () => game.restart(true) }),
      el('button', { class: `btn${game.flagFrame > 0 ? ' active' : ''}`, title: 'Flag: resume playback from this moment (F). Shift+F clears.', text: game.flagFrame > 0 ? `⚑ ${(game.flagFrame / 40).toFixed(1)}s` : '⚑', onClick: () => (game.flagFrame > 0 ? game.clearFlag() : game.setFlag()) }),
      el('div', { class: 'sep' }),
      ...[0.5, 1, 2, 4].map((s) =>
        el('button', { class: `btn small${game.speed === s ? ' active' : ''}`, text: `${s}x`, title: 'Playback speed ( , and . )', onClick: () => game.setSpeed(s) }),
      ),
      el('div', { class: 'sep' }),
      el('button', { class: `btn small${game.ghostEnabled ? ' active' : ''}`, text: 'ghost', title: 'Race your best run (G)', onClick: () => game.toggleGhost() }),
      el('button', { class: `btn small${game.audio.enabled ? ' active' : ''}`, text: '♪', title: 'Music and sound', onClick: () => game.toggleMusic() }),
    );

    const level = game.level;
    clear(this.title);
    const modeLabel = game.mode === 'free' ? 'FREE RIDE' : game.mode === 'arcade' ? 'RUSH' : game.mode === 'daily' ? 'DAILY' : game.mode === 'coop' ? 'CO-OP' : game.mode === 'library' ? 'COMMUNITY' : level?.mode.toUpperCase() ?? '';
    this.title.append(
      el('div', { class: 'name', text: level ? level.name : game.libraryTrack ? game.libraryTrack.title : game.mode === 'arcade' ? 'Cyber Rush' : 'Free Ride' }),
      el('div', { class: 'mode', text: `${modeLabel} · ${game.environment.name} · ${game.riderDef.name}` }),
    );
    this.inkBox2.style.display = game.coop.active ? '' : 'none';
    this.hint.textContent =
      game.mode === 'arcade'
        ? 'Draw ahead of Bosh. Ink grows with distance. Space pauses.'
        : TOUCH
          ? 'One finger draws · two fingers pan and zoom · the Flip tool taps a line'
          : 'Space play · R restart · F flag · scroll zoom · middle-drag pan · right-click flips a line';
    this.update();
  }

  showMessage(text: string, color: string, big = false): void {
    this.message.textContent = text;
    this.message.style.color = color;
    this.message.classList.toggle('big', big);
    this.message.classList.add('show');
    this.messageTimer = big ? 2.2 : 1.4;
  }

  /** Per-frame refresh of live values. */
  update(dt = 0): void {
    const game = this.game;
    const editor = game.editor;
    if (this.messageTimer > 0) {
      this.messageTimer -= dt;
      if (this.messageTimer <= 0) this.message.classList.remove('show');
    }
    const budget = editor.constraints.budget;
    if (game.coop.active) {
      const [b1, b2] = game.coop.budgets;
      const u1 = game.track.inkUsed(1);
      const u2 = game.track.inkUsed(2);
      this.inkFill.style.width = `${Math.max(0, Math.min(100, (1 - u1 / b1) * 100))}%`;
      this.inkText.textContent = `P1 ${Math.max(0, b1 - u1).toFixed(1)} m`;
      this.inkFill2.style.width = `${Math.max(0, Math.min(100, (1 - u2 / b2) * 100))}%`;
      this.inkText2.textContent = `P2 ${Math.max(0, b2 - u2).toFixed(1)} m`;
    } else if (budget === null) {
      this.inkFill.style.width = '100%';
      this.inkText.textContent = `∞ · ${game.inkUsed.toFixed(1)} m drawn`;
    } else {
      const used = game.inkUsed;
      const frac = Math.max(0, Math.min(1, 1 - used / budget));
      this.inkFill.style.width = `${frac * 100}%`;
      this.inkFill.classList.toggle('low', frac < 0.15);
      this.inkText.textContent = `INK ${Math.max(0, budget - used).toFixed(1)} / ${budget.toFixed(0)} m`;
    }

    const run = game.run;
    const parts: string[] = [];
    parts.push(`<div class="stat"><span class="k">TIME</span><span class="v">${run ? fmtTime(run.frame) : '0.00s'}</span></div>`);
    if (game.mode === 'arcade') {
      parts.push(`<div class="stat"><span class="k">DIST</span><span class="v">${game.metersTravelled().toFixed(0)} m</span></div>`);
      parts.push(`<div class="stat"><span class="k">BEST</span><span class="v">${game.progress.data.arcadeBest} m</span></div>`);
    }
    const score = run?.tricks.score ?? 0;
    parts.push(`<div class="stat"><span class="k">TRICKS</span><span class="v">${score}${run && run.tricks.combo > 1 ? ` <em>x${run.tricks.combo}</em>` : ''}</span></div>`);
    const level = game.level;
    if (level?.flags?.length) {
      parts.push(`<div class="stat"><span class="k">FLAGS</span><span class="v">${run ? run.flags.filter(Boolean).length : 0}/${level.flags.length}</span></div>`);
    }
    if (level?.rescues?.length) {
      parts.push(`<div class="stat"><span class="k">RESCUED</span><span class="v">${run ? run.rescues.filter(Boolean).length : 0}/${level.rescues.length}</span></div>`);
    }
    if (level?.mode === 'delivery') {
      const integ = run ? Math.round(run.cargoIntegrity) : 100;
      parts.push(`<div class="stat"><span class="k">CARGO</span><span class="v ${integ < 50 ? 'bad' : ''}">${run?.cargoLost ? 'LOST' : `${integ}%`}</span></div>`);
    }
    if (level?.mode === 'destruction') {
      parts.push(`<div class="stat"><span class="k">CHAOS</span><span class="v">${run ? run.chaosScore() : 0}</span></div>`);
    }
    if (level?.timeLimit) {
      const left = Math.max(0, level.timeLimit - (run ? run.frame / 40 : 0));
      parts.push(`<div class="stat"><span class="k">LIMIT</span><span class="v ${left < 5 ? 'bad' : ''}">${left.toFixed(1)}s</span></div>`);
    }
    const html = parts.join('');
    if (this.stats.innerHTML !== html) this.stats.innerHTML = html;

    // Objectives list.
    if (level) {
      const summary = run?.summary();
      const rows = level.objectives.map((o: Objective) => {
        const done = summary ? evaluateObjective(o, summary) : false;
        return `<div class="obj${done ? ' done' : ''}${o.optional ? ' optional' : ''}"><span class="mark">${done ? '◆' : '◇'}</span><span>${objectiveLabel(o)}</span>${o.optional ? '<span class="tag">MEDAL</span>' : ''}</div>`;
      });
      const h = rows.join('');
      if (this.objectives.innerHTML !== h) this.objectives.innerHTML = h;
      this.objectives.style.display = '';
    } else if (game.mode === 'library' && game.libraryTrack) {
      const h = `<div class="obj"><span class="mark">◇</span><span>${game.track.finish ? 'Reach the finish' : 'Ride free'}</span></div>`;
      if (this.objectives.innerHTML !== h) this.objectives.innerHTML = h;
      this.objectives.style.display = '';
    } else {
      this.objectives.style.display = 'none';
    }
  }

  setVisible(v: boolean): void {
    this.root.style.display = v ? '' : 'none';
  }
}
