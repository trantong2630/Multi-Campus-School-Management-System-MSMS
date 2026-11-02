import { ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { InlineAlert } from '@/shared/components/InlineAlert';
import './school.css';

export interface DataGapItem {
  key: string;
  label: string;
  count: number | null | undefined;
  /** Màn hình sửa; chỉ hiện lối tắt khi người xem được mở màn đó. */
  to?: string;
}

/**
 * Dữ liệu cần bổ sung ngay trên màn danh mục: chỉ hiện mục có số > 0 và chỉ cho người sửa được
 * (mục có `to` mà người xem không vào được màn đó thì ẩn, để Hiệu phó chỉ xem không thấy việc của người khác).
 */
export function DataGapAlert({ items }: { items: DataGapItem[] }) {
  const { user } = useAuth();
  const shown = items.filter((i) => (i.count ?? 0) > 0 && (!i.to || (user && isPathAllowed(i.to, user))));
  if (!shown.length) return null;
  return (
    <InlineAlert
      type="warning"
      message="Dữ liệu cần bổ sung"
      description={(
        <ul className="gap-alert">
          {shown.map((i) => (
            <li key={i.key}>
              <b className="tabular">{i.count}</b> {i.label}
              {i.to && <Link to={i.to} className="gap-alert__go">Mở màn sửa <ArrowRight size={13} /></Link>}
            </li>
          ))}
        </ul>
      )}
    />
  );
}
