import { useQuery } from '@tanstack/react-query';
import { DatePicker, Segmented, Select } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { FilterBar } from '@/shared/components/FilterBar';
import { PageHeader } from '@/shared/components/PageHeader';
import { toDateString } from '@/shared/lib/format';
import { attApi } from '../api';
import { dayText } from '../attFormat';
import { ClassSummaryView } from '../components/ClassSummaryView';
import { SessionTable } from '../components/SessionTable';

type View = 'today' | 'homeroom';
type Mode = 'day' | 'range';

/** Giáo viên: tiết hôm nay (4.5.1); nếu đang chủ nhiệm thêm chuyên cần lớp chủ nhiệm (4.5.13). */
export function TeacherAttendanceView() {
  const { user } = useAuth();
  const isHomeroom = !!user?.scope?.homeroomClassIds.length;
  const [view, setView] = useState<View>('today');

  const today = useQuery({ queryKey: ['att', 'today'], queryFn: attApi.today, enabled: view === 'today' });
  const date = today.data?.[0]?.date ?? toDateString(dayjs());

  return (
    <>
      <PageHeader
        title="Điểm danh"
      />
      {isHomeroom && (
        <FilterBar>
          <Segmented<View>
            value={view} onChange={setView}
            options={[{ value: 'today', label: 'Tiết hôm nay' }, { value: 'homeroom', label: 'Lớp chủ nhiệm' }]}
          />
        </FilterBar>
      )}
      {view === 'today'
        ? <SessionTable sessions={today.data ?? []} loading={today.isLoading} showClass emptyText="Hôm nay không có tiết dạy nào." />
        : <HomeroomPanel />}
    </>
  );
}

/** Chuyên cần lớp chủ nhiệm (4.5.13): chọn theo ngày hoặc khoảng ngày. */
export function HomeroomPanel() {
  const classes = useQuery({ queryKey: ['att', 'classes'], queryFn: attApi.classes });
  const [classId, setClassId] = useState<number | undefined>();
  const [mode, setMode] = useState<Mode>('day');
  const [day, setDay] = useState<Dayjs>(() => dayjs());
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf('week'), dayjs()]);
  const selected = classId ?? classes.data?.[0]?.classId;
  const [from, to] = mode === 'day' ? [toDateString(day), toDateString(day)] : [toDateString(range[0]), toDateString(range[1])];

  return (
    <>
      <FilterBar>
        <Segmented<Mode> value={mode} onChange={setMode} options={[{ value: 'day', label: 'Theo ngày' }, { value: 'range', label: 'Khoảng ngày' }]} />
        {(classes.data?.length ?? 0) > 1 && (
          <Select placeholder="Lớp" value={selected} onChange={setClassId} options={classes.data?.map((c) => ({ value: c.classId, label: c.name }))} />
        )}
        {mode === 'day'
          ? <DatePicker format="DD/MM/YYYY" allowClear={false} value={day} onChange={(d) => d && setDay(d)} />
          : (
            <DatePicker.RangePicker
              format="DD/MM/YYYY" allowClear={false} value={range}
              onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])}
            />
          )}
      </FilterBar>
      {selected !== undefined && <ClassSummaryView classId={selected} from={from} to={to} />}
    </>
  );
}
