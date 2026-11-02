import type { ReactNode } from 'react';
import './shared.css';

/**
 * Dải ô chỉ số đầu màn danh mục: các StatCard (Design_Web §8.6) xếp một hàng, cách bảng bên dưới 16px.
 * Màn danh mục không dùng ô nhấn (ô nhấn dành cho Dashboard và Trang chủ).
 */
export function StatStrip({ children }: { children: ReactNode }) {
  return <div className="stat-strip">{children}</div>;
}
