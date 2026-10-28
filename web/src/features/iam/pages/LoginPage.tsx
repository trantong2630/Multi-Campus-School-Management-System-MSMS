import { Button, Form, Input, Skeleton } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { AuthLayout } from '@/app/AuthLayout';
import { useAuth } from '@/app/AuthProvider';

interface FormValues {
  username: string;
  password: string;
}

/** Lời chào theo buổi trong ngày, tính theo giờ máy người dùng. */
function greeting() {
  const hour = new Date().getHours();
  if (hour < 12) return 'Chào buổi sáng';
  if (hour < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

export function LoginPage() {
  const { user, loading, login } = useAuth();
  const navigate = useNavigate();
  const [form] = Form.useForm<FormValues>();
  const location = useLocation();
  const [submitting, setSubmitting] = useState(false);
  const submitPending = useRef(false);
  const [error, setError] = useState<string | null>(null);
  const [capsLock, setCapsLock] = useState(false);
  const [title] = useState(greeting);

  if (user) return <Navigate to="/" replace />;
  if (loading) {
    return (
      <AuthLayout title={title} subtitle="Đang kiểm tra phiên đăng nhập">
        <Skeleton active paragraph={{ rows: 4 }} />
      </AuthLayout>
    );
  }

  // `username` được trang quên mật khẩu gửi về để không phải gõ lại.
  const state = location.state as { from?: string; username?: string } | null;
  const from = state?.from ?? '/';

  const onFinish = async (values: FormValues) => {
    // Khóa ngay trước khi React render lại, kể cả khi Enter được nhấn liên tiếp.
    if (submitPending.current) return;
    submitPending.current = true;
    setSubmitting(true);
    setError(null);
    try {
      await login(values.username, values.password);
      navigate(from, { replace: true });
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      submitPending.current = false;
      setSubmitting(false);
    }
  };

  // Gõ sai mật khẩu vì quên tắt Caps Lock là lỗi đăng nhập hay gặp nhất, báo ngay khi đang gõ.
  const trackCapsLock = (e: KeyboardEvent<HTMLInputElement>) => setCapsLock(e.getModifierState('CapsLock'));

  return (
    <AuthLayout title={title} subtitle="Đăng nhập để tiếp tục">
      <Form<FormValues>
        form={form}
        layout="vertical"
        requiredMark={false}
        aria-busy={submitting}
        initialValues={{ username: state?.username }}
        onFinish={onFinish}
        onValuesChange={() => setError(null)}
      >
        <Form.Item
          label="Tên đăng nhập"
          name="username"
          rules={[{ required: true, whitespace: true, message: 'Nhập tên đăng nhập để tiếp tục.' }]}
        >
          <Input autoComplete="username" autoFocus readOnly={submitting} placeholder="vd. kientdhs186194" />
        </Form.Item>
        <Form.Item
          label="Mật khẩu"
          name="password"
          rules={[{ required: true, message: 'Nhập mật khẩu để tiếp tục.' }]}
          extra={
            <Button
              type="link"
              disabled={submitting}
              onClick={() => navigate('/forgot-password', { state: { username: form.getFieldValue('username') } })}
            >
              Quên mật khẩu?
            </Button>
          }
        >
          <Input.Password
            autoComplete="current-password"
            readOnly={submitting}
            placeholder="Nhập mật khẩu"
            suffix={capsLock ? <span className="auth-caps" role="status" title="Caps Lock đang bật">CAPS</span> : undefined}
            onKeyDown={trackCapsLock}
            onKeyUp={trackCapsLock}
            onBlur={() => setCapsLock(false)}
          />
        </Form.Item>

        {error && <InlineAlert type="error" title={error} />}

        <Button
          type="primary"
          htmlType="submit"
          block
          className={`auth-submit${submitting ? ' auth-submit--busy' : ''}`}
          aria-busy={submitting}
          aria-disabled={submitting}
        >
          Đăng nhập
        </Button>
      </Form>
    </AuthLayout>
  );
}
