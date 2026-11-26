import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Checkbox, DatePicker, Form, Input, Modal, Progress, Radio, Select, Tag } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { schApi } from '@/features/sch/api';
import { FormModal } from '@/shared/components/FormModal';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { getErrorMessage } from '@/app/apiClient';
import { message } from '@/shared/lib/antdStatic';
import { formatDate, formatTime, sessionOfDayLabel, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import { slotLabel } from '../labels';
import type { PendingSession, ResolveAction } from '../types';
import { TeacherSelect } from './TeacherSelect';
import './scd.css';

/** Hai thẻ chọn cách xử lý tiết Chờ xử lý; dùng được trực tiếp trong Form.Item. */
function ResolveChoice({ value, onChange }: { value?: ResolveAction; onChange?: (v: ResolveAction) => void }) {
  const opts: { value: ResolveAction; title: string; hint: string; danger?: boolean }[] = [
    { value: 'Substitute', title: 'Chọn giáo viên dạy thay', hint: 'Tiết vẫn diễn ra với giáo viên khác' },
    { value: 'Cancel', title: 'Hủy tiết', hint: 'Lớp nghỉ tiết này, không điểm danh', danger: true },
  ];
  return (
    <div className="rs-choice" role="radiogroup">
      {opts.map((o) => (
        <button
          key={o.value} type="button" role="radio" aria-checked={value === o.value}
          className={`rs-choice__opt${o.danger ? ' rs-choice__opt--danger' : ''}${value === o.value ? ' rs-choice__opt--on' : ''}`}
          onClick={() => onChange?.(o.value)}
        >
          <span className="rs-choice__title">{o.title}</span>
          <span className="rs-choice__hint">{o.hint}</span>
        </button>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------- Khai báo giáo viên vắng
type Scope = 'days' | 'slots';

/**
 * Khai báo giáo viên vắng (BR-16): theo ngày (khoảng ngày, cả ngày) hoặc theo tiết (một ngày, tick các tiết vắng).
 * Khai báo không tự đổi lịch — các tiết bị ảnh hưởng ở phân hiệu của bạn chuyển sang Chờ xử lý và để trống giáo viên.
 * Đây không phải đơn xin nghỉ của cán bộ.
 */
export function DeclareAbsenceModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const qc = useQueryClient();
  const [teacherId, setTeacherId] = useState<number | undefined>();
  const [scope, setScope] = useState<Scope>('days');
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [day, setDay] = useState<Dayjs | null>(null);
  const [slotIds, setSlotIds] = useState<number[]>([]);
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const teachers = useQuery({ queryKey: ['scd', 'departmentTeachers'], queryFn: scdApi.departmentTeachers, enabled: open });
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
      const body = {
        teacherId, fromDate: toDateString(from), toDate: toDateString(to),
        slotIds: scope === 'slots' ? slotIds : undefined, reason: reason.trim() || undefined,
      };
      const res = await scdApi.declareDepartmentAbsence(body);
      message.success(`Đã khai báo: ${res.pendingCount} tiết chuyển sang Chờ xử lý. Quyết định từng tiết (người dạy thay hoặc hủy) ở tab Tiết cần dạy thay; hiệu phó sẽ duyệt khi bạn quyết định xong.`);
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
        Chỉ ghi nhận hệ quả lên lịch dạy, không phải đơn xin nghỉ của cán bộ. Khai cho giáo viên của tổ mình, ảnh hưởng tiết ở mọi phân hiệu giáo viên đó dạy.
        Sau khi khai, bạn quyết định từng tiết (người dạy thay hoặc hủy); hiệu phó phân hiệu có tiết chỉ duyệt.
      </p>
      <Form layout="vertical" requiredMark={false} onValuesChange={() => setError(null)}>
        <Form.Item label="Giáo viên" required>
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn giáo viên" loading={teachers.isLoading} value={teacherId} onChange={setTeacherId}
            notFoundContent="Tổ chưa có giáo viên nào"
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
  /** Quyết định của tổ trưởng cho tiết (ví dụ "Tổ trưởng đề xuất Toán B dạy thay."): hiện cho hiệu phó tham khảo, hiệu phó vẫn được chọn cách khác. */
  proposal?: string | null;
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
      {target?.proposal && <InlineAlert type="info" title={`${target.proposal} Bạn có thể làm theo hoặc chọn cách khác.`} />}
      <Form.Item name="action" label="Cách xử lý" rules={[{ required: true }]}>
        <ResolveChoice />
      </Form.Item>
      {action === 'Substitute' && target && (
        <Form.Item name="teacherId" label="Giáo viên dạy thay (gợi ý giáo viên rảnh trong tổ, tối đa 10, sắp theo tên)" rules={[{ required: true, message: 'Chọn giáo viên' }]}>
          <TeacherSelect suggestForSessionId={target.sessionId} />
        </Form.Item>
      )}
      {action === 'Cancel' && (
        <InlineAlert type="info" title="Tiết chuyển sang Đã hủy: không dạy, không điểm danh, không tính chuyên cần. Giáo viên và học sinh của lớp được thông báo." />
      )}
    </FormModal>
  );
}

// ---------------------------------------------------------------- Tổ trưởng quyết định từng tiết
interface DecideValues {
  action: 'Substitute' | 'Cancel';
  teacherId?: number;
}

/**
 * Tổ trưởng quyết định một tiết Chờ xử lý của môn thuộc tổ mình: giáo viên dạy thay (gợi ý giáo viên rảnh cùng tổ) hoặc hủy tiết.
 * Quyết định chưa đổi lịch: quyết định hết các tiết của tổ rồi gửi duyệt ở danh sách khai báo, hiệu phó của phân hiệu mới duyệt (BR-16).
 * Mở từ bên phải như các form nhiều thông tin khác; hiện bối cảnh của tiết (giờ học, lý do vắng) và tiến độ cả danh sách.
 */
export function ProposeModal({ target, decided, total, onClose, onDone }: {
  target: PendingSession | null;
  /** Số tiết đã có quyết định trên tổng số tiết đang chờ của tổ. */
  decided: number;
  total: number;
  onClose: () => void;
  onDone: () => void;
}) {
  const [form] = Form.useForm<DecideValues>();
  const action = Form.useWatch('action', form) as DecideValues['action'] | undefined;

  const submit = async (v: DecideValues) => {
    await scdApi.proposeSession(target!.sessionId, { action: v.action, teacherId: v.action === 'Substitute' ? v.teacherId : null });
    message.success('Đã lưu quyết định cho tiết này.');
    onDone();
  };

  const slotText = target ? `${sessionOfDayLabel(target.sessionOfDay)} · Tiết ${target.periodNo} · ${formatTime(target.startTime)}–${formatTime(target.endTime)}` : '';
  const absenceRange = target?.absenceFrom && target.absenceTo
    ? (target.absenceFrom === target.absenceTo ? formatDate(target.absenceFrom) : `${formatDate(target.absenceFrom)} – ${formatDate(target.absenceTo)}`)
    : null;

  return (
    <FormModal
      open={target !== null} form={form} onClose={onClose} onSubmit={submit} variant="drawer" width={560} okText="Lưu quyết định"
      title="Quyết định tiết cần dạy thay"
      initialValues={{ action: target?.proposedAction ?? 'Substitute', teacherId: target?.proposedTeacherId }}
    >
      {target && (
        <>
          <div className="dc-progress">
            <span className="tabular">Đã quyết định {decided}/{total} tiết của tổ</span>
            <Progress percent={total > 0 ? Math.round((decided / total) * 100) : 0} size="small" showInfo={false} />
          </div>
          <div className="dc-card">
            <div className="dc-card__title">
              <span>{target.subjectName} — lớp {target.className}</span>
              <small>{target.campusName}</small>
              {target.proposedAction === 'Cancel' && <Tag color="red">Đang đề xuất hủy</Tag>}
              {target.proposedAction === 'Substitute' && <Tag color="blue">Đang đề xuất {target.proposedTeacherName}</Tag>}
            </div>
            <dl className="dc-card__grid">
              <div><dt>Ngày</dt><dd className="tabular">{weekdayOfDate(target.date)}, {formatDate(target.date)}</dd></div>
              <div><dt>Tiết</dt><dd className="tabular">{slotText}</dd></div>
              <div><dt>Giáo viên vắng</dt><dd>{target.absentTeacherName ?? '—'}</dd></div>
              <div><dt>Khoảng vắng</dt><dd className="tabular">{absenceRange ?? '—'}</dd></div>
              <div className="dc-card__wide"><dt>Lý do vắng</dt><dd>{target.absenceReason ?? <span className="muted">Không ghi lý do</span>}</dd></div>
            </dl>
          </div>
        </>
      )}
      <Form.Item name="action" label="Quyết định" rules={[{ required: true }]}>
        <ResolveChoice />
      </Form.Item>
      {action === 'Substitute' && target && (
        <Form.Item name="teacherId" label="Giáo viên dạy thay (giáo viên rảnh trong tổ, tối đa 10, sắp theo tên)" rules={[{ required: true, message: 'Chọn giáo viên' }]}>
          <TeacherSelect suggestForSessionId={target.sessionId} asDepartmentHead />
        </Form.Item>
      )}
      <InlineAlert type="info" title="Quyết định chưa đổi lịch: tiết vẫn Chờ xử lý cho tới khi hiệu phó duyệt. Quyết định hết các tiết của tổ rồi bấm Gửi duyệt ở danh sách khai báo; từ lúc gửi, bạn không sửa được cho tới khi hiệu phó duyệt hoặc trả lại." />
    </FormModal>
  );
}

// ---------------------------------------------------------------- Hiệu phó trả khai báo về
/**
 * Hiệu phó trả cả khai báo về cho tổ trưởng kèm lý do (BR-16): đề xuất mở khóa để tổ trưởng sửa rồi gửi lại. Không đổi lịch.
 */
export function ReturnAbsenceModal({ target, onClose, onDone }: {
  target: { absenceId: number; title: string } | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const [form] = Form.useForm<{ reason: string }>();

  const submit = async (v: { reason: string }) => {
    await scdApi.returnAbsence(target!.absenceId, v.reason.trim());
    message.success('Đã trả khai báo về cho tổ trưởng.');
    onDone();
  };

  return (
    <FormModal
      open={target !== null} form={form} onClose={onClose} onSubmit={submit} width={520} okText="Trả lại"
      title={target ? `Trả lại khai báo vắng của ${target.title}` : ''}
    >
      <InlineAlert type="info" title="Tổ trưởng được báo, sửa đề xuất rồi gửi lại. Lịch dạy chưa đổi." />
      <Form.Item name="reason" label="Lý do trả lại" rules={[{ required: true, whitespace: true, message: 'Nhập lý do để tổ trưởng biết cần sửa gì' }, { max: 300 }]}>
        <Input.TextArea rows={4} maxLength={300} showCount />
      </Form.Item>
    </FormModal>
  );
}
