import { entityStaticLines } from './entities';
import type { LevelDef } from './level';
import { Track } from './track';

/** Build a fresh editable track from a level definition (prebuilt geometry on the level layer). */
export function trackFromLevel(level: LevelDef): Track {
  const track = new Track();
  track.start = { ...level.start };
  track.finish = level.finish ? { ...level.finish } : undefined;
  for (const l of level.lines) track.addLine({ ...l, layer: 'level' });
  for (const def of level.entities ?? []) {
    for (const l of entityStaticLines(def)) track.addLine({ ...l, layer: 'level' });
  }
  return track;
}
