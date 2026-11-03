import { formatDate, formatTime, sessionOfDayLabel, weekdayOfDate } from '@/shared/lib/format';
import { statusGroup, statusLabel } from '@/shared/lib/labels';
import type { BadgeGroup } from '@/shared/lib/tokens';
import type { AttendanceCounts, AttendanceLabel, AttendanceSession } from './types';

export interface CountItem {
  key: string;
  label: string;
  value: number;
  /** Nhóm màu badge (Design_Web §2.4) cho chấm màu trước nhãn. */
  group: BadgeGroup;
}

const LABEL_ORDER: AttendanceLabel[] = ['Present', 'Late', 'ExcusedAbsent', 'UnexcusedAbsent', 'NotMarked'];
const COUNT_KEY: Record<AttendanceLabel, keyof AttendanceCounts> = {
  Present: 'present', Late: 'late', ExcusedAbsent: 'excusedAbsent', UnexcusedAbsent: 'unexcusedAbsent', NotMarked: 'notMarked',
};

/** Bốn nhãn cuối + Chưa điểm danh, theo thứ tự Conventions §4.4. */
export function labelCountItems(counts: AttendanceCounts): CountItem[] {
  return LABEL_ORDER.map((l) => ({ key: l, label: statusLabel('attendance', l), value: counts[COUNT_KEY[l]], group: statusGroup('attendance', l) }));
}

/** "Sáng · Tiết 2 (07:50–08:35)" */
export const slotText = (s: Pick<AttendanceSession, 'sessionOfDay' | 'periodNo' | 'startTime' | 'endTime'>) =>
  `${sessionOfDayLabel(s.sessionOfDay)} · Tiết ${s.periodNo} (${formatTime(s.startTime)}–${formatTime(s.endTime)})`;

/** "Thứ 3 12/10/2026" */
export const dayText = (date: string) => `${weekdayOfDate(date)} ${formatDate(date)}`;
