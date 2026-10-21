import { Avatar } from 'antd';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import './shared.css';

// Avatar chữ cái đầu, đổi màu pastel theo tên để danh sách có điểm nhấn nhưng ổn định giữa các lần tải.
const TINTS: AccentName[] = ['green', 'teal', 'sky', 'violet', 'amber', 'coral'];

function tintOf(name: string): AccentName {
  let h = 0;
  for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return TINTS[h % TINTS.length];
}

interface Props {
  name: string;
  /** Dòng phụ nhỏ dưới tên (mã hồ sơ, email...). */
  sub?: string;
  muted?: boolean;
}

/** Ô "người" trong bảng: avatar + tên + dòng phụ (Design_Web §8.2b). */
export function PersonCell({ name, sub, muted }: Props) {
  const t = accent[tintOf(name)];
  const initial = name.trim().split(/\s+/).at(-1)?.[0]?.toUpperCase() ?? '?';
  return (
    <div className={`person-cell${muted ? ' person-cell--muted' : ''}`}>
      <Avatar size={36} style={{ background: t.bg, color: t.fg, fontWeight: 600 }}>{initial}</Avatar>
      <div className="person-cell__text">
        <span className="person-cell__name">{name}</span>
        {sub && <span className="person-cell__sub tabular">{sub}</span>}
      </div>
    </div>
  );
}
