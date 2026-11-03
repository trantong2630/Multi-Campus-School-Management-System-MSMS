import { useQuery } from '@tanstack/react-query';
import { DatePicker, Segmented, Select } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { toDateString } from '@/shared/lib/format';
import { attApi } from '../api';
import { ClassSummaryView } from '../components/ClassSummaryView';
import { SessionTable } from '../components/SessionTable';

type View = 'sessions' | 'summary';
type Filter = 'all' | 'missing';

/**
 * School Admin, Hiệu trưởng, Hiệu phó: chọn lớp và khoảng ngày.
 * - Theo tiết: danh sách tiết; "Chưa điểm danh" là tiết đã qua chưa có bản ghi nào (4.5.6). SA mở tiết để nhập bù hoặc sửa (4.5.7, 4.5.8).
 * - Chuyên cần lớp: số liệu theo học sinh (4.5.13). HT, HP chỉ xem, không sửa (BR-17).
 */
export function ManageAttendanceView() {
  const { user } = useAuth();
  const isAdmin = user?.accountType === 'SchoolAdmin';
  const [view, setView] = useState<View>('sessions');
  const [filter, setFilter] = useState<Filter>(isAdmin ? 'missing' : 'all');
  const [classId, setClassId] = useState<number | undefined>();
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().subtract(6, 'day'), dayjs()]);
  const from = toDateString(range[0]);
  const to = toDateString(range[1]);

  const classes = useQuery({ queryKey: ['att', 'classes'], queryFn: attApi.classes });
  const campusCount = new Set(classes.data?.map((c) => c.campusId)).size;
  const sessions = useQuery({
    queryKey: ['att', 'sessions', classId, from, to, filter],
    queryFn: () => attApi.sessions(classId!, from, to, filter === 'missing'),
    enabled: classId !== undefined && view === 'sessions',
  });

  return (
    <>
      <PageHeader
        title="Điểm danh"
        description={isAdmin
          ? 'Chọn lớp và khoảng ngày để tìm tiết cần nhập bù. Nhập bù và sửa tiết đã qua ghi nhận người sửa trên từng bản ghi.'
          : 'Số liệu điểm danh theo lớp. Hiệu trưởng và hiệu phó xem, không sửa điểm danh.'}
      />
      <FilterBar>
        <Segmented<View>
          value={view} onChange={setView}
          options={[{ value: 'sessions', label: 'Theo tiết' }, { value: 'summary', label: 'Chuyên cần lớp' }]}
        />
        <Select
          showSearch optionFilterProp="label" placeholder="Chọn lớp" loading={classes.isLoading} value={classId} onChange={setClassId}
          options={classes.data?.map((c) => ({ value: c.classId, label: campusCount > 1 ? `${c.name} — ${c.campusName}` : c.name }))}
        />
        <DatePicker.RangePicker
          format="DD/MM/YYYY" allowClear={false} value={range}
          disabledDate={(d) => d.isAfter(dayjs(), 'day') && view === 'sessions' && filter === 'missing'}
          onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])}
        />
        {view === 'sessions' && (
          <Segmented<Filter>
            value={filter} onChange={setFilter}
            options={[{ value: 'missing', label: 'Chưa điểm danh' }, { value: 'all', label: 'Tất cả tiết' }]}
          />
        )}
      </FilterBar>

      {classId === undefined ? (
        <InlineAlert type="info" title="Chọn lớp để xem điểm danh" description="Danh sách gồm các lớp năm học hiện hành trong phạm vi được xem." />
      ) : view === 'sessions' ? (
        <SessionTable
          sessions={sessions.data ?? []} loading={sessions.isLoading} showDate
          emptyText={filter === 'missing' ? 'Không có tiết nào đã qua mà chưa điểm danh.' : 'Không có tiết nào trong khoảng ngày này.'}
        />
      ) : (
        <ClassSummaryView classId={classId} from={from} to={to} />
      )}
    </>
  );
}
