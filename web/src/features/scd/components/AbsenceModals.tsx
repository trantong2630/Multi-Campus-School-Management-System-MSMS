import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Checkbox, DatePicker, Form, Input, Modal, Radio, Select } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { schApi } from '@/features/sch/api';
import { FormModal } from '@/shared/components/FormModal';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { getErrorMessage } from '@/app/apiClient';
import { message } from '@/shared/lib/antdStatic';
import { toDateString } from '@/shared/lib/format';
import { scdApi } from '../api';
import { slotLabel } from '../labels';
import type { ResolveAction } from '../types';
import { TeacherSelect } from './TeacherSelect';
import './scd.css';

// ---------------------------------------------------------------- Khai báo giáo viên vắng
type Scope = 'days' | 'slots';

/**
 * Khai báo giáo viên vắng (BR-16): theo ngày (khoảng ngày, cả ngày) hoặc theo tiết (một ngày, tick các tiết vắng).
 * Khai báo không tự đổi lịch — các tiết bị ảnh hưởng ở phân hiệu của bạn chuyển sang Chờ xử lý và để trống giáo viên.
 * Đây không phải đơn xin nghỉ của cán bộ.
 */
export function DeclareAbsenceModal({ open, campusId, onClose, onDone }: { open: boolean; campusId: number; onClose: () => void; onDone: () => void }) {
  const qc = useQueryClient();
  const [teacherId, setTeacherId] = useState<number | undefined>();
  const [scope, setScope] = useState<Scope>('days');
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [day, setDay] = useState<Dayjs | null>(null);
  const [slotIds, setSlotIds] = useState<number[]>([]);
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const teachers = useQuery({ queryKey: ['scd', 'absenceTeachers', campusId], queryFn: () => scdApi.absenceTeachers(campusId), enabled: open });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list, enabled: open });
  const activeSlots = (slots.data ?? []).filter((p) => p.isActive)
    .sort((a, b) => (a.sessionOfDay === b.sessionOfDay ? a.periodNo - b.periodNo : a.sessionOfDay === 'Morning' ? -1 : 1));

  const ready = teacherId !== undefined && (scope === 'days' ? range !== null : day !== null && slotIds.length > 0);

  const reset = () => { setTeacherId(undefined); setScope('days'); setRange(null); setDay(null); setSlotIds([]); setReason(''); setError(null); };
  const close = () => { reset(); onClose(); };

  const submit = async () => {
    if (!ready) return;
    setSubmitting(true);
    setError(null);
    try {
      const from = scope === 'days' ? range![0] : day!;
      const to = scope === 'days' ? range![1] : day!;
      const res = await scdApi.declareAbsence({
        teacherId, fromDate: toDateString(from), toDate: toDateString(to),
        slotIds: scope === 'slots' ? slotIds : undefined, reason: reason.trim() || undefined,
      });
      message.success(`Đã khai báo: ${res.pendingCount} tiết chuyển sang Chờ xử lý. Nhấp vào tiết viền vàng trên lưới Điều chỉnh hoặc mở danh sách Tiết chờ xử lý để xử lý.`);
      await qc.invalidateQueries({ queryKey: ['scd'] });
      reset();
      onDone();
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      open={open} width={600} onCancel={close} mask={{ closable: false }} title="Khai báo giáo viên vắng"
      okText="Khai báo vắng" cancelText="Đóng" onOk={submit} confirmLoading={submitting} okButtonProps={{ disabled: !ready }}
    >
      <p className="muted" style={{ marginTop: 0 }}>
        Chỉ ghi nhận hệ quả lên lịch dạy, không phải đơn xin nghỉ của cán bộ. Chỉ ảnh hưởng tiết ở phân hiệu của bạn; giáo viên dạy phân hiệu khác thì hiệu phó phân hiệu đó tự khai.
      </p>
      <Form layout="vertical" requiredMark={false} onValuesChange={() => setError(null)}>
        <Form.Item label="Giáo viên" required>
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn giáo viên" loading={teachers.isLoading} value={teacherId} onChange={setTeacherId}
            notFoundContent="Phân hiệu chưa có giáo viên nào còn tiết từ hôm nay"
            options={teachers.data?.map((t) => ({ value: t.teacherId, label: t.abbr ? `${t.name} (${t.abbr})` : t.name }))}
          />
        </Form.Item>
        <Form.Item label="Vắng theo" required>
          <Radio.Group
            value={scope} onChange={(e) => setScope(e.target.value as Scope)} optionType="button" buttonStyle="solid"
            options={[{ value: 'days', label: 'Ngày (cả ngày)' }, { value: 'slots', label: 'Tiết trong một ngày' }]}
          />
        </Form.Item>
        {scope === 'days' ? (
          <Form.Item label="Từ ngày – đến ngày" required>
            <DatePicker.RangePicker
              format="DD/MM/YYYY" style={{ width: '100%' }} value={range}
              onChange={(v) => setRange(v && v[0] && v[1] ? [v[0], v[1]] : null)}
              disabledDate={(d) => d.isBefore(dayjs(), 'day')}
            />
          </Form.Item>
        ) : (
          <>
            <Form.Item label="Ngày" required>
              <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} value={day} onChange={setDay} disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
            </Form.Item>
            <Form.Item label="Các tiết vắng" required>
              <Checkbox.Group
                value={slotIds} onChange={(v) => setSlotIds(v as number[])}
                options={activeSlots.map((p) => ({ value: p.periodSlotId, label: slotLabel(p) }))}
                style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}
              />
            </Form.Item>
          </>
        )}
        <Form.Item label="Lý do (không bắt buộc)">
          <Input maxLength={255} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Form.Item>
      </Form>
      {error && <InlineAlert type="error" title={error} />}
    </Modal>
  );
}

// ---------------------------------------------------------------- Xử lý tiết Chờ xử lý
/** Thông tin tối thiểu của một tiết Chờ xử lý, đủ cho cả lưới Điều chỉnh lẫn danh sách tiết chờ. */
export interface ResolveTarget {
  sessionId: number;
  title: string;
  /** Dòng mô tả: ngày, khung giờ, nguyên nhân. */
  summary: string;
}

interface ResolveValues {
  action: ResolveAction;
  teacherId?: number;
}

/**
 * Hai lựa chọn cho một tiết Chờ xử lý (BR-16): chọn giáo viên dạy thay từ gợi ý giáo viên rảnh, hoặc hủy.
 * Tiết đã qua ngày bị khóa nên không xử lý nữa. Muốn bù thì thêm tiết bù vào ô trống sau khi hủy.
 */
export function ResolveModal({ target, onClose, onDone }: { target: ResolveTarget | null; onClose: () => void; onDone: () => void }) {
  const [form] = Form.useForm<ResolveValues>();
  const action = Form.useWatch('action', form) as ResolveAction | undefined;

  const submit = async (v: ResolveValues) => {
    const res = await scdApi.resolvePending(target!.sessionId, { action: v.action, teacherId: v.action === 'Substitute' ? v.teacherId : undefined });
    message.success(`Đã xử lý tiết, thông báo cho ${res.notifiedCount} người.`);
    onDone();
  };

  return (
    <FormModal
      open={target !== null} form={form} onClose={onClose} onSubmit={submit} width={560} okText="Xác nhận"
      title={target ? `Xử lý tiết ${target.title}` : ''}
      initialValues={{ action: 'Substitute' }}
    >
      {target && <p className="muted" style={{ marginTop: 0 }}>{target.summary}</p>}
      <Form.Item name="action" label="Cách xử lý" rules={[{ required: true }]}>
        <Radio.Group>
          <Radio value="Substitute" style={{ display: 'block', marginBottom: 6 }}>Chọn giáo viên dạy thay</Radio>
          <Radio value="Cancel" style={{ display: 'block' }}>Hủy tiết</Radio>
        </Radio.Group>
      </Form.Item>
      {action === 'Substitute' && target && (
        <Form.Item name="teacherId" label="Giáo viên dạy thay (gợi ý giáo viên rảnh, tối đa 10, sắp theo tên)" rules={[{ required: true, message: 'Chọn giáo viên' }]}>
          <TeacherSelect suggestForSessionId={target.sessionId} />
        </Form.Item>
      )}
      {action === 'Cancel' && (
        <InlineAlert type="info" title="Tiết chuyển sang Đã hủy: không dạy, không điểm danh, không tính chuyên cần. Giáo viên và học sinh của lớp được thông báo." />
      )}
    </FormModal>
  );
}
