import { Card } from 'antd';
import type { ReactNode } from 'react';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import './shared.css';

interface Props {
  label: string;
  value: ReactNode;
  icon: ReactNode;
  /** Màu nhấn của thẻ; `pastel` tô cả nền thẻ, mặc định thẻ trắng với ô icon màu (Design_Web §8.6). */
  tone?: AccentName;
  pastel?: boolean;
  onClick?: () => void;
}

/** Thẻ chỉ số của Dashboard: ô icon, số lớn 30/600, nhãn nhỏ bên dưới. */
export function StatCard({ label, value, icon, tone = 'green', pastel = false, onClick }: Props) {
  const t = accent[tone];
  const clickable = !!onClick;
  return (
    <Card
      className={`stat-card${clickable ? ' stat-card--clickable' : ''}`}
      style={pastel ? { background: t.bg } : undefined}
      onClick={onClick}
      role={clickable ? 'button' : undefined}
      tabIndex={clickable ? 0 : undefined}
      onKeyDown={clickable ? (e) => { if (e.key === 'Enter' || e.key === ' ') onClick?.(); } : undefined}
    >
      <span className="stat-card__icon" style={{ background: pastel ? 'var(--c-surface)' : t.bg, color: t.fg }}>{icon}</span>
      <div className="stat-card__value tabular">{value}</div>
      <div className="stat-card__label">{label}</div>
    </Card>
  );
}
