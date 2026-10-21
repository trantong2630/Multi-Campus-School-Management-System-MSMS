import { Card, Descriptions } from 'antd';
import { useAuth } from '@/app/AuthProvider';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { accountTypeLabel } from '@/shared/lib/labels';

export function ProfilePage() {
  const { user } = useAuth();
  if (!user) return null;
  return (
    <>
      <PageHeader title="Hồ sơ cá nhân" />
      <Card>
        <Descriptions column={2} styles={{ label: { color: 'var(--c-text2)', width: 160 } }}>
          <Descriptions.Item label="Họ và tên">{user.fullName}</Descriptions.Item>
          <Descriptions.Item label="Mã hồ sơ">
            <span className="tabular">{user.personCode}</span>
          </Descriptions.Item>
          <Descriptions.Item label="Loại tài khoản">{accountTypeLabel[user.accountType]}</Descriptions.Item>
          <Descriptions.Item label="Trạng thái">
            <StatusBadge domain="account" value={user.status} />
          </Descriptions.Item>
        </Descriptions>
      </Card>
    </>
  );
}
