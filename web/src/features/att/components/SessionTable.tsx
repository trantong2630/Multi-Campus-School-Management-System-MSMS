import { Card } from 'antd';
import { ClipboardCheck, Eye } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { DataTable } from '@/shared/components/DataTable';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import type { AttendanceSession } from '../types';
import { dayParts, slotParts } from '../attFormat';
import { MarkedCell } from './AttendanceParts';

/** Tình trạng điểm danh của tiết: tiết không Đã xếp lịch hiện trạng thái tiết; tiết chưa có bản ghi hiện "Chưa điểm danh". */
function SessionState({ s }: { s: AttendanceSession }) {
  if (s.status !== 'Scheduled') return <StatusBadge domain="session" value={s.status} />;
  if (s.markedCount === 0) return <StatusBadge domain="attendance" value="NotMarked" />;
  return <StatusBadge domain="session" value="Scheduled" />;
}

/** Danh sách tiết (4.5.1, 4.5.6). Bấm vào dòng hoặc nút thao tác để mở bảng điểm danh. */
export function SessionTable({ sessions, loading, emptyText, showDate, showClass }: {
  sessions: AttendanceSession[];
  loading: boolean;
  emptyText: string;
  showDate?: boolean;
  showClass?: boolean;
}) {
  const navigate = useNavigate();
  const open = (s: AttendanceSession) => navigate(`/att/sessions/${s.sessionId}`);
  return (
    <Card className="table-card">
      <DataTable<AttendanceSession>
        rowKey="sessionId" loading={loading} dataSource={sessions} emptyText={emptyText}
        onRow={(s) => ({ onClick: () => open(s), style: { cursor: 'pointer' } })}
        columns={[
          ...(showDate ? [{ title: 'Ngày', key: 'date', width: 150, className: 'tabular', render: (_: unknown, s: AttendanceSession) => <DayCell date={s.date} /> }] : []),
          { title: 'Tiết', key: 'slot', width: 220, className: 'tabular', render: (_: unknown, s: AttendanceSession) => <SlotCell s={s} /> },
          ...(showClass ? [{ title: 'Lớp', dataIndex: 'className', width: 100 }] : []),
          { title: 'Môn', dataIndex: 'subjectName', width: 170 },
          { title: 'Giáo viên', key: 'teacher', width: 190, render: (_: unknown, s: AttendanceSession) => s.teacherName ?? <span className="muted">Chưa có</span> },
          { title: 'Đã điểm danh', key: 'marked', width: 130, align: 'right', render: (_: unknown, s: AttendanceSession) => <MarkedCell s={s} /> },
          { title: 'Trạng thái', key: 'state', width: 160, align: 'center', render: (_: unknown, s: AttendanceSession) => <SessionState s={s} /> },
          {
            title: 'Thao tác', key: 'actions', width: 90, align: 'center', fixed: 'right',
            render: (_: unknown, s: AttendanceSession) => (
              <span onClick={(e) => e.stopPropagation()}>
                <RowActions actions={[s.canWrite
                  ? { key: 'mark', label: s.markedCount > 0 ? 'Sửa điểm danh' : 'Điểm danh', icon: <ClipboardCheck size={19} />, onClick: () => open(s) }
                  : { key: 'view', label: 'Xem điểm danh', icon: <Eye size={19} />, onClick: () => open(s) }]}
                />
              </span>
            ),
          },
        ]}
      />
    </Card>
  );
}

/** Hai dòng: Thứ ở trên, ngày tháng năm nhỏ ở dưới. */
function DayCell({ date }: { date: string }) {
  const [weekday, dmy] = dayParts(date);
  return <>{weekday}<span className="cell-sub cell-sub--date">{dmy}</span></>;
}

/** Hai dòng: "Sáng · Tiết 3" ở trên, giờ ở dưới. */
function SlotCell({ s }: { s: AttendanceSession }) {
  const [title, time] = slotParts(s);
  return <>{title}<span className="cell-sub">{time}</span></>;
}
