'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { Button, Empty, Select, Spin } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useAuth } from '@/app/AuthProvider';
import { parApi } from '../api';
import { PageHeader } from '@/shared/components/PageHeader';
import { DataTable } from '@/shared/components/DataTable';
import { formatDateTime } from '@/shared/lib/format';
import type { AttendanceSummary, WeeklyAssignmentItem, WeeklyReportData } from '../types';
import './weekly-report.css';

type AssignmentStatus = WeeklyAssignmentItem['status'];

const attendanceParts = [
  { key: 'presentCount', label: 'Có mặt', color: '#2f9e5b' },
  { key: 'lateCount', label: 'Đi muộn', color: '#f2a93b' },
  { key: 'excusedCount', label: 'Vắng có phép', color: '#4c8bf5' },
  { key: 'unexcusedCount', label: 'Vắng không phép', color: '#e5584f' },
  { key: 'unmarkedCount', label: 'Chưa điểm danh', color: '#b4bcc6' },
] as const;

const assignmentParts: { key: AssignmentStatus; label: string; color: string }[] = [
  { key: 'Submitted', label: 'Đã nộp', color: '#2f9e5b' },
  { key: 'Unsubmitted', label: 'Chưa nộp', color: '#f2a93b' },
  { key: 'Missed', label: 'Không nộp', color: '#e5584f' },
];

export function WeeklyReportPage() {
  const { user } = useAuth();
  // Mở từ Hồ sơ học sinh: ?studentId= chọn sẵn em đó nếu em thuộc lớp chủ nhiệm.
  const [params] = useSearchParams();
  const requestedId = Number(params.get('studentId')) || null;
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(null);
  const [selectedWeekStart, setSelectedWeekStart] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<AssignmentStatus | null>(null);

  const { data: children, isLoading: childrenLoading } = useQuery({
    queryKey: ['par-my-children'],
    queryFn: () => parApi.leave.getMyChildren(),
    enabled: user?.accountType === 'Parent',
  });

  const { data: homeroomStudents, isLoading: homeroomStudentsLoading } = useQuery({
    queryKey: ['par-homeroom-students'],
    queryFn: () => parApi.report.getHomeroomStudents(),
    enabled: user?.accountType === 'Teacher',
  });

  useEffect(() => {
    if (user?.accountType === 'Student') {
      setSelectedStudentId(user.personId);
    } else if (user?.accountType === 'Parent' && children && children.length > 0) {
      setSelectedStudentId(children[0].studentId);
    } else if (user?.accountType === 'Teacher' && homeroomStudents && homeroomStudents.length > 0) {
      const requested = homeroomStudents.find((s) => s.studentId === requestedId);
      setSelectedStudentId((requested ?? homeroomStudents[0]).studentId);
    }
  }, [user, children, homeroomStudents, requestedId]);

  const { data: weeks, isLoading: weeksLoading } = useQuery({
    queryKey: ['par-report-weeks', selectedStudentId],
    queryFn: () => (selectedStudentId ? parApi.report.getWeeks(selectedStudentId) : Promise.resolve([])),
    enabled: selectedStudentId !== null,
  });

  useEffect(() => {
    if (weeks && weeks.length > 0) {
      const currentWeek = weeks.find((w) => w.isCurrent);
      setSelectedWeekStart(currentWeek?.weekStart || weeks[0].weekStart);
    }
  }, [weeks]);

  // Đổi học sinh hoặc đổi tuần thì bỏ bộ lọc trạng thái của danh sách bài tập.
  useEffect(() => setStatusFilter(null), [selectedStudentId, selectedWeekStart]);

  const { data: report, isLoading: reportLoading } = useQuery({
    queryKey: ['par-report-data', selectedStudentId, selectedWeekStart],
    queryFn: () => (selectedStudentId && selectedWeekStart ? parApi.report.getReport(selectedStudentId, selectedWeekStart) : Promise.resolve(null)),
    enabled: selectedStudentId !== null && selectedWeekStart !== null,
  });

  const isStudent = user?.accountType === 'Student';
  const isParent = user?.accountType === 'Parent';
  const isTeacher = user?.accountType === 'Teacher';

  const studentOptions = useMemo(() => {
    if (isParent && children) {
      return children.map((child) => ({ label: `${child.fullName} (${child.className || 'N/A'})`, value: child.studentId }));
    }
    if (isTeacher && homeroomStudents) {
      return homeroomStudents.map((s) => ({ label: `${s.fullName} - ${s.className}`, value: s.studentId }));
    }
    return [];
  }, [isParent, isTeacher, children, homeroomStudents]);

  const weekIndex = weeks?.findIndex((w) => w.weekStart === selectedWeekStart) ?? -1;
  const currentWeek = weeks?.find((w) => w.isCurrent);
  const goToWeek = (index: number) => {
    if (weeks && weeks[index]) setSelectedWeekStart(weeks[index].weekStart);
  };

  // Thoát sớm sau mọi hook để số hook không đổi giữa các lần render.
  if (!user) {
    return <div>Vui lòng đăng nhập.</div>;
  }

  return (
    <div className="weekly-report-page">
      <PageHeader title="Báo cáo tuần" />
      <div className="weekly-report__controls">
        {!isStudent && (
          <Select
            placeholder="Chọn học sinh"
            value={selectedStudentId}
            onChange={setSelectedStudentId}
            options={studentOptions}
            loading={childrenLoading || homeroomStudentsLoading}
            showSearch
            optionFilterProp="label"
            style={{ width: 300 }}
          />
        )}
        {weeks && weeks.length > 0 && weekIndex >= 0 && (
          <div className="week-nav">
            <Button aria-label="Tuần trước" icon={<ChevronLeft size={16} />} disabled={weekIndex <= 0} onClick={() => goToWeek(weekIndex - 1)} />
            <div className="week-nav__label">{weeks[weekIndex].label}</div>
            <Button aria-label="Tuần sau" icon={<ChevronRight size={16} />} disabled={weekIndex >= weeks.length - 1} onClick={() => goToWeek(weekIndex + 1)} />
            {currentWeek && currentWeek.weekStart !== selectedWeekStart && (
              <Button type="link" onClick={() => setSelectedWeekStart(currentWeek.weekStart)}>Về tuần này</Button>
            )}
          </div>
        )}
        {weeksLoading && <Spin size="small" />}
      </div>
      {reportLoading && (
        <div className="weekly-report__loading">
          <Spin size="large" />
        </div>
      )}
      {report && !reportLoading && (
        <div className="weekly-report__content">
          <ReportHero report={report} />
          <AttendanceCard attendance={report.attendance} />
          <AssignmentsCard
            report={report}
            statusFilter={statusFilter}
            onFilter={(status) => setStatusFilter((cur) => (cur === status ? null : status))}
          />
        </div>
      )}
    </div>
  );
}

/** Một câu tóm tắt tuần để người xem nắm ngay việc cần chú ý. */
function summarize(report: WeeklyReportData): string {
  const { attendance: a, assignments: s } = report;
  const notes: string[] = [];
  if (a.totalSessions === 0) notes.push('Tuần này chưa có tiết nào diễn ra');
  else if (a.unexcusedCount > 0) notes.push(`Vắng ${a.unexcusedCount} tiết không phép`);
  else if (a.excusedCount > 0 || a.lateCount > 0) notes.push('Có tiết vắng có phép hoặc đi muộn');
  else notes.push('Đi học đầy đủ');
  if (s.assignedCount === 0) notes.push('không có bài tập đến hạn');
  else if (s.missedCount > 0) notes.push(`${s.missedCount} bài không nộp`);
  else if (s.unsubmittedCount > 0) notes.push(`còn ${s.unsubmittedCount} bài chưa nộp`);
  else notes.push('đã nộp đủ bài tập');
  return `${notes.join(', ')}.`;
}

function ReportHero({ report }: { report: WeeklyReportData }) {
  return (
    <div className="report-hero">
      <div>
        <h2 className="report-hero__name">{report.studentName}</h2>
        <div className="report-hero__meta">{report.studentCode} · Lớp {report.className} · {formatDate(report.weekStart)} – {formatDate(report.weekEnd)}</div>
      </div>
      <div className="report-hero__summary">{summarize(report)}</div>
    </div>
  );
}

function AttendanceCard({ attendance }: { attendance: AttendanceSummary }) {
  const total = attendance.totalSessions;
  return (
    <section className="report-card">
      <h3 className="report-card__title">Chuyên cần</h3>
      {total === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Tuần này chưa có tiết nào diễn ra" />
      ) : (
        <div className="attendance">
          <div className="attendance__figure">
            <span className="attendance__big">{attendance.presentCount}</span>
            <span className="attendance__of">/ {total} tiết có mặt</span>
          </div>
          <div className="attendance__main">
            <StackedBar parts={attendanceParts.map((p) => ({ label: p.label, color: p.color, value: attendance[p.key] }))} total={total} />
            <ul className="legend">
              {attendanceParts.map((p) => (
                <li key={p.key} className={attendance[p.key] === 0 ? 'legend__item legend__item--zero' : 'legend__item'}>
                  <span className="legend__dot" style={{ background: p.color }} />
                  <span className="legend__label">{p.label}</span>
                  <b className="legend__value">{attendance[p.key]}</b>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}
    </section>
  );
}

function AssignmentsCard({ report, statusFilter, onFilter }: {
  report: WeeklyReportData;
  statusFilter: AssignmentStatus | null;
  onFilter: (status: AssignmentStatus) => void;
}) {
  const { assignments } = report;
  const count: Record<AssignmentStatus, number> = {
    Submitted: assignments.submittedCount,
    Unsubmitted: assignments.unsubmittedCount,
    Missed: assignments.missedCount,
  };
  const items = statusFilter ? assignments.items.filter((i) => i.status === statusFilter) : assignments.items;
  return (
    <section className="report-card">
      <h3 className="report-card__title">Bài tập rèn luyện <span className="report-card__sub">{assignments.assignedCount} bài đến hạn trong tuần</span></h3>
      {assignments.assignedCount === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có bài tập đến hạn trong tuần này" />
      ) : (
        <>
          <StackedBar parts={assignmentParts.map((p) => ({ label: p.label, color: p.color, value: count[p.key] }))} total={assignments.assignedCount} />
          <div className="status-chips" role="group" aria-label="Lọc bài tập theo trạng thái">
            {assignmentParts.map((p) => (
              <button
                key={p.key}
                type="button"
                className={`status-chip${statusFilter === p.key ? ' status-chip--active' : ''}`}
                style={{ ['--chip-color' as string]: p.color }}
                aria-pressed={statusFilter === p.key}
                onClick={() => onFilter(p.key)}
              >
                <span className="status-chip__value">{count[p.key]}</span>
                <span className="status-chip__label">{p.label}</span>
              </button>
            ))}
          </div>
          <AssignmentsTable items={items} />
        </>
      )}
    </section>
  );
}

function StackedBar({ parts, total }: { parts: { label: string; color: string; value: number }[]; total: number }) {
  return (
    <div className="stacked-bar" role="img" aria-label={parts.map((p) => `${p.label} ${p.value}`).join(', ')}>
      {parts.filter((p) => p.value > 0).map((p) => (
        <div key={p.label} className="stacked-bar__part" style={{ width: `${(p.value / total) * 100}%`, background: p.color }} title={`${p.label}: ${p.value}`} />
      ))}
    </div>
  );
}

function AssignmentsTable({ items }: { items: WeeklyAssignmentItem[] }) {
  const status: Record<AssignmentStatus, { label: string; color: string }> = {
    Submitted: { label: 'Đã nộp', color: '#2f9e5b' },
    Unsubmitted: { label: 'Chưa nộp', color: '#c98312' },
    Missed: { label: 'Không nộp', color: '#e5584f' },
  };
  const columns: ColumnsType<WeeklyAssignmentItem> = [
    { title: 'Tên bài tập', dataIndex: 'title', key: 'title' },
    { title: 'Môn học', dataIndex: 'subjectName', key: 'subjectName', width: 140 },
    { title: 'Hạn nộp', dataIndex: 'dueAt', key: 'dueAt', width: 170, render: (text: string) => formatDateTime(text) },
    {
      title: 'Trạng thái', dataIndex: 'status', key: 'status', width: 120,
      render: (value: AssignmentStatus) => <span className="status-tag" style={{ color: status[value].color, background: `${status[value].color}1f` }}>{status[value].label}</span>,
    },
    { title: 'Điểm', dataIndex: 'score', key: 'score', width: 90, align: 'right', render: (score?: number | null) => (score !== null && score !== undefined ? `${score}/10` : '—') },
  ];
  return <DataTable columns={columns} dataSource={items} rowKey="assignmentId" pagination={false} />;
}

function formatDate(value?: string | null) {
  if (!value) return '—';
  return new Date(value).toLocaleDateString('vi-VN', { year: 'numeric', month: '2-digit', day: '2-digit' });
}
