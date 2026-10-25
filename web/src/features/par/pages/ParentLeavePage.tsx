import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input, Radio, Select } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { Plus, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, formatDateTime, toDateString } from '@/shared/lib/format';
import { parApi } from '../api';
import type { LeaveRequestItem } from '../types';

interface SubmitLeaveValues {
  studentId: number;
  range: [Dayjs, Dayjs];
  sessionOfDay: string;
  reason?: string;
}

const SESSION_OF_DAY_LABEL: Record<string, string> = {
  AllDay: 'Cả ngày',
  Morning: 'Buổi sáng',
  Afternoon: 'Buổi chiều',
};

/**
 * PAR-01: Parent view leave requests for their children.
 * Features:
 * - View list of children
 * - View leave requests for selected child
 * - Submit new leave request
 * - Withdraw pending leave request
 */
export function ParentLeavePage() {
  const qc = useQueryClient();
  const [form] = Form.useForm<SubmitLeaveValues>();
  const [selectedStudentId, setSelectedStudentId] = useState<number | undefined>();
  const [modalOpen, setModalOpen] = useState(false);

  // Get list of parent's children
  const { data: children = [], isLoading: childrenLoading } = useQuery({
    queryKey: ['par', 'leave', 'my-children'],
    queryFn: () => parApi.leave.getMyChildren(),
  });

  // Set default selected student (first child)
  useEffect(() => {
    if (children.length > 0 && !selectedStudentId) {
      setSelectedStudentId(children[0].studentId);
    }
  }, [children, selectedStudentId]);

  // Get leave requests for selected child
  const { data: leaveRequests = [], isLoading: leavesLoading } = useQuery({
    queryKey: ['par', 'leave', 'student', selectedStudentId],
    queryFn: () => parApi.leave.getMyChildLeaves(selectedStudentId!),
    enabled: !!selectedStudentId,
  });

  // Submit leave request: FormModal handles modal closing and inline error presentation
  const handleSubmit = async (values: SubmitLeaveValues) => {
    await parApi.leave.submitLeaveRequest({
      studentId: values.studentId,
      fromDate: toDateString(values.range[0]),
      toDate: toDateString(values.range[1]),
      sessionOfDay: values.sessionOfDay,
      reason: values.reason?.trim() || undefined,
    });
    void qc.invalidateQueries({ queryKey: ['par', 'leave', 'student', selectedStudentId] });
    message.success('Gửi đơn xin nghỉ thành công.');
  };

  // Withdraw leave request mutation
  const withdrawLeave = useMutation({
    mutationFn: (id: number) => parApi.leave.withdrawLeaveRequest(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['par', 'leave', 'student', selectedStudentId] });
      message.success('Đã rút đơn xin nghỉ.');
    },
    onError: (e) => showError(e),
  });

  const handleWithdraw = (id: number) => {
    confirmAction({
      title: 'Rút đơn xin nghỉ?',
      content: 'Bạn chắc chắn muốn rút đơn này? Trạng thái sẽ chuyển về Đã rút.',
      okText: 'Rút đơn',
      onOk: () => withdrawLeave.mutateAsync(id),
    });
  };

  return (
    <>
      <PageHeader
        title="Đơn xin nghỉ học"
        extra={
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <Select
              style={{ width: 250 }}
              value={selectedStudentId}
              placeholder={childrenLoading ? 'Đang tải học sinh...' : 'Chọn học sinh'}
              onChange={setSelectedStudentId}
              options={children.map((c) => ({
                value: c.studentId,
                label: `${c.fullName}${c.className ? ` - ${c.className}` : ''}`,
              }))}
              loading={childrenLoading}
            />
            <Button
              type="primary"
              icon={<Plus size={16} />}
              disabled={!selectedStudentId || childrenLoading}
              onClick={() => {
                form.resetFields();
                setModalOpen(true);
              }}
            >
              Gửi đơn xin nghỉ
            </Button>
          </div>
        }
      />

      <Card>
        <DataTable<LeaveRequestItem>
          rowKey="leaveRequestId"
          loading={leavesLoading}
          dataSource={leaveRequests}
          emptyText={
            children.length === 0
              ? 'Tài khoản chưa được liên kết với học sinh nào.'
              : 'Học sinh chưa có đơn xin nghỉ nào.'
          }
          columns={[
            {
              title: 'Từ ngày',
              key: 'fromDate',
              width: 120,
              render: (_: unknown, record: LeaveRequestItem) => formatDate(record.fromDate),
            },
            {
              title: 'Đến ngày',
              key: 'toDate',
              width: 120,
              render: (_: unknown, record: LeaveRequestItem) => formatDate(record.toDate),
            },
            {
              title: 'Buổi',
              key: 'sessionOfDay',
              width: 110,
              render: (_: unknown, record: LeaveRequestItem) =>
                SESSION_OF_DAY_LABEL[record.sessionOfDay] || record.sessionOfDay,
            },
            {
              title: 'Lý do',
              key: 'reason',
              render: (_: unknown, record: LeaveRequestItem) => (
                <div>
                  <div>{record.reason || '—'}</div>
                  {record.decisionNote && (
                    <div style={{ fontSize: 12, color: 'var(--text-secondary, #666)', marginTop: 4 }}>
                      <strong>Phản hồi GV:</strong> {record.decisionNote}
                    </div>
                  )}
                </div>
              ),
            },
            {
              title: 'Ngày gửi',
              key: 'createdAt',
              width: 160,
              render: (_: unknown, record: LeaveRequestItem) => formatDateTime(record.createdAt),
            },
            {
              title: 'Trạng thái',
              key: 'status',
              width: 140,
              render: (_: unknown, record: LeaveRequestItem) => (
                <div>
                  <StatusBadge domain="leave" value={record.status} />
                  {record.decidedByPersonName && (
                    <div style={{ fontSize: 11, color: 'var(--text-secondary, #888)', marginTop: 2 }}>
                      {record.decidedByPersonName}
                    </div>
                  )}
                </div>
              ),
            },
            {
              title: 'Thao tác',
              key: 'actions',
              width: 110,
              fixed: 'right',
              align: 'center',
              render: (_: unknown, record: LeaveRequestItem) =>
                record.status === 'Pending' ? (
                  <RowActions
                    actions={[
                      {
                        key: 'withdraw',
                        label: 'Rút đơn',
                        icon: <Trash2 size={16} />,
                        danger: false,
                        onClick: () => handleWithdraw(record.leaveRequestId),
                      },
                    ]}
                  />
                ) : null,
            },
          ]}
        />
      </Card>

      <FormModal<SubmitLeaveValues>
        open={modalOpen}
        title="Gửi đơn xin nghỉ học"
        form={form}
        onClose={() => setModalOpen(false)}
        onSubmit={handleSubmit}
        okText="Gửi đơn"
        initialValues={{
          studentId: selectedStudentId,
          sessionOfDay: 'AllDay',
        }}
      >
        <Form.Item
          label="Học sinh"
          name="studentId"
          rules={[{ required: true, message: 'Chọn học sinh.' }]}
        >
          <Select
            options={children.map((c) => ({
              value: c.studentId,
              label: `${c.fullName}${c.className ? ` - ${c.className}` : ''}`,
            }))}
            disabled
          />
        </Form.Item>

        <Form.Item
          label="Khoảng ngày"
          name="range"
          rules={[{ required: true, message: 'Chọn ngày bắt đầu và ngày kết thúc.' }]}
        >
          <DatePicker.RangePicker
            format="DD/MM/YYYY"
            style={{ width: '100%' }}
            disabledDate={(current) => current && current < dayjs().startOf('day')}
          />
        </Form.Item>

        <Form.Item
          label="Buổi"
          name="sessionOfDay"
          rules={[{ required: true, message: 'Chọn buổi xin nghỉ.' }]}
        >
          <Radio.Group
            options={[
              { label: 'Cả ngày', value: 'AllDay' },
              { label: 'Buổi sáng', value: 'Morning' },
              { label: 'Buổi chiều', value: 'Afternoon' },
            ]}
          />
        </Form.Item>

        <Form.Item
          label="Lý do"
          name="reason"
        >
          <Input.TextArea
            rows={3}
            maxLength={500}
            placeholder="Nhập lý do xin nghỉ..."
            showCount
          />
        </Form.Item>
      </FormModal>
    </>
  );
}
