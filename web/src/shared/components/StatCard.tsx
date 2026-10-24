import { ArrowRight } from 'lucide-react';
import type { ReactNode } from 'react';
import './shared.css';

interface Props {
  label: string;
  value: ReactNode;
  /** Dòng phụ dưới số: bối cảnh suy ra từ dữ liệu (bình quân, ngày…), không lặp lại nhãn. */
  hint?: ReactNode;
  /** `accent`: ô nhấn nền xanh chữ trắng, mỗi màn tối đa một ô. */
  variant?: 'default' | 'accent';
  /** Chỉ số "cần chú ý" đang lớn hơn 0: chấm hổ phách trước nhãn, số đổi màu hổ phách đậm. */
  attention?: boolean;
  onClick?: () => void;
  /** Lớp vị trí trong lưới bento (vd. ô cao hai hàng). */
  className?: string;
  /** Nội dung phụ dưới số (vd. phân bổ theo khối trong ô nhấn). */
  children?: ReactNode;
}

/** Ô chỉ số dạng bento của Dashboard (Design_Web §8.6): nhãn, số lớn, dòng phụ, lối "Xem". */
export function StatCard({ label, value, hint, variant = 'default', attention = false, onClick, className, children }: Props) {
  const clickable = !!onClick;
  const cls = [
    'bento-tile', 'stat-card',
    variant === 'accent' && 'bento-tile--accent',
    clickable && 'stat-card--clickable',
    attention && 'stat-card--attention',
    className,
  ].filter(Boolean).join(' ');
  return (
    <div
      className={cls}
      onClick={onClick}
      role={clickable ? 'button' : undefined}
      tabIndex={clickable ? 0 : undefined}
      onKeyDown={clickable ? (e) => { if (e.key === 'Enter' || e.key === ' ') onClick?.(); } : undefined}
    >
      <div className="stat-card__top">
        <div className="stat-card__label">{label}</div>
      </div>
      <div className="stat-card__value tabular">{value}</div>
      {children}
      <div className="stat-card__foot">
        <span>{hint}</span>
        {clickable && <span className="stat-card__go">Xem <ArrowRight size={13} /></span>}
      </div>
    </div>
  );
}
