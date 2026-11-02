/** Định dạng số liệu Dashboard: số theo vi-VN, phần trăm một chữ số thập phân, "—" khi chưa có dữ liệu. */
export { formatNumber as fmt } from '@/shared/lib/format';
export const pct = (v: number | null | undefined) => (v === null || v === undefined ? '—' : `${v.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}%`);

/** Ngưỡng màu tỷ lệ nộp bài: từ 80% đạt, từ 60% cần chú ý, dưới 60% thấp. */
export type RateTone = 'good' | 'warn' | 'low' | 'none';
export function rateTone(rate: number | null | undefined): RateTone {
  if (rate === null || rate === undefined) return 'none';
  if (rate >= 80) return 'good';
  if (rate >= 60) return 'warn';
  return 'low';
}
