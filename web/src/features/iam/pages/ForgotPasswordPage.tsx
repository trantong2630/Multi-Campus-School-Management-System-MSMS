import { Button, Form, Input } from 'antd';
import { useEffect, useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { AuthLayout } from '@/app/AuthLayout';
import { useAuth } from '@/app/AuthProvider';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { iamApi } from '../api';
import { MIN_PASSWORD_LENGTH } from '../components/ChangePasswordForm';

/** Gửi lại mã sớm nhất sau 60 giây (BR-04). */
const RESEND_SECONDS = 60;

/**
 * Quên mật khẩu (BR-04): bước 1 nhập tên đăng nhập để nhận OTP qua email trong hồ sơ,
 * bước 2 nhập mã và mật khẩu mới. Hồ sơ không có email thì nhờ School Admin cấp lại mật khẩu tạm.
 */
export function ForgotPasswordPage() {
  const { user } = useAuth();
  const location = useLocation();
  // null = đang ở bước 1; có giá trị = đã gửi yêu cầu cho tên đăng nhập này.
  const [username, setUsername] = useState<string | null>(null);
  const [typed, setTyped] = useState(() => (location.state as { username?: string } | null)?.username ?? '');

  if (user) return <Navigate to="/" replace />;

  return username === null ? (
    <RequestStep initialUsername={typed} onSent={setUsername} />
  ) : (
    <ResetStep
      username={username}
      onChangeUsername={() => {
        setTyped(username);
        setUsername(null);
      }}
    />
  );
}

function RequestStep({ initialUsername, onSent }: { initialUsername: string; onSent: (username: string) => void }) {
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onFinish = async ({ username }: { username: string }) => {
    if (submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await iamApi.requestPasswordReset(username.trim());
      onSent(username.trim());
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout title="Quên mật khẩu" subtitle="Nhập tên đăng nhập để nhận mã xác nhận qua email trong hồ sơ">
      <Form<{ username: string }>
        layout="vertical"
        requiredMark={false}
        initialValues={{ username: initialUsername }}
        onFinish={onFinish}
        onValuesChange={() => setError(null)}
      >
        <Form.Item
          label="Tên đăng nhập"
          name="username"
          rules={[{ required: true, whitespace: true, message: 'Nhập tên đăng nhập để nhận mã.' }]}
        >
          <Input autoComplete="username" autoFocus placeholder="vd. kientdhs186194" />
        </Form.Item>

        {error && <InlineAlert type="error" title={error} />}

        <SubmitButton busy={submitting}>Gửi mã</SubmitButton>
      </Form>
      <div className="auth-layout__footer">
        <Button type="link" onClick={() => navigate('/login', { state: { username: initialUsername } })}>
          Quay lại đăng nhập
        </Button>
      </div>
    </AuthLayout>
  );
}

interface ResetValues {
  code: string;
  newPassword: string;
  confirmPassword: string;
}

function ResetStep({ username, onChangeUsername }: { username: string; onChangeUsername: () => void }) {
  const navigate = useNavigate();
  const [form] = Form.useForm<ResetValues>();
  const [submitting, setSubmitting] = useState(false);
  const [resending, setResending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [wait, restartWait] = useCountdown(RESEND_SECONDS);

  const onFinish = async (values: ResetValues) => {
    if (submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await iamApi.resetPassword(username, values.code, values.newPassword);
      message.success('Đã đặt lại mật khẩu. Đăng nhập bằng mật khẩu mới.');
      navigate('/login', { replace: true, state: { username } });
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  const resend = async () => {
    setResending(true);
    setError(null);
    try {
      await iamApi.requestPasswordReset(username);
      restartWait();
      form.setFieldValue('code', '');   // mã mới thay mã cũ
      message.info('Nếu tài khoản hợp lệ, mã mới đã được gửi. Mã cũ không còn dùng được.');
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setResending(false);
    }
  };

  return (
    <AuthLayout
      title="Đặt lại mật khẩu"
      subtitle={`Nếu tài khoản ${username} hợp lệ và hồ sơ có email, mã 6 chữ số đã được gửi tới email đó.`}
    >
      <Form<ResetValues>
        form={form}
        layout="vertical"
        requiredMark={false}
        onFinish={onFinish}
        onValuesChange={() => setError(null)}
      >
        <Form.Item
          label="Mã xác nhận"
          name="code"
          rules={[
            { required: true, message: 'Nhập mã 6 chữ số trong email.' },
            { pattern: /^\d{6}$/, message: 'Mã gồm đủ 6 chữ số.' },
          ]}
          extra={
            <>
              <span>Mã có hiệu lực 10 phút.</span>
              <Button type="link" disabled={wait > 0} loading={resending} onClick={() => void resend()}>
                {wait > 0 ? `Gửi lại mã sau ${wait} giây` : 'Gửi lại mã'}
              </Button>
            </>
          }
        >
          <Input.OTP length={6} autoFocus inputMode="numeric" formatter={(s) => s.replace(/\D/g, '')} />
        </Form.Item>
        <Form.Item
          label="Mật khẩu mới"
          name="newPassword"
          rules={[
            { required: true, message: 'Nhập mật khẩu mới.' },
            { min: MIN_PASSWORD_LENGTH, message: `Mật khẩu mới cần ít nhất ${MIN_PASSWORD_LENGTH} ký tự.` },
          ]}
        >
          <Input.Password autoComplete="new-password" placeholder={`Ít nhất ${MIN_PASSWORD_LENGTH} ký tự`} />
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

        <SubmitButton busy={submitting}>Đặt lại mật khẩu</SubmitButton>
      </Form>
      <div className="auth-layout__footer">
        <Button type="link" onClick={onChangeUsername}>
          Đổi tên đăng nhập
        </Button>
        <Button type="link" onClick={() => navigate('/login', { state: { username } })}>
          Quay lại đăng nhập
        </Button>
      </div>
      <p className="auth-help">
        Không nhận được mã? Kiểm tra thư rác. Hồ sơ chưa có email thì liên hệ quản trị trường để được cấp lại mật khẩu tạm.
      </p>
    </AuthLayout>
  );
}

/** Nút gửi của màn chưa đăng nhập: đang gửi thì chạy vạch tiến trình thay vì icon xoay (Design_Web §6.0). */
function SubmitButton({ busy, children }: { busy: boolean; children: string }) {
  return (
    <Button
      type="primary"
      htmlType="submit"
      block
      className={`auth-submit${busy ? ' auth-submit--busy' : ''}`}
      aria-busy={busy}
    >
      {children}
    </Button>
  );
}

/** Đếm lùi `seconds` giây ngay khi mở, dừng ở 0; `restart` đếm lại từ đầu. Tính theo mốc giờ nên tab ngủ cũng không lệch. */
function useCountdown(seconds: number) {
  const [until, setUntil] = useState(() => Date.now() + seconds * 1000);
  const [left, setLeft] = useState(seconds);
  useEffect(() => {
    if (left <= 0) return;
    const id = window.setTimeout(() => setLeft(Math.max(0, Math.ceil((until - Date.now()) / 1000))), 1000);
    return () => window.clearTimeout(id);
  }, [left, until]);
  const restart = () => {
    setUntil(Date.now() + seconds * 1000);
    setLeft(seconds);
  };
  return [left, restart] as const;
}
