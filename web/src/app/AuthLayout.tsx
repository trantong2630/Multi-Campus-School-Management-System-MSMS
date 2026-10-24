import { ConfigProvider } from 'antd';
import { useEffect, type ReactNode } from 'react';
import { BrandLogo } from '@/shared/components/BrandLogo';
import './auth-layout.css';

const YEAR = new Date().getFullYear();

interface Props {
  /** Tiêu đề lớn, ví dụ lời chào. */
  title: string;
  /** Một dòng ngay dưới tiêu đề, nói người dùng cần làm gì. */
  subtitle?: string;
  children: ReactNode;
}

/** Khung chung cho mọi màn chưa đăng nhập: một thẻ liền khối, trái là form trắng căn trái, phải là tấm lụa xanh với wordmark MSMS (Design_Web §6.0). */
export function AuthLayout({ title, subtitle, children }: Props) {
  useEffect(() => {
    document.title = 'MSMS';
  }, []);

  return (
    <div className="auth-layout">
      <div className="auth-card">
        <section className="auth-card__main">
          <div className="auth-card__form">
            <h1 className="auth-card__title">{title}</h1>
            {subtitle && <p className="auth-card__subtitle">{subtitle}</p>}
            <ConfigProvider componentSize="large">{children}</ConfigProvider>
          </div>
          <p className="auth-card__caption">© {YEAR} · Hệ thống quản lý trường học đa phân hiệu</p>
        </section>

        <section className="auth-art" aria-hidden>
          <SilkArt />
          <BrandLogo tone="white" size={104} />
        </section>
      </div>
    </div>
  );
}

/** Tấm phải: phong cảnh đơn giản, trời aqua nhạt chuyển xanh non, mặt trời, mây, ba lớp đồi xanh lá và hai cái cây; lớp đồi gần đậm nhất, đặt dưới wordmark. Màu lấy từ CSS. */
function SilkArt() {
  return (
    <svg className="auth-art__scene" viewBox="0 0 800 720" preserveAspectRatio="xMidYMid slice">
      <defs>
        <linearGradient id="art-sky" x1="0" y1="0" x2="0" y2="1">
          <stop className="art-sky--top" offset="0" />
          <stop className="art-sky--bottom" offset="1" />
        </linearGradient>
      </defs>

      <rect width="800" height="720" fill="url(#art-sky)" />

      <circle className="art-sun" cx="620" cy="130" r="92" opacity="0.25" />
      <circle className="art-sun" cx="620" cy="130" r="58" opacity="0.8" />

      <g className="art-cloud" opacity="0.92">
        <rect x="150" y="150" width="130" height="34" rx="17" />
        <circle cx="195" cy="148" r="30" />
        <circle cx="236" cy="136" r="38" />
      </g>
      <g className="art-cloud" opacity="0.7">
        <rect x="470" y="270" width="90" height="24" rx="12" />
        <circle cx="500" cy="268" r="20" />
        <circle cx="528" cy="260" r="26" />
      </g>

      <path className="art-hill--far" d="M0,430 C160,380 320,410 480,440 S700,470 800,420 L800,720 L0,720 Z" />
      <path className="art-hill--mid" d="M0,520 C170,470 330,540 510,525 S720,480 800,495 L800,720 L0,720 Z" />

      <g>
        <line className="art-tree__trunk" x1="150" y1="545" x2="150" y2="488" />
        <circle className="art-tree__crown" cx="150" cy="462" r="40" />
        <line className="art-tree__trunk" x1="240" y1="552" x2="240" y2="515" />
        <circle className="art-tree__crown" cx="240" cy="498" r="26" />
      </g>

      <path className="art-hill--near" d="M0,665 C200,645 380,605 560,575 S740,545 800,535 L800,720 L0,720 Z" />
      <circle className="art-bush" cx="80" cy="672" r="26" />
      <circle className="art-bush" cx="116" cy="680" r="20" />
    </svg>
  );
}
