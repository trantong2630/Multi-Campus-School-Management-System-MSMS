import { Avatar } from 'antd';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import './shared.css';

interface Props {
  name: string;
  /** Dòng phụ nhỏ dưới tên (mã hồ sơ, email...). */
  sub?: string;
  muted?: boolean;
}

const TONES: AccentName[] = ['green', 'sky', 'violet', 'amber', 'teal', 'coral'];

/** Cùng một tên luôn ra cùng một tông, nên avatar của một người không đổi màu giữa các màn. */
function toneOf(name: string): AccentName {
  let h = 0;
  for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return TONES[h % TONES.length];
}

/** Ô "người" trong bảng: avatar chữ cái đầu trên nền pastel theo tên, tên, dòng phụ (Design_Web §8.2b). */
export function PersonCell({ name, sub, muted }: Props) {
  const initial = name.trim().split(/\s+/).at(-1)?.[0]?.toUpperCase() ?? '?';
  const tone = accent[toneOf(name)];
  return (
    <div className={`person-cell${muted ? ' person-cell--muted' : ''}`}>
      <Avatar size={38} className="person-cell__avatar" style={muted ? undefined : { color: tone.fg, background: tone.bg }}>{initial}</Avatar>
      <div className="person-cell__text">
        <span className="person-cell__name">{name}</span>
        {sub && <span className="person-cell__sub tabular">{sub}</span>}
      </div>
    </div>
  );
}
