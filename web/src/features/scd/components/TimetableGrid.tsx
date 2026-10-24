import { Fragment } from 'react';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import { sessionOfDayLabel, weekdayLabel } from '@/shared/lib/format';
import type { GridCell, VersionGrid } from '../types';
import './scd.css';

/**
 * Lưới thời khóa biểu (Design_Web §9.1): hàng là Thứ × Buổi × Tiết, cột là lớp.
 * Hàng tiêu đề và ba cột đầu dính khi cuộn; buổi sáng và chiều cách nhau bằng một dải mảnh.
 */
export function TimetableGrid({ grid }: { grid: VersionGrid }) {
  const days = [...new Set(grid.rows.map((r) => r.dayOfWeek))];

  return (
    <div className="tt-scroll">
      <table className="tt-grid">
        <thead>
          <tr>
            <th className="tt-sticky tt-col-day">Thứ</th>
            <th className="tt-sticky tt-col-session">Buổi</th>
            <th className="tt-sticky tt-col-period">Tiết</th>
            {grid.classes.map((c) => <th key={c.classId} className="tt-class">{c.name}</th>)}
          </tr>
        </thead>
        <tbody>
          {days.map((day) => {
            const rows = grid.rows.filter((r) => r.dayOfWeek === day);
            // Dải phân cách buổi cũng chiếm một hàng nên cột Thứ phải gộp thêm các hàng đó.
            const bands = rows.filter((r, j) => j > 0 && rows[j - 1].sessionOfDay !== r.sessionOfDay).length;
            return rows.map((row, i) => {
              const firstOfDay = i === 0;
              const newSession = i > 0 && rows[i - 1].sessionOfDay !== row.sessionOfDay;
              const sessionRows = rows.filter((r) => r.sessionOfDay === row.sessionOfDay).length;
              const firstOfSession = i === 0 || newSession;
              return (
                <Fragment key={`${day}-${row.sessionOfDay}-${row.periodNo}`}>
                  {newSession && (
                    <tr className="tt-band"><td colSpan={2 + grid.classes.length} /></tr>
                  )}
                  <tr className={firstOfDay ? 'tt-day-start' : undefined}>
                    {firstOfDay && <td className="tt-sticky tt-col-day tt-day" rowSpan={rows.length + bands}>{weekdayLabel(day)}</td>}
                    {firstOfSession && <td className="tt-sticky tt-col-session" rowSpan={sessionRows}>{sessionOfDayLabel(row.sessionOfDay)}</td>}
                    <td className="tt-sticky tt-col-period tabular">{row.periodNo}</td>
                    {row.cells.map((cell, ci) => <Cell key={grid.classes[ci].classId} cell={cell} />)}
                  </tr>
                </Fragment>
              );
            });
          })}
        </tbody>
      </table>
    </div>
  );
}

export const SUBJECT_TONES: AccentName[] = ['green', 'sky', 'violet', 'amber', 'coral', 'teal'];

/** Cùng một môn luôn ra cùng một màu: băm viết tắt môn vào bảng màu nhấn. */
export function toneOf(abbr: string): AccentName {
  let h = 0;
  for (const ch of abbr) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return SUBJECT_TONES[h % SUBJECT_TONES.length];
}

function Cell({ cell }: { cell: GridCell | null }) {
  if (!cell) return <td className="tt-cell tt-cell--empty" />;
  const a = accent[toneOf(cell.subjectAbbr)];
  return (
    <td className="tt-cell" title={`${cell.subjectName} — ${cell.teacherName}`}>
      <div className="tt-chip" style={{ '--tt-bg': a.bg, '--tt-fg': a.fg } as React.CSSProperties}>
        <div className="tt-subject">{cell.subjectAbbr}</div>
        <div className="tt-teacher">{cell.teacherAbbr}</div>
      </div>
    </td>
  );
}
