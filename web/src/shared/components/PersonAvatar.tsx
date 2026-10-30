import { Avatar } from 'antd';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import './shared.css';

interface Props {
  name: string;
  size?: number;
  /** Nền xám trung tính, dùng cho người đã nghỉ/không còn hiệu lực. */
  muted?: boolean;
}

const TONES: AccentName[] = ['green', 'sky', 'violet', 'amber', 'teal', 'coral'];

/** Cùng một tên luôn ra cùng một tông, nên avatar của một người không đổi màu giữa các màn. */
function toneOf(name: string): AccentName {
  let h = 0;
  for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return TONES[h % TONES.length];
}

/** Avatar chữ cái cuối của tên trên nền pastel theo tên; dùng chung cho mọi danh sách người. */
export function PersonAvatar({ name, size = 38, muted }: Props) {
  const initial = name.trim().split(/\s+/).at(-1)?.[0]?.toUpperCase() ?? '?';
  const tone = accent[toneOf(name)];
  return (
    <Avatar
      size={size}
      className="person-cell__avatar"
      style={{ ...(muted ? undefined : { color: tone.fg, background: tone.bg }), fontSize: Math.max(11, Math.round(size * 0.36)) }}
      aria-hidden
    >
      {initial}
    </Avatar>
  );
}
