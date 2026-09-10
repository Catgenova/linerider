import { Rng, hashString } from '../core/rng';
import { Camera } from '../editor/camera';
import { Editor, type ToolId } from '../editor/editor';
import { FRAME_MS, PX_PER_METER, SIM_FPS } from '../physics/constants';
import { MATERIAL_ORDER, type MaterialId } from '../physics/materials';
import { Effects } from '../render/effects';
import { GameAudio } from './audio';
import { Renderer, type Marker, type Scene } from '../render/renderer';
import { ENVIRONMENTS, ENVIRONMENT_ORDER, type Environment, type EnvironmentId } from './environments';
import { evaluateLevel, type LevelDef, type Medal, type ObjectiveResult, type RunSummary } from './level';
import { LEVELS, REGIONS, levelById, nextLevel } from './levels';
import { trackFromLevel } from './loadLevel';
import { LocalTrackStore, type PublishedTrack } from './library';
import { Progress } from './progress';
import { RIDERS, RIDER_ORDER, type RiderDef, type RiderId } from './riders';
import { Run } from './run';
import { Track } from './track';
import { buildEntity } from './entities';
import { Prop } from '../physics/prop';
import { World } from '../physics/world';
import type { ObjectKind } from './objectKinds';
import { ArcadeDirector } from '../modes/arcade';
import { AttractDirector } from '../modes/attract';
import { generateDaily, dailySeedFor, todayKey } from '../modes/daily';
import { BUILTIN_TRACKS } from '../modes/builtin';

export type GameMode = 'campaign' | 'free' | 'arcade' | 'daily' | 'coop' | 'library' | 'attract';
export type PlayState = 'edit' | 'play' | 'pause';

export interface ResultsInfo {
  mode: GameMode;
  level: LevelDef | null;
  summary: RunSummary;
  results: ObjectiveResult[];
  medal: Medal;
  complete: boolean;
  improvements: { newMedal: boolean; newTime: boolean; newInk: boolean; newTrick: boolean } | null;
  arcadeBest?: number;
  next: LevelDef | null;
}

export interface GameCallbacks {
  onResults: (info: ResultsInfo) => void;
  onStateChange: () => void;
  onMessage: (text: string, color: string, big?: boolean) => void;
}

/** Top-level game state: current mode, level, track, editor and playback. */
export class Game {
  readonly camera = new Camera();
  readonly renderer: Renderer;
  readonly effects = new Effects();
  readonly progress = new Progress();
  readonly audio: GameAudio;
  readonly store = new LocalTrackStore(BUILTIN_TRACKS);
  track = new Track();
  editor: Editor;
  run: Run | null = null;
  ghostRun: Run | null = null;
  ghostEnabled = true;
  level: LevelDef | null = null;
  mode: GameMode = 'free';
  environment: Environment = ENVIRONMENTS.mountain;
  riderDef: RiderDef = RIDERS.bosh;
  playState: PlayState = 'edit';
  speed = 1;
  flagFrame = -1;
  time = 0;
  following = true;
  coop = { active: false, player: 1 as 1 | 2, budgets: [100, 100] as [number, number] };
  arcade: ArcadeDirector | null = null;
  attract: AttractDirector | null = null;
  private attractIndex = 0;
  dailyKey: string | null = null;
  libraryTrack: PublishedTrack | null = null;
  callbacks: GameCallbacks = { onResults: () => {}, onStateChange: () => {}, onMessage: () => {} };
  private accumulator = 0;
  private lastNow = 0;
  private resultsPending = false;
  private resultsTimer = 0;
  private readonly markers: Marker[] = [];
  private finishPlacement: 'none' | 'start' | 'finish' = 'none';
  /** Animated stand-in for placed objects while editing (no rider). */
  private previewWorld: World | null = null;
  private previewRevision = -1;

  constructor(public readonly canvas: HTMLCanvasElement) {
    this.renderer = new Renderer(canvas);
    this.editor = new Editor(this.track, this.camera);
    this.bindEditorEvents();
    this.renderer.resize(this.camera);
    const savedRider = this.progress.data.settings.rider as RiderId;
    if (RIDERS[savedRider]) this.riderDef = RIDERS[savedRider];
    this.audio = new GameAudio(this.progress.data.settings.music);
  }

  toggleMusic(): void {
    const v = !this.audio.enabled;
    this.audio.setEnabled(v);
    this.progress.data.settings.music = v;
    this.progress.save();
    this.callbacks.onStateChange();
  }

  private bindEditorEvents(): void {
    this.editor.events = {
      onInkExhausted: () => this.callbacks.onMessage('OUT OF INK', '#ff3d7f'),
      onEdit: () => this.callbacks.onStateChange(),
    };
    this.track.onChange = null;
    this.previewRevision = -1;
  }

  /** Objects cannot be hot-swapped into a running world, so placing one rewinds the run. */
  private watchObjectChanges(): void {
    const track = this.track;
    const prev = track.onChange;
    track.onChange = (c) => {
      prev?.(c);
      if (c.type === 'objects' && this.run) this.stop();
    };
  }

  private refreshPreview(): void {
    const track = this.track;
    if (this.previewRevision === track.revision && this.previewWorld) return;
    this.previewRevision = track.revision;
    if (track.objects.size === 0 && track.props.size === 0) {
      this.previewWorld = null;
      return;
    }
    const world = new World();
    for (const o of track.objects.values()) {
      const e = buildEntity(o.def, world);
      if (e) world.addEntity(e);
    }
    for (const o of track.props.values()) world.addProp(new Prop({ ...o.def, id: world.nextDynamicId++ }));
    this.previewWorld = world;
  }

  setObjectKind(kind: ObjectKind): void {
    this.editor.objectKind = kind;
    this.editor.tool = 'object';
    this.finishPlacement = 'none';
    this.callbacks.onStateChange();
  }

  // ---------------------------------------------------------------- mode setup

  private resetTrack(track: Track): void {
    this.stop();
    if (this.mode === 'attract') {
      // The demo borrows riders; give the player theirs back.
      const saved = this.progress.data.settings.rider as RiderId;
      if (RIDERS[saved]) this.riderDef = RIDERS[saved];
    }
    this.attract = null;
    this.track = track;
    this.editor = new Editor(track, this.camera);
    this.bindEditorEvents();
    this.flagFrame = -1;
    this.effects.clear();
    this.ghostRun = null;
    this.libraryTrack = null;
    this.dailyKey = null;
    this.arcade = null;
    this.coop.active = false;
    this.finishPlacement = 'none';
  }

  loadLevel(level: LevelDef, riderId?: RiderId, coop = false): void {
    this.resetTrack(trackFromLevel(level));
    this.level = level;
    this.mode = coop ? 'coop' : 'campaign';
    this.environment = ENVIRONMENTS[level.environment];
    if (level.rider) this.riderDef = RIDERS[level.rider];
    else if (riderId) this.setRider(riderId);
    this.editor.constraints = {
      budget: coop ? level.budget * 0.6 : level.budget,
      materials: level.materials,
      canEraseLevel: false,
      player: coop ? 1 : 0,
      locked: false,
      canPlaceObjects: false,
    };
    if (coop) {
      this.coop = { active: true, player: 1, budgets: [level.budget * 0.6, level.budget * 0.6] };
    }
    this.editor.material = level.materials[0];
    this.editor.tool = 'pencil';
    this.camera.zoom = level.zoom ?? 2;
    this.focusLevelCamera();
    this.loadGhost(level.id);
    this.progress.recordAttempt(level.id);
    this.callbacks.onStateChange();
  }

  /** Centre on the level's focus point while keeping the start clear of the toolbar. */
  focusLevelCamera(): void {
    const level = this.level;
    if (!level) return;
    const focus = level.focus ?? level.start;
    const maxCx = level.start.x + (this.camera.width / 2 - 290) / this.camera.zoom;
    this.camera.snapTo(Math.min(focus.x, maxCx), focus.y);
  }

  private loadGhost(levelId: string): void {
    const rec = this.progress.level(levelId);
    if (!rec.ghost) return;
    try {
      const ghostTrack = Track.fromJSON(rec.ghost.track);
      const rider = RIDERS[rec.ghost.rider as RiderId] ?? RIDERS.bosh;
      this.ghostRun = new Run({ track: ghostTrack, level: this.level, riderDef: rider, environment: this.environment });
    } catch {
      this.ghostRun = null;
    }
  }

  startFree(environment: EnvironmentId = 'mountain'): void {
    const track = new Track();
    track.start = { x: 0, y: 0 };
    this.resetTrack(track);
    this.level = null;
    this.mode = 'free';
    this.environment = ENVIRONMENTS[environment];
    this.editor.constraints = { budget: null, materials: [...MATERIAL_ORDER], canEraseLevel: true, player: 0, locked: false, canPlaceObjects: true };
    this.camera.zoom = 2.5;
    this.camera.snapTo(60, 20);
    this.callbacks.onStateChange();
  }

  startCoopFree(environment: EnvironmentId = 'rooftops', budget = 120): void {
    this.startFree(environment);
    this.mode = 'coop';
    this.coop = { active: true, player: 1, budgets: [budget, budget] };
    this.track.finish = { x: 700, y: 120, w: 40, h: 70 };
    this.track.addLine({ x1: -30, y1: 10, x2: 60, y2: 20, layer: 'level' });
    this.track.addLine({ x1: 700, y1: 190, x2: 760, y2: 190, layer: 'level' });
    this.editor.constraints = { budget, materials: [...MATERIAL_ORDER], canEraseLevel: false, player: 1, locked: false, canPlaceObjects: false };
    this.callbacks.onStateChange();
  }

  startArcade(): void {
    const track = new Track();
    track.start = { x: 0, y: 0 };
    this.resetTrack(track);
    this.level = null;
    this.mode = 'arcade';
    this.environment = ENVIRONMENTS.rooftops;
    this.arcade = new ArcadeDirector(track, new Rng(Date.now() >>> 0));
    this.editor.constraints = { budget: this.arcade.budget, materials: ['normal', 'accel', 'spring'], canEraseLevel: false, player: 0, locked: false, canPlaceObjects: false };
    this.editor.tool = 'pencil';
    this.camera.zoom = 2;
    this.camera.snapTo(120, 0);
    this.play();
    this.callbacks.onStateChange();
  }

  /** Menu backdrop: an endless demo ride that cycles environments and riders. */
  startAttract(): void {
    const idx = this.attractIndex++;
    const track = new Track();
    this.resetTrack(track);
    this.level = null;
    this.mode = 'attract';
    this.environment = ENVIRONMENTS[ENVIRONMENT_ORDER[idx % ENVIRONMENT_ORDER.length]];
    this.riderDef = RIDERS[RIDER_ORDER[idx % RIDER_ORDER.length]];
    this.attract = new AttractDirector(track, new Rng((Date.now() + idx * 7919) >>> 0));
    this.editor.constraints = { budget: null, materials: ['normal'], canEraseLevel: false, player: 0, locked: true, canPlaceObjects: false };
    this.camera.zoom = 2.3;
    this.camera.snapTo(120, 30);
    this.effects.clear();
    this.play();
    this.effects.flash = 0.25;
    this.effects.flashColor = this.environment.visuals.accent;
    this.callbacks.onStateChange();
  }

  startDaily(): void {
    const key = todayKey();
    const level = generateDaily(dailySeedFor(key), key);
    this.loadLevel(level);
    this.mode = 'daily';
    this.dailyKey = key;
    this.ghostRun = null;
    this.callbacks.onStateChange();
  }

  playLibrary(pub: PublishedTrack): void {
    const track = Track.fromJSON(pub.track);
    this.resetTrack(track);
    this.level = null;
    this.mode = 'library';
    this.libraryTrack = pub;
    this.environment = ENVIRONMENTS[pub.environment] ?? ENVIRONMENTS.mountain;
    this.editor.constraints = {
      budget: pub.budget,
      materials: [...MATERIAL_ORDER],
      canEraseLevel: pub.budget === null,
      player: 0,
      locked: false,
      canPlaceObjects: pub.budget === null,
    };
    // Published lines count as level geometry when there's a budget (a puzzle), else all editable.
    for (const l of track.lines.values()) l.layer = pub.budget === null ? 'player' : 'level';
    this.camera.zoom = 2;
    this.camera.snapTo(track.start.x + 80, track.start.y);
    pub.plays++;
    this.store.update(pub);
    this.callbacks.onStateChange();
  }

  setRider(id: RiderId): void {
    this.riderDef = RIDERS[id];
    this.progress.data.settings.rider = id;
    this.progress.save();
    if (this.run) this.restart(true);
    this.callbacks.onStateChange();
  }

  riderUnlocked(id: RiderId): boolean {
    const def = RIDERS[id];
    return def.unlock === null || this.progress.isComplete(def.unlock);
  }

  // ---------------------------------------------------------------- playback

  play(): void {
    if (this.playState === 'play') return;
    if (!this.run) {
      this.startRun(this.flagFrame > 0 ? this.flagFrame : 0);
    }
    this.playState = 'play';
    this.following = true;
    this.callbacks.onStateChange();
  }

  private startRun(skipFrames: number): void {
    this.run?.dispose();
    this.run = new Run({
      track: this.track,
      level: this.level,
      riderDef: this.riderDef,
      environment: this.environment,
      skipFrames,
      endless: this.mode === 'arcade',
    });
    this.watchObjectChanges();
    if (this.ghostRun) {
      const g = this.ghostRun;
      this.ghostRun = new Run({ track: g.track, level: this.level, riderDef: g.riderDef, environment: this.environment, skipFrames });
    }
    this.effects.clear();
    this.resultsPending = false;
    this.resultsTimer = 0;
    this.accumulator = 0;
  }

  pause(): void {
    if (this.playState !== 'play') return;
    this.playState = 'pause';
    this.callbacks.onStateChange();
  }

  togglePlay(): void {
    if (this.playState === 'play') this.pause();
    else this.play();
  }

  /** Stop playback and return to editing at frame zero. */
  stop(): void {
    this.run?.dispose();
    this.run = null;
    this.playState = 'edit';
    this.resultsPending = false;
    this.effects.clear();
    if (this.ghostRun) {
      const g = this.ghostRun;
      this.ghostRun = new Run({ track: g.track, level: this.level, riderDef: g.riderDef, environment: this.environment });
    }
    this.callbacks.onStateChange();
  }

  /** Restart the run. With keepLines=false the player's drawing is wiped. */
  restart(keepLines: boolean): void {
    this.stop();
    if (!keepLines) {
      this.editor.clearPlayerLines();
      this.editor.clearHistory();
      this.flagFrame = -1;
    }
    if (this.mode === 'arcade') {
      this.startArcade();
      return;
    }
    if (this.mode === 'attract') {
      this.startAttract();
      return;
    }
    this.play();
  }

  setFlag(): void {
    if (!this.run) return;
    this.flagFrame = this.run.frame;
    this.callbacks.onMessage(`FLAG SET AT ${(this.flagFrame / SIM_FPS).toFixed(1)}s`, '#39f6ff');
    this.callbacks.onStateChange();
  }

  clearFlag(): void {
    this.flagFrame = -1;
    this.callbacks.onStateChange();
  }

  setSpeed(s: number): void {
    this.speed = s;
    this.callbacks.onStateChange();
  }

  setTool(tool: ToolId): void {
    this.editor.tool = tool;
    this.finishPlacement = 'none';
    this.callbacks.onStateChange();
  }

  setMaterial(m: MaterialId): void {
    if (!this.editor.canUseMaterial(m)) return;
    this.editor.material = m;
    this.callbacks.onStateChange();
  }

  beginPlacement(kind: 'start' | 'finish'): void {
    this.finishPlacement = kind;
    this.callbacks.onMessage(kind === 'start' ? 'CLICK TO PLACE START' : 'CLICK TO PLACE FINISH', '#4dff9d');
  }

  /** Returns true if the click was consumed by a placement action. */
  handlePlacementClick(wx: number, wy: number): boolean {
    if (this.finishPlacement === 'none') return false;
    if (this.finishPlacement === 'start') {
      this.track.start = { x: wx, y: wy };
      this.stop();
    } else {
      this.track.finish = { x: wx - 20, y: wy - 60, w: 40, h: 70 };
    }
    this.track.revision++;
    this.finishPlacement = 'none';
    this.callbacks.onStateChange();
    return true;
  }

  switchCoopPlayer(): void {
    if (!this.coop.active) return;
    this.coop.player = this.coop.player === 1 ? 2 : 1;
    this.editor.constraints.player = this.coop.player;
    this.editor.constraints.budget = this.coop.budgets[this.coop.player - 1];
    this.callbacks.onMessage(`PLAYER ${this.coop.player} DRAWING`, this.coop.player === 1 ? '#39f6ff' : '#ff2bd6');
    this.callbacks.onStateChange();
  }

  toggleGhost(): void {
    this.ghostEnabled = !this.ghostEnabled;
    this.callbacks.onStateChange();
  }

  // ---------------------------------------------------------------- loop

  tick(now: number): void {
    if (!this.lastNow) this.lastNow = now;
    let dt = (now - this.lastNow) / 1000;
    this.lastNow = now;
    if (dt > 0.25) dt = 0.25;
    this.time += dt;
    this.renderer.resize(this.camera);

    if (this.playState === 'play' && this.run) {
      this.accumulator += dt * 1000 * this.speed;
      let steps = 0;
      while (this.accumulator >= FRAME_MS && steps < 12) {
        this.simStep();
        this.accumulator -= FRAME_MS;
        steps++;
      }
      if (steps === 12) this.accumulator = 0;
    }
    if (!this.run) {
      this.refreshPreview();
      const pw = this.previewWorld;
      if (pw) {
        pw.frame++;
        for (const e of pw.entities) if (e.active) e.update(pw);
      }
    }
    this.audio.riding = this.playState === 'play' && !!this.run && !this.run.done && this.mode !== 'attract';
    this.effects.update((dt * 1000 * (this.playState === 'play' ? this.speed : 1)) / FRAME_MS);
    this.updateCamera(dt);
    this.camera.update(dt);
    this.renderer.render(this.buildScene());
  }

  private simStep(): void {
    const run = this.run!;
    if (this.attract) {
      // Demo rides never show results: crash, fall or time out and the next ride begins.
      const crashed = run.rider.deathFrame >= 0 && run.frame - run.rider.deathFrame > 70;
      if (run.done || crashed || run.frame > 40 * 50) {
        this.startAttract();
        return;
      }
      run.step();
      this.attract.update(run);
      this.consumeRunEvents(run);
      this.spawnContactSparks(run);
      return;
    }
    if (run.done) {
      // Let the crash play out a little before results.
      if (!this.resultsPending) return;
    }
    run.step();
    if (this.ghostRun && this.ghostEnabled) this.ghostRun.step();
    if (this.arcade) this.arcade.update(run, this.editor);
    this.consumeRunEvents(run);
    this.spawnContactSparks(run);
    if (run.done && !this.resultsPending) {
      this.resultsPending = true;
      this.resultsTimer = run.finished ? 30 : 45;
    }
    if (this.resultsPending) {
      this.resultsTimer--;
      if (this.resultsTimer <= 0) this.finishRun();
    }
  }

  private consumeRunEvents(run: Run): void {
    const fx = this.effects;
    for (const ev of run.events) {
      switch (ev.type) {
        case 'trick':
          fx.popup(ev.x, ev.y, ev.text, ev.color, ev.text.startsWith('BAIL') ? 1 : 1.1);
          if (ev.points) this.audio.trick(ev.points);
          break;
        case 'flag':
          this.audio.pickup();
          fx.popup(ev.x, ev.y, 'FLAG', '#ffe93a', 1.2);
          fx.ring(ev.x, ev.y + 16, 30, '#ffe93a');
          fx.spark(ev.x, ev.y + 16, 20, '#ffe93a', 2.5);
          break;
        case 'rescue':
          this.audio.pickup();
          fx.popup(ev.x, ev.y, 'RESCUED', '#ff7ae8', 1.2);
          fx.ring(ev.x, ev.y + 16, 30, '#ff7ae8');
          break;
        case 'finish':
          fx.popup(ev.x, ev.y, 'FINISH', '#4dff9d', 1.6);
          fx.ring(ev.x, ev.y + 16, 60, '#4dff9d', 30);
          fx.spark(ev.x, ev.y + 16, 60, '#4dff9d', 4);
          fx.flash = 0.35;
          fx.flashColor = '#4dff9d';
          this.audio.finish();
          this.callbacks.onMessage('FINISH', '#4dff9d', true);
          break;
        case 'death':
          fx.popup(ev.x, ev.y, 'WIPEOUT', '#ff4d4d', 1.4);
          fx.spark(ev.x, ev.y + 12, 40, '#ff4d4d', 3);
          fx.shake = 8;
          this.audio.crash();
          this.callbacks.onMessage('WIPEOUT', '#ff4d4d', true);
          break;
        case 'message':
          fx.popup(ev.x, ev.y, ev.text, ev.color, 1);
          break;
      }
    }
    run.events.length = 0;
    for (const ev of run.world.events) {
      if (ev.type === 'crumble') {
        fx.shards(ev.line.x1, ev.line.y1, ev.line.x2, ev.line.y2, '#ff7a45');
      } else if (ev.type === 'break') {
        fx.shards(ev.line.x1, ev.line.y1, ev.line.x2, ev.line.y2, '#ffb347');
        fx.popup(ev.x, ev.y - 10, 'SMASH', '#ffb347', 1.2);
        fx.shake = Math.max(fx.shake, 5);
      } else if (ev.type === 'explode') {
        this.audio.explosion();
        fx.explosion(ev.x, ev.y, ev.radius);
        fx.popup(ev.x, ev.y - 30, 'BOOM', '#ffb347', 1.5);
      } else if (ev.type === 'impact' && ev.speed > 4) {
        fx.spark(ev.x, ev.y, 6, '#ffffff', 1.5);
      }
    }
  }

  private spawnContactSparks(run: Run): void {
    const rider = run.rider;
    if (run.frame % 2 !== 0) return;
    for (const i of rider.model.vehicle) {
      const p = rider.points[i];
      if (!p.contact) continue;
      const s = Math.hypot(p.x - p.px, p.y - p.py);
      if (s > 4 && Math.random() < 0.5) {
        this.effects.spark(p.x, p.y, 1, p.contact.material.color, s * 0.25, 1.2, Math.atan2(-(p.y - p.py), -(p.x - p.px)), 0.05);
      }
      if (p.contact.material.accel && Math.random() < 0.6) {
        this.effects.spark(p.x, p.y, 1, p.contact.material.color, 2, 0.8, Math.atan2(-p.contact.uy, -p.contact.ux), 0);
      }
    }
  }

  private updateCamera(dt: number): void {
    void dt;
    if (this.run && this.playState !== 'edit' && this.following) {
      const rider = this.run.rider;
      const c = rider.center();
      const v = rider.velocity();
      const lead = this.mode === 'arcade' ? 90 / this.camera.zoom : this.mode === 'attract' ? 110 / this.camera.zoom : 12;
      this.camera.follow(c.x + v.x * 6 + lead, c.y + v.y * 3);
    }
  }

  private finishRun(): void {
    const run = this.run!;
    const summary = run.summary();
    this.resultsPending = false;
    this.playState = 'pause';
    const level = this.level;
    let results: ObjectiveResult[] = [];
    let medal: Medal = 'none';
    let complete = summary.finished;
    let improvements: ResultsInfo['improvements'] = null;
    let arcadeBest: number | undefined;
    if (level) {
      const ev = evaluateLevel(level, summary);
      results = ev.results;
      medal = ev.medal;
      complete = ev.complete;
      if (this.mode === 'campaign' && complete) {
        const ghost = summary.finished ? { track: this.track.toJSON(), rider: this.riderDef.id, frames: summary.frames } : null;
        improvements = this.progress.recordResult(level.id, medal, summary.finished ? summary.frames : null, summary.inkUsed, summary.trickScore, ghost);
      } else if (this.mode === 'daily' && this.dailyKey) {
        const rec = this.progress.data.daily[this.dailyKey] ?? { time: null, ink: null, trick: 0 };
        improvements = { newMedal: false, newTime: false, newInk: false, newTrick: false };
        if (summary.finished && (rec.time === null || summary.frames < rec.time)) {
          rec.time = summary.frames;
          improvements.newTime = true;
        }
        if (summary.finished && (rec.ink === null || summary.inkUsed < rec.ink)) {
          rec.ink = summary.inkUsed;
          improvements.newInk = true;
        }
        if (summary.trickScore > rec.trick) {
          rec.trick = summary.trickScore;
          improvements.newTrick = true;
        }
        this.progress.data.daily[this.dailyKey] = rec;
        this.progress.save();
      }
    } else if (this.mode === 'arcade') {
      arcadeBest = this.progress.data.arcadeBest;
      if (summary.distance > arcadeBest) {
        this.progress.data.arcadeBest = Math.round(summary.distance);
        this.progress.save();
        improvements = { newMedal: false, newTime: false, newInk: false, newTrick: true };
      }
    } else if (this.mode === 'library' && this.libraryTrack && summary.finished) {
      const rec = this.libraryTrack.records;
      improvements = { newMedal: false, newTime: false, newInk: false, newTrick: false };
      if (rec.bestFrames === null || summary.frames < rec.bestFrames) {
        rec.bestFrames = summary.frames;
        improvements.newTime = true;
      }
      if (rec.bestInk === null || summary.inkUsed < rec.bestInk) {
        rec.bestInk = summary.inkUsed;
        improvements.newInk = true;
      }
      if (summary.trickScore > rec.bestTrick) {
        rec.bestTrick = summary.trickScore;
        improvements.newTrick = true;
      }
      this.store.update(this.libraryTrack);
    }
    this.callbacks.onResults({
      mode: this.mode,
      level,
      summary,
      results,
      medal,
      complete,
      improvements,
      arcadeBest,
      next: level && this.mode === 'campaign' ? nextLevel(level.id) : null,
    });
  }

  // ---------------------------------------------------------------- scene

  private buildScene(): Scene {
    const level = this.level;
    const markers = this.markers;
    markers.length = 0;
    if (level) {
      (level.flags ?? []).forEach((f, i) => markers.push({ x: f.x, y: f.y, kind: 'flag', done: this.run?.flags[i] ?? false }));
      (level.rescues ?? []).forEach((r, i) => markers.push({ x: r.x, y: r.y, kind: 'rescue', done: this.run?.rescues[i] ?? false }));
    }
    const styles = [{ color: this.riderDef.color, glow: this.riderDef.glow }];
    return {
      camera: this.camera,
      track: this.track,
      world: this.run?.world ?? null,
      previewWorld: this.run ? null : this.previewWorld,
      ghostWorld: this.ghostEnabled && this.ghostRun && this.playState !== 'edit' ? this.ghostRun.world : null,
      environment: this.environment,
      editor: this.editor,
      riderStyles: styles,
      effects: this.effects,
      time: this.time,
      start: this.track.start,
      finish: level?.finish ?? this.track.finish ?? null,
      markers,
      focusRider: 0,
      focusPoint: this.editor.cursorWorld,
      showEditorOverlay: this.mode !== 'attract',
    };
  }

  // ---------------------------------------------------------------- helpers

  get inkBudget(): number | null {
    return this.editor.constraints.budget;
  }

  get inkUsed(): number {
    return this.editor.inkUsed;
  }

  /** Human-readable ink figure. */
  inkLabel(): string {
    const b = this.inkBudget;
    const used = this.inkUsed;
    if (b === null) return `${used.toFixed(1)} m`;
    return `${Math.max(0, b - used).toFixed(1)} / ${b.toFixed(0)} m`;
  }

  metersTravelled(): number {
    if (!this.run) return 0;
    return (this.run.maxX - this.track.start.x) / PX_PER_METER;
  }

  levelsInRegion(region: string): LevelDef[] {
    return LEVELS.filter((l) => l.region === region);
  }

  regionUnlocked(regionIndex: number): boolean {
    if (regionIndex === 0) return true;
    const prev = REGIONS[regionIndex - 1];
    const levels = this.levelsInRegion(prev.id);
    const done = levels.filter((l) => this.progress.isComplete(l.id)).length;
    return done >= Math.min(2, levels.length);
  }

  levelUnlocked(level: LevelDef): boolean {
    const regionIndex = REGIONS.findIndex((r) => r.id === level.region);
    if (!this.regionUnlocked(regionIndex)) return false;
    const levels = this.levelsInRegion(level.region);
    const idx = levels.indexOf(level);
    if (idx <= 0) return true;
    return this.progress.isComplete(levels[idx - 1].id);
  }

  levelByIdSafe(id: string): LevelDef | undefined {
    return levelById(id);
  }

  seedLabel(): string {
    return this.dailyKey ? hashString(this.dailyKey).toString(16) : '';
  }
}
