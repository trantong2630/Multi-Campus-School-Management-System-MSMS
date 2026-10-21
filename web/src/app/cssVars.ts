import { color, focusRing, gradient, radius, shadow } from '@/shared/lib/tokens';

/** Đẩy token sang CSS variables để file .css không phải ghi hex (Design_Web quy tắc 1). */
export function applyCssVars() {
  const root = document.documentElement.style;
  for (const [k, v] of Object.entries(color)) root.setProperty(`--c-${k}`, v);
  for (const [k, v] of Object.entries(shadow)) root.setProperty(`--shadow-${k}`, v);
  for (const [k, v] of Object.entries(radius)) root.setProperty(`--radius-${k}`, `${v}px`);
  for (const [k, v] of Object.entries(gradient)) root.setProperty(`--grad-${k}`, v);
  root.setProperty('--focus-ring', focusRing);
}
