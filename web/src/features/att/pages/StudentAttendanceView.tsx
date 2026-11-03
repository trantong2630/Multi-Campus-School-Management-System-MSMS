import { useQuery } from '@tanstack/react-query';
import { Card, Segmented, Select } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { useAuth } from '@/app/AuthProvider';
import { parApi } from '@/features/par/api';
import { WeekFrame, WeekNav } from '@/features/scd/components/WeekFrame';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { sessionOfDayLabel, toDateString } from '@/shared/lib/format';
import { attApi } from '../api';
import { dayText, labelCountItems } from '../attFormat';
import { CountStrip } from '../components/AttendanceParts';
import type { StudentAttendanceItem } from '../types';
import '../components/att.css';

type View = 'week' | 'list';

/** Học sinh xem chuyên cần của mình (4.5.11); phụ huynh chọn con rồi xem (4.5.12). Bốn nhãn suy ra ở Backend (BR-18). */
export function StudentAttendanceView() {
  const { user } = useAuth();
  const isParent = user?.accountType === 'Parent';
  const [monday, setMonday] = useState<Dayjs>(() => dayjs().startOf('week'));
  const [view, setView] = useState<View>('week');
  const [pickedChild, setChildId] = useState<number | undefined>();
  const from = toDateString(monday);
  const to = toDateString(monday.add(5, 'day'));

  const children = useQuery({ queryKey: ['par', 'leave', 'my-children'], queryFn: () => parApi.leave.getMyChildren(), enabled: isParent });
  // Chưa chọn thì mặc định con đầu tiên (suy ra khi render, không đồng bộ bằng effect).
  const childId = pickedChild ?? children.data?.[0]?.studentId;

  const ready = !isParent || childId !== undefined;
  const q = useQuery({
    queryKey: ['att', 'student', isParent ? childId : 'me', from, to],
    queryFn: () => (isParent ? attApi.child(childId!, from, to) : attApi.mine(from, to)),
    enabled: ready,
  });
  const items = q.data?.items ?? [];
  const today = toDateString(dayjs());

  return (
    <>
      <PageHeader
        title={isParent ? 'Chuyên cần của con' : 'Chuyên cần'}
        description="Chuyên cần theo tiết và theo tuần: Có mặt, Muộn, Vắng có phép, Vắng không phép. Vắng có phép khi có đơn xin nghỉ đã duyệt đúng ngày và buổi."
      />
      <FilterBar>
        <Segmented<View> value={view} onChange={setView} options={[{ value: 'week', label: 'Theo tuần' }, { value: 'list', label: 'Theo tiết' }]} />
        {isParent && (
          <Select
            placeholder="Chọn con" value={childId} onChange={setChildId} loading={children.isLoading}
            options={children.data?.map((c) => ({ value: c.studentId, label: c.className ? `${c.fullName} — ${c.className}` : c.fullName }))}
          />
        )}
        <WeekNav monday={monday} onChange={setMonday} />
      </FilterBar>

      {isParent && children.isSuccess && children.data.length === 0 && (
        <InlineAlert type="info" title="Chưa có học sinh nào liên kết" description="Liên hệ nhà trường để liên kết tài khoản với con." />
      )}
      {q.error && <InlineAlert type="error" title="Không tải được chuyên cần" description={getErrorMessage(q.error)} />}
      {q.data && <CountStrip items={labelCountItems(q.data.counts)} />}

      {ready && view === 'week' && (
        <Card className="table-card">
          <WeekFrame
            monday={monday} loading={q.isLoading} usedSlotIds={new Set(items.map((i) => i.periodSlotId))}
            renderCell={(slot, d) => {
              const cell = items.filter((i) => i.periodSlotId === slot.periodSlotId && i.date === toDateString(d));
              if (cell.length === 0) return <td className="tt-cell tt-cell--empty" />;
              return (
                <td className="tt-cell">
                  {cell.map((i) => (
                    <div key={i.sessionId} title={i.note ?? undefined}>
                      <div className="att-cell-subject">{i.subjectName}</div>
                      {i.date > today
                        ? <span className="att-cell-future">Chưa diễn ra</span>
                        : <StatusBadge domain="attendance" value={i.label} />}
                    </div>
                  ))}
                </td>
              );
            }}
          />
        </Card>
      )}

      {ready && view === 'list' && (
        <Card className="table-card">
          <DataTable<StudentAttendanceItem>
            rowKey="sessionId" loading={q.isLoading} dataSource={items.filter((i) => i.date <= today)} pagination={false}
            emptyText="Tuần này chưa có tiết nào đã diễn ra."
            columns={[
              { title: 'Ngày', key: 'date', width: 150, className: 'tabular', render: (_: unknown, i) => dayText(i.date) },
              { title: 'Tiết', key: 'slot', width: 130, render: (_: unknown, i) => `${sessionOfDayLabel(i.sessionOfDay)} · Tiết ${i.periodNo}` },
              { title: 'Môn', dataIndex: 'subjectName', width: 170 },
              { title: 'Giáo viên', key: 'teacher', width: 190, render: (_: unknown, i) => i.teacherName ?? '—' },
              { title: 'Trạng thái', key: 'label', width: 170, align: 'center', render: (_: unknown, i) => <StatusBadge domain="attendance" value={i.label} /> },
              { title: 'Ghi chú', key: 'note', render: (_: unknown, i) => i.note ?? <span className="muted">—</span> },
            ]}
          />
        </Card>
      )}
    </>
  );
}
