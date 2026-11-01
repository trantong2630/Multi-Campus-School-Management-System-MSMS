import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input, Modal, Segmented, Select } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { Plus, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { CSSProperties } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, formatDateTime, toDateString } from '@/shared/lib/format';
import { parApi } from '../api';
import type { LeaveDayInput, LeaveRequestItem } from '../types';

type SubmitMode = 'range' | 'flexible';

type DayKind = 'AllDay' | 'Morning' | 'Afternoon' | 'MorningPeriods' | 'AfternoonPeriods';

interface LeaveDayFormValue {
  date?: Dayjs;
  kind?: DayKind;
  slotIds?: number[];
}

interface SubmitLeaveValues {
  studentId: number;
  range?: [Dayjs, Dayjs];
  kind?: DayKind;
  slotIds?: number[];
  days: LeaveDayFormValue[];
  reason?: string;
}

const SESSION_OF_DAY_LABEL: Record<string, string> = {
  AllDay: 'Cả ngày',
  Morning: 'Buổi sáng',
  Afternoon: 'Buổi chiều',
};

const DAY_KIND_OPTIONS: { label: string; value: DayKind }[] = [
  { label: 'Cả ngày', value: 'AllDay' },
  { label: 'Buổi sáng', value: 'Morning' },
  { label: 'Buổi chiều', value: 'Afternoon' },
  { label: 'Tiết lẻ - buổi sáng', value: 'MorningPeriods' },
  { label: 'Tiết lẻ - buổi chiều', value: 'AfternoonPeriods' },
];

const PERIOD_OPTIONS = [1, 2, 3, 4, 5].map((no) => ({ label: `Tiết ${no}`, value: no }));

const MODE_OPTIONS: { label: string; value: SubmitMode }[] = [
  { label: 'Nghỉ liên tiếp (Khoảng ngày)', value: 'range' },
  { label: 'Nghỉ linh hoạt (Theo từng ngày)', value: 'flexible' },
];

const isPeriodKind = (kind?: DayKind) => kind === 'MorningPeriods' || kind === 'AfternoonPeriods';

const ellipsisStyle: CSSProperties = { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' };

/** Tách kiểu nghỉ trên form thành (buổi, tiết) đúng với hợp đồng API. */
const toSessionAndSlots = (kind: DayKind = 'AllDay', slotIds?: number[]) => ({
  sessionOfDay: kind === 'AllDay' ? 'AllDay' : kind.startsWith('Morning') ? 'Morning' : 'Afternoon',
  slotIds: isPeriodKind(kind) ? slotIds ?? [] : undefined,
});

const toLeaveDayInput = (day: LeaveDayFormValue): LeaveDayInput => ({
  date: toDateString(day.date!),
  ...toSessionAndSlots(day.kind, day.slotIds),
});

const formatSession = (record: LeaveRequestItem) => {
  const session = SESSION_OF_DAY_LABEL[record.sessionOfDay] || record.sessionOfDay;
  if (!record.slotIds) return session;
  return `${session} - Tiết ${record.slotIds.split(',').join(', ')}`;
};

/**
 * PAR-01: Parent view leave requests for their children.
 * Features:
 * - View list of children
 * - View leave requests for selected child (kể cả tiết lẻ)
 * - Submit one continuous range (1 đơn) or flexible per-day rows (N đơn, một lần gửi)
 * - Withdraw pending leave request
 */
export function ParentLeavePage() {
  const qc = useQueryClient();
  const [form] = Form.useForm<SubmitLeaveValues>();
  const [selectedStudentId, setSelectedStudentId] = useState<number | undefined>();
  const [modalOpen, setModalOpen] = useState(false);
  const [mode, setMode] = useState<SubmitMode>('range');
  const [detail, setDetail] = useState<LeaveRequestItem | null>(null);

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

  // Chế độ range: 1 đơn duy nhất cho cả khoảng ngày; chế độ flexible: N đơn (mỗi ngày một đơn)
  const handleSubmit = async (values: SubmitLeaveValues) => {
    const reason = values.reason?.trim() || undefined;

    if (mode === 'range') {
      const [from, to] = values.range!;
      const { sessionOfDay, slotIds } = toSessionAndSlots(values.kind, values.slotIds);
      await parApi.leave.submitLeaveRequest({
        studentId: values.studentId,
        fromDate: toDateString(from),
        toDate: toDateString(to),
        sessionOfDay,
        slotIds,
        reason,
      });
    } else {
      await parApi.leave.submitBulkLeaveRequests({
        studentId: values.studentId,
        reason,
        days: values.days.map(toLeaveDayInput),
      });
    }

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

  const openModal = () => {
    form.resetFields();
    setMode('range');
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setMode('range');
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
              onClick={openModal}
            >
              Gửi đơn xin nghỉ
            </Button>
          </div>
        }
      />

      <Card>
        <DataTable<LeaveRequestItem>
          rowKey="leaveRequestId"
          tableLayout="fixed"
          scroll={{ x: 1010 }}
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
              title: 'Buổi / Tiết',
              key: 'sessionOfDay',
              width: 180,
              render: (_: unknown, record: LeaveRequestItem) => formatSession(record),
            },
            {
              title: 'Lý do',
              key: 'reason',
              width: 180,
              render: (_: unknown, record: LeaveRequestItem) => (
                <Button
                  type="link"
                  size="small"
                  onClick={() => setDetail(record)}
                  style={{ padding: 0, height: 'auto', maxWidth: '100%', textAlign: 'left' }}
                >
                  <span style={{ ...ellipsisStyle, display: 'block' }}>{record.reason || 'Xem chi tiết'}</span>
                </Button>
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
        width={640}
        onClose={closeModal}
        onSubmit={handleSubmit}
        okText="Gửi đơn"
        initialValues={{
          studentId: selectedStudentId,
          kind: 'AllDay',
          days: [{ kind: 'AllDay' }],
        }}
      >
        <Form.Item label="Chế độ gửi">
          <Segmented<SubmitMode>
            block
            value={mode}
            onChange={setMode}
            options={MODE_OPTIONS}
          />
        </Form.Item>

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

        {mode === 'range' ? (
          <>
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

            <Form.Item label="Kiểu nghỉ" name="kind" rules={[{ required: true, message: 'Chọn kiểu nghỉ.' }]}>
              <Select options={DAY_KIND_OPTIONS} />
            </Form.Item>

            <Form.Item
              noStyle
              shouldUpdate={(prev, cur) => prev.kind !== cur.kind}
            >
              {({ getFieldValue }) =>
                isPeriodKind(getFieldValue('kind')) ? (
                  <Form.Item
                    label="Các tiết nghỉ"
                    name="slotIds"
                    rules={[{ required: true, message: 'Chọn ít nhất một tiết.' }]}
                  >
                    <Select mode="multiple" options={PERIOD_OPTIONS} placeholder="Chọn tiết" />
                  </Form.Item>
                ) : null
              }
            </Form.Item>
          </>
        ) : (
          <Form.Item label="Các ngày nghỉ" required>
            <Form.List
              name="days"
              rules={[
                {
                  validator: async (_, days: LeaveDayFormValue[] | undefined) => {
                    if (!days || days.length === 0) throw new Error('Thêm ít nhất một ngày nghỉ.');
                  },
                },
              ]}
            >
              {(fields, { add, remove }, { errors }) => (
                <>
                  {fields.map((field) => (
                    <div key={field.key} style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                      <Form.Item
                        name={[field.name, 'date']}
                        rules={[{ required: true, message: 'Chọn ngày.' }]}
                        style={{ flex: '0 0 160px' }}
                      >
                        <DatePicker
                          format="DD/MM/YYYY"
                          style={{ width: '100%' }}
                          placeholder="Ngày nghỉ"
                          disabledDate={(current) => current && current < dayjs().startOf('day')}
                        />
                      </Form.Item>

                      <Form.Item name={[field.name, 'kind']} style={{ flex: '0 0 190px' }}>
                        <Select options={DAY_KIND_OPTIONS} />
                      </Form.Item>

                      <Form.Item
                        noStyle
                        shouldUpdate={(prev, cur) =>
                          prev.days?.[field.name]?.kind !== cur.days?.[field.name]?.kind}
                      >
                        {({ getFieldValue }) =>
                          isPeriodKind(getFieldValue(['days', field.name, 'kind'])) ? (
                            <Form.Item
                              name={[field.name, 'slotIds']}
                              rules={[{ required: true, message: 'Chọn ít nhất một tiết.' }]}
                              style={{ flex: 1 }}
                            >
                              <Select mode="multiple" options={PERIOD_OPTIONS} placeholder="Chọn tiết" />
                            </Form.Item>
                          ) : null
                        }
                      </Form.Item>

                      {fields.length > 1 && (
                        <Button
                          type="text"
                          danger
                          icon={<Trash2 size={16} />}
                          onClick={() => remove(field.name)}
                          aria-label="Xóa ngày nghỉ"
                        />
                      )}
                    </div>
                  ))}

                  <Form.ErrorList errors={errors} />

                  <Button
                    type="dashed"
                    block
                    icon={<Plus size={16} />}
                    onClick={() => add({ kind: 'AllDay' })}
                  >
                    Thêm ngày nghỉ
                  </Button>
                </>
              )}
            </Form.List>
          </Form.Item>
        )}

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

      <Modal
        open={!!detail}
        title="Chi tiết đơn xin nghỉ"
        onCancel={() => setDetail(null)}
        footer={<Button onClick={() => setDetail(null)}>Đóng</Button>}
      >
        {detail && (
          <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', rowGap: 8 }}>
            <span>Thời gian</span>
            <span>{formatDate(detail.fromDate)} – {formatDate(detail.toDate)}</span>
            <span>Buổi / Tiết</span>
            <span>{formatSession(detail)}</span>
            <span>Lý do</span>
            <span style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{detail.reason || '—'}</span>
            <span>Trạng thái</span>
            <span><StatusBadge domain="leave" value={detail.status} /></span>
            {detail.decisionNote && (
              <>
                <span>Phản hồi GV</span>
                <span style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{detail.decisionNote}</span>
              </>
            )}
            <span>Ngày gửi</span>
            <span>{formatDateTime(detail.createdAt)}</span>
          </div>
        )}
      </Modal>
    </>
  );
}
