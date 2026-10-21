import { statusGroup, statusLabel } from '@/shared/lib/labels';
import type { StatusDomain } from '@/shared/lib/labels';
import { badgeColor } from '@/shared/lib/tokens';
import './shared.css';

interface Props {
  domain: StatusDomain;
  value: string;
}

/** Badge = nền nhạt + chữ đậm, không viền, không icon (Design_Web §2.4, §8.5). */
export function StatusBadge({ domain, value }: Props) {
  const c = badgeColor[statusGroup(domain, value)];
  return (
    <span className="status-badge" style={{ color: c.fg, background: c.bg }}>
      {statusLabel(domain, value)}
    </span>
  );
}
