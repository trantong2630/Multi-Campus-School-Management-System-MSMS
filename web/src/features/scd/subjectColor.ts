import type { CSSProperties } from 'react';

/** Góc vàng: hai id liền nhau luôn lệch xa trên vòng màu nên các môn không trùng màu, không giới hạn số môn. */
const GOLDEN_ANGLE = 137.508;

/** Cùng một môn luôn ra cùng một màu: sắc độ sinh từ subjectId, độ sáng và bão hòa cố định cho đều độ đậm (chỉ có màu nền). */
export function subjectColorStyle(subjectId: number): CSSProperties {
  const hue = Math.round((subjectId * GOLDEN_ANGLE) % 360);
  return {
    '--tt-bg': `hsl(${hue} 65% 92%)`,
  } as CSSProperties;
}
