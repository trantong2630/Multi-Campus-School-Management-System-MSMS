import { Button, Form, Input } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { useAuth } from '@/app/AuthProvider';
import { iamApi } from '../api';

interface FormValues {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

const MIN_LENGTH = 8;

export function ChangePasswordForm({ onDone }: { onDone?: () => void }) {
  const { refresh } = useAuth();
  const [form] = Form.useForm<FormValues>();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onFinish = async (values: FormValues) => {
    setSubmitting(true);
    setError(null);
    try {
      await iamApi.changePassword(values.currentPassword, values.newPassword);
      message.success('Đã đổi mật khẩu.');
      form.resetFields();
      await refresh();   // trạng thái chuyển Chờ đổi mật khẩu → Đang hoạt động
      onDone?.();
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Form<FormValues>
      form={form}
      layout="vertical"
      requiredMark={false}
      onFinish={onFinish}
      onValuesChange={() => setError(null)}
    >
      <Form.Item
        label="Mật khẩu hiện tại"
        name="currentPassword"
        rules={[{ required: true, message: 'Nhập mật khẩu đang dùng. Với tài khoản mới, đó là mật khẩu tạm được bàn giao.' }]}
      >
        <Input.Password autoComplete="current-password" />
      </Form.Item>
      <Form.Item
        label="Mật khẩu mới"
        name="newPassword"
        rules={[
          { required: true, message: 'Nhập mật khẩu mới.' },
          { min: MIN_LENGTH, message: `Mật khẩu mới cần ít nhất ${MIN_LENGTH} ký tự.` },
        ]}
      >
        <Input.Password autoComplete="new-password" />
      </Form.Item>
      <Form.Item
        label="Nhập lại mật khẩu mới"
        name="confirmPassword"
        dependencies={['newPassword']}
        rules={[
          { required: true, message: 'Nhập lại mật khẩu mới để xác nhận.' },
          ({ getFieldValue }) => ({
            validator: (_, value) =>
              !value || getFieldValue('newPassword') === value
                ? Promise.resolve()
                : Promise.reject(new Error('Hai mật khẩu chưa giống nhau. Nhập lại cho khớp.')),
          }),
        ]}
      >
        <Input.Password autoComplete="new-password" />
      </Form.Item>

      {error && <InlineAlert type="error" title={error} />}

      <Button type="primary" htmlType="submit" loading={submitting}>
        Lưu
      </Button>
    </Form>
  );
}
