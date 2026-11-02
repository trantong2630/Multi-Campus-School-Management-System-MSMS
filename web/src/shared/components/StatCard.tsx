import { ArrowRight } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import './shared.css';

interface Props {
  label: string;
  /** Icon góc phải (Design_Web §9.4), nền nhạt theo tông; ô nhấn luôn nền trắng chữ xanh. */
  icon?: LucideIcon;
  tone?: AccentName;
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
export function StatCard({ label, icon: Icon, tone = 'green', value, hint, variant = 'default', attention = false, onClick, className, children }: Props) {
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
        {Icon && (
          <span className="stat-card__icon" style={variant === 'accent' ? undefined : { color: accent[tone].fg, background: accent[tone].bg }}>
            <Icon size={20} strokeWidth={2} />
          </span>
        )}
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
