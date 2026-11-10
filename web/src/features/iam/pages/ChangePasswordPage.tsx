import { Card } from 'antd';
import { KeyRound, LaptopMinimal, Mail, ShieldCheck } from 'lucide-react';
import type { ReactNode } from 'react';
import { PageHeader } from '@/shared/components/PageHeader';
import { ChangePasswordForm, MIN_PASSWORD_LENGTH } from '../components/ChangePasswordForm';
import './change-password.css';

/** Lưu ý hiển thị bên cạnh form — chỉ mô tả lại các quy tắc Backend đang áp dụng (BR-04), không thêm điều kiện mới. */
const TIPS: { icon: ReactNode; title: string; text: string }[] = [
  { icon: <KeyRound size={18} />, title: `Ít nhất ${MIN_PASSWORD_LENGTH} ký tự`, text: 'Nên kết hợp chữ hoa, chữ thường, số và ký tự đặc biệt.' },
  { icon: <ShieldCheck size={18} />, title: 'Khác mật khẩu hiện tại', text: 'Mật khẩu mới trùng mật khẩu đang dùng sẽ không được chấp nhận.' },
  { icon: <LaptopMinimal size={18} />, title: 'Thiết bị khác đăng xuất', text: 'Sau khi đổi, các thiết bị khác phải đăng nhập lại; thiết bị này vẫn giữ phiên.' },
  { icon: <Mail size={18} />, title: 'Quên mật khẩu?', text: 'Dùng "Quên mật khẩu" ở màn đăng nhập để nhận mã qua email trong hồ sơ cá nhân.' },
];

export function ChangePasswordPage() {
  return (
    <>
      <PageHeader title="Đổi mật khẩu" />
      <div className="cpw-grid">
        <Card title="Mật khẩu đăng nhập" className="cpw-card">
          <ChangePasswordForm />
        </Card>
        <Card title="Lưu ý" className="cpw-card">
          <ul className="cpw-tips">
            {TIPS.map((t) => (
              <li key={t.title} className="cpw-tip">
                <span className="cpw-tip__icon" aria-hidden>{t.icon}</span>
                <span className="cpw-tip__body">
                  <span className="cpw-tip__title">{t.title}</span>
                  <span className="cpw-tip__text">{t.text}</span>
                </span>
              </li>
            ))}
          </ul>
        </Card>
      </div>
    </>
  );
}
