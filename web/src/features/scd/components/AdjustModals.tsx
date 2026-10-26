import { useQuery } from '@tanstack/react-query';
import { DatePicker, Form, Select } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { schApi } from '@/features/sch/api';
import { FormModal } from '@/shared/components/FormModal';
import { message } from '@/shared/lib/antdStatic';
import { formatDate, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import { slotLabel } from '../labels';
import type { AdjustSession } from '../types';
import { TeacherSelect } from './TeacherSelect';
import './scd.css';

/** Tiết bù chỉ nhận ngày từ hôm nay trở đi và không phải Chủ nhật (BR-15); ngày nghỉ và học kỳ do Backend kiểm. */
const disabledMakeUpDate = (d: Dayjs) => d.isBefore(dayjs(), 'day') || d.day() === 0;

// ---------------------------------------------------------------- Đổi giáo viên
interface ChangeTeacherProps {
  session: AdjustSession | null;
  className: string;
  onClose: () => void;
  onDone: () => void;
}

/** Đổi giáo viên của tiết chưa bị khóa (BR-15). Danh sách là giáo viên toàn trường; ai đang bận hoặc vắng thì mờ đi (BR-13). */
export function ChangeTeacherModal({ session, className, onClose, onDone }: ChangeTeacherProps) {
  const [form] = Form.useForm<{ teacherId: number }>();

  const submit = async (v: { teacherId: number }) => {
    const res = await scdApi.changeSessionTeacher(session!.sessionId, v.teacherId);
    message.success(`Đã đổi giáo viên, thông báo cho ${res.notifiedCount} người.`);
    onDone();
  };

  return (
    <FormModal
      open={session !== null} form={form} onClose={onClose} onSubmit={submit} width={520}
      title={session ? `Đổi giáo viên — ${session.subjectName} ${className}, ${weekdayOfDate(session.date)} ${formatDate(session.date)}` : ''}
      okText="Đổi giáo viên"
    >
      <p className="muted" style={{ marginTop: 0 }}>
        Giáo viên hiện tại: <strong>{session?.teacherName ?? '—'}</strong>. Chỉ đổi lịch dạy của riêng tiết này, không đổi phụ trách chuyên môn.
      </p>
      <Form.Item name="teacherId" label="Giáo viên mới" rules={[{ required: true, message: 'Chọn giáo viên' }]}>
        {session && <TeacherSelect date={session.date} slotId={session.periodSlotId} excludeSessionId={session.sessionId} />}
      </Form.Item>
    </FormModal>
  );
}

// ---------------------------------------------------------------- Thêm tiết bù
export interface MakeUpTarget {
  classId: number;
  className: string;
  campusId: number;
  /** Ô đã chọn trên lưới. */
  date: string;
  slotId: number;
}

interface MakeUpValues {
  date: Dayjs;
  periodSlotId: number;
  subjectId: number;
  teacherId: number;
}

/** Tiết bù là một tiết thêm mới ở (ngày, khung giờ) trống của lớp. */
export function MakeUpModal({ target, onClose, onDone }: { target: MakeUpTarget | null; onClose: () => void; onDone: () => void }) {
  const [form] = Form.useForm<MakeUpValues>();
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });
  const options = useQuery({
    queryKey: ['scd', 'draftOptions', target?.campusId],
    queryFn: () => scdApi.draftOptions(target!.campusId),
    enabled: target !== null,
  });
  const date = Form.useWatch('date', form);
  const slotId = Form.useWatch('periodSlotId', form);

  const submit = async (v: MakeUpValues) => {
    const res = await scdApi.addMakeUp({
      classId: target!.classId, date: toDateString(v.date), periodSlotId: v.periodSlotId, subjectId: v.subjectId, teacherId: v.teacherId,
    });
    message.success(`Đã thêm tiết bù, thông báo cho ${res.notifiedCount} người.`);
    onDone();
  };

  return (
    <FormModal
      open={target !== null} form={form} onClose={onClose} onSubmit={submit} width={520}
      title={target ? `Thêm tiết bù — lớp ${target.className}` : ''} okText="Thêm tiết bù"
      initialValues={target ? { date: dayjs(target.date), periodSlotId: target.slotId } : undefined}
    >
      <Form.Item name="date" label="Ngày" rules={[{ required: true, message: 'Chọn ngày' }]}>
        <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={disabledMakeUpDate} />
      </Form.Item>
      <Form.Item name="periodSlotId" label="Khung giờ" rules={[{ required: true, message: 'Chọn khung giờ' }]}>
        <Select
          placeholder="Chọn tiết" loading={slots.isLoading}
          options={slots.data?.filter((p) => p.isActive)
            .sort((a, b) => (a.sessionOfDay === b.sessionOfDay ? a.periodNo - b.periodNo : a.sessionOfDay === 'Morning' ? -1 : 1))
            .map((p) => ({ value: p.periodSlotId, label: slotLabel(p) }))}
        />
      </Form.Item>
      <Form.Item name="subjectId" label="Môn" rules={[{ required: true, message: 'Chọn môn' }]}>
        <Select
          showSearch optionFilterProp="label" placeholder="Chọn môn" loading={options.isLoading}
          options={options.data?.subjects.map((s) => ({ value: s.subjectId, label: `${s.name} (${s.abbr})` }))}
        />
      </Form.Item>
      <Form.Item name="teacherId" label="Giáo viên" rules={[{ required: true, message: 'Chọn giáo viên' }]}>
        <TeacherSelect date={date ? toDateString(date) : undefined} slotId={slotId} />
      </Form.Item>
    </FormModal>
  );
}
