import type { CSSProperties } from 'react';
import { scoreBand } from '../time';
import type { ScoreBand } from '../time';

const GOLDEN_ANGLE = 137.508;

/** Cùng một môn luôn ra cùng một sắc độ (cùng công thức với lưới thời khóa biểu), kèm sắc đậm để làm chữ và nét. */
export function subjectVars(subjectId: number): CSSProperties {
  const hue = Math.round((subjectId * GOLDEN_ANGLE) % 360);
  return {
    '--sj-bg': `hsl(${hue} 70% 93%)`,
    '--sj-mid': `hsl(${hue} 62% 60%)`,
    '--sj-fg': `hsl(${hue} 55% 28%)`,
  } as CSSProperties;
}

const BAND_COLOR: Record<ScoreBand, string> = {
  top: 'var(--c-green600)',
  good: 'var(--c-skyFg)',
  fair: 'var(--c-amberFg)',
  low: 'var(--c-coralFg)',
};
export const bandColor = (score: number | null | undefined) => BAND_COLOR[scoreBand(score)];

