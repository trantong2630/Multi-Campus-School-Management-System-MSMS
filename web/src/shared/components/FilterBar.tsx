import { Card } from 'antd';
import type { ReactNode } from 'react';
import './shared.css';

/** Thanh lọc là một card riêng ngay trên bảng, không giấu vào drawer (Design_Web §8.3). */
export function FilterBar({ children }: { children: ReactNode }) {
  return (
    <Card className="filter-bar" styles={{ body: { padding: 16 } }}>
      <div className="filter-bar__row">{children}</div>
    </Card>
  );
}
