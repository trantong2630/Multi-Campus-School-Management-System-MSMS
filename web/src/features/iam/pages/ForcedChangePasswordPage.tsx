import { Button } from 'antd';
import { AuthLayout } from '@/app/AuthLayout';
import { useAuth } from '@/app/AuthProvider';
import { ChangePasswordForm } from '../components/ChangePasswordForm';

/** Tài khoản ở trạng thái Chờ đổi mật khẩu chỉ làm được việc này hoặc đăng xuất (BR-04). */
export function ForcedChangePasswordPage() {
  const { logout } = useAuth();
  return (
    <AuthLayout title="Đổi mật khẩu lần đầu" description="Đặt mật khẩu mới để tiếp tục">
      <ChangePasswordForm />
      <div className="auth-layout__footer">
        <Button type="link" onClick={() => void logout()}>
          Đăng xuất
        </Button>
      </div>
    </AuthLayout>
  );
}
