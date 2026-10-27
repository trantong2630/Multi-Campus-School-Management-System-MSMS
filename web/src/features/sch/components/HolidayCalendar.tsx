import { Button, Tooltip } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import type { KeyboardEvent } from 'react';
import type { Holiday } from '../types';
import './school.css';

interface Props {
  /** Tháng đang xem (ngày nào trong tháng cũng được). */
  month: Dayjs;
  onMonthChange: (month: Dayjs) => void;
  /** Khoảng của năm học đang xem (YYYY-MM-DD); ngoài khoảng này ô bị làm mờ và không bấm thêm được. */
  yearStart: string;
  yearEnd: string;
  holidays: Holiday[];
  canWrite: boolean;
  onAdd: (date: Dayjs) => void;
  onRemove: (holiday: Holiday) => void;
}

const WEEKDAYS = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'CN'];
const MAX_CHIPS = 2;

const chipText = (h: Holiday) => h.reason?.trim() || 'Nghỉ';
const chipTip = (h: Holiday, canWrite: boolean) =>
  `${h.campusName ?? 'Toàn trường'}${h.reason ? ` · ${h.reason}` : ''}${canWrite ? ' — bấm để xóa' : ''}`;
const chipClass = (h: Holiday) => `hol-chip ${h.campusId === null ? 'hol-chip--school' : 'hol-chip--campus'}`;

/**
 * Lịch tháng của ngày nghỉ (SCH-03, 4.2.18). Khung là thẻ trắng bo tròn, tiêu đề cột nền lõm, ngày nghỉ là viên pastel bo góc
 * giống lưới thời khóa biểu (Design_Web §9.1). Nghỉ toàn trường màu hổ phách, nghỉ riêng một phân hiệu màu xanh trời; màu luôn đi kèm
 * chữ lý do. Bấm ô trống để thêm ngày nghỉ, bấm viên ngày nghỉ để xóa (cả hai chỉ School Admin).
 */
export function HolidayCalendar({ month, onMonthChange, yearStart, yearEnd, holidays, canWrite, onAdd, onRemove }: Props) {
  const first = month.startOf('month');
  const lead = (first.day() + 6) % 7;   // Thứ 2 là cột đầu
  const daysInMonth = first.daysInMonth();
  const weeks = Math.ceil((lead + daysInMonth) / 7);
  const today = dayjs();

  const byDate = new Map<string, Holiday[]>();
  for (const h of holidays) byDate.set(h.holidayDate, [...(byDate.get(h.holidayDate) ?? []), h]);

  const startMonth = dayjs(yearStart).startOf('month');
  const endMonth = dayjs(yearEnd).startOf('month');
  const inYear = (d: Dayjs) => !d.isBefore(yearStart, 'day') && !d.isAfter(yearEnd, 'day');

  const cell = (date: Dayjs) => {
    const key = date.format('YYYY-MM-DD');
    const list = byDate.get(key) ?? [];
    const shown = list.slice(0, MAX_CHIPS);
    const rest = list.slice(MAX_CHIPS);
    const clickable = canWrite && inYear(date);
    const classes = [
      'hol-cell',
      !inYear(date) && 'hol-cell--out',
      date.day() === 0 && 'hol-cell--sunday',
      clickable && 'hol-cell--clickable',
    ].filter(Boolean).join(' ');
    const open = () => { if (clickable) onAdd(date); };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); open(); } };

    return (
      <td
        key={key} className={classes} onClick={open}
        {...(clickable ? { role: 'button', tabIndex: 0, onKeyDown: onKey, 'aria-label': `Thêm ngày nghỉ ${date.format('DD/MM/YYYY')}` } : {})}
      >
        <span className={`hol-cell__day tabular${date.isSame(today, 'day') ? ' hol-cell__day--today' : ''}`}>{date.date()}</span>
        <div className="hol-cell__chips">
          {shown.map((h) => (
            <Tooltip key={h.holidayId} title={chipTip(h, canWrite)}>
              {canWrite ? (
                <button type="button" className={chipClass(h)} onClick={(e) => { e.stopPropagation(); onRemove(h); }}>{chipText(h)}</button>
              ) : (
                <span className={chipClass(h)}>{chipText(h)}</span>
              )}
            </Tooltip>
          ))}
          {rest.length > 0 && (
            <Tooltip title={rest.map((h) => <div key={h.holidayId}>{chipTip(h, false)}</div>)}>
              <span className="hol-more">+{rest.length} khác</span>
            </Tooltip>
          )}
        </div>
      </td>
    );
  };

  return (
    <div className="hol-cal">
      <div className="hol-cal__head">
        <div className="hol-cal__nav">
          <Button
            type="text" className="icon-btn" aria-label="Tháng trước" icon={<ChevronLeft size={18} />}
            disabled={!first.isAfter(startMonth, 'month')} onClick={() => onMonthChange(first.subtract(1, 'month'))}
          />
          <strong className="hol-cal__title tabular">Tháng {first.month() + 1} năm {first.year()}</strong>
          <Button
            type="text" className="icon-btn" aria-label="Tháng sau" icon={<ChevronRight size={18} />}
            disabled={!first.isBefore(endMonth, 'month')} onClick={() => onMonthChange(first.add(1, 'month'))}
          />
        </div>
        <div className="hol-cal__legend">
          <span className="hol-chip hol-chip--school">Toàn trường</span>
          <span className="hol-chip hol-chip--campus">Riêng phân hiệu</span>
        </div>
      </div>
      <table className="hol-grid">
        <thead>
          <tr>{WEEKDAYS.map((w) => <th key={w}>{w}</th>)}</tr>
        </thead>
        <tbody>
          {Array.from({ length: weeks }, (_, w) => (
            <tr key={w}>
              {Array.from({ length: 7 }, (_, c) => {
                const n = w * 7 + c - lead + 1;
                return n < 1 || n > daysInMonth
                  ? <td key={c} className="hol-cell hol-cell--blank" />
                  : cell(first.date(n));
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
