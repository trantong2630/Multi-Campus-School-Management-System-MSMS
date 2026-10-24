import { Input, Modal } from 'antd';
import type { InputRef } from 'antd';
import { CornerDownLeft, Search } from 'lucide-react';
import { useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { MenuGroup } from './menu';

interface Entry {
  key: string;
  label: string;
  group?: string;
  path: string;
}

/** Bỏ dấu và đưa về chữ thường để gõ "can bo" vẫn ra "Cán bộ". */
const fold = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();

interface Props {
  open: boolean;
  onClose: () => void;
  groups: MenuGroup[];
}

/** Tìm nhanh màn hình (Ctrl+K): chỉ liệt kê màn hình có trên menu của người đang đăng nhập, Enter để mở (Design_Web §6.3). */
export function CommandPalette({ open, onClose, groups }: Props) {
  const inputRef = useRef<InputRef>(null);
  // destroyOnHidden: mỗi lần mở là một PaletteBody mới nên ô tìm kiếm luôn trống.
  return (
    <Modal
      open={open} onCancel={onClose} footer={null} closable={false} width={560} className="cmdk"
      styles={{ body: { padding: 0 } }} style={{ top: 96 }}
      afterOpenChange={(v) => v && inputRef.current?.focus()} destroyOnHidden
    >
      <PaletteBody inputRef={inputRef} onClose={onClose} groups={groups} />
    </Modal>
  );
}

function PaletteBody({ inputRef, onClose, groups }: Omit<Props, 'open'> & { inputRef: React.RefObject<InputRef | null> }) {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);

  const entries = useMemo<Entry[]>(() => {
    const list: Entry[] = [];
    for (const g of groups) {
      if (g.path) list.push({ key: g.key, label: g.label, path: g.path });
      g.children?.forEach((c) => list.push({ key: c.key, label: c.label, group: g.label, path: c.path }));
    }
    list.push(
      { key: 'profile', label: 'Hồ sơ cá nhân', group: 'Tài khoản', path: '/profile' },
      { key: 'password', label: 'Đổi mật khẩu', group: 'Tài khoản', path: '/change-password' },
    );
    return list;
  }, [groups]);

  const results = useMemo(() => {
    const q = fold(query.trim());
    return q ? entries.filter((e) => fold(`${e.group ?? ''} ${e.label}`).includes(q)) : entries;
  }, [entries, query]);

  const go = (e: Entry | undefined) => {
    if (!e) return;
    onClose();
    navigate(e.path);
  };

  const onKeyDown = (ev: React.KeyboardEvent) => {
    if (ev.key === 'ArrowDown') { ev.preventDefault(); setActive((i) => Math.min(i + 1, results.length - 1)); }
    else if (ev.key === 'ArrowUp') { ev.preventDefault(); setActive((i) => Math.max(i - 1, 0)); }
    else if (ev.key === 'Enter') { ev.preventDefault(); go(results[active]); }
  };

  return (
    <>
      <div className="cmdk__search">
        <Search size={18} />
        <Input
          ref={inputRef} variant="borderless" placeholder="Tìm màn hình, ví dụ: cán bộ, thời khóa biểu…"
          value={query} onChange={(e) => { setQuery(e.target.value); setActive(0); }} onKeyDown={onKeyDown}
        />
        <kbd className="kbd">Esc</kbd>
      </div>
      {results.length === 0 ? (
        <div className="cmdk__empty">Không có màn hình nào khớp "{query}".</div>
      ) : (
        <ul className="cmdk__list" role="listbox">
          {results.map((e, i) => (
            <li
              key={e.key} role="option" aria-selected={i === active}
              className={`cmdk__item${i === active ? ' cmdk__item--active' : ''}`}
              onMouseEnter={() => setActive(i)} onClick={() => go(e)}
            >
              <span className="cmdk__label">
                {e.group && <span className="cmdk__group">{e.group}</span>}
                {e.label}
              </span>
              {i === active && <CornerDownLeft size={14} className="cmdk__enter" />}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
