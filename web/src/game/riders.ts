import { BOARD_MODEL, SLED_MODEL, type RiderModel } from '../physics/rider';
import { START_VELOCITY } from '../physics/constants';

export type RiderId = 'bosh' | 'vex' | 'tank' | 'nova';

export interface RiderDef {
  id: RiderId;
  name: string;
  tagline: string;
  description: string;
  color: string;
  glow: string;
  model: RiderModel;
  gravityScale: number;
  frictionScale: number;
  enduranceScale: number;
  startVelocity: number;
  /** Level id that unlocks the rider, or null if available from the start. */
  unlock: string | null;
}

export const RIDERS: Record<RiderId, RiderDef> = {
  bosh: {
    id: 'bosh',
    name: 'Bosh',
    tagline: 'The original',
    description: 'Classic sled physics. Balanced weight, honest momentum, breaks like you remember.',
    color: '#39f6ff',
    glow: '#00c8ff',
    model: SLED_MODEL,
    gravityScale: 1,
    frictionScale: 1,
    enduranceScale: 1,
    startVelocity: START_VELOCITY,
    unlock: null,
  },
  vex: {
    id: 'vex',
    name: 'Vex',
    tagline: 'Featherweight',
    description: 'Floats. Lower effective gravity means longer airtime and slower, softer landings, but the frame is fragile.',
    color: '#c6ff4a',
    glow: '#8cff00',
    model: SLED_MODEL,
    gravityScale: 0.78,
    frictionScale: 1,
    enduranceScale: 0.85,
    startVelocity: START_VELOCITY,
    unlock: 'peaks-3',
  },
  tank: {
    id: 'tank',
    name: 'Tank',
    tagline: 'Juggernaut',
    description: 'Heavy and stubborn. Carries momentum through mud and bumps, shrugs off crashes, but drops like a stone.',
    color: '#ffa640',
    glow: '#ff7a00',
    model: SLED_MODEL,
    gravityScale: 1.18,
    frictionScale: 0.45,
    enduranceScale: 1.9,
    startVelocity: START_VELOCITY * 1.5,
    unlock: 'glacier-2',
  },
  nova: {
    id: 'nova',
    name: 'Nova',
    tagline: 'Boarder',
    description: 'Rides a board instead of a sled. Longer contact patch, higher centre of mass: stable on rails, wild in the air.',
    color: '#ff2bd6',
    glow: '#ff00c8',
    model: BOARD_MODEL,
    gravityScale: 1,
    frictionScale: 0.7,
    enduranceScale: 1.25,
    startVelocity: START_VELOCITY,
    unlock: 'roof-2',
  },
};

export const RIDER_ORDER: RiderId[] = ['bosh', 'vex', 'tank', 'nova'];
