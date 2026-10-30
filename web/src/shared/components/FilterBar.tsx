import { ConfigProvider } from 'antd';
import type { ThemeConfig } from 'antd';
import type { ReactNode } from 'react';
import './shared.css';

/** Kiểu ô lọc dạng viên thuốc, dùng chung cho thanh lọc và các ô tìm/lọc đứng riêng ở những màn không dùng FilterBar. */
export const FILTER_THEME: ThemeConfig = { token: { borderRadius: 22, borderRadiusLG: 22, borderRadiusSM: 18, controlHeight: 42 } };

/** Thanh lọc là một hàng công cụ phẳng ngay trên danh sách, ô lọc dạng viên thuốc, không bọc card, không giấu vào drawer (Design_Web §8.3). */
export function FilterBar({ children }: { children: ReactNode }) {
  return (
    <ConfigProvider theme={FILTER_THEME}>
      <div className="filter-bar" role="search">
        {children}
      </div>
    </ConfigProvider>
  );
}
