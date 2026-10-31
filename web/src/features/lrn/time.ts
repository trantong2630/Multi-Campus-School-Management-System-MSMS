import dayjs from 'dayjs';

/** Còn bao lâu tới mốc, dạng MM:SS, thêm giờ khi trên một giờ (HH:MM:SS). */
export function formatClock(ms: number): string {
  const total = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const mm = String(m).padStart(2, '0');
  const ss = String(s).padStart(2, '0');
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`;
}

export type DueTone = 'ok' | 'soon' | 'late';

/** Nhãn ngắn về hạn nộp: "Còn 2 giờ", "Còn 3 ngày", "Đã quá hạn". Dưới 24 giờ chuyển sang màu cảnh báo. */
export function dueHint(dueAt: string, nowMs: number): { text: string; tone: DueTone } {
  const diff = dayjs(dueAt).valueOf() - nowMs;
  if (diff <= 0) return { text: 'Đã quá hạn', tone: 'late' };
  const minutes = Math.ceil(diff / 60000);
  if (minutes < 60) return { text: `Còn ${minutes} phút`, tone: 'soon' };
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return { text: `Còn ${hours} giờ`, tone: 'soon' };
  return { text: `Còn ${Math.floor(hours / 24)} ngày`, tone: 'ok' };
}

/** Nhãn ngày cho mốc nhóm: Hôm nay, Ngày mai, hoặc "Thứ 4, 12/11". */
export function dayLabel(iso: string, nowMs: number): string {
  const d = dayjs(iso);
  const today = dayjs(nowMs).startOf('day');
  const diff = d.startOf('day').diff(today, 'day');
  if (diff < 0) return 'Đã quá hạn';
  if (diff === 0) return 'Hôm nay';
  if (diff === 1) return 'Ngày mai';
  const wd = d.day() === 0 ? 'CN' : `Thứ ${d.day() + 1}`;
  return `${wd}, ${d.format('DD/MM')}`;
}

export const fmtScore = (v: number | null | undefined) =>
  (v === null || v === undefined ? '—' : v.toLocaleString('vi-VN', { minimumFractionDigits: 0, maximumFractionDigits: 2 }));

export type ScoreBand = 'top' | 'good' | 'fair' | 'low';
export const scoreBand = (score: number | null | undefined): ScoreBand => {
  const s = score ?? 0;
  return s >= 8 ? 'top' : s >= 6.5 ? 'good' : s >= 5 ? 'fair' : 'low';
};

export const durationText = (minutes: number | null) => (minutes === null ? 'Không giới hạn giờ' : `${minutes} phút`);
