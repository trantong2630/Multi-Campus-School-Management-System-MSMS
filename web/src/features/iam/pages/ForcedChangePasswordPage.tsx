import { Button } from 'antd';
import { AuthLayout } from '@/app/AuthLayout';
import { useAuth } from '@/app/AuthProvider';
import { ChangePasswordForm } from '../components/ChangePasswordForm';
import { logoutErrorTitle, logoutLabel } from '@/shared/lib/labels';
import { showError } from '@/shared/lib/feedback';

/** Tài khoản ở trạng thái Chờ đổi mật khẩu chỉ làm được việc này hoặc đăng xuất (BR-04). */
export function ForcedChangePasswordPage() {
  const { logout, loggingOut } = useAuth();
  return (
    <AuthLayout title="Đổi mật khẩu" subtitle="Đặt mật khẩu mới trước khi tiếp tục">
      <ChangePasswordForm />
      <div className="auth-layout__footer">
        <Button type="link" disabled={loggingOut} aria-busy={loggingOut}
          onClick={() => { void logout().catch((error: unknown) => showError(error, logoutErrorTitle)); }}>
          {logoutLabel(loggingOut)}
        </Button>
      </div>
    </AuthLayout>
  );
}
