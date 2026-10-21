import type { ReactNode } from 'react';
import { BrandLogo } from '@/shared/components/BrandLogo';
import './auth-layout.css';

interface Props {
  title: string;
  description?: string;
  children: ReactNode;
}

/** Khung chung cho mọi màn chưa đăng nhập: trái là logo MSMS, phải là tấm xanh bo góc chứa form (Design_Web §6.3). */
export function AuthLayout({ title, description, children }: Props) {
  return (
    <div className="auth-layout">
      <section className="auth-layout__brand">
        <BrandLogo tone="green" size={96} />
      </section>
      <section className="auth-layout__panel">
        <div className="auth-layout__form">
          <h1 className="auth-layout__title">{title}</h1>
          {description && <p className="auth-layout__desc">{description}</p>}
          {children}
        </div>
      </section>
    </div>
  );
}
