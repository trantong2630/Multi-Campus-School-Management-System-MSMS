import { Card } from 'antd';
import { PageHeader } from '@/shared/components/PageHeader';
import { ChangePasswordForm } from '../components/ChangePasswordForm';

export function ChangePasswordPage() {
  return (
    <>
      <PageHeader title="Đổi mật khẩu" description="Mật khẩu mới có ít nhất 8 ký tự." />
      <Card style={{ maxWidth: 520 }}>
        <ChangePasswordForm />
      </Card>
    </>
  );
}
