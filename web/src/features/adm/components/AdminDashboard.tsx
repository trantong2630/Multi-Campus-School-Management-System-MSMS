import { useQuery } from '@tanstack/react-query';
import { Card, Skeleton } from 'antd';
import dayjs from 'dayjs';
import { ArrowRight, CircleCheck, ClipboardList, KeyRound, Lock } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { DataTable } from '@/shared/components/DataTable';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatCard } from '@/shared/components/StatCard';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { color } from '@/shared/lib/tokens';
import { admApi } from '../api';
import type { AccountTypeRow } from '../types';
import { RateBar } from './DashboardParts';
import { fmt } from './dashboardFormat';

/** Thiếu sót dữ liệu: nhãn và màn hình School Admin mở ra để sửa. Màn chưa triển khai thì chỉ hiện số. */
const gapMeta: Record<string, { label: string; to: string }> = {
  ClassesWithoutHomeroom: { label: 'Lớp chưa có giáo viên chủ nhiệm', to: '/ppl/responsibilities' },
  StudentsWithoutClass: { label: 'Học sinh đang học chưa ghi danh vào lớp', to: '/sch/classes' },
  StudentsWithoutParent: { label: 'Học sinh chưa liên kết phụ huynh', to: '/ppl/students' },
  StaffWithoutAccount: { label: 'Cán bộ chưa có tài khoản', to: '/adm/accounts' },
  TeachersWithoutDepartment: { label: 'Giáo viên chưa thuộc tổ chuyên môn', to: '/ppl/staff' },
  DepartmentsWithoutHead: { label: 'Tổ chuyên môn chưa có tổ trưởng', to: '/ppl/responsibilities' },
  CampusesWithoutLeader: { label: 'Phân hiệu chưa có hiệu phó phụ trách', to: '/ppl/responsibilities' },
};

const categoryLabel: Record<string, string> = {
  Account: 'Tài khoản',
  TimetablePublish: 'Phát hành thời khóa biểu',
  ScheduleAdjust: 'Điều chỉnh lịch dạy',
};

/**
 * Dashboard School Admin: tài khoản, chất lượng dữ liệu, hoạt động hệ thống, tiến độ thời khóa biểu. Không có số liệu giảng dạy.
 * Quy mô và cấu trúc trường (phân hiệu, tổ, môn, lớp) xem trên các màn Thiết lập, nơi đồng thời sửa được.
 */
export function AdminDashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const data = useQuery({ queryKey: ['adm', 'dashboard', 'admin'], queryFn: admApi.adminDashboard });
  const d = data.data;
  const can = (path: string) => !!user && isPathAllowed(path, user);
  const go = (path: string) => (can(path) ? () => navigate(path) : undefined);

  const accounts = d?.accounts ?? [];
  const totalAccounts = accounts.reduce((s, a) => s + a.total, 0);
  const lockedAccounts = accounts.reduce((s, a) => s + a.locked, 0);
  const activeAccounts = accounts.reduce((s, a) => s + a.active, 0);
  const pendingAccounts = accounts.reduce((s, a) => s + a.pendingPasswordChange, 0);
  const neverLoggedIn = accounts.reduce((s, a) => s + a.neverLoggedIn, 0);
  const gapTotal = d?.gaps.reduce((s, g) => s + g.count, 0) ?? 0;
  const activityTotal = d?.activity.reduce((s, a) => s + a.count, 0) ?? 0;
  const semester = d?.semesterName
    ? `${d.semesterName} (${dayjs(d.semesterStart).format('DD/MM')} – ${dayjs(d.semesterEnd).format('DD/MM/YYYY')})`
    : 'Ngoài học kỳ';

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={['Toàn trường', d?.academicYearName && `Năm học ${d.academicYearName}`, semester, dayjs().format('DD/MM/YYYY')].filter(Boolean).join(' · ')}
      />

      <div className="dash-bento">
        <StatCard variant="accent" icon={CircleCheck} label="Tài khoản" value={fmt(d && totalAccounts)}
          hint={d ? `${fmt(activeAccounts)} đang hoạt động` : undefined} onClick={go('/adm/accounts')} />
        <StatCard icon={KeyRound} tone="sky" label="Chờ đổi mật khẩu" value={fmt(d && pendingAccounts)}
          hint={d ? `${fmt(neverLoggedIn)} tài khoản chưa đăng nhập lần nào` : undefined} onClick={go('/adm/accounts')} />
        <StatCard icon={Lock} tone="coral" label="Tài khoản đang khóa" value={fmt(d && lockedAccounts)}
          attention={lockedAccounts > 0} hint={d && !lockedAccounts ? 'Không có tài khoản bị khóa' : undefined} onClick={go('/adm/accounts')} />
        <StatCard icon={ClipboardList} tone="amber" label="Dữ liệu cần bổ sung" value={fmt(d && gapTotal)}
          attention={gapTotal > 0} hint={d ? (gapTotal ? `${d.gaps.filter((g) => g.count > 0).length} loại thiếu sót` : 'Dữ liệu đã đầy đủ') : undefined} />

        <Card title="Dữ liệu cần bổ sung" className="dash-card dash-card--list dash-bento__half"
          extra={<span className={`dash-card__meta${gapTotal ? ' dash-cell--warn' : ''}`}>{gapTotal ? `${fmt(gapTotal)} mục` : 'Đầy đủ'}</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 6 }} title={false} /> : (
            <ul className="dash-list">
              {d?.gaps.map((g) => {
                const meta = gapMeta[g.key] ?? { label: g.key, to: '' };
                const link = g.count > 0 && can(meta.to);
                return (
                  <li key={g.key} className={`dash-list__row${link ? ' dash-list__row--link' : ''}`} onClick={link ? () => navigate(meta.to) : undefined}>
                    <span className={`dash-dot dash-dot--${g.count > 0 ? 'warn' : 'ok'}`} />
                    <div className="dash-list__main"><b>{meta.label}</b></div>
                    <span className={`dash-list__count tabular${g.count > 0 ? ' dash-cell--warn' : ''}`}>{fmt(g.count)}</span>
                    {link ? <ArrowRight size={14} className="dash-list__go" /> : <span className="dash-list__go" />}
                  </li>
                );
              })}
            </ul>
          )}
        </Card>

        <Card title="Tài khoản theo loại" className="dash-card dash-card--table dash-bento__half">
          <DataTable<AccountTypeRow>
            rowKey="accountType" dataSource={accounts} loading={data.isLoading} pagination={false}
            columns={[
              { title: 'Loại', dataIndex: 'accountType', render: (v: string) => <b>{accountTypeLabel[v as AccountType] ?? v}</b> },
              { title: 'Tổng', dataIndex: 'total', align: 'right', className: 'tabular' },
              { title: 'Hoạt động', dataIndex: 'active', align: 'right', className: 'tabular' },
              { title: 'Chờ đổi mật khẩu', dataIndex: 'pendingPasswordChange', align: 'right', className: 'tabular' },
              { title: 'Đang khóa', dataIndex: 'locked', align: 'right', className: 'tabular', render: (v: number) => <span className={v ? 'dash-cell--warn' : undefined}>{v}</span> },
              { title: 'Chưa đăng nhập', dataIndex: 'neverLoggedIn', align: 'right', className: 'tabular' },
            ]}
          />
        </Card>

        <Card title="Hoạt động hệ thống 14 ngày" className="dash-card dash-bento__half"
          extra={<span className="dash-card__meta tabular">{fmt(activityTotal)} thao tác được ghi nhật ký</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 6 }} title={false} /> : (
            <>
              <ResponsiveContainer width="100%" height={200}>
                <BarChart data={d?.activity.map((a) => ({ ...a, label: dayjs(a.date).format('DD/MM') }))} margin={{ top: 12, right: 0, bottom: 0, left: -20 }}>
                  <CartesianGrid vertical={false} stroke={color.border} strokeDasharray="2 4" />
                  <XAxis dataKey="label" tickLine={false} axisLine={{ stroke: color.borderStrong }} tick={{ fill: color.text3, fontSize: 11 }} interval={1} />
                  <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: color.text3, fontSize: 12 }} />
                  <Tooltip cursor={{ fill: color.sunken }} content={({ active, payload }) => active && payload?.length ? (
                    <div className="dash-tip"><b>{dayjs((payload[0].payload as { date: string }).date).format('dddd, DD/MM')}</b>
                      <span className="tabular">{fmt(Number(payload[0].value))} thao tác</span></div>
                  ) : null} />
                  <Bar dataKey="count" fill={color.violetFg} radius={[6, 6, 0, 0]} maxBarSize={20} />
                </BarChart>
              </ResponsiveContainer>
              <div className="dash-tags">
                {d?.activityByCategory.length ? d.activityByCategory.map((c) => (
                  <span key={c.category} className="dash-tag">{categoryLabel[c.category] ?? c.category}<b className="tabular">{fmt(c.count)}</b></span>
                )) : <span className="dash-card__meta">Chưa có thao tác nào trong 14 ngày.</span>}
              </div>
            </>
          )}
        </Card>

        <Card title="Thời khóa biểu học kỳ hiện tại" className="dash-card dash-card--list dash-bento__half"
          extra={<span className="dash-card__meta">Lớp đã có bản phát hành</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 4 }} title={false} /> : !d?.semesterName ? (
            <div className="dash-empty">Hôm nay không nằm trong học kỳ nào.</div>
          ) : (
            <ul className="dash-list">
              {d.timetables.map((t) => (
                <li key={t.campusId} className="dash-list__row">
                  <div className="dash-list__main"><b>{t.name}</b><span className="tabular">{t.publishedClassCount}/{t.classCount} lớp</span></div>
                  <RateBar rate={t.classCount ? Math.round((t.publishedClassCount / t.classCount) * 100) : null} width={140} />
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </>
  );
}
