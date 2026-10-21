import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, InputNumber, Select, TimePicker } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { Pencil, Plus, Power } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatTime, sessionOfDayLabel, toTimeString } from '@/shared/lib/format';
import { schApi } from '../api';
import { usePeriodSlots } from '../hooks/useCatalogs';
import type { PeriodSlot } from '../types';

interface FormValues { sessionOfDay: string; periodNo: number; times: [Dayjs, Dayjs] }

const toTime = (value: string) => dayjs(`2000-01-01T${value}`);

export function PeriodSlotsPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const { data, isLoading } = usePeriodSlots();
  const [form] = Form.useForm<FormValues>();
  const [editing, setEditing] = useState<PeriodSlot | 'new' | null>(null);

  const refresh = () => qc.invalidateQueries({ queryKey: ['sch', 'slots'] });
  const toggle = useMutation({
    mutationFn: (s: PeriodSlot) => schApi.periodSlots.setActive(s.periodSlotId, !s.isActive),
    onSuccess: () => { void refresh(); message.success('Đã cập nhật khung giờ.'); },
    onError: (e) => showError(e),
  });

  const save = async (v: FormValues) => {
    const times = { startTime: toTimeString(v.times[0]), endTime: toTimeString(v.times[1]) };
    if (editing === 'new') await schApi.periodSlots.create({ sessionOfDay: v.sessionOfDay, periodNo: v.periodNo, ...times });
    else if (editing) await schApi.periodSlots.updateTimes(editing.periodSlotId, times);
    await refresh();
    message.success('Đã lưu khung giờ tiết.');
  };

  return (
    <>
      <PageHeader
        title="Khung giờ tiết"
        description="Khung giờ dùng chung toàn trường, khóa là cặp (Buổi, Tiết). Số tiết mỗi buổi do trường tự khai."
        extra={canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm khung giờ</Button>}
      />
      <Card className="table-card">
        <DataTable<PeriodSlot>
          rowKey="periodSlotId" loading={isLoading} dataSource={data} pagination={false}
          emptyText="Chưa khai khung giờ tiết nào. Cần khai trước khi tải mẫu thời khóa biểu."
          columns={[
            { title: 'Buổi', dataIndex: 'sessionOfDay', width: 120, render: (v: string) => sessionOfDayLabel(v) },
            { title: 'Tiết', dataIndex: 'periodNo', width: 100, className: 'tabular' },
            { title: 'Bắt đầu', dataIndex: 'startTime', width: 130, className: 'tabular', render: (v: string) => formatTime(v) },
            { title: 'Kết thúc', dataIndex: 'endTime', width: 130, className: 'tabular', render: (v: string) => formatTime(v) },
            { title: 'Trạng thái', dataIndex: 'isActive', width: 160, align: 'center', render: (v: boolean) => <StatusBadge domain="catalog" value={v ? 'Active' : 'Inactive'} /> },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 120, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, s: PeriodSlot) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa giờ', icon: <Pencil size={16} />, onClick: () => setEditing(s) },
                  {
                    key: 'toggle', label: s.isActive ? 'Ngừng dùng' : 'Dùng lại', icon: <Power size={16} />, danger: s.isActive,
                    onClick: () => s.isActive
                      ? confirmAction({
                        title: `Ngừng dùng ${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo}?`,
                        content: 'Khung đang có tiết học hoặc thời khóa biểu tham chiếu thì hệ thống sẽ từ chối.',
                        okText: 'Ngừng dùng', danger: true, onOk: () => toggle.mutateAsync(s).catch(() => undefined),
                      })
                      : toggle.mutate(s),
                  },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      <FormModal<FormValues>
        open={editing !== null} title={editing === 'new' ? 'Thêm khung giờ tiết' : 'Sửa giờ của khung tiết'}
        form={form} onClose={() => setEditing(null)} onSubmit={save}
        initialValues={editing && editing !== 'new'
          ? { sessionOfDay: editing.sessionOfDay, periodNo: editing.periodNo, times: [toTime(editing.startTime), toTime(editing.endTime)] }
          : { sessionOfDay: 'Morning' }}
      >
        <Form.Item label="Buổi" name="sessionOfDay" rules={[{ required: true }]}>
          <Select disabled={editing !== 'new'} options={[{ value: 'Morning', label: 'Sáng' }, { value: 'Afternoon', label: 'Chiều' }]} />
        </Form.Item>
        <Form.Item label="Tiết" name="periodNo" rules={[{ required: true, message: 'Nhập số tiết (từ 1).' }]}>
          <InputNumber min={1} max={20} disabled={editing !== 'new'} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Giờ bắt đầu – kết thúc" name="times" rules={[{ required: true, message: 'Chọn giờ bắt đầu và giờ kết thúc.' }]}>
          <TimePicker.RangePicker format="HH:mm" minuteStep={5} needConfirm={false} style={{ width: '100%' }} />
        </Form.Item>
      </FormModal>
    </>
  );
}
