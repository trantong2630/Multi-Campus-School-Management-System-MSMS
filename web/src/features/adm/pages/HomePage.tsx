import { Avatar, Card } from 'antd';
import dayjs from 'dayjs';
import { Inbox } from 'lucide-react';
import { useAuth } from '@/app/AuthProvider';
import { HeroBanner } from '@/shared/components/HeroBanner';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { accountTypeLabel } from '@/shared/lib/labels';
import { color } from '@/shared/lib/tokens';
import './home.css';

export function HomePage() {
  const { user } = useAuth();
  if (!user) return null;
  const given = user.fullName.trim().split(/\s+/).at(-1) ?? user.fullName;

  return (
    <>
      <HeroBanner eyebrow={dayjs().format('dddd, DD/MM/YYYY')} title={`Chào, ${given}`} description="Tổng quan tài khoản đang đăng nhập." />

      <Card className="home-welcome">
        <Avatar size={56} style={{ background: color.green100, color: color.green700, fontWeight: 600, fontSize: 22 }}>
          {given[0]?.toUpperCase()}
        </Avatar>
        <div className="home-welcome__info">
          <div className="home-welcome__name">{user.fullName}</div>
          <div className="home-welcome__meta">
            {accountTypeLabel[user.accountType]} · <span className="tabular">{user.personCode}</span>
          </div>
        </div>
        <StatusBadge domain="account" value={user.status} />
      </Card>

      <Card className="home-empty">
        <Inbox size={48} strokeWidth={1.25} color={color.disabled} />
        <p>Chưa có chức năng nào được mở cho tài khoản này.</p>
      </Card>
    </>
  );
}
