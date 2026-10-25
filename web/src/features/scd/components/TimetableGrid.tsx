import { DndContext, DragOverlay, PointerSensor, useDndContext, useDraggable, useDroppable, useSensor, useSensors } from '@dnd-kit/core';
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core';
import { Fragment, useRef, useState } from 'react';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import { sessionOfDayLabel, weekdayLabel } from '@/shared/lib/format';
import type { GridCell, VersionGrid } from '../types';
import './scd.css';

export interface CellPos {
  classId: number;
  day: number;
  slotId: number;
}

/** Chế độ sửa bản Nháp (BR-12): kéo tiết trong cột của cùng một lớp, nhấp vào ô để thêm, đổi giáo viên hoặc xóa. */
export interface GridEditProps {
  onMove: (from: CellPos, to: { day: number; slotId: number }) => void;
  onCellClick: (pos: CellPos, cell: GridCell | null) => void;
}

const cellId = (p: CellPos) => `${p.classId}|${p.day}|${p.slotId}`;

/**
 * Lưới thời khóa biểu (Design_Web §9.1): hàng là Thứ × Buổi × Tiết, cột là lớp.
 * Hàng tiêu đề và ba cột đầu dính khi cuộn; buổi sáng và chiều cách nhau bằng một dải mảnh.
 * Có `edit` thì ô kéo và nhấp được; không có thì chỉ xem.
 */
export function TimetableGrid({ grid, edit }: { grid: VersionGrid; edit?: GridEditProps }) {
  const days = [...new Set(grid.rows.map((r) => r.dayOfWeek))];
  const [dragging, setDragging] = useState<{ pos: CellPos; cell: GridCell } | null>(null);
  const justDragged = useRef(false);
  // Kéo bắt đầu sau 6px để một cú nhấp không bị hiểu là kéo.
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

  const cellAt = (p: CellPos) => {
    const ci = grid.classes.findIndex((c) => c.classId === p.classId);
    return grid.rows.find((r) => r.dayOfWeek === p.day && r.periodSlotId === p.slotId)?.cells[ci] ?? null;
  };

  const onDragStart = (e: DragStartEvent) => {
    const pos = e.active.data.current as CellPos;
    const cell = cellAt(pos);
    if (cell) setDragging({ pos, cell });
  };

  const onDragEnd = (e: DragEndEvent) => {
    setDragging(null);
    justDragged.current = true;
    setTimeout(() => { justDragged.current = false; }, 0);
    const from = e.active.data.current as CellPos | undefined;
    const to = e.over?.data.current as CellPos | undefined;
    // Chỉ trong cột của cùng một lớp: thả sang lớp khác là bỏ qua.
    if (!edit || !from || !to || from.classId !== to.classId) return;
    if (from.day === to.day && from.slotId === to.slotId) return;
    edit.onMove(from, { day: to.day, slotId: to.slotId });
  };

  const table = (
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
                    {row.cells.map((cell, ci) => {
                      const pos = { classId: grid.classes[ci].classId, day, slotId: row.periodSlotId };
                      return edit
                        ? (
                          <EditableCell
                            key={pos.classId} pos={pos} cell={cell}
                            onClick={() => { if (!justDragged.current) edit.onCellClick(pos, cell); }}
                          />
                        )
                        : <Cell key={pos.classId} cell={cell} />;
                    })}
                  </tr>
                </Fragment>
              );
            });
          })}
        </tbody>
      </table>
    </div>
  );

  if (!edit) return table;
  return (
    <DndContext sensors={sensors} onDragStart={onDragStart} onDragEnd={onDragEnd} onDragCancel={() => setDragging(null)}>
      {table}
      <DragOverlay dropAnimation={null}>
        {dragging && <Chip cell={dragging.cell} className="tt-chip--overlay" />}
      </DragOverlay>
    </DndContext>
  );
}

export const SUBJECT_TONES: AccentName[] = ['green', 'sky', 'violet', 'amber', 'coral', 'teal'];

/** Cùng một môn luôn ra cùng một màu: băm viết tắt môn vào bảng màu nhấn. */
export function toneOf(abbr: string): AccentName {
  let h = 0;
  for (const ch of abbr) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return SUBJECT_TONES[h % SUBJECT_TONES.length];
}

function Chip({ cell, className = '' }: { cell: GridCell; className?: string }) {
  const a = accent[toneOf(cell.subjectAbbr)];
  return (
    <div className={`tt-chip ${className}`} style={{ '--tt-bg': a.bg, '--tt-fg': a.fg } as React.CSSProperties}>
      <div className="tt-subject">{cell.subjectAbbr}</div>
      <div className="tt-teacher">{cell.teacherAbbr}</div>
    </div>
  );
}

function Cell({ cell }: { cell: GridCell | null }) {
  if (!cell) return <td className="tt-cell tt-cell--empty" />;
  return (
    <td className="tt-cell" title={`${cell.subjectName} — ${cell.teacherName}`}>
      <Chip cell={cell} />
    </td>
  );
}

function EditableCell({ pos, cell, onClick }: { pos: CellPos; cell: GridCell | null; onClick: () => void }) {
  const { active } = useDndContext();
  const activeClassId = (active?.data.current as CellPos | undefined)?.classId;
  // Đang kéo một tiết của lớp khác thì ô này không nhận.
  const accepts = activeClassId === undefined || activeClassId === pos.classId;
  const { setNodeRef, isOver } = useDroppable({ id: cellId(pos), data: pos, disabled: !accepts });

  const classes = [
    'tt-cell', 'tt-cell--editable',
    !cell && 'tt-cell--empty',
    isOver && accepts && 'tt-cell--over',
    active && !accepts && 'tt-cell--nodrop',
    cell?.inConflict && 'tt-cell--conflict',
    cell?.edited && 'tt-cell--edited',
  ].filter(Boolean).join(' ');

  return (
    <td
      ref={setNodeRef} className={classes} onClick={onClick}
      title={cell ? `${cell.subjectName} — ${cell.teacherName}${cell.inConflict ? ' (trùng lịch giáo viên)' : ''}` : 'Nhấp để thêm tiết'}
    >
      {cell && <DraggableChip pos={pos} cell={cell} />}
    </td>
  );
}

function DraggableChip({ pos, cell }: { pos: CellPos; cell: GridCell }) {
  const { setNodeRef, listeners, attributes, isDragging } = useDraggable({ id: cellId(pos), data: pos });
  return (
    <div ref={setNodeRef} {...listeners} {...attributes} className="tt-draggable">
      <Chip cell={cell} className={isDragging ? 'tt-chip--dragging' : undefined} />
    </div>
  );
}
