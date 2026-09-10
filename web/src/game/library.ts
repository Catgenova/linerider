import type { EnvironmentId } from './environments';
import type { TrackJSON } from './track';

export interface PublishedTrack {
  id: string;
  title: string;
  author: string;
  description: string;
  tags: string[];
  difficulty: number;
  environment: EnvironmentId;
  budget: number | null;
  thumbnail: string;
  created: number;
  likes: number;
  plays: number;
  liked: boolean;
  records: { bestFrames: number | null; bestInk: number | null; bestTrick: number };
  track: TrackJSON;
  /** Built-in showcase tracks cannot be deleted. */
  builtin?: boolean;
}

const KEY = 'neon-linerider-library-v1';

/**
 * Track sharing store. This implementation keeps everything in localStorage and exchanges tracks
 * through share codes; swap `TrackStore` for a server-backed one to get global leaderboards.
 */
export interface TrackStore {
  list(): PublishedTrack[];
  get(id: string): PublishedTrack | undefined;
  publish(track: Omit<PublishedTrack, 'id' | 'created' | 'likes' | 'plays' | 'liked' | 'records'>): PublishedTrack;
  update(track: PublishedTrack): void;
  remove(id: string): void;
}

export class LocalTrackStore implements TrackStore {
  private items: PublishedTrack[];

  constructor(private readonly builtins: PublishedTrack[] = []) {
    this.items = LocalTrackStore.load();
  }

  private static load(): PublishedTrack[] {
    try {
      const raw = localStorage.getItem(KEY);
      return raw ? (JSON.parse(raw) as PublishedTrack[]) : [];
    } catch {
      return [];
    }
  }

  private save(): void {
    try {
      localStorage.setItem(KEY, JSON.stringify(this.items));
    } catch {
      /* ignore */
    }
  }

  list(): PublishedTrack[] {
    return [...this.items, ...this.builtins.filter((b) => !this.items.some((i) => i.id === b.id))].sort(
      (a, b) => b.created - a.created,
    );
  }

  get(id: string): PublishedTrack | undefined {
    return this.items.find((t) => t.id === id) ?? this.builtins.find((t) => t.id === id);
  }

  publish(input: Omit<PublishedTrack, 'id' | 'created' | 'likes' | 'plays' | 'liked' | 'records'>): PublishedTrack {
    const track: PublishedTrack = {
      ...input,
      id: `t_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 7)}`,
      created: Date.now(),
      likes: 0,
      plays: 0,
      liked: false,
      records: { bestFrames: null, bestInk: null, bestTrick: 0 },
    };
    this.items.unshift(track);
    this.save();
    return track;
  }

  update(track: PublishedTrack): void {
    const i = this.items.findIndex((t) => t.id === track.id);
    if (i >= 0) this.items[i] = track;
    else this.items.unshift(track);
    this.save();
  }

  remove(id: string): void {
    this.items = this.items.filter((t) => t.id !== id);
    this.save();
  }
}

/** Encode a published track as a shareable string (base64 JSON). */
export function encodeShareCode(track: PublishedTrack): string {
  const payload = {
    t: track.title,
    a: track.author,
    d: track.description,
    g: track.tags,
    f: track.difficulty,
    e: track.environment,
    b: track.budget,
    k: track.track,
  };
  const json = JSON.stringify(payload);
  const bytes = new TextEncoder().encode(json);
  let bin = '';
  for (const b of bytes) bin += String.fromCharCode(b);
  return 'NLR1.' + btoa(bin);
}

export function decodeShareCode(code: string): Omit<PublishedTrack, 'id' | 'created' | 'likes' | 'plays' | 'liked' | 'records' | 'thumbnail'> | null {
  try {
    const trimmed = code.trim();
    if (!trimmed.startsWith('NLR1.')) return null;
    const bin = atob(trimmed.slice(5));
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    const payload = JSON.parse(new TextDecoder().decode(bytes));
    if (!payload.k || !Array.isArray(payload.k.lines)) return null;
    return {
      title: String(payload.t ?? 'Untitled'),
      author: String(payload.a ?? 'Anonymous'),
      description: String(payload.d ?? ''),
      tags: Array.isArray(payload.g) ? payload.g.map(String) : [],
      difficulty: Number(payload.f ?? 2),
      environment: payload.e ?? 'mountain',
      budget: payload.b ?? null,
      track: payload.k,
    };
  } catch {
    return null;
  }
}
