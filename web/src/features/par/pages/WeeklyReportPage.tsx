'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Card, Select, Space, Spin } from 'antd';
import { AlertCircle } from 'lucide-react';
import { useAuth } from '@/app/AuthProvider';
import { parApi } from '../api';
import { PageHeader } from '@/shared/components/PageHeader';
import { DataTable } from '@/shared/components/DataTable';
import type { ColumnsType } from 'antd/es/table';
import { formatDateTime } from '@/shared/lib/format';
import type { WeeklyAssignmentItem } from '../types';
import './weekly-report.css';

export function WeeklyReportPage() {
  const { user } = useAuth();
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(null);
  const [selectedWeekStart, setSelectedWeekStart] = useState<string | null>(null);

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
      setSelectedStudentId(homeroomStudents[0].studentId);
    }
  }, [user, children, homeroomStudents]);

  const { data: weeks, isLoading: weeksLoading } = useQuery({
    queryKey: ['par-report-weeks', selectedStudentId],
    queryFn: () => (selectedStudentId ? parApi.report.getWeeks(selectedStudentId) : Promise.resolve([])),
    enabled: selectedStudentId !== null,
  });

  useEffect(() => {
    if (weeks && weeks.length > 0) {
      const currentWeek = weeks.find(w => w.isCurrent);
      setSelectedWeekStart(currentWeek?.weekStart || weeks[0].weekStart);
    }
  }, [weeks]);

  const { data: report, isLoading: reportLoading } = useQuery({
    queryKey: ['par-report-data', selectedStudentId, selectedWeekStart],
    queryFn: () => (selectedStudentId && selectedWeekStart ? parApi.report.getReport(selectedStudentId, selectedWeekStart) : Promise.resolve(null)),
    enabled: selectedStudentId !== null && selectedWeekStart !== null,
  });

  if (!user) {
    return <div>Vui lòng đăng nhập.</div>;
  }

  const isStudent = user.accountType === 'Student';
  const isParent = user.accountType === 'Parent';
  const isTeacher = user.accountType === 'Teacher';

  const studentOptions = useMemo(() => {
    if (isStudent) return [];
    if (isParent && children) {
      return children.map(child => ({ label: `${child.fullName} (${child.className || 'N/A'})`, value: child.studentId }));
    }
    if (isTeacher && homeroomStudents) {
      return homeroomStudents.map(s => ({ label: `${s.fullName} - ${s.className}`, value: s.studentId }));
    }
    return [];
  }, [isStudent, isParent, isTeacher, children, homeroomStudents]);

  const weekOptions = useMemo(() => {
    if (!weeks) return [];
    return weeks.map(w => ({ label: w.label, value: w.weekStart }));
  }, [weeks]);

  return (
    <div className="weekly-report-page">
      <PageHeader title="Sổ liên lạc điện tử" />
      <div className="weekly-report__controls">
        <Space size="large" wrap>
          {!isStudent && (
            <div>
              <label className="weekly-report__label">Chọn học sinh:</label>
              <Select
                placeholder="Chọn học sinh"
                value={selectedStudentId}
                onChange={setSelectedStudentId}
                options={studentOptions}
                loading={childrenLoading || homeroomStudentsLoading}
                style={{ width: 300 }}
              />
            </div>
          )}
          {selectedStudentId && (
            <div>
              <label className="weekly-report__label">Chọn tuần:</label>
              <Select
                placeholder="Chọn tuần"
                value={selectedWeekStart}
                onChange={setSelectedWeekStart}
                options={weekOptions}
                loading={weeksLoading}
                style={{ width: 300 }}
              />
            </div>
          )}
        </Space>
      </div>
      {reportLoading && (
        <div className="weekly-report__loading">
          <Spin size="large" />
        </div>
      )}
      {report && !reportLoading && (
        <div className="weekly-report__content">
          <div className="weekly-report__student-info">
            <h2>{report.studentName} ({report.studentCode}) - {report.className}</h2>
            <p>Tuần từ {formatDate(report.weekStart)} đến {formatDate(report.weekEnd)}</p>
          </div>
          <Card className="weekly-report__card">
            <h3 className="weekly-report__section-title">Chuyên cần trong tuần</h3>
            <div className="weekly-report__metrics">
              <MetricCard label="Có mặt" value={report.attendance.presentCount} total={report.attendance.totalSessions} variant="success" />
              <MetricCard label="Đi muộn" value={report.attendance.lateCount} total={report.attendance.totalSessions} variant="warning" />
              <MetricCard label="Vắng có phép" value={report.attendance.excusedCount} total={report.attendance.totalSessions} variant="info" />
              <MetricCard label="Vắng không phép" value={report.attendance.unexcusedCount} total={report.attendance.totalSessions} variant="danger" />
              <MetricCard label="Chưa điểm danh" value={report.attendance.unmarkedCount} total={report.attendance.totalSessions} variant="default" />
            </div>
          </Card>
          <Card className="weekly-report__card">
            <h3 className="weekly-report__section-title">Bài tập rèn luyện trong tuần</h3>
            <div className="weekly-report__assignment-summary">
              <SummaryCard label="Được giao" value={report.assignments.assignedCount} variant="default" />
              <SummaryCard label="Đã nộp" value={report.assignments.submittedCount} variant="success" />
              <SummaryCard label="Chưa nộp" value={report.assignments.unsubmittedCount} variant="warning" />
              <SummaryCard label="Không nộp" value={report.assignments.missedCount} variant="danger" />
            </div>
            {report.assignments.items.length > 0 ? (
              <AssignmentsTable items={report.assignments.items} />
            ) : (
              <div className="weekly-report__empty">
                <AlertCircle size={32} />
                <p>Không có bài tập trong tuần này.</p>
              </div>
            )}
          </Card>

        </div>
      )}
    </div>
  );
}

function MetricCard({ label, value, total, variant }: any) {
  const colors: Record<string, string> = { success: '#52c41a', warning: '#faad14', info: '#1890ff', danger: '#ff4d4f', default: '#8c8c8c' };
  return (
    <div className="metric-card" style={{ borderLeftColor: colors[variant] }}>
      <div className="metric-card__label">{label}</div>
      <div className="metric-card__value">{value}</div>
      <div className="metric-card__total">Trên tổng {total} tiết</div>
    </div>
  );
}

function SummaryCard({ label, value, variant }: any) {
  const colors: Record<string, { bg: string; fg: string }> = {
    success: { bg: '#f6ffed', fg: '#52c41a' },
    warning: { bg: '#fffbe6', fg: '#faad14' },
    danger: { bg: '#fff1f0', fg: '#ff4d4f' },
    default: { bg: '#fafafa', fg: '#8c8c8c' },
  };
  const c = colors[variant];
  return (
    <div className="summary-card" style={{ backgroundColor: c.bg }}>
      <div className="summary-card__label" style={{ color: c.fg }}>{label}</div>
      <div className="summary-card__value" style={{ color: c.fg }}>{value}</div>
    </div>
  );
}

function AssignmentsTable({ items }: { items: WeeklyAssignmentItem[] }) {
  const statusColor: Record<string, string> = { Submitted: '#52c41a', Unsubmitted: '#faad14', Missed: '#ff4d4f' };
  const columns: ColumnsType<any> = [
    { title: 'Tên bài tập', dataIndex: 'title', key: 'title', width: 250 },
    { title: 'Môn học', dataIndex: 'subjectName', key: 'subjectName', width: 100 },
    { title: 'Hạn nộp', dataIndex: 'dueAt', key: 'dueAt', width: 150, render: (text: string) => formatDateTime(text) },
    { title: 'Trạng thái', dataIndex: 'status', key: 'status', width: 100, render: (status: string) => (<span style={{ display: 'inline-block', padding: '4px 8px', borderRadius: '4px', backgroundColor: statusColor[status] + '20', color: statusColor[status], fontSize: '12px', fontWeight: 500 }}>{status === 'Submitted' && 'Đã nộp'}{status === 'Unsubmitted' && 'Chưa nộp'}{status === 'Missed' && 'Không nộp'}</span>) },
    { title: 'Điểm số', dataIndex: 'score', key: 'score', width: 80, render: (score: number) => (score !== null && score !== undefined ? `${score}/10` : '—') },
    { title: 'Nhận xét AI', dataIndex: 'aiFeedback', key: 'aiFeedback', width: 200, render: (feedback: string) => (feedback ? <span title={feedback} style={{ overflow: 'hidden', textOverflow: 'ellipsis', display: 'block', whiteSpace: 'nowrap' }}>{feedback}</span> : '—') },
  ];
  return <DataTable columns={columns} dataSource={items} pagination={false} />;
}

function formatDate(value?: string | null) {
  if (!value) return '—';
  const date = new Date(value);
  return date.toLocaleDateString('vi-VN', { year: 'numeric', month: '2-digit', day: '2-digit' });
}
