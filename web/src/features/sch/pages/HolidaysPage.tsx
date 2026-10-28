import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input, Segmented, Select } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { schApi } from '../api';
import { HolidayCalendar } from '../components/HolidayCalendar';
import { useCampuses, useCurrentYear, useYears } from '../hooks/useCatalogs';
import type { Holiday } from '../types';

interface FormValues { date: Dayjs; scope: number | 0; reason?: string }

const WHOLE_SCHOOL = 0;

export function HolidaysPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const years = useYears();
  const { current } = useCurrentYear();
  const campuses = useCampuses();
  const [yearId, setYearId] = useState<number | undefined>();
  const [campusId, setCampusId] = useState<number | undefined>();
  const [view, setView] = useState<'calendar' | 'list'>('calendar');
  const [monthPick, setMonthPick] = useState<Dayjs | null>(null);
  const [adding, setAdding] = useState<{ date?: Dayjs } | null>(null);
  const [form] = Form.useForm<FormValues>();

  const effectiveYear = yearId ?? current?.academicYearId;
  const holidays = useQuery({
    queryKey: ['sch', 'holidays', effectiveYear, campusId],
    queryFn: () => schApi.holidays.list({ yearId: effectiveYear, campusId }),
    enabled: effectiveYear !== undefined,
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['sch', 'holidays'] });
  const remove = useMutation({
    mutationFn: (h: Holiday) => schApi.holidays.remove(h.holidayId),
    onSuccess: () => { void refresh(); message.success('Đã xóa ngày nghỉ.'); },
    onError: (e) => showError(e),
  });

  const save = async (v: FormValues) => {
    await schApi.holidays.create({
      academicYearId: effectiveYear, campusId: v.scope === WHOLE_SCHOOL ? null : v.scope,
      holidayDate: toDateString(v.date), reason: v.reason,
    });
    await refresh();
    message.success('Đã khai báo ngày nghỉ.');
  };

  const yearRange = years.data?.find((y) => y.academicYearId === effectiveYear);

  // Mở lịch ở tháng hiện tại nếu nằm trong năm học, không thì ở tháng đầu hoặc cuối năm học gần nhất; đổi năm học thì chọn lại.
  const defaultMonth = () => {
    const now = dayjs();
    if (!yearRange) return now.startOf('month');
    if (now.isBefore(yearRange.startDate, 'day')) return dayjs(yearRange.startDate).startOf('month');
    if (now.isAfter(yearRange.endDate, 'day')) return dayjs(yearRange.endDate).startOf('month');
    return now.startOf('month');
  };
  const month = monthPick ?? defaultMonth();

  const askRemove = (h: Holiday) => confirmAction({
    title: `Xóa ngày nghỉ ${formatDate(h.holidayDate)}?`,
    content: 'Xóa không khôi phục các tiết đã bị hủy trước đó. Muốn dạy lại thì thêm tiết bù.',
    okText: 'Xóa', danger: true, onOk: () => remove.mutateAsync(h).catch(() => undefined),
  });

  return (
    <>
      <PageHeader
        title="Ngày nghỉ"
        description="Ngày nghỉ khai trước khi phát hành thì phát hành bỏ qua ngày đó. Ngày nghỉ đột xuất khai ở Lịch dạy."
        extra={canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setAdding({})} disabled={!effectiveYear}>Thêm ngày nghỉ</Button>}
      />
      <FilterBar>
        <Segmented<'calendar' | 'list'>
          value={view} onChange={setView}
          options={[{ value: 'calendar', label: 'Lịch' }, { value: 'list', label: 'Danh sách' }]}
        />
        <Select
          placeholder="Năm học" value={effectiveYear} onChange={(v) => { setYearId(v); setMonthPick(null); }}
          options={years.data?.map((y) => ({ value: y.academicYearId, label: `Năm học ${y.name}` }))}
        />
        <Select
          allowClear placeholder="Toàn trường và mọi phân hiệu" value={campusId} onChange={setCampusId}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
      </FilterBar>
      {view === 'calendar' && yearRange && (
        <HolidayCalendar
          month={month} onMonthChange={setMonthPick} yearStart={yearRange.startDate} yearEnd={yearRange.endDate}
          holidays={holidays.data ?? []} canWrite={canWrite} onAdd={(date) => setAdding({ date })} onRemove={askRemove}
        />
      )}
      {view === 'list' && (
      <Card className="table-card">
        <DataTable<Holiday>
          rowKey="holidayId" loading={holidays.isLoading || years.isLoading} dataSource={holidays.data}
          emptyText="Chưa khai ngày nghỉ nào trong năm học này."
          columns={[
            { title: 'Ngày', dataIndex: 'holidayDate', width: 130, className: 'tabular', render: (v: string) => formatDate(v) },
            { title: 'Thứ', dataIndex: 'holidayDate', key: 'weekday', width: 90, render: (v: string) => weekdayOfDate(v) },
            { title: 'Phạm vi', dataIndex: 'campusName', width: 200, render: (v: string | null) => v ?? 'Toàn trường' },
            { title: 'Lý do', dataIndex: 'reason', render: (v: string | null) => v ?? <span className="muted">—</span> },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 100, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, h: Holiday) => (
                <RowActions actions={[{
                  key: 'delete', label: 'Xóa ngày nghỉ', icon: <Trash2 size={16} />, danger: true,
                  onClick: () => askRemove(h),
                }]} />
              ),
            }] : []),
          ]}
        />
      </Card>
      )}

      <FormModal<FormValues>
        open={adding !== null} title="Thêm ngày nghỉ" form={form} onClose={() => setAdding(null)} onSubmit={save}
        initialValues={{ scope: campusId ?? WHOLE_SCHOOL, date: adding?.date }}
      >
        <Form.Item
          label="Ngày nghỉ" name="date" rules={[{ required: true, message: 'Chọn ngày nghỉ.' }]}
          extra={yearRange ? `Phải nằm trong năm học ${yearRange.name}.` : undefined}
        >
          <DatePicker
            format="DD/MM/YYYY" style={{ width: '100%' }}
            disabledDate={(d) => !!yearRange && (d.isBefore(yearRange.startDate, 'day') || d.isAfter(yearRange.endDate, 'day'))}
          />
        </Form.Item>
        <Form.Item label="Phạm vi" name="scope" rules={[{ required: true }]}>
          <Select options={[{ value: WHOLE_SCHOOL, label: 'Toàn trường' }, ...(campuses.data?.filter((c) => c.isActive).map((c) => ({ value: c.campusId, label: c.name })) ?? [])]} />
        </Form.Item>
        <Form.Item label="Lý do" name="reason"><Input maxLength={255} /></Form.Item>
      </FormModal>
    </>
  );
}
