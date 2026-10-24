import type { ReactNode } from 'react';
import './shared.css';

interface Props {
  eyebrow?: string;
  title: string;
  description?: string;
  extra?: ReactNode;
}

/**
 * Lời chào đầu Trang chủ (Design_Web §8.5b): dòng trường · năm học chữ hoa nhỏ màu xám, lời chào chữ lớn,
 * dòng ngày hôm nay, logo trường bên phải. Không nền màu, không hình trang trí.
 */
export function HeroBanner({ eyebrow, title, description, extra }: Props) {
  return (
    <section className="greeting">
      <div className="greeting__text">
        {eyebrow && <span className="greeting__eyebrow">{eyebrow}</span>}
        <h1 className="greeting__title">{title}</h1>
        {description && <p className="greeting__desc">{description}</p>}
      </div>
      {extra && <div className="greeting__extra">{extra}</div>}
    </section>
  );
}
