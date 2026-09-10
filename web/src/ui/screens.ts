import type { Game, ResultsInfo } from '../game/game';
import { ENVIRONMENTS, ENVIRONMENT_ORDER, type EnvironmentId } from '../game/environments';
import { objectiveLabel, type LevelDef, type Medal } from '../game/level';
import { LEVELS, REGIONS } from '../game/levels';
import { decodeShareCode, encodeShareCode, type PublishedTrack } from '../game/library';
import { RIDERS, RIDER_ORDER, type RiderId } from '../game/riders';
import { MATERIALS } from '../physics/materials';
import { Renderer } from '../render/renderer';
import { $, clear, el, fmtInk, fmtTime } from './dom';
import { todayKey } from '../modes/daily';

export type ScreenName = 'title' | 'map' | 'intro' | 'results' | 'library' | 'riders' | 'coop' | 'pause' | 'daily' | 'none';

const MEDAL_ICON: Record<Medal, string> = { none: '', bronze: '●', silver: '●', gold: '●' };

/** Full-screen menus layered over the canvas. */
export class Screens {
  private root = $('overlay');
  current: ScreenName = 'none';
  private selectedRegion = 0;
  private pendingCoop = false;

  constructor(
    private readonly game: Game,
    private readonly onEnterGame: () => void,
  ) {}

  get open(): boolean {
    return this.current !== 'none';
  }

  hide(): void {
    this.current = 'none';
    clear(this.root);
    this.root.classList.remove('show');
  }

  private mount(name: ScreenName, ...nodes: HTMLElement[]): void {
    this.current = name;
    clear(this.root);
    this.root.append(...nodes);
    this.root.classList.add('show');
  }

  private panel(cls: string, children: (HTMLElement | string | null | false)[]): HTMLElement {
    return el('div', { class: `panel ${cls}` }, children);
  }

  // ------------------------------------------------------------------ title

  title(): void {
    const g = this.game;
    const medals = g.progress.totalMedals();
    const done = LEVELS.filter((l) => g.progress.isComplete(l.id)).length;
    this.mount(
      'title',
      el('div', { class: 'title-screen' }, [
        el('div', { class: 'logo' }, [el('span', { class: 'l1', text: 'CYBER' }), el('span', { class: 'l2', text: 'RIDER' })]),
        el('div', { class: 'tagline', text: 'Draw the line. Ride the pulse.' }),
        el('div', { class: 'menu' }, [
          this.bigButton('ADVENTURE', `${done}/${LEVELS.length} levels · ${medals.gold} gold`, () => this.map()),
          this.bigButton('FREE RIDE', 'Unlimited ink, every material, publish your tracks', () => this.freeRideEnv()),
          this.bigButton('CYBER RUSH', `Draw while riding · best ${g.progress.data.arcadeBest} m`, () => {
            g.startArcade();
            this.hide();
            this.onEnterGame();
          }),
          this.bigButton('DAILY CHALLENGE', todayKey(), () => this.daily()),
          this.bigButton('TRACK LIBRARY', 'Community tracks, share codes, records', () => this.library()),
          this.bigButton('CO-OP', 'Two players, two colours of ink', () => this.coop()),
          this.bigButton('RIDERS', `Riding as ${g.riderDef.name}`, () => this.riders()),
        ]),
        el('div', { class: 'foot' }, [
          el('button', {
            class: 'btn ghost',
            text: 'reset progress',
            onClick: () => {
              if (confirm('Erase all campaign progress, records and ghosts?')) {
                g.progress.reset();
                this.title();
              }
            },
          }),
        ]),
      ]),
    );
  }

  private bigButton(title: string, sub: string, onClick: () => void): HTMLElement {
    return el('button', { class: 'big-btn', onClick }, [el('span', { class: 't', text: title }), el('span', { class: 's', text: sub })]);
  }

  private freeRideEnv(): void {
    const g = this.game;
    this.mount(
      'title',
      this.panel('narrow', [
        el('h2', { text: 'FREE RIDE' }),
        el('p', { class: 'muted', text: 'Pick a world. Each one brings its own physics gimmick.' }),
        el('div', { class: 'grid envs' }, ENVIRONMENT_ORDER.map((id) => this.envCard(id, () => {
          g.startFree(id);
          this.hide();
          this.onEnterGame();
        }))),
        el('div', { class: 'row' }, [el('button', { class: 'btn', text: '← back', onClick: () => this.title() })]),
      ]),
    );
  }

  private envCard(id: EnvironmentId, onClick: () => void): HTMLElement {
    const e = ENVIRONMENTS[id];
    return el('button', { class: 'env-card', style: `--a:${e.visuals.accent};--b:${e.visuals.bg[1]}`, onClick }, [
      el('span', { class: 'n', text: e.name }),
      el('span', { class: 'g', text: e.gimmick }),
    ]);
  }

  // ------------------------------------------------------------------ adventure map

  map(regionIndex = this.selectedRegion): void {
    const g = this.game;
    this.selectedRegion = regionIndex;
    const region = REGIONS[regionIndex];
    const svgPoints = REGIONS.map((r) => `${r.mapX * 100},${r.mapY * 100}`).join(' ');
    const mapArea = el('div', { class: 'map-area' });
    mapArea.innerHTML = `<svg viewBox="0 0 100 100" preserveAspectRatio="none" class="map-path"><polyline points="${svgPoints}" /></svg>`;
    let furthest = 0;
    REGIONS.forEach((r, i) => {
      const unlocked = g.regionUnlocked(i);
      if (unlocked) furthest = i;
      const levels = g.levelsInRegion(r.id);
      const done = levels.filter((l) => g.progress.isComplete(l.id)).length;
      const node = el('button', {
        class: `map-node${unlocked ? '' : ' locked'}${i === regionIndex ? ' selected' : ''}`,
        style: `left:${r.mapX * 100}%;top:${r.mapY * 100}%;--a:${ENVIRONMENTS[r.environment].visuals.accent}`,
        onClick: () => unlocked && this.map(i),
        title: r.blurb,
      }, [el('span', { class: 'dot' }), el('span', { class: 'nm', text: r.name }), el('span', { class: 'ct', text: unlocked ? `${done}/${levels.length}` : '🔒' })]);
      mapArea.append(node);
    });
    const marker = REGIONS[furthest];
    mapArea.append(el('div', { class: 'map-bosh', style: `left:${marker.mapX * 100}%;top:${marker.mapY * 100 - 9}%`, text: '⛷' }));

    const levels = g.levelsInRegion(region.id);
    const list = el('div', { class: 'level-list' }, levels.map((lv) => this.levelCard(lv)));
    this.mount(
      'map',
      this.panel('wide', [
        el('div', { class: 'row between' }, [
          el('h2', { text: 'ADVENTURE MAP' }),
          el('button', { class: 'btn', text: '← menu', onClick: () => this.title() }),
        ]),
        mapArea,
        el('div', { class: 'region-head' }, [
          el('h3', { text: region.name, style: `color:${ENVIRONMENTS[region.environment].visuals.accent}` }),
          el('span', { class: 'muted', text: `${region.blurb} · ${ENVIRONMENTS[region.environment].gimmick}` }),
        ]),
        list,
      ]),
    );
  }

  private levelCard(lv: LevelDef): HTMLElement {
    const g = this.game;
    const rec = g.progress.level(lv.id);
    const unlocked = g.levelUnlocked(lv);
    return el('button', {
      class: `level-card${unlocked ? '' : ' locked'} medal-${rec.medal}`,
      onClick: () => unlocked && this.intro(lv),
    }, [
      el('span', { class: 'medal', text: MEDAL_ICON[rec.medal] }),
      el('span', { class: 'n', text: lv.name }),
      el('span', { class: 'm', text: `${lv.mode.toUpperCase()} · ${lv.budget} m ink` }),
      el('span', { class: 't', text: lv.tagline }),
      el('span', { class: 'r', text: unlocked ? (rec.bestFrames !== null ? `best ${fmtTime(rec.bestFrames)} · ${fmtInk(rec.bestInk)} · ${rec.bestTrick} pts` : 'no record yet') : 'complete the previous level' }),
    ]);
  }

  // ------------------------------------------------------------------ level intro

  intro(level: LevelDef, coop = false): void {
    const g = this.game;
    this.pendingCoop = coop;
    let riderId: RiderId = level.rider ?? g.riderDef.id;
    if (!g.riderUnlocked(riderId)) riderId = 'bosh';
    const riderRow = el('div', { class: 'rider-row' });
    const renderRiders = () => {
      clear(riderRow);
      for (const id of RIDER_ORDER) {
        const r = RIDERS[id];
        const unlocked = g.riderUnlocked(id);
        riderRow.append(
          el('button', {
            class: `chip${riderId === id ? ' active' : ''}${unlocked ? '' : ' locked'}`,
            style: `--a:${r.color}`,
            text: unlocked ? r.name : `${r.name} 🔒`,
            title: unlocked ? r.description : `Unlock by completing ${r.unlock}`,
            onClick: () => {
              if (!unlocked) return;
              riderId = id;
              renderRiders();
            },
          }),
        );
      }
    };
    renderRiders();
    const env = ENVIRONMENTS[level.environment];
    this.mount(
      'intro',
      this.panel('narrow intro', [
        el('div', { class: 'eyebrow', text: `${REGIONS.find((r) => r.id === level.region)?.name ?? ''} · ${level.mode.toUpperCase()}${coop ? ' · CO-OP' : ''}` }),
        el('h2', { text: level.name }),
        el('p', { class: 'tag', text: level.tagline }),
        el('p', { class: 'brief', text: level.briefing }),
        el('div', { class: 'gimmick', style: `--a:${env.visuals.accent}` }, [el('b', { text: env.name }), ` · ${env.gimmick}`]),
        el('div', { class: 'objectives-list' }, level.objectives.map((o) => el('div', { class: `obj${o.optional ? ' optional' : ''}` }, [
          el('span', { class: 'mark', text: '◇' }),
          el('span', { text: objectiveLabel(o) }),
          o.optional ? el('span', { class: 'tag', text: 'MEDAL' }) : null,
        ]))),
        el('div', { class: 'row wrap' }, [
          el('span', { class: 'pill', text: `INK ${coop ? Math.round(level.budget * 0.6) + ' m each' : level.budget + ' m'}` }),
          ...level.materials.map((m) => el('span', { class: 'pill mat', style: `--mat:${MATERIALS[m].color}`, text: MATERIALS[m].name })),
          level.timeLimit ? el('span', { class: 'pill', text: `LIMIT ${level.timeLimit}s` }) : null,
        ]),
        level.rider ? el('p', { class: 'muted', text: `Rider locked: ${RIDERS[level.rider].name}` }) : riderRow,
        el('div', { class: 'row end' }, [
          el('button', { class: 'btn', text: '← back', onClick: () => (coop ? this.coop() : this.map()) }),
          el('button', {
            class: 'btn primary',
            text: coop ? 'START CO-OP' : 'START',
            onClick: () => {
              g.loadLevel(level, riderId, coop);
              this.hide();
              this.onEnterGame();
            },
          }),
        ]),
      ]),
    );
  }

  // ------------------------------------------------------------------ results

  results(info: ResultsInfo): void {
    const g = this.game;
    const s = info.summary;
    const heading = info.mode === 'arcade' ? 'RUN OVER' : info.complete ? 'LEVEL COMPLETE' : s.failReason ? s.failReason.toUpperCase() : 'NOT YET';
    const color = info.complete ? '#4dff9d' : '#ff3d7f';
    const imp = info.improvements;
    const badges: HTMLElement[] = [];
    if (imp?.newMedal) badges.push(el('span', { class: 'badge', text: `NEW MEDAL: ${info.medal.toUpperCase()}` }));
    if (imp?.newTime) badges.push(el('span', { class: 'badge', text: 'NEW BEST TIME' }));
    if (imp?.newInk) badges.push(el('span', { class: 'badge', text: 'LEAST INK' }));
    if (imp?.newTrick) badges.push(el('span', { class: 'badge', text: info.mode === 'arcade' ? 'NEW BEST DISTANCE' : 'NEW TRICK RECORD' }));
    const stats = el('div', { class: 'stat-grid' }, [
      this.stat('TIME', s.finished ? fmtTime(s.frames) : '--'),
      this.stat('INK USED', `${s.inkUsed.toFixed(1)} m`),
      info.level ? this.stat('INK LEFT', `${Math.max(0, info.level.budget - s.inkUsed).toFixed(1)} m`) : null,
      this.stat('TRICKS', `${s.trickScore}`),
      this.stat('AIRTIME', `${(s.airtimeFrames / 40).toFixed(1)}s`),
      info.mode === 'arcade' ? this.stat('DISTANCE', `${s.distance.toFixed(0)} m`) : this.stat('FLIPS', `${s.flips}`),
      s.flagsTotal ? this.stat('FLAGS', `${s.flagsCollected}/${s.flagsTotal}`) : null,
      s.rescueTotal ? this.stat('RESCUED', `${s.rescued}/${s.rescueTotal}`) : null,
      info.level?.mode === 'delivery' ? this.stat('CARGO', s.cargoLost ? 'LOST' : `${s.cargoIntegrity}%`) : null,
      info.level?.mode === 'destruction' ? this.stat('CHAOS', `${s.chaos}`) : null,
    ]);
    const objectives = info.results.length
      ? el('div', { class: 'objectives-list' }, info.results.map((r) => el('div', { class: `obj${r.done ? ' done' : ''}${r.objective.optional ? ' optional' : ''}` }, [
          el('span', { class: 'mark', text: r.done ? '◆' : '◇' }),
          el('span', { text: objectiveLabel(r.objective) }),
          r.objective.optional ? el('span', { class: 'tag', text: 'MEDAL' }) : null,
        ])))
      : null;
    const buttons: HTMLElement[] = [
      el('button', { class: 'btn', text: 'RETRY (keep lines)', onClick: () => { this.hide(); g.restart(true); } }),
      el('button', { class: 'btn', text: 'REBUILD (wipe)', onClick: () => { this.hide(); g.restart(false); } }),
    ];
    if (info.mode === 'arcade') buttons.length = 0, buttons.push(el('button', { class: 'btn primary', text: 'RUN AGAIN', onClick: () => { this.hide(); g.startArcade(); } }));
    if (info.next && info.complete && g.levelUnlocked(info.next)) buttons.push(el('button', { class: 'btn primary', text: `NEXT: ${info.next.name} →`, onClick: () => this.intro(info.next!) }));
    if (info.mode === 'free' || info.mode === 'library') buttons.push(el('button', { class: 'btn', text: 'KEEP EDITING', onClick: () => { this.hide(); g.stop(); } }));
    if (info.mode === 'free') buttons.push(el('button', { class: 'btn', text: 'PUBLISH TRACK', onClick: () => this.publishForm() }));
    buttons.push(el('button', { class: 'btn ghost', text: info.mode === 'campaign' ? 'MAP' : 'MENU', onClick: () => (info.mode === 'campaign' ? this.map() : this.title()) }));
    this.mount(
      'results',
      this.panel('narrow results', [
        el('h2', { text: heading, style: `color:${color}` }),
        info.level ? el('p', { class: 'muted', text: info.level.name }) : null,
        info.mode === 'campaign' || info.mode === 'daily' ? el('div', { class: `medal-big medal-${info.medal}` }, [el('span', { text: info.medal === 'none' ? 'no medal' : `${info.medal} medal` })]) : null,
        badges.length ? el('div', { class: 'row wrap' }, badges) : null,
        stats,
        objectives,
        el('div', { class: 'row wrap end' }, buttons),
      ]),
    );
  }

  private stat(k: string, v: string): HTMLElement {
    return el('div', { class: 'stat' }, [el('span', { class: 'k', text: k }), el('span', { class: 'v', text: v })]);
  }

  // ------------------------------------------------------------------ pause

  pause(): void {
    const g = this.game;
    const wasPlaying = g.playState === 'play';
    if (wasPlaying) g.pause();
    const lv = g.level;
    this.mount(
      'pause',
      this.panel('narrow', [
        el('h2', { text: 'PAUSED' }),
        lv ? el('p', { class: 'brief', text: lv.briefing }) : null,
        el('div', { class: 'row wrap end' }, [
          el('button', { class: 'btn primary', text: 'RESUME', onClick: () => { this.hide(); if (wasPlaying) g.play(); } }),
          el('button', { class: 'btn', text: 'RESTART', onClick: () => { this.hide(); g.restart(true); } }),
          el('button', { class: 'btn', text: 'WIPE LINES', onClick: () => { this.hide(); g.restart(false); g.stop(); } }),
          g.mode === 'free' ? el('button', { class: 'btn', text: 'PUBLISH TRACK', onClick: () => this.publishForm() }) : null,
          el('button', { class: 'btn ghost', text: 'QUIT TO MENU', onClick: () => { g.stop(); this.title(); } }),
        ]),
      ]),
    );
  }

  // ------------------------------------------------------------------ riders

  riders(): void {
    const g = this.game;
    this.mount(
      'riders',
      this.panel('wide', [
        el('div', { class: 'row between' }, [el('h2', { text: 'RIDERS' }), el('button', { class: 'btn', text: '← menu', onClick: () => this.title() })]),
        el('div', { class: 'grid riders' }, RIDER_ORDER.map((id) => {
          const r = RIDERS[id];
          const unlocked = g.riderUnlocked(id);
          return el('button', {
            class: `rider-card${g.riderDef.id === id ? ' active' : ''}${unlocked ? '' : ' locked'}`,
            style: `--a:${r.color}`,
            onClick: () => { if (unlocked) { g.setRider(id); this.riders(); } },
          }, [
            el('span', { class: 'n', text: r.name }),
            el('span', { class: 't', text: r.tagline }),
            el('span', { class: 'd', text: r.description }),
            el('span', { class: 'stats' }, [
              this.bar('gravity', r.gravityScale / 1.3),
              this.bar('grip', r.frictionScale),
              this.bar('toughness', r.enduranceScale / 2),
            ]),
            unlocked ? null : el('span', { class: 'lock', text: `Unlock: complete ${r.unlock}` }),
          ]);
        })),
      ]),
    );
  }

  private bar(label: string, frac: number): HTMLElement {
    return el('span', { class: 'bar' }, [el('span', { class: 'bl', text: label }), el('span', { class: 'bt' }, [el('span', { class: 'bf', style: `width:${Math.min(100, Math.max(4, frac * 100))}%` })])]);
  }

  // ------------------------------------------------------------------ co-op

  coop(): void {
    const g = this.game;
    const unlockedLevels = LEVELS.filter((l) => g.levelUnlocked(l) && l.mode !== 'destruction');
    this.mount(
      'coop',
      this.panel('wide', [
        el('div', { class: 'row between' }, [el('h2', { text: 'CO-OP' }), el('button', { class: 'btn', text: '← menu', onClick: () => this.title() })]),
        el('p', { class: 'brief', text: 'Two players share one screen and one rider. Player 1 draws in cyan, player 2 in magenta, each with their own ink budget. Press Tab (or the P1/P2 button) to hand over the pen. Finish the level together.' }),
        el('div', { class: 'row wrap' }, [
          el('button', { class: 'btn primary', text: 'FREE CANVAS (120 m each)', onClick: () => { g.startCoopFree('rooftops', 120); this.hide(); this.onEnterGame(); } }),
        ]),
        el('h3', { text: 'Campaign levels' }),
        el('div', { class: 'level-list' }, unlockedLevels.map((lv) => el('button', { class: 'level-card', onClick: () => this.intro(lv, true) }, [
          el('span', { class: 'n', text: lv.name }),
          el('span', { class: 'm', text: `${lv.mode.toUpperCase()} · ${Math.round(lv.budget * 0.6)} m each` }),
          el('span', { class: 't', text: lv.tagline }),
        ]))),
      ]),
    );
    void this.pendingCoop;
  }

  // ------------------------------------------------------------------ daily

  daily(): void {
    const g = this.game;
    const key = todayKey();
    const rec = g.progress.data.daily[key];
    this.mount(
      'daily',
      this.panel('narrow', [
        el('div', { class: 'eyebrow', text: 'DAILY CHALLENGE' }),
        el('h2', { text: key }),
        el('p', { class: 'brief', text: 'Everyone gets the same terrain, flags, finish and ink budget today. Three boards: fastest time, least ink, highest trick score. Records are stored on this device.' }),
        el('div', { class: 'stat-grid' }, [
          this.stat('BEST TIME', rec?.time != null ? fmtTime(rec.time) : '--'),
          this.stat('LEAST INK', rec?.ink != null ? `${rec.ink.toFixed(1)} m` : '--'),
          this.stat('TRICK SCORE', `${rec?.trick ?? 0}`),
        ]),
        el('div', { class: 'row end' }, [
          el('button', { class: 'btn', text: '← menu', onClick: () => this.title() }),
          el('button', { class: 'btn primary', text: 'PLAY TODAY', onClick: () => { g.startDaily(); this.hide(); this.onEnterGame(); } }),
        ]),
      ]),
    );
  }

  // ------------------------------------------------------------------ library

  library(filter: 'all' | 'mine' | 'builtin' = 'all'): void {
    const g = this.game;
    const items = g.store.list().filter((t) => (filter === 'all' ? true : filter === 'mine' ? !t.builtin : t.builtin));
    const importBox = el('textarea', { class: 'code', placeholder: 'Paste a share code (CYR1.…) to import a track' }) as HTMLTextAreaElement;
    const cards = el('div', { class: 'track-grid' }, items.map((t) => this.trackCard(t, () => this.library(filter))));
    this.mount(
      'library',
      this.panel('wide', [
        el('div', { class: 'row between' }, [el('h2', { text: 'TRACK LIBRARY' }), el('button', { class: 'btn', text: '← menu', onClick: () => this.title() })]),
        el('div', { class: 'row wrap' }, [
          ...(['all', 'mine', 'builtin'] as const).map((f) => el('button', { class: `btn small${filter === f ? ' active' : ''}`, text: f.toUpperCase(), onClick: () => this.library(f) })),
          el('span', { class: 'muted', text: 'Tracks live in this browser. Share codes move them between players; a server store can replace the local one.' }),
        ]),
        cards.childElementCount ? cards : el('p', { class: 'muted', text: 'Nothing here yet. Build something in Free Ride and publish it.' }),
        el('h3', { text: 'Import' }),
        el('div', { class: 'row' }, [
          importBox,
          el('button', {
            class: 'btn',
            text: 'IMPORT',
            onClick: () => {
              const decoded = decodeShareCode(importBox.value);
              if (!decoded) {
                alert('That code did not decode. Codes start with CYR1.');
                return;
              }
              const env = ENVIRONMENTS[decoded.environment] ?? ENVIRONMENTS.mountain;
              const track = Game_trackForThumb(decoded.track);
              g.store.publish({ ...decoded, thumbnail: Renderer.thumbnail(track, 320, 180, env) });
              this.library('mine');
            },
          }),
        ]),
      ]),
    );
  }

  private trackCard(t: PublishedTrack, refresh: () => void): HTMLElement {
    const g = this.game;
    const env = ENVIRONMENTS[t.environment] ?? ENVIRONMENTS.mountain;
    const thumb = t.thumbnail || Renderer.thumbnail(Game_trackForThumb(t.track), 320, 180, env);
    return el('div', { class: 'track-card', style: `--a:${env.visuals.accent}` }, [
      el('img', { class: 'thumb' }),
      el('div', { class: 'body' }, [
        el('div', { class: 'row between' }, [el('span', { class: 'n', text: t.title }), el('span', { class: 'diff', text: '★'.repeat(t.difficulty) + '☆'.repeat(5 - t.difficulty) })]),
        el('div', { class: 'muted small', text: `by ${t.author} · ${env.name} · ${t.budget === null ? 'free ride' : `${t.budget} m ink`}` }),
        t.description ? el('div', { class: 'desc', text: t.description }) : null,
        el('div', { class: 'row wrap tags' }, t.tags.map((tag) => el('span', { class: 'pill', text: `#${tag}` }))),
        el('div', { class: 'muted small', text: `best ${fmtTime(t.records.bestFrames)} · ${fmtInk(t.records.bestInk)} · ${t.records.bestTrick} pts · ${t.plays} plays` }),
        el('div', { class: 'row wrap' }, [
          el('button', { class: 'btn primary small', text: 'PLAY', onClick: () => { g.playLibrary(t); this.hide(); this.onEnterGame(); } }),
          el('button', { class: `btn small${t.liked ? ' active' : ''}`, text: `♥ ${t.likes}`, onClick: () => { t.liked = !t.liked; t.likes += t.liked ? 1 : -1; g.store.update(t); refresh(); } }),
          el('button', {
            class: 'btn small',
            text: 'SHARE CODE',
            onClick: async () => {
              const code = encodeShareCode(t);
              try {
                await navigator.clipboard.writeText(code);
                alert('Share code copied to the clipboard.');
              } catch {
                prompt('Copy this share code:', code);
              }
            },
          }),
          t.builtin ? null : el('button', { class: 'btn small ghost', text: 'DELETE', onClick: () => { if (confirm(`Delete "${t.title}"?`)) { g.store.remove(t.id); refresh(); } } }),
        ]),
      ]),
    ]).tap((card) => {
      (card.querySelector('img.thumb') as HTMLImageElement).src = thumb;
    });
  }

  publishForm(): void {
    const g = this.game;
    const title = el('input', { class: 'input', placeholder: 'Track title', value: 'Untitled run' }) as HTMLInputElement;
    const author = el('input', { class: 'input', placeholder: 'Your name', value: 'Anonymous' }) as HTMLInputElement;
    const desc = el('textarea', { class: 'code', placeholder: 'Description' }) as HTMLTextAreaElement;
    const tags = el('input', { class: 'input', placeholder: 'tags, comma separated (speed, stunt, puzzle)' }) as HTMLInputElement;
    const budget = el('input', { class: 'input', placeholder: 'Ink budget in metres (empty = free ride)', type: 'number' }) as HTMLInputElement;
    const diff = el('input', { class: 'input', type: 'number', value: '2' }) as HTMLInputElement;
    diff.min = '1';
    diff.max = '5';
    this.mount(
      'library',
      this.panel('narrow', [
        el('h2', { text: 'PUBLISH TRACK' }),
        el('p', { class: 'muted', text: 'Your current free-ride drawing, start point and finish zone become a community track. Set an ink budget to turn it into a puzzle for others.' }),
        el('label', { text: 'Title' }), title,
        el('label', { text: 'Author' }), author,
        el('label', { text: 'Description' }), desc,
        el('label', { text: 'Tags' }), tags,
        el('label', { text: 'Difficulty (1-5)' }), diff,
        el('label', { text: 'Ink budget' }), budget,
        el('div', { class: 'row end' }, [
          el('button', { class: 'btn', text: 'cancel', onClick: () => { this.hide(); } }),
          el('button', {
            class: 'btn primary',
            text: 'PUBLISH',
            onClick: () => {
              const track = g.track.clone();
              const pub = g.store.publish({
                title: title.value.trim() || 'Untitled run',
                author: author.value.trim() || 'Anonymous',
                description: desc.value.trim(),
                tags: tags.value.split(',').map((s) => s.trim().toLowerCase()).filter(Boolean),
                difficulty: Math.max(1, Math.min(5, Number(diff.value) || 2)),
                environment: g.environment.id,
                budget: budget.value ? Math.max(1, Number(budget.value)) : null,
                thumbnail: Renderer.thumbnail(track, 320, 180, g.environment),
                track: track.toJSON(),
              });
              alert(`Published "${pub.title}". Share code copied? Use the library card to copy it.`);
              this.library('mine');
            },
          }),
        ]),
      ]),
    );
  }
}

// Small helper so the library can render thumbnails for imported JSON without touching Game internals.
import { Track } from '../game/track';
import type { TrackJSON } from '../game/track';
function Game_trackForThumb(json: TrackJSON): Track {
  return Track.fromJSON(json);
}

declare global {
  interface HTMLElement {
    tap(fn: (node: HTMLElement) => void): HTMLElement;
  }
}
HTMLElement.prototype.tap = function (fn: (node: HTMLElement) => void): HTMLElement {
  fn(this);
  return this;
};
