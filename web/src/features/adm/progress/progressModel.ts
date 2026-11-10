import dayjs from 'dayjs';
import type { ProgressItem, ProgressTeacher } from '../types';
import type { TrendPoint } from '../components/BoardKit';

/** Phần tối thiểu để tính tỷ lệ và xu hướng; màn tổ và màn lớp dùng chung với bài của màn giám sát. */
export type Core = Pick<ProgressItem, 'openAt' | 'dueAt' | 'isClosed' | 'recipientCount' | 'submittedCount'>;

export type Lens = 'campus' | 'department' | 'subject' | 'class' | 'teacher';

export interface Filters {
  campusId?: number;
  departmentId?: number;
  subjectId?: number;
  classId?: number;
  teacherId?: number;
}

export interface Group {
  key: number;
  name: string;
  sub?: string;
  total: number;
  open: number;
  closed: number;
  /** Bài đã đóng mà dưới một nửa người nhận đã nộp. */
  low: number;
  rate: number | null;
  /** Chênh lệch điểm phần trăm: 4 tuần qua so với 4 tuần trước đó. Null khi một trong hai kỳ chưa có bài đóng. */
  delta: number | null;
}

const DAY = 86_400_000;

export const percent = (sub: number, rec: number) => (rec > 0 ? Math.round((sub * 1000) / rec) / 10 : null);

export function closedRate(items: Core[]): number | null {
  const closed = items.filter((i) => i.isClosed);
  return percent(closed.reduce((n, i) => n + i.submittedCount, 0), closed.reduce((n, i) => n + i.recipientCount, 0));
}

export const isLow = (i: Core) => i.isClosed && i.recipientCount > 0 && i.submittedCount / i.recipientCount < 0.5;

export function applyFilters(items: ProgressItem[], f: Filters): ProgressItem[] {
  return items.filter((i) =>
    (f.campusId === undefined || i.campusId === f.campusId)
    && (f.departmentId === undefined || i.departmentId === f.departmentId)
    && (f.subjectId === undefined || i.subjectId === f.subjectId)
    && (f.classId === undefined || i.classId === f.classId)
    && (f.teacherId === undefined || i.teacherId === f.teacherId));
}

/** Tỷ lệ nộp của các bài hết hạn trong khoảng (from, to]. */
function rateBetween(items: Core[], from: number, to: number): number | null {
  return closedRate(items.filter((i) => {
    const t = dayjs(i.dueAt).valueOf();
    return t > from && t <= to;
  }));
}

export function deltaOf(items: Core[], nowMs: number): number | null {
  const recent = rateBetween(items, nowMs - 28 * DAY, nowMs);
  const before = rateBetween(items, nowMs - 56 * DAY, nowMs - 28 * DAY);
  return recent === null || before === null ? null : Math.round((recent - before) * 10) / 10;
}

const keyOf: Record<Lens, (i: ProgressItem) => { key: number; name: string; sub?: string }> = {
  campus: (i) => ({ key: i.campusId, name: i.campusName }),
  department: (i) => ({ key: i.departmentId, name: i.departmentName }),
  subject: (i) => ({ key: i.subjectId, name: i.subjectName, sub: i.departmentName }),
  class: (i) => ({ key: i.classId, name: i.className, sub: i.campusName }),
  teacher: (i) => ({ key: i.teacherId, name: i.teacherName, sub: i.departmentName }),
};

/** Gom bài theo một chiều; nhóm có tỷ lệ nộp thấp nhất lên đầu, nhóm chưa có bài đóng xuống cuối. */
export function groupItems(items: ProgressItem[], lens: Lens, nowMs: number): Group[] {
  const map = new Map<number, { head: { key: number; name: string; sub?: string }; items: ProgressItem[] }>();
  for (const i of items) {
    const head = keyOf[lens](i);
    const cur = map.get(head.key) ?? { head, items: [] };
    cur.items.push(i);
    map.set(head.key, cur);
  }
  return [...map.values()].map(({ head, items: g }) => ({
    ...head,
    total: g.length,
    open: g.filter((i) => !i.isClosed).length,
    closed: g.filter((i) => i.isClosed).length,
    low: g.filter(isLow).length,
    rate: closedRate(g),
    delta: deltaOf(g, nowMs),
  })).sort((a, b) => (a.rate ?? 101) - (b.rate ?? 101) || a.name.localeCompare(b.name, 'vi'));
}

/** 8 tuần gần nhất: số bài mở giao trong tuần và tỷ lệ nộp của các bài hết hạn trong tuần. */
export function weekTrend(items: Core[], nowMs: number): TrendPoint[] {
  const thisWeek = dayjs(nowMs).startOf('day').subtract((dayjs(nowMs).day() + 6) % 7, 'day');
  return Array.from({ length: 8 }, (_, k) => thisWeek.subtract(7 * (7 - k), 'day')).map((start) => {
    const end = start.add(7, 'day');
    const within = (iso: string) => { const d = dayjs(iso); return !d.isBefore(start) && d.isBefore(end); };
    return {
      weekStart: start.format('YYYY-MM-DD'),
      count: items.filter((i) => within(i.openAt)).length,
      rate: closedRate(items.filter((i) => within(i.dueAt))),
    };
  });
}

export interface TeacherGap { teacher: ProgressTeacher; lastOpenAt: string | null }

/** Giáo viên có phân công dạy nhưng quá 14 ngày chưa giao bài nào (tính trên mọi bài của họ trong phạm vi). */
export function idleTeachers(teachers: ProgressTeacher[], all: ProgressItem[], f: Filters, nowMs: number): TeacherGap[] {
  const last = new Map<number, string>();
  for (const i of all) if (!last.has(i.teacherId) || i.openAt > last.get(i.teacherId)!) last.set(i.teacherId, i.openAt);
  return teachers
    .filter((t) => (f.campusId === undefined || t.campusId === f.campusId)
      && (f.departmentId === undefined || t.departmentId === f.departmentId)
      && (f.teacherId === undefined || t.teacherId === f.teacherId))
    .map((teacher) => ({ teacher, lastOpenAt: last.get(teacher.teacherId) ?? null }))
    .filter((g) => !g.lastOpenAt || dayjs(g.lastOpenAt).valueOf() < nowMs - 14 * DAY);
}

/** Tỷ lệ nộp của các bài hết hạn trong tuần này (từ thứ Hai tới giờ) so với tuần trước. Delta null khi một trong hai tuần chưa có bài hết hạn. */
export function weekCompare(items: Core[], nowMs: number): { thisRate: number | null; lastRate: number | null; delta: number | null } {
  const monday = dayjs(nowMs).startOf('day').subtract((dayjs(nowMs).day() + 6) % 7, 'day').valueOf();
  const thisRate = rateBetween(items, monday - 1, nowMs);
  const lastRate = rateBetween(items, monday - 7 * DAY - 1, monday - 1);
  return { thisRate, lastRate, delta: thisRate === null || lastRate === null ? null : Math.round((thisRate - lastRate) * 10) / 10 };
}
