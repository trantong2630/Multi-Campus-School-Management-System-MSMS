import type { ReactNode } from 'react';
import './shared.css';

interface Props {
  title: string;
  extra?: ReactNode;
}

/**
 * Đầu trang (Design_Web §8.0): tiêu đề và
 * nút thao tác bên phải. Không icon trang trí.
 */
export function PageHeader({ title, extra }: Props) {
  return (
    <header className="page-header">
      <div className="page-header__text">
        <h1 className="page-header__title">{title}</h1>
      </div>
      {extra && <div className="page-header__extra">{extra}</div>}
    </header>
  );
}
