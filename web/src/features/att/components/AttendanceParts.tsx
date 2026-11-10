import { badgeColor } from '@/shared/lib/tokens';
import type { CountItem } from '../attFormat';
import type { AttendanceSession } from '../types';
import './att.css';

/** Dải số đếm theo trạng thái ở đầu màn (Design_Web §9.3). Màu chỉ là chấm phụ, luôn kèm chữ. */
export function CountStrip({ items, total }: { items: CountItem[]; total?: number }) {
  return (
    <div className="att-counts" aria-live="polite">
      {total !== undefined && (
        <div className="att-count att-count--total">
          <span className="att-count__label">Sĩ số</span>
          <span className="att-count__value tabular">{total}</span>
        </div>
      )}
      {items.map((i) => (
        <div key={i.key} className="att-count">
          <span className="att-count__label">
            <span className="att-count__dot" style={{ background: badgeColor[i.group].bg }} aria-hidden />
            {i.label}
          </span>
          <span className="att-count__value tabular">{i.value}</span>
        </div>
      ))}
    </div>
  );
}

/** Ô "đã điểm danh x/y" của danh sách tiết. */
export function MarkedCell({ s }: { s: AttendanceSession }) {
  if (s.status !== 'Scheduled') return <span className="muted">—</span>;
  const done = s.rosterCount > 0 && s.markedCount >= s.rosterCount;
  return (
    <span className={`att-marked tabular${done ? ' att-marked--done' : ''}`}>
      {s.markedCount}/{s.rosterCount}
    </span>
  );
}
