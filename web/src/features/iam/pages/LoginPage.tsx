import { Button, Form, Input } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { LockKeyhole, UserRound } from 'lucide-react';
import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { AuthLayout } from '@/app/AuthLayout';
import { useAuth } from '@/app/AuthProvider';

interface FormValues {
  username: string;
  password: string;
}

export function LoginPage() {
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (user) return <Navigate to="/" replace />;

  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const onFinish = async (values: FormValues) => {
    setSubmitting(true);
    setError(null);
    try {
      await login(values.username, values.password);
      navigate(from, { replace: true });
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout title="Chào mừng" description="Đăng nhập vào tài khoản của bạn">
      <Form<FormValues> layout="vertical" requiredMark={false} onFinish={onFinish} onValuesChange={() => setError(null)}>
        <Form.Item
          label="Tên đăng nhập"
          name="username"
          rules={[{ required: true, whitespace: true, message: 'Nhập tên đăng nhập để tiếp tục.' }]}
        >
          <Input prefix={<UserRound size={16} />} autoComplete="username" autoFocus placeholder="Tên đăng nhập" />
        </Form.Item>
        <Form.Item
          label="Mật khẩu"
          name="password"
          rules={[{ required: true, message: 'Nhập mật khẩu để tiếp tục.' }]}
        >
          <Input.Password prefix={<LockKeyhole size={16} />} autoComplete="current-password" placeholder="Mật khẩu" />
        </Form.Item>

        {error && <InlineAlert type="error" title={error} />}

        <Button type="primary" htmlType="submit" block loading={submitting}>
          Đăng nhập
        </Button>
      </Form>
    </AuthLayout>
  );
}
