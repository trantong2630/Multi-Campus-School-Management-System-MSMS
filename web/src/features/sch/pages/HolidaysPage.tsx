import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input, Select } from 'antd';
import { message } from '@/shared/lib/antdStatic';
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
  const [open, setOpen] = useState(false);
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

  return (
    <>
      <PageHeader
        title="Ngày nghỉ"
        description="Ngày nghỉ khai trước khi phát hành thì phát hành bỏ qua ngày đó. Ngày nghỉ đột xuất khai ở Lịch dạy."
        extra={canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setOpen(true)} disabled={!effectiveYear}>Thêm ngày nghỉ</Button>}
      />
      <FilterBar>
        <Select
          placeholder="Năm học" value={effectiveYear} onChange={setYearId}
          options={years.data?.map((y) => ({ value: y.academicYearId, label: `Năm học ${y.name}` }))}
        />
        <Select
          allowClear placeholder="Toàn trường và mọi phân hiệu" value={campusId} onChange={setCampusId}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
      </FilterBar>
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
                  onClick: () => confirmAction({
                    title: `Xóa ngày nghỉ ${formatDate(h.holidayDate)}?`,
                    content: 'Xóa không khôi phục các tiết đã bị hủy trước đó. Muốn dạy lại thì thêm tiết bù.',
                    okText: 'Xóa', danger: true, onOk: () => remove.mutateAsync(h).catch(() => undefined),
                  }),
                }]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      <FormModal<FormValues>
        open={open} title="Thêm ngày nghỉ" form={form} onClose={() => setOpen(false)} onSubmit={save}
        initialValues={{ scope: WHOLE_SCHOOL }}
      >
        <Form.Item
          label="Ngày nghỉ" name="date" rules={[{ required: true, message: 'Chọn ngày nghỉ.' }]}
          extra={yearRange ? `Phải nằm trong năm học ${yearRange.name}.` : undefined}
        >
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Phạm vi" name="scope" rules={[{ required: true }]}>
          <Select options={[{ value: WHOLE_SCHOOL, label: 'Toàn trường' }, ...(campuses.data?.filter((c) => c.isActive).map((c) => ({ value: c.campusId, label: c.name })) ?? [])]} />
        </Form.Item>
        <Form.Item label="Lý do" name="reason"><Input maxLength={255} /></Form.Item>
      </FormModal>
    </>
  );
}
