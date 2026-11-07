import { useQueryClient } from '@tanstack/react-query';
import { Form, Input, Modal, Segmented, Select } from 'antd';
import { useEffect, useState } from 'react';
import { message } from '@/shared/lib/antdStatic';
import { showError } from '@/shared/lib/feedback';
import { pplApi } from '../api';
import type { Student } from '../types';

interface Props {
  student: Student | null;
  onClose: () => void;
}

interface FormValues {
  existingParentId?: number;
  fullName?: string;
  phoneNumber?: string;
  email?: string;
  relationship?: string;
}

/**
 * 4.3.26: đổi phụ huynh đại diện của một học sinh sang phụ huynh có sẵn hoặc khai mới.
 * Mỗi học sinh chỉ có một phụ huynh đại diện; hộp thư của phụ huynh cũ chuyển sang chỉ đọc.
 */
export function ChangeGuardianModal({ student, onClose }: Props) {
  const qc = useQueryClient();
  const [form] = Form.useForm<FormValues>();
  const [mode, setMode] = useState<'existing' | 'new'>('existing');
  const [options, setOptions] = useState<{ value: number; label: string }[]>([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (student) { form.resetFields(); setMode('existing'); setOptions([]); }
  }, [student, form]);

  const searchGuardians = async (term: string) => {
    if (term.trim().length < 2) { setOptions([]); return; }
    const res = await pplApi.guardians.list({ search: term, pageSize: 20 });
    setOptions(res.items.map((g) => ({ value: g.parentId, label: `${g.fullName} · ${g.phoneNumber ?? 'chưa có số'} (${g.personCode})` })));
  };

  const submit = async () => {
    if (!student) return;
    const v = await form.validateFields();
    setSaving(true);
    try {
      await pplApi.guardians.changeRepresentative(student.studentId, mode === 'existing'
        ? { existingParentId: v.existingParentId, relationship: v.relationship }
        : { newGuardian: { fullName: v.fullName!, phoneNumber: v.phoneNumber!, email: v.email }, relationship: v.relationship });
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['ppl', 'students'] }),
        qc.invalidateQueries({ queryKey: ['ppl', 'guardians'] }),
      ]);
      message.success('Đã đổi phụ huynh đại diện. Hộp thư của phụ huynh cũ chuyển sang chỉ đọc.');
      onClose();
    } catch (e) {
      showError(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={student !== null} title={student ? `Đổi phụ huynh đại diện — ${student.fullName}` : ''} okText="Đổi phụ huynh" cancelText="Hủy"
      confirmLoading={saving} onOk={submit} onCancel={onClose} mask={{ closable: false }} destroyOnHidden
    >
      <p>
        Phụ huynh đại diện hiện tại: <strong>{student?.guardianName ?? 'chưa có'}</strong>. Mỗi học sinh chỉ có một phụ huynh đại diện;
        hội thoại của phụ huynh cũ chuyển sang chỉ đọc.
      </p>
      <Form form={form} layout="vertical" requiredMark={false}>
        <Segmented<'existing' | 'new'> block value={mode} onChange={setMode} style={{ marginBottom: 16 }}
          options={[{ value: 'existing', label: 'Phụ huynh có sẵn' }, { value: 'new', label: 'Khai phụ huynh mới' }]} />
        {mode === 'existing' ? (
          <Form.Item label="Phụ huynh" name="existingParentId" rules={[{ required: true, message: 'Chọn một phụ huynh.' }]}>
            <Select showSearch filterOption={false} onSearch={searchGuardians} options={options} placeholder="Gõ tên, mã hoặc số điện thoại (từ 2 ký tự)" />
          </Form.Item>
        ) : (
          <>
            <Form.Item label="Họ và tên" name="fullName" rules={[{ required: true, whitespace: true, message: 'Nhập họ và tên.' }]}><Input maxLength={150} /></Form.Item>
            <Form.Item label="Số điện thoại" name="phoneNumber" extra="Đúng 10 chữ số, bắt đầu bằng 0." rules={[{ required: true, message: 'Nhập số điện thoại.' }, { pattern: /^0\d{9}$/, message: 'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng số 0.' }]}><Input maxLength={10} inputMode="tel" /></Form.Item>
            <Form.Item label="Email" name="email" rules={[{ type: 'email', message: 'Email chưa đúng định dạng.' }]}><Input maxLength={150} /></Form.Item>
          </>
        )}
        <Form.Item label="Quan hệ với học sinh" name="relationship"><Input maxLength={50} placeholder="Ví dụ: Bố, Mẹ, Ông, Bà" /></Form.Item>
      </Form>
    </Modal>
  );
}