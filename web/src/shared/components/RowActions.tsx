import { Button, Dropdown, Tooltip } from 'antd';
import type { MenuProps } from 'antd';
import { Ellipsis } from 'lucide-react';
import type { ReactNode } from 'react';

export interface RowAction {
  key: string;
  label: string;
  icon: ReactNode;
  onClick: () => void;
  danger?: boolean;
  hidden?: boolean;
}

/** Cột thao tác: icon-only có tooltip, tối đa 3 icon rồi gộp vào "…" (Design_Web §8.2). */
export function RowActions({ actions }: { actions: RowAction[] }) {
  const visible = actions.filter((a) => !a.hidden);
  const inline = visible.length > 3 ? visible.slice(0, 2) : visible;
  const overflow = visible.length > 3 ? visible.slice(2) : [];

  const menu: MenuProps['items'] = overflow.map((a) => ({
    key: a.key, icon: a.icon, label: a.label, danger: a.danger, onClick: a.onClick,
  }));

  return (
    <div className="row-actions">
      {inline.map((a) => (
        <Tooltip key={a.key} title={a.label}>
          <Button type="text" className="icon-btn" danger={a.danger} aria-label={a.label} icon={a.icon} onClick={a.onClick} />
        </Tooltip>
      ))}
      {overflow.length > 0 && (
        <Dropdown menu={{ items: menu }} trigger={['click']}>
          <Button type="text" className="icon-btn" aria-label="Thao tác khác" icon={<Ellipsis size={18} />} />
        </Dropdown>
      )}
    </div>
  );
}
