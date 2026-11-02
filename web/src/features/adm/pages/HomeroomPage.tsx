import { useQuery } from '@tanstack/react-query';
import { Card, Select, Skeleton } from 'antd';
import dayjs from 'dayjs';
import { BookOpen, CalendarX2, FileText, HeartHandshake, KeyRound, UserRoundX, UsersRound } from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { DataTable } from '@/shared/components/DataTable';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatCard } from '@/shared/components/StatCard';
import { formatDate } from '@/shared/lib/format';
import { admApi } from '../api';
import { AttentionStrip, SubjectRateList } from '../components/DashboardParts';
import { fmt, pct } from '../components/dashboardFormat';
import type { HomeroomStudentRow } from '../types';
import './dashboard.css';

const genderLabel: Record<string, string> = { Male: 'Nam', Female: 'Nữ', Other: 'Khác' };

/**
 * Màn theo dõi lớp chủ nhiệm: sĩ số, đơn nghỉ, phụ huynh và giáo viên bộ môn của lớp (phạm vi chủ nhiệm, Business Spec §2.3).
 * Bài tập chỉ có tỷ lệ nộp tổng hợp theo môn, không có bài nộp hay điểm từng học sinh.
 */
export function HomeroomPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const classId = params.get('classId') ? Number(params.get('classId')) : undefined;
  const data = useQuery({ queryKey: ['adm', 'homeroom', classId], queryFn: () => admApi.homeroom(classId) });
  const d = data.data;
  const can = (path: string) => (user && isPathAllowed(path, user) ? path : undefined);
  const go = (path: string) => (can(path) ? () => navigate(path) : undefined);

  return (
    <>
      <PageHeader
        title={d ? `Lớp chủ nhiệm ${d.className}` : 'Lớp chủ nhiệm'}
        description={d ? [d.campusName, `Khối ${d.gradeLevel}`, d.academicYearName && `Năm học ${d.academicYearName}`, dayjs().format('DD/MM/YYYY')].filter(Boolean).join(' · ') : undefined}
        extra={d && d.classes.length > 1 && (
          <Select className="dash-scope" value={d.classId} onChange={(v: number) => setParams({ classId: String(v) })}
            options={d.classes.map((c) => ({ value: c.id, label: `Lớp ${c.name}` }))} />
        )}
      />

      <div className="dash-bento">
        <StatCard variant="accent" icon={UsersRound} label="Sĩ số" value={fmt(d?.studentCount)}
          hint={d ? `${d.maleCount} nam · ${d.femaleCount} nữ` : undefined} />
        <StatCard icon={FileText} tone="amber" label="Đơn nghỉ chờ duyệt" value={fmt(d?.pendingLeave)}
          attention={!!d?.pendingLeave} hint={d?.pendingLeave ? 'Cần duyệt sớm' : 'Không có đơn chờ'} onClick={go('/par/leave-requests')} />
        <StatCard icon={CalendarX2} tone="sky" label="Lượt nghỉ 30 ngày" value={fmt(d?.leaveLast30Days)}
          hint="Đơn đã duyệt và đang chờ" />
        <StatCard icon={BookOpen} tone="violet" label="Tỷ lệ nộp bài" value={pct(d?.submissionRate)}
          hint={d ? `${d.subjects.length} môn đã giao bài` : undefined} />

        {d && (
          <AttentionStrip title="Cần theo dõi" items={[
            { key: 'leave', label: 'đơn nghỉ chờ duyệt', count: d.pendingLeave, icon: FileText, to: can('/par/leave-requests') },
            { key: 'noparent', label: 'học sinh chưa liên kết phụ huynh', count: d.studentsWithoutParent, icon: UserRoundX },
            { key: 'noaccount', label: 'học sinh có phụ huynh chưa có tài khoản', count: d.parentsWithoutAccount, icon: KeyRound },
          ]} />
        )}

        <Card title="Nộp bài theo môn" className="dash-card dash-bento__half"
          extra={<span className="dash-card__meta">Tỷ lệ cả lớp, trên bài đã đóng</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 5 }} title={false} /> : <SubjectRateList subjects={d?.subjects ?? []} />}
        </Card>

        <Card title="Giáo viên bộ môn" className="dash-card dash-bento__half"
          extra={can('/par/inbox') && <a className="dash-card__link" onClick={() => navigate('/par/inbox')}>Hộp thư <HeartHandshake size={13} /></a>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 5 }} title={false} /> : !d?.teachers.length ? (
            <div className="dash-empty">Lớp chưa có thời khóa biểu phát hành trong học kỳ này.</div>
          ) : (
            <ul className="dash-list">
              {d.teachers.map((t) => (
                <li key={`${t.subjectName}-${t.teacherName}`} className="dash-list__row">
                  <span className="dash-badge">{t.subjectName}</span>
                  <div className="dash-list__main"><b>{t.teacherName}</b></div>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card title="Danh sách học sinh" className="dash-card dash-card--table dash-bento__full"
          extra={d && <span className="dash-card__meta tabular">{d.studentCount} học sinh</span>}>
          <DataTable<HomeroomStudentRow>
            rowKey="studentId" dataSource={d?.students} loading={data.isLoading} pagination={false}
            emptyText="Lớp chưa có học sinh ghi danh."
            columns={[
              { title: 'STT', key: 'no', width: 64, className: 'tabular', render: (_: unknown, __, i) => i + 1 },
              {
                title: 'Học sinh', dataIndex: 'fullName',
                render: (v: string, r) => <div className="dash-cell"><b>{v}</b><span className="tabular">{r.personCode}</span></div>,
              },
              { title: 'Giới tính', dataIndex: 'gender', render: (v: string | null) => (v ? genderLabel[v] ?? v : '—') },
              { title: 'Ngày sinh', dataIndex: 'dateOfBirth', className: 'tabular', render: (v: string | null) => formatDate(v) },
              {
                title: 'Nghỉ 30 ngày', dataIndex: 'leaveCount', align: 'right',
                render: (v: number, r) => (
                  <span className="tabular">{v}{r.pendingLeave > 0 && <span className="dash-pill dash-pill--now dash-pill--sm">{r.pendingLeave} chờ duyệt</span>}</span>
                ),
              },
              {
                title: 'Phụ huynh', dataIndex: 'parentName',
                render: (v: string | null, r) => (v
                  ? <div className="dash-cell"><b>{v}</b><span className="tabular">{r.parentPhone ?? 'Chưa có số điện thoại'}{r.parentCount > 1 ? ` · +${r.parentCount - 1} người` : ''}</span></div>
                  : <span className="dash-cell--warn">Chưa liên kết</span>),
              },
              {
                title: 'Tài khoản phụ huynh', dataIndex: 'parentHasAccount',
                render: (v: boolean, r) => (r.parentCount === 0 ? '—' : v ? 'Đã có' : <span className="dash-cell--warn">Chưa có</span>),
              },
            ]}
          />
        </Card>
      </div>
    </>
  );
}
