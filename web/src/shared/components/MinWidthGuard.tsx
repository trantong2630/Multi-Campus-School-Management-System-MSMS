import { Monitor } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import './shared.css';

const MIN_WIDTH = 1280;

/** MSMS web là giao diện máy trạm: dưới 1280px hiện thông báo thay vì để layout vỡ (Design_Web §10). */
export function MinWidthGuard({ children }: { children: ReactNode }) {
  const [tooNarrow, setTooNarrow] = useState(window.innerWidth < MIN_WIDTH);

  useEffect(() => {
    const onResize = () => setTooNarrow(window.innerWidth < MIN_WIDTH);
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  if (!tooNarrow) return <>{children}</>;
  return (
    <div className="narrow-notice">
      <Monitor size={48} strokeWidth={1.5} />
      <h2>Cần màn hình rộng hơn</h2>
      <p>MSMS trên web dùng cho máy tính. Mở bằng màn hình rộng từ {MIN_WIDTH}px để tiếp tục.</p>
    </div>
  );
}
