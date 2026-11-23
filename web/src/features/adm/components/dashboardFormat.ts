import dayjs from 'dayjs';

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

/** Ngưỡng cảnh báo chuyên cần (mục 5.1): dưới 90% là lớp cần chú ý. */
export const ATTENDANCE_ALERT = 90;
/** Ngưỡng cảnh báo nộp bài rèn luyện: dưới 50%. */
export const SUBMISSION_ALERT = 50;

/** Nhãn tuần theo năm học: "Tuần 12"; chưa có năm học thì dùng ngày thứ Hai. */
export function weekLabel(weekStarts: string[], index: number, weekNo: number | null) {
  if (weekNo !== null) {
    const n = weekNo - (weekStarts.length - 1 - index);
    if (n >= 1) return `Tuần ${n}`;
  }
  return dayjs(weekStarts[index]).format('DD/MM');
}


/** Một phần của thanh cơ cấu. */
export interface Segment { key: string; label: string; value: number; tone: 'good' | 'soft' | 'warn' | 'low' | 'ink' }

/** Nhãn và màu của kết quả xử lý tiết bị ảnh hưởng. */
export const OUTCOME: Record<string, { label: string; tone: 'good' | 'warn' | 'low' }> = {
  Substituted: { label: 'Đã dạy thay', tone: 'good' },
  Cancelled: { label: 'Đã hủy', tone: 'warn' },
  Pending: { label: 'Chưa xử lý', tone: 'low' },
};

