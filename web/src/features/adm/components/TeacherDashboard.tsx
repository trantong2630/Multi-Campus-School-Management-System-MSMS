import { useQuery } from '@tanstack/react-query';
import { Card, Skeleton } from 'antd';
import dayjs from 'dayjs';
import { ArrowRight, BookOpen, CalendarClock, ChartColumn, Clock, GraduationCap, House, School, TriangleAlert } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { DataTable } from '@/shared/components/DataTable';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatCard } from '@/shared/components/StatCard';
import { formatDateTime, formatTime, sessionOfDayLabel } from '@/shared/lib/format';
import { admApi } from '../api';
import type { TeacherAssignmentItem, TeacherSessionItem } from '../types';
import { AttentionStrip, RateBar } from './DashboardParts';
import { fmt, pct } from './dashboardFormat';

type SlotState = 'done' | 'now' | 'next' | 'later';

function slotState(s: TeacherSessionItem, nowTime: string, nextId: number | undefined): SlotState {
  if (s.endTime <= nowTime) return 'done';
  if (s.startTime <= nowTime) return 'now';
  return s.sessionId === nextId ? 'next' : 'later';
}

/** Đồng hồ phút để tiết "đang dạy" tự chuyển mà không render dày. */
function useNowTime() {
  const [now, setNow] = useState(() => dayjs());
  useEffect(() => {
    const id = window.setInterval(() => setNow(dayjs()), 30_000);
    return () => window.clearInterval(id);
  }, []);
  return now.format('HH:mm:ss');
}

/**
 * Dashboard giáo viên: chỉ dữ liệu của chính mình (tiết, lớp và môn phụ trách, bài đã giao).
 * Chủ nhiệm, tổ trưởng có thêm thẻ đường tắt sang màn trách nhiệm; số liệu của lớp, của tổ nằm ở các màn đó.
 */
export function TeacherDashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const nowTime = useNowTime();
  const data = useQuery({ queryKey: ['adm', 'dashboard', 'teacher'], queryFn: admApi.teacherDashboard });
  const d = data.data;
  const can = (path: string) => (user && isPathAllowed(path, user) ? path : undefined);
  const go = (path: string) => (can(path) ? () => navigate(path) : undefined);

  const nextId = d?.today.find((s) => s.startTime > nowTime)?.sessionId;
  const remaining = d?.today.filter((s) => s.endTime > nowTime).length ?? 0;
  const nextSession = d?.today.find((s) => s.sessionId === nextId);
  const shortcuts = [
    ...(d?.homeroomClasses.map((c) => ({ key: `h${c.id}`, icon: House, title: `Lớp chủ nhiệm ${c.name}`, desc: 'Học sinh, đơn nghỉ, phụ huynh', to: can('/homeroom') && `/homeroom?classId=${c.id}` })) ?? []),
    ...(d?.headDepartments.map((t) => ({ key: `t${t.id}`, icon: ChartColumn, title: `Tổ ${t.name}`, desc: 'Tiến độ giao bài của tổ', to: can('/lrn/department') })) ?? []),
  ];

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={[d?.fullName ?? user?.fullName, d?.academicYearName && `Năm học ${d.academicYearName}`, d?.semesterName, dayjs().format('DD/MM/YYYY')].filter(Boolean).join(' · ')}
      />

      <div className="dash-bento">
        <StatCard variant="accent" icon={CalendarClock} label="Tiết dạy hôm nay" value={fmt(d?.sessionsToday)}
          hint={d ? (nextSession ? `Tiếp theo: ${nextSession.className} lúc ${formatTime(nextSession.startTime)}` : `Tuần này ${d.sessionsThisWeek} tiết`) : undefined}
          onClick={go('/scd/schedule')} />
        <StatCard icon={School} tone="sky" label="Lớp tôi dạy" value={fmt(d?.classCount)}
          hint={d ? `${d.subjectCount} môn phụ trách` : undefined} />
        <StatCard icon={GraduationCap} tone="violet" label="Học sinh" value={fmt(d?.studentCount)}
          hint={d && d.classCount > 0 ? `Bình quân ${Math.round(d.studentCount / d.classCount)} học sinh mỗi lớp` : undefined} />
        <StatCard icon={BookOpen} tone="amber" label="Bài đang mở" value={fmt(d?.openAssignments)}
          hint={d ? `Tỷ lệ nộp chung ${pct(d.submissionRate)}` : undefined} onClick={go('/lrn/assigned')} />

        {d && (
          <AttentionStrip title="Việc cần làm" items={[
            { key: 'remaining', label: 'tiết còn lại hôm nay', count: remaining, icon: Clock, to: can('/scd/schedule') },
            { key: 'due', label: 'bài hết hạn trong 48 giờ', count: d.dueSoon, icon: BookOpen, to: can('/lrn/assigned') },
            { key: 'missing', label: 'bài vừa đóng còn học sinh chưa nộp', count: d.assignmentsWithMissing, icon: TriangleAlert, to: can('/lrn/assigned') },
          ]} />
        )}

        {shortcuts.length > 0 && (
          <div className="dash-shortcuts dash-bento__full">
            {shortcuts.map((s) => (
              <button key={s.key} type="button" className="bento-tile dash-shortcut" disabled={!s.to} onClick={() => s.to && navigate(s.to)}>
                <span className="dash-shortcut__icon"><s.icon size={20} /></span>
                <span className="dash-shortcut__text"><b>{s.title}</b><span>{s.desc}</span></span>
                <ArrowRight size={16} className="dash-shortcut__go" />
              </button>
            ))}
          </div>
        )}

        <Card title="Lịch dạy hôm nay" className="dash-card dash-bento__half"
          extra={<span className="dash-card__meta tabular">{dayjs().format('dddd, DD/MM')}</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 5 }} title={false} /> : !d?.today.length ? (
            <div className="dash-empty">Hôm nay bạn không có tiết dạy.</div>
          ) : (
            <ol className="dash-timeline">
              {d.today.map((s) => {
                const state = slotState(s, nowTime, nextId);
                return (
                  <li key={s.sessionId} className={`dash-timeline__item dash-timeline__item--${state}`}>
                    <div className="dash-timeline__time tabular"><b>{formatTime(s.startTime)}</b><span>{formatTime(s.endTime)}</span></div>
                    <span className="dash-timeline__node" />
                    <div className="dash-timeline__body">
                      <span className="dash-timeline__period">{sessionOfDayLabel(s.sessionOfDay)} · Tiết {s.periodNo}</span>
                      <b>{s.className} · {s.subjectName}</b>
                    </div>
                    {state === 'now' && <span className="dash-pill dash-pill--now">Đang diễn ra</span>}
                    {state === 'next' && <span className="dash-pill">Tiếp theo</span>}
                  </li>
                );
              })}
            </ol>
          )}
        </Card>

        <Card title="Lớp tôi dạy" className="dash-card dash-bento__half"
          extra={<span className="dash-card__meta">Tỷ lệ nộp trên bài đã đóng</span>}>
          {data.isLoading ? <Skeleton active paragraph={{ rows: 5 }} title={false} /> : !d?.classes.length ? (
            <div className="dash-empty">Học kỳ này bạn chưa được phân công lớp nào.</div>
          ) : (
            <ul className="dash-list">
              {d.classes.map((c) => (
                <li key={c.classId} className="dash-list__row">
                  <span className="dash-badge tabular">{c.className}</span>
                  <div className="dash-list__main"><b>{c.subjects.join(', ')}</b><span>{c.studentCount} học sinh · {c.assignmentCount} bài</span></div>
                  <RateBar rate={c.submissionRate} width={110} />
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card title="Bài giao gần đây" className="dash-card dash-card--table dash-bento__full"
          extra={can('/lrn/assigned') && <a className="dash-card__link" onClick={() => navigate('/lrn/assigned')}>Xem tất cả <ArrowRight size={13} /></a>}>
          <DataTable<TeacherAssignmentItem>
            rowKey="assignmentId" dataSource={d?.recent} loading={data.isLoading} pagination={false}
            emptyText="Bạn chưa giao bài nào trong năm học."
            columns={[
              { title: 'Bài tập', dataIndex: 'title', render: (v: string) => <b>{v}</b> },
              { title: 'Lớp', dataIndex: 'className' },
              { title: 'Môn', dataIndex: 'subjectName' },
              { title: 'Hạn nộp', dataIndex: 'dueAt', className: 'tabular', render: (v: string) => formatDateTime(v) },
              {
                title: 'Trạng thái', dataIndex: 'isClosed',
                render: (v: boolean) => <span className={`dash-pill${v ? '' : ' dash-pill--open'}`}>{v ? 'Đã đóng' : 'Đang mở'}</span>,
              },
              {
                title: 'Đã nộp', dataIndex: 'submittedCount', align: 'right', width: 220,
                render: (_: number, r) => (
                  <div className="dash-rate">
                    <span className="dash-card__meta tabular">{r.submittedCount}/{r.recipientCount}</span>
                    <RateBar rate={r.recipientCount ? Math.round((r.submittedCount / r.recipientCount) * 100) : null} width={80} />
                  </div>
                ),
              },
            ]}
          />
        </Card>
      </div>
    </>
  );
}
