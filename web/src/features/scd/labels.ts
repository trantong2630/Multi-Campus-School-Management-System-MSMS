import type { PeriodSlot } from '@/features/sch/types';
import { formatTime, sessionOfDayLabel } from '@/shared/lib/format';

export const slotLabel = (p: PeriodSlot) => `${sessionOfDayLabel(p.sessionOfDay)} tiết ${p.periodNo} (${formatTime(p.startTime)}–${formatTime(p.endTime)})`;

/** Phạm vi vắng trong ngày: danh sách tiết trống là cả ngày. */
export const absenceScopeLabel = (slotIds: number[], slots: PeriodSlot[] | undefined) => {
  if (slotIds.length === 0) return 'Cả ngày';
  const byId = new Map((slots ?? []).map((p) => [p.periodSlotId, p]));
  return slotIds
    .map((id) => byId.get(id))
    .filter((p): p is PeriodSlot => p !== undefined)
    .sort((a, b) => (a.sessionOfDay === b.sessionOfDay ? a.periodNo - b.periodNo : a.sessionOfDay === 'Morning' ? -1 : 1))
    .map((p) => `${sessionOfDayLabel(p.sessionOfDay)} tiết ${p.periodNo}`)
    .join(', ');
};
