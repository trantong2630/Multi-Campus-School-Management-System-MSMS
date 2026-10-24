import { ConfigProvider } from 'antd';
import type { ReactNode } from 'react';
import './shared.css';

/** Thanh lọc là một hàng công cụ phẳng ngay trên danh sách, ô lọc dạng viên thuốc, không bọc card, không giấu vào drawer (Design_Web §8.3). */
export function FilterBar({ children }: { children: ReactNode }) {
  return (
    <ConfigProvider theme={{ token: { borderRadius: 22, borderRadiusLG: 22, borderRadiusSM: 18, controlHeight: 42 } }}>
      <div className="filter-bar" role="search">
        {children}
      </div>
    </ConfigProvider>
  );
}
