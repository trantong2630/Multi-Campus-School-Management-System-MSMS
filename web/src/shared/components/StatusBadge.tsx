import { CircleAlert, CircleCheck, CircleMinus, Clock, Info } from 'lucide-react';
import { statusGroup, statusLabel } from '@/shared/lib/labels';
import type { StatusDomain } from '@/shared/lib/labels';
import { badgeColor } from '@/shared/lib/tokens';
import './shared.css';

interface Props {
  domain: StatusDomain;
  value: string;
}

const icons = {
  active: CircleCheck,
  pending: Clock,
  neutral: CircleMinus,
  danger: CircleAlert,
  info: Info,
};

/** Trạng thái = thẻ bo góc nền đậm vừa + icon theo nhóm + chữ trắng, không viền (Design_Web §2.4, §8.5). Chữ luôn có, màu không bao giờ đứng một mình. */
export function StatusBadge({ domain, value }: Props) {
  const group = statusGroup(domain, value);
  const c = badgeColor[group];
  const Icon = icons[group];
  return (
    <span className="status-badge" style={{ color: c.fg, background: c.bg }}>
      <Icon size={14} strokeWidth={2.4} className="status-badge__icon" />
      {statusLabel(domain, value)}
    </span>
  );
}
