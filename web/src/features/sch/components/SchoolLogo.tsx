import { GraduationCap } from 'lucide-react';
import { useState } from 'react';
import { useSchool } from '../hooks/useSchool';
import './school.css';

/** Logo trường; chưa có logo hoặc ảnh lỗi thì hiện biểu tượng mặc định trên nền xanh. */
export function SchoolLogo({ size = 32 }: { size?: number }) {
  const { logoSrc, name } = useSchool();
  const [failedSrc, setFailedSrc] = useState<string | null>(null);
  const failed = failedSrc === logoSrc;   // ảnh mới có đường dẫn khác nên tự thử lại, không cần đặt lại trạng thái

  if (logoSrc && !failed) {
    return (
      <img
        className="school-logo school-logo--image"
        style={{ width: size, height: size }}
        src={logoSrc}
        alt={`Logo ${name}`}
        onError={() => setFailedSrc(logoSrc)}
      />
    );
  }
  return (
    <span className="school-logo school-logo--fallback" style={{ width: size, height: size }} aria-hidden>
      <GraduationCap size={Math.round(size * 0.6)} />
    </span>
  );
}
