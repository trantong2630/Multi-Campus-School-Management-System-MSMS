import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, Modal, Popconfirm } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import { Check, X } from 'lucide-react';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonCell } from '@/shared/components/PersonCell';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { showError } from '@/shared/lib/feedback';
import { formatDate, formatDateTime } from '@/shared/lib/format';
import { parApi } from '../api';
import type { HomeroomLeaveItem } from '../types';

const SESSION_OF_DAY_LABEL: Record<string, string> = {
  AllDay: 'Cả ngày',
  Morning: 'Buổi sáng',
  Afternoon: 'Buổi chiều',
};

interface RejectValues {
  decisionNote: string;
}

/**
 * PAR-03: Homeroom teacher view and approve/reject leave requests from their class.
 * Features:
 * - View leave requests for the class
 * - Approve pending requests
 * - Reject pending requests with reason
 * - View decision history
 */
export function HomeroomLeavePage() {
  const qc = useQueryClient();
  const [form] = Form.useForm<RejectValues>();
  const [rejectingId, setRejectingId] = useState<number | null>(null);
  const [rejectModalOpen, setRejectModalOpen] = useState(false);

  // Get homeroom leave requests
  const { data: leaves = [], isLoading } = useQuery({
    queryKey: ['par', 'leave', 'homeroom'],
    queryFn: () => parApi.leave.getHomeroomLeaves(),
  });

  // Approve leave request mutation
  const approveMutation = useMutation({
    mutationFn: (id: number) => parApi.leave.approveLeaveRequest(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['par', 'leave', 'homeroom'] });
      message.success('Đã duyệt đơn xin nghỉ.');
    },
    onError: (e) => showError(e),
  });

  // Reject leave request mutation
  const rejectMutation = useMutation({
    mutationFn: ({ id, decisionNote }: { id: number; decisionNote: string }) =>
      parApi.leave.rejectLeaveRequest(id, decisionNote),
    onSuccess: () => {
      setRejectingId(null);
      setRejectModalOpen(false);
      form.resetFields();
      void qc.invalidateQueries({ queryKey: ['par', 'leave', 'homeroom'] });
      message.success('Đã từ chối đơn xin nghỉ.');
    },
    onError: (e) => showError(e),
  });

  const handleApprove = (id: number) => {
    approveMutation.mutate(id);
  };

  const handleRejectClick = (id: number) => {
    setRejectingId(id);
    form.resetFields();
    setRejectModalOpen(true);
  };

  const handleRejectSubmit = async (values: RejectValues) => {
    if (!rejectingId) return;
    await rejectMutation.mutateAsync({ id: rejectingId, decisionNote: values.decisionNote });
  };

  const renderActions = (record: HomeroomLeaveItem) => {
    if (record.status === 'Pending') {
      return (
        <div style={{ display: 'flex', gap: 8 }}>
          <Popconfirm
            title="Duyệt đơn xin nghỉ này?"
            onConfirm={() => handleApprove(record.leaveRequestId)}
            okText="Duyệt"
            cancelText="Đóng"
            disabled={approveMutation.isPending}
          >
            <Button
              type="primary"
              size="small"
              icon={<Check size={14} />}
              loading={approveMutation.isPending && approveMutation.variables === record.leaveRequestId}
            >
              Duyệt
            </Button>
          </Popconfirm>
          <Button
            danger
            size="small"
            icon={<X size={14} />}
            onClick={() => handleRejectClick(record.leaveRequestId)}
            loading={rejectMutation.isPending && rejectingId === record.leaveRequestId}
          >
            Từ chối
          </Button>
        </div>
      );
    }

    // Show decision info for approved or rejected requests
    return (
      <div style={{ fontSize: '12px', color: 'var(--c-text2)' }}>
        <div>{record.decidedByPersonName || '—'}</div>
        {record.decidedAt && <div>{formatDateTime(record.decidedAt)}</div>}
        {record.decisionNote && <div style={{ marginTop: 4 }}>{record.decisionNote}</div>}
      </div>
    );
  };

  return (
    <>
      <PageHeader title="Hộp đơn xin nghỉ của lớp chủ nhiệm" />

      <Card>
        <DataTable<HomeroomLeaveItem>
          rowKey="leaveRequestId"
          loading={isLoading}
          dataSource={leaves}
          emptyText="Lớp chủ nhiệm hiện chưa có đơn xin nghỉ nào."
          columns={[
            {
              title: 'Học sinh',
              key: 'studentName',
              width: 200,
              render: (_: unknown, record: HomeroomLeaveItem) => (
                <PersonCell name={record.studentName} sub={record.studentCode} />
              ),
            },
            {
              title: 'Thời gian nghỉ',
              key: 'timeRange',
              width: 180,
              render: (_: unknown, record: HomeroomLeaveItem) =>
                `${formatDate(record.fromDate)} – ${formatDate(record.toDate)}`,
            },
            {
              title: 'Buổi',
              key: 'sessionOfDay',
              width: 100,
              render: (_: unknown, record: HomeroomLeaveItem) =>
                SESSION_OF_DAY_LABEL[record.sessionOfDay] || record.sessionOfDay,
            },
            {
              title: 'Lý do',
              key: 'reason',
              render: (_: unknown, record: HomeroomLeaveItem) => record.reason || '—',
            },
            {
              title: 'Ngày gửi',
              key: 'createdAt',
              width: 160,
              className: 'tabular',
              render: (_: unknown, record: HomeroomLeaveItem) => formatDateTime(record.createdAt),
            },
            {
              title: 'Trạng thái',
              key: 'status',
              width: 130,
              render: (_: unknown, record: HomeroomLeaveItem) => (
                <StatusBadge domain="leave" value={record.status} />
              ),
            },
            {
              title: 'Thao tác',
              key: 'actions',
              width: 220,
              fixed: 'right',
              render: (_: unknown, record: HomeroomLeaveItem) => renderActions(record),
            },
          ]}
        />
      </Card>

      <Modal
        title="Từ chối đơn xin nghỉ"
        open={rejectModalOpen}
        onCancel={() => {
          setRejectModalOpen(false);
          setRejectingId(null);
          form.resetFields();
        }}
        onOk={() => form.submit()}
        okText="Từ chối"
        okButtonProps={{ danger: true, loading: rejectMutation.isPending }}
        cancelText="Đóng"
      >
        <Form<RejectValues>
          form={form}
          layout="vertical"
          requiredMark={false}
          onFinish={handleRejectSubmit}
        >
          <Form.Item
            label="Lý do từ chối"
            name="decisionNote"
            rules={[{ required: true, whitespace: true, message: 'Nhập lý do từ chối đơn xin nghỉ.' }]}
          >
            <Input.TextArea
              rows={4}
              maxLength={500}
              placeholder="Nhập lý do từ chối..."
              showCount
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
