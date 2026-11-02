import { useQuery } from '@tanstack/react-query';
import { Card, Select, Skeleton } from 'antd';
import dayjs from 'dayjs';
import { CalendarClock, CalendarX2, FileText, GraduationCap, School, UserRoundX, UsersRound } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { scdApi } from '@/features/scd/api';
import { DataTable } from '@/shared/components/DataTable';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatCard } from '@/shared/components/StatCard';
import { admApi } from '../api';
import type { DashboardCampusRow } from '../types';
import { AttentionStrip, GradeChart, RateBar, SubjectRateList, WeekChart } from './DashboardParts';
import { fmt } from './dashboardFormat';
import type { AttentionItem } from './DashboardParts';

const ALL = 0;

/**
 * Dashboard Hiệu trưởng / Hiệu phó: chỉ số liệu vận hành — quy mô hôm nay, việc cần chú ý, xu hướng 8 tuần,
 * nộp bài theo môn và so sánh phân hiệu. Cấu trúc (tổ, môn, phân hiệu chi tiết) xem ở các màn Thiết lập; bấm số có màn gốc thì mở màn đó.
 */
export function LeadershipDashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const isPrincipal = user?.accountType === 'Principal';
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number>(ALL);
  const canFilter = isPrincipal || (campuses.data?.length ?? 0) > 1;

  const scope = campusId === ALL ? undefined : campusId;
  const data = useQuery({ queryKey: ['adm', 'dashboard', scope], queryFn: () => admApi.dashboard(scope) });
  const d = data.data;
  const allowed = (path: string) => (user && isPathAllowed(path, user) ? path : undefined);
  const go = (path: string) => (allowed(path) ? () => navigate(path) : undefined);

  const scopeName = scope === undefined
    ? (isPrincipal ? 'Toàn trường' : campuses.data?.map((c) => c.name).join(', '))
    : campuses.data?.find((c) => c.campusId === scope)?.name;
  const showCompare = !!d && d.campuses.length > 1;
  const maxStudents = Math.max(1, ...(d?.campuses.map((c) => c.studentCount) ?? []));
  const today = dayjs().format('dddd, DD/MM');

  const attention: AttentionItem[] = d ? [
    { key: 'pending', label: 'tiết chờ xử lý', count: d.attention.pendingSessions, icon: CalendarX2, to: allowed('/scd/absences') },
    { key: 'absence', label: 'giáo viên vắng hôm nay', count: d.attention.teacherAbsencesToday, icon: UserRoundX, to: allowed('/scd/absences') },
    ...(d.attention.pendingLeaveRequests === null ? [] : [
      { key: 'leave', label: 'đơn nghỉ chờ duyệt', count: d.attention.pendingLeaveRequests, icon: FileText, to: allowed('/par/leave-requests') },
    ]),
    { key: 'homeroom', label: 'lớp chưa có chủ nhiệm', count: d.attention.classesWithoutHomeroom, icon: School, to: allowed('/ppl/responsibilities') },
  ] : [];

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={[scopeName, d?.academicYearName && `Năm học ${d.academicYearName}`, d?.semesterName, dayjs().format('DD/MM/YYYY')].filter(Boolean).join(' · ')}
        extra={canFilter && (
          <Select
            className="dash-scope" value={campusId} onChange={setCampusId} loading={campuses.isLoading}
            options={[
              { value: ALL, label: isPrincipal ? 'Toàn trường' : 'Tất cả phân hiệu phụ trách' },
              ...(campuses.data?.map((c) => ({ value: c.campusId, label: c.name })) ?? []),
            ]}
          />
        )}
      />

      <div className="dash-bento">
        <StatCard variant="accent" icon={GraduationCap} label="Học sinh đang học" value={fmt(d?.studentCount)}
          hint={d && d.classCount > 0 ? `Bình quân ${Math.round(d.studentCount / d.classCount)} học sinh mỗi lớp` : undefined} onClick={go('/sch/classes')} />
        <StatCard icon={School} tone="sky" label="Lớp đang hoạt động" value={fmt(d?.classCount)}
          hint={d && d.gradeCount > 0 ? `${d.gradeCount} khối đang có lớp` : undefined} onClick={go('/sch/classes')} />
        <StatCard icon={UsersRound} tone="violet" label="Cán bộ đang công tác" value={fmt(d?.staffCount)}
          hint={d && d.campusCount > 1 ? `Trên ${d.campusCount} phân hiệu` : undefined} onClick={go('/ppl/staff')} />
        <StatCard icon={CalendarClock} tone="amber" label="Tiết dạy hôm nay" value={fmt(d?.sessionsToday)}
          hint={today.charAt(0).toUpperCase() + today.slice(1)} onClick={go('/scd/schedule')} />

        {d && <AttentionStrip title="Cần chú ý hôm nay" items={attention} />}

        <Card
          title="Học sinh theo khối" className={`dash-card ${showCompare ? 'dash-bento__half' : 'dash-bento__full'}`}
          extra={d && d.studentCount > 0 && <span className="dash-card__meta tabular">Tổng {fmt(d.studentCount)} học sinh</span>}
        >
          {data.isLoading ? <Skeleton active paragraph={{ rows: 6 }} title={false} /> : <GradeChart data={d?.byGrade ?? []} />}
        </Card>

        {showCompare && (
          <Card title="So sánh phân hiệu" className="dash-card dash-card--table dash-bento__half"
            extra={<span className="dash-card__meta">Bấm một hàng để lọc</span>}>
            <DataTable<DashboardCampusRow>
              rowKey="campusId" dataSource={d.campuses} pagination={false}
              onRow={(r) => ({ onClick: () => setCampusId(r.campusId), className: 'dash-row-click' })}
              columns={[
                {
                  title: 'Phân hiệu', dataIndex: 'name',
                  render: (v: string, r) => (
                    <div className="dash-cell">
                      <b>{v}</b>
                      <span className={r.leaders.length ? undefined : 'dash-cell--warn'}>{r.leaders.length ? r.leaders.join(', ') : 'Chưa có hiệu phó phụ trách'}</span>
                    </div>
                  ),
                },
                { title: 'Lớp', dataIndex: 'classCount', align: 'right', className: 'tabular' },
                {
                  title: 'Học sinh', dataIndex: 'studentCount', align: 'right', className: 'tabular',
                  render: (v: number) => (
                    <div className="dash-share">
                      <span className="dash-share__track"><i style={{ width: `${(v / maxStudents) * 100}%` }} /></span>
                      <span>{fmt(v)}</span>
                    </div>
                  ),
                },
                { title: 'Cán bộ', dataIndex: 'staffCount', align: 'right', className: 'tabular' },
                { title: 'Nộp bài', dataIndex: 'submissionRate', align: 'right', render: (v: number | null) => <RateBar rate={v} width={56} /> },
              ]}
            />
          </Card>
        )}

        <Card title="Hoạt động 8 tuần" className="dash-card dash-bento__half"
          extra={<span className="dash-card__meta">Tỷ lệ nộp tính trên bài đã đóng</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 6 }} title={false} /> : <WeekChart weeks={d?.weeks ?? []} />}
        </Card>

        <Card title="Nộp bài theo môn" className="dash-card dash-bento__half"
          extra={<span className="dash-card__meta">Môn thấp nhất lên đầu</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 6 }} title={false} /> : <SubjectRateList subjects={d?.subjects ?? []} />}
        </Card>
      </div>
    </>
  );
}
