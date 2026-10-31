import { Button } from 'antd';
import { ShieldOff } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { color } from '@/shared/lib/tokens';
import { forbiddenLabel } from '@/shared/lib/labels';

/** Mở thẳng đường dẫn ngoài quyền: giao diện chỉ chặn cho đỡ nhầm, Backend mới là nơi kiểm quyền thật (BR-01). */
export function Forbidden() {
  const navigate = useNavigate();
  return (
    <div className="empty-state" style={{ paddingTop: 96 }}>
      <ShieldOff size={48} strokeWidth={1.25} color={color.disabled} />
      <h2 style={{ margin: 0, fontSize: 16, fontWeight: 600, color: color.text1 }}>{forbiddenLabel.title}</h2>
      <p>{forbiddenLabel.description}</p>
      <Button type="primary" onClick={() => navigate('/')}>{forbiddenLabel.home}</Button>
    </div>
  );
}
