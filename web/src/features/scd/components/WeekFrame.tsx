import { useQuery } from '@tanstack/react-query';
import { Button, DatePicker } from 'antd';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { Fragment } from 'react';
import type { ReactNode } from 'react';
import { schApi } from '@/features/sch/api';
import type { PeriodSlot } from '@/features/sch/types';
import { DataTable } from '@/shared/components/DataTable';
import { formatTime, sessionOfDayLabel, weekdayOfDate } from '@/shared/lib/format';
import './scd.css';

/** Khung lưới tuần dùng chung: hàng là Buổi × Tiết, cột là Thứ 2 → Thứ 7 của tuần đang chọn. `renderCell` trả về một ô `<td>`. */
export function WeekFrame({ monday, loading, usedSlotIds, renderCell, renderDayHeader }: {
  monday: Dayjs;
  loading: boolean;
  /** Khung đã ngừng dùng vẫn hiện nếu tuần này còn tiết nằm ở khung đó. */
  usedSlotIds: Set<number>;
  renderCell: (slot: PeriodSlot, day: Dayjs) => ReactNode;
  renderDayHeader?: (day: Dayjs) => ReactNode;
}) {
  const days = Array.from({ length: 6 }, (_, i) => monday.add(i, 'day'));
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });
  const rows = (slots.data ?? [])
    .filter((p) => p.isActive || usedSlotIds.has(p.periodSlotId))
    .sort((a, b) => (a.sessionOfDay === b.sessionOfDay ? a.periodNo - b.periodNo : a.sessionOfDay === 'Morning' ? -1 : 1));
  const today = dayjs();

  if (slots.isLoading || loading) return <DataTable<object> rowKey="x" loading dataSource={[]} columns={[]} />;
  return (
    <div className="tt-scroll">
      <table className="tt-grid tt-week">
        <thead>
          <tr>
            <th className="tt-sticky tt-col-session" style={{ left: 0 }}>Buổi</th>
            <th className="tt-sticky" style={{ left: 64, minWidth: 110 }}>Tiết</th>
            {days.map((d) => (
              <th key={d.valueOf()} className={`tt-class${d.isSame(today, 'day') ? ' tt-today' : ''}`}>
                {weekdayOfDate(d)}
                <span className="tt-time">{d.format('DD/MM')}</span>
                {renderDayHeader?.(d)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((slot, i) => {
            const newSession = i > 0 && rows[i - 1].sessionOfDay !== slot.sessionOfDay;
            const firstOfSession = i === 0 || newSession;
            return (
              <Fragment key={slot.periodSlotId}>
                {newSession && <tr className="tt-band"><td colSpan={2 + days.length} /></tr>}
                <tr>
                  {firstOfSession && (
                    <td className="tt-sticky tt-col-session" style={{ left: 0 }} rowSpan={rows.filter((r) => r.sessionOfDay === slot.sessionOfDay).length}>
                      {sessionOfDayLabel(slot.sessionOfDay)}
                    </td>
                  )}
                  <td className="tt-sticky tabular" style={{ left: 64, minWidth: 110 }}>
                    {slot.periodNo}
                    <span className="tt-time">{formatTime(slot.startTime)}–{formatTime(slot.endTime)}</span>
                  </td>
                  {days.map((d) => <Fragment key={d.valueOf()}>{renderCell(slot, d)}</Fragment>)}
                </tr>
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export function WeekNav({ monday, onChange }: { monday: Dayjs; onChange: (m: Dayjs) => void }) {
  const end = monday.add(5, 'day');
  return (
    <>
      <Button icon={<ChevronLeft size={16} />} aria-label="Tuần trước" onClick={() => onChange(monday.subtract(1, 'week'))} />
      <DatePicker
        picker="week" allowClear={false} value={monday}
        format={() => `${monday.format('DD/MM')} – ${end.format('DD/MM/YYYY')}`}
        onChange={(d) => d && onChange(d.startOf('week'))}
      />
      <Button icon={<ChevronRight size={16} />} aria-label="Tuần sau" onClick={() => onChange(monday.add(1, 'week'))} />
    </>
  );
}
