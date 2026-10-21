import type { ReactNode } from 'react';
import './shared.css';

interface Props {
  title: string;
  description?: string;
  extra?: ReactNode;
}

export function PageHeader({ title, description, extra }: Props) {
  return (
    <div className="page-header">
      <div>
        <h1 className="page-header__title">{title}</h1>
        {description && <p className="page-header__desc">{description}</p>}
      </div>
      {extra && <div className="page-header__extra">{extra}</div>}
    </div>
  );
}
