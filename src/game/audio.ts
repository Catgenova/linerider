/**
 * Procedural soundtrack and effects built on Web Audio: a pulsing bass beat while riding, a soft
 * pad while editing, and short synth cues for tricks, crashes and finishes. Everything is
 * generated, so there are no audio assets to load.
 */
export class GameAudio {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private padGain: GainNode | null = null;
  private beatTimer: number | null = null;
  private nextBeat = 0;
  private beatIndex = 0;
  enabled: boolean;
  riding = false;
  readonly bpm = 96;

  constructor(enabled: boolean) {
    this.enabled = enabled;
  }

  /** Create the context on the first user gesture (browsers require it). */
  unlock(): void {
    if (this.ctx) {
      if (this.ctx.state === 'suspended') void this.ctx.resume();
      return;
    }
    try {
      const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!Ctor) return;
      this.ctx = new Ctor();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.enabled ? 0.5 : 0;
      this.master.connect(this.ctx.destination);
      this.startPad();
      this.startBeatLoop();
    } catch {
      this.ctx = null;
    }
  }

  setEnabled(v: boolean): void {
    this.enabled = v;
    if (this.master && this.ctx) this.master.gain.setTargetAtTime(v ? 0.5 : 0, this.ctx.currentTime, 0.05);
  }

  private startPad(): void {
    const ctx = this.ctx!;
    this.padGain = ctx.createGain();
    this.padGain.gain.value = 0.05;
    const filter = ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = 420;
    filter.Q.value = 2;
    this.padGain.connect(filter).connect(this.master!);
    const freqs = [55, 82.41, 110, 164.81];
    freqs.forEach((f, i) => {
      const osc = ctx.createOscillator();
      osc.type = i % 2 ? 'sawtooth' : 'triangle';
      osc.frequency.value = f;
      osc.detune.value = (i - 1.5) * 6;
      const lfo = ctx.createOscillator();
      lfo.frequency.value = 0.07 + i * 0.03;
      const lfoGain = ctx.createGain();
      lfoGain.gain.value = 4;
      lfo.connect(lfoGain).connect(osc.detune);
      lfo.start();
      osc.connect(this.padGain!);
      osc.start();
    });
    // Slow filter sweep for the pulse feel.
    const sweep = ctx.createOscillator();
    sweep.frequency.value = this.bpm / 60 / 4;
    const sweepGain = ctx.createGain();
    sweepGain.gain.value = 180;
    sweep.connect(sweepGain).connect(filter.frequency);
    sweep.start();
  }

  private startBeatLoop(): void {
    const ctx = this.ctx!;
    this.nextBeat = ctx.currentTime + 0.1;
    const period = 60 / this.bpm / 2; // eighth notes
    const tick = () => {
      if (!this.ctx) return;
      while (this.nextBeat < ctx.currentTime + 0.2) {
        if (this.riding && this.enabled) this.scheduleBeat(this.nextBeat, this.beatIndex);
        this.nextBeat += period;
        this.beatIndex = (this.beatIndex + 1) % 8;
      }
      this.beatTimer = window.setTimeout(tick, 60);
    };
    tick();
  }

  private scheduleBeat(t: number, i: number): void {
    const ctx = this.ctx!;
    if (i % 2 === 0) {
      // Kick with pitch drop.
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.frequency.setValueAtTime(i % 4 === 0 ? 120 : 100, t);
      osc.frequency.exponentialRampToValueAtTime(38, t + 0.16);
      g.gain.setValueAtTime(0.5, t);
      g.gain.exponentialRampToValueAtTime(0.001, t + 0.3);
      osc.connect(g).connect(this.master!);
      osc.start(t);
      osc.stop(t + 0.32);
    } else {
      // Hi-hat from filtered noise.
      const len = Math.floor(ctx.sampleRate * 0.05);
      const buf = ctx.createBuffer(1, len, ctx.sampleRate);
      const data = buf.getChannelData(0);
      for (let k = 0; k < len; k++) data[k] = (Math.random() * 2 - 1) * (1 - k / len);
      const src = ctx.createBufferSource();
      src.buffer = buf;
      const hp = ctx.createBiquadFilter();
      hp.type = 'highpass';
      hp.frequency.value = 6000;
      const g = ctx.createGain();
      g.gain.value = i === 7 ? 0.12 : 0.06;
      src.connect(hp).connect(g).connect(this.master!);
      src.start(t);
    }
    if (i === 0 || i === 6) {
      // Bass pluck on the beat.
      const osc = ctx.createOscillator();
      osc.type = 'square';
      osc.frequency.value = i === 0 ? 55 : 65.41;
      const f = ctx.createBiquadFilter();
      f.type = 'lowpass';
      f.frequency.setValueAtTime(900, t);
      f.frequency.exponentialRampToValueAtTime(120, t + 0.25);
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.18, t);
      g.gain.exponentialRampToValueAtTime(0.001, t + 0.35);
      osc.connect(f).connect(g).connect(this.master!);
      osc.start(t);
      osc.stop(t + 0.36);
    }
  }

  private tone(freq: number, start: number, dur: number, type: OscillatorType, vol: number): void {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.enabled) return;
    const t = ctx.currentTime + start;
    const osc = ctx.createOscillator();
    osc.type = type;
    osc.frequency.value = freq;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    osc.connect(g).connect(this.master);
    osc.start(t);
    osc.stop(t + dur + 0.02);
  }

  trick(points: number): void {
    const n = points >= 500 ? 4 : points >= 150 ? 3 : 2;
    const base = points >= 500 ? 440 : 523.25;
    for (let i = 0; i < n; i++) this.tone(base * Math.pow(2, i / 12 * 4), i * 0.06, 0.18, 'square', 0.08);
  }

  pickup(): void {
    this.tone(880, 0, 0.12, 'triangle', 0.12);
    this.tone(1318.5, 0.08, 0.16, 'triangle', 0.12);
  }

  crash(): void {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.enabled) return;
    const len = Math.floor(ctx.sampleRate * 0.35);
    const buf = ctx.createBuffer(1, len, ctx.sampleRate);
    const data = buf.getChannelData(0);
    for (let k = 0; k < len; k++) data[k] = (Math.random() * 2 - 1) * Math.pow(1 - k / len, 2);
    const src = ctx.createBufferSource();
    src.buffer = buf;
    const lp = ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.value = 1200;
    const g = ctx.createGain();
    g.gain.value = 0.35;
    src.connect(lp).connect(g).connect(this.master);
    src.start();
    this.tone(110, 0, 0.4, 'sawtooth', 0.15);
  }

  finish(): void {
    [523.25, 659.25, 783.99, 1046.5].forEach((f, i) => this.tone(f, i * 0.09, 0.35, 'square', 0.1));
  }

  explosion(): void {
    this.crash();
    this.tone(55, 0, 0.6, 'sine', 0.3);
  }

  click(): void {
    this.tone(1760, 0, 0.04, 'square', 0.03);
  }

  dispose(): void {
    if (this.beatTimer !== null) window.clearTimeout(this.beatTimer);
    void this.ctx?.close();
    this.ctx = null;
  }
}
