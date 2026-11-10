import { useQuery } from '@tanstack/react-query';
import { Card } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { DataTable } from '@/shared/components/DataTable';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PersonCell } from '@/shared/components/PersonCell';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { getErrorMessage } from '@/app/apiClient';
import { sessionOfDayLabel } from '@/shared/lib/format';
import { attApi } from '../api';
import type { AttendanceCounts, ClassAttendanceStudent } from '../types';
import { labelCountItems } from '../attFormat';
import { CountStrip } from './AttendanceParts';
import './att.css';

const sum = (rows: ClassAttendanceStudent[]): AttendanceCounts => rows.reduce<AttendanceCounts>((a, r) => ({
  present: a.present + r.counts.present,
  late: a.late + r.counts.late,
  excusedAbsent: a.excusedAbsent + r.counts.excusedAbsent,
  unexcusedAbsent: a.unexcusedAbsent + r.counts.unexcusedAbsent,
  notMarked: a.notMarked + r.counts.notMarked,
}), { present: 0, late: 0, excusedAbsent: 0, unexcusedAbsent: 0, notMarked: 0 });

const countCol = (title: string, key: keyof AttendanceCounts): ColumnsType<ClassAttendanceStudent>[number] => ({
  title, key, width: 120, align: 'right', className: 'tabular',
  render: (_: unknown, r: ClassAttendanceStudent) => r.counts[key] || <span className="muted">0</span>,
});

/**
 * 4.5.13: chuyên cần của lớp. Một ngày → lưới học sinh × tiết với nhãn từng tiết; khoảng ngày → số buổi theo từng nhãn.
 * Chỉ tiết Đã xếp lịch; tiết sau hôm nay chưa tính vào số đếm.
 */
export function ClassSummaryView({ classId, from, to }: { classId: number; from: string; to: string }) {
  const singleDay = from === to;
  const q = useQuery({ queryKey: ['att', 'class-summary', classId, from, to], queryFn: () => attApi.classSummary(classId, from, to) });

  if (q.error) return <InlineAlert type="error" title="Không tải được chuyên cần của lớp" description={getErrorMessage(q.error)} />;

  const data = q.data;
  const isFuture = dayjs(from).isAfter(dayjs(), 'day');
  const columns: ColumnsType<ClassAttendanceStudent> = [
    { title: 'Học sinh', key: 'student', width: 240, fixed: 'left', render: (_: unknown, r) => <PersonCell name={r.fullName} sub={r.studentCode} /> },
  ];
  if (singleDay) {
    for (const s of data?.sessions ?? []) {
      columns.push({
        title: <span>{sessionOfDayLabel(s.sessionOfDay)} T{s.periodNo}<br /><span className="muted">{s.subjectName}</span></span>,
        key: String(s.sessionId), width: 150, align: 'center',
        render: (_: unknown, r) => {
          const label = r.labels[String(s.sessionId)];
          return label ? <StatusBadge domain="attendance" value={label} /> : <span className="muted">—</span>;
        },
      });
    }
  } else {
    columns.push(
      countCol('Có mặt', 'present'), countCol('Muộn', 'late'), countCol('Vắng có phép', 'excusedAbsent'),
      countCol('Vắng không phép', 'unexcusedAbsent'), countCol('Chưa điểm danh', 'notMarked'),
    );
  }

  return (
    <>
      {data && !isFuture && <CountStrip items={labelCountItems(sum(data.students))} />}
      {data && data.sessions.length === 0 && (
        <InlineAlert type="info" title="Không có tiết nào trong khoảng ngày này" description="Ngày nghỉ, chưa phát hành thời khóa biểu hoặc tiết đã hủy." />
      )}
      <Card className="table-card">
        <DataTable<ClassAttendanceStudent>
          rowKey="studentId" loading={q.isLoading} dataSource={data?.students ?? []} columns={columns}
          emptyText="Lớp không có học sinh nào trong khoảng ngày này." pagination={{ pageSize: 50 }}
        />
      </Card>
    </>
  );
}
