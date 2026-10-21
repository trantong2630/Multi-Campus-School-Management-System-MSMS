import type { ReactNode } from 'react';
import './shared.css';

interface Props {
  eyebrow?: string;
  title: string;
  description?: string;
  extra?: ReactNode;
}

/** Banner chào đầu Trang chủ và Dashboard (Design_Web §8.5b). */
export function HeroBanner({ eyebrow, title, description, extra }: Props) {
  return (
    <section className="hero-banner">
      <div className="hero-banner__text">
        {eyebrow && <span className="hero-banner__eyebrow">{eyebrow}</span>}
        <h1 className="hero-banner__title">{title}</h1>
        {description && <p className="hero-banner__desc">{description}</p>}
      </div>
      {extra && <div className="hero-banner__extra">{extra}</div>}
      <span className="hero-banner__orb hero-banner__orb--a" aria-hidden />
      <span className="hero-banner__orb hero-banner__orb--b" aria-hidden />
    </section>
  );
}
