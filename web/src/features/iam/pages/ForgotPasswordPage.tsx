import { Button, Form, Input } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { AuthLayout } from '@/app/AuthLayout';
import { useAuth } from '@/app/AuthProvider';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { iamApi } from '../api';
import type { PasswordResetVerification } from '../api';
import { MIN_PASSWORD_LENGTH } from '../components/ChangePasswordForm';

/** Gửi lại mã sớm nhất sau 60 giây (BR-04). */
const RESEND_SECONDS = 60;

/**
 * IAM-03: yêu cầu mã → xác minh OTP → đặt mật khẩu mới.
 * Bằng chứng xác minh chỉ giữ trong bộ nhớ trang, không đưa vào URL hoặc lưu trình duyệt.
 */
export function ForgotPasswordPage() {
  const { user } = useAuth();
  const location = useLocation();
  // null = đang ở bước 1; có giá trị = đã gửi yêu cầu cho tên đăng nhập này.
  const [username, setUsername] = useState<string | null>(null);
  const [typed, setTyped] = useState(() => (location.state as { username?: string } | null)?.username ?? '');
  const [verification, setVerification] = useState<PasswordResetVerification | null>(null);
  const [resendAt, setResendAt] = useState(0);

  if (user) return <Navigate to="/" replace />;

  return username === null ? (
    <RequestStep initialUsername={typed} onSent={(name, deadline) => {
      setUsername(name);
      setResendAt(deadline);
      setVerification(null);
    }} />
  ) : verification === null ? (
    <VerificationStep
      username={username}
      resendAt={resendAt}
      onResent={setResendAt}
      onVerified={setVerification}
      onChangeUsername={() => {
        setTyped(username);
        setUsername(null);
      }}
    />
  ) : (
    <NewPasswordStep username={username} verification={verification} onRequestCode={() => {
      setTyped(username);
      setUsername(null);
      setVerification(null);
    }} />
  );
}

function RequestStep({ initialUsername, onSent }: { initialUsername: string; onSent: (username: string, deadline: number) => void }) {
  const navigate = useNavigate();
  const [form] = Form.useForm<{ username: string }>();
  const typedUsername = Form.useWatch('username', form) ?? initialUsername;
  const wait = useCountdown(readResendAt(typedUsername));
  const pending = useRef(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onFinish = async ({ username }: { username: string }) => {
    if (pending.current || wait > 0) return;
    pending.current = true;
    setSubmitting(true);
    setError(null);
    try {
      const seconds = await iamApi.requestPasswordReset(username.trim());
      const deadline = rememberResendAt(username, seconds);
      onSent(username.trim(), deadline);
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      pending.current = false;
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout title="Quên mật khẩu" subtitle="Nhập tên đăng nhập để nhận mã xác nhận qua email trong hồ sơ">
      <Form<{ username: string }>
        form={form}
        layout="vertical"
        requiredMark={false}
        aria-busy={submitting}
        initialValues={{ username: initialUsername }}
        onFinish={onFinish}
        onValuesChange={() => setError(null)}
      >
        <Form.Item
          label="Tên đăng nhập"
          name="username"
          rules={[{ required: true, whitespace: true, message: 'Nhập tên đăng nhập để nhận mã.' }]}
        >
          <Input autoComplete="username" autoFocus readOnly={submitting} maxLength={100} placeholder="vd. kientdhs186194" />
        </Form.Item>

        {error && <InlineAlert type="error" title={error} />}

        <SubmitButton busy={submitting} disabled={wait > 0}>
          {wait > 0 ? `Gửi mã sau ${wait} giây` : 'Gửi mã'}
        </SubmitButton>
      </Form>
      <div className="auth-layout__footer">
        <Button type="link" onClick={() => navigate('/login', { state: { username: form.getFieldValue('username')?.trim() ?? '' } })}>
          Quay lại đăng nhập
        </Button>
      </div>
    </AuthLayout>
  );
}

interface VerificationValues {
  code: string;
}

interface PasswordValues {
  newPassword: string;
  confirmPassword: string;
}

function VerificationStep({ username, resendAt, onResent, onVerified, onChangeUsername }: {
  username: string;
  resendAt: number;
  onResent: (deadline: number) => void;
  onVerified: (verification: PasswordResetVerification) => void;
  onChangeUsername: () => void;
}) {
  const navigate = useNavigate();
  const [form] = Form.useForm<VerificationValues>();
  const pending = useRef(false);
  const [submitting, setSubmitting] = useState(false);
  const [resending, setResending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const wait = useCountdown(resendAt);

  const onFinish = async (values: VerificationValues) => {
    if (pending.current) return;
    pending.current = true;
    setSubmitting(true);
    setError(null);
    try {
      const verified = await iamApi.verifyPasswordReset(username, values.code);
      onVerified(verified);
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      pending.current = false;
      setSubmitting(false);
    }
  };

  const resend = async () => {
    if (pending.current || wait > 0) return;
    pending.current = true;
    setResending(true);
    setError(null);
    try {
      const seconds = await iamApi.requestPasswordReset(username);
      onResent(rememberResendAt(username, seconds));
      form.setFieldValue('code', '');   // mã mới thay mã cũ
      message.info('Yêu cầu gửi lại mã đã được tiếp nhận. Hãy kiểm tra email mới nhất.');
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      pending.current = false;
      setResending(false);
    }
  };

  return (
    <AuthLayout
      title="Xác minh mã"
      subtitle={`Nếu tài khoản ${username} hợp lệ và hồ sơ có email, mã xác nhận sẽ được gửi tới email đó. Hãy mở email mới nhất.`}
    >
      <Form<VerificationValues>
        form={form}
        layout="vertical"
        requiredMark={false}
        aria-busy={submitting || resending}
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
              <Button type="link" disabled={wait > 0 || submitting} loading={resending} onClick={() => void resend()}>
                {wait > 0 ? `Gửi lại mã sau ${wait} giây` : 'Gửi lại mã'}
              </Button>
            </>
          }
        >
          <Input.OTP length={6} autoFocus inputMode="numeric" autoComplete="one-time-code"
            disabled={submitting || resending} formatter={(s) => s.replace(/\D/g, '')} />
        </Form.Item>

        {error && <InlineAlert type="error" title={error} />}

        <SubmitButton busy={submitting} disabled={resending}>Xác minh mã</SubmitButton>
      </Form>
      <div className="auth-layout__footer">
        <Button type="link" onClick={onChangeUsername}>Đổi tên đăng nhập</Button>
        <Button type="link" onClick={() => navigate('/login', { state: { username } })}>Quay lại đăng nhập</Button>
      </div>
      <p className="auth-help">
        Không nhận được mã? Kiểm tra thư rác và mở email mới nhất. Hồ sơ chưa có email thì liên hệ quản trị trường để được cấp lại mật khẩu tạm.
      </p>
    </AuthLayout>
  );
}

function NewPasswordStep({ username, verification, onRequestCode }: {
  username: string;
  verification: PasswordResetVerification;
  onRequestCode: () => void;
}) {
  const navigate = useNavigate();
  const pending = useRef(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onFinish = async (values: PasswordValues) => {
    if (pending.current) return;
    pending.current = true;
    setSubmitting(true);
    setError(null);
    try {
      await iamApi.resetPassword(username, verification.resetToken, values.newPassword);
      message.success('Đã đặt lại mật khẩu. Đăng nhập bằng mật khẩu mới.');
      navigate('/login', { replace: true, state: { username } });
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      pending.current = false;
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout title="Đặt lại mật khẩu" subtitle="Mã đã được xác minh. Đặt mật khẩu mới khác mật khẩu hiện tại.">
      <Form<PasswordValues> layout="vertical" requiredMark={false} aria-busy={submitting}
        onFinish={onFinish} onValuesChange={() => setError(null)}>
        <Form.Item
          label="Mật khẩu mới"
          name="newPassword"
          rules={[
            { required: true, message: 'Nhập mật khẩu mới.' },
            { min: MIN_PASSWORD_LENGTH, message: `Mật khẩu mới cần ít nhất ${MIN_PASSWORD_LENGTH} ký tự.` },
          ]}
        >
          <Input.Password autoComplete="new-password" autoFocus readOnly={submitting} placeholder={`Ít nhất ${MIN_PASSWORD_LENGTH} ký tự`} />
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
          <Input.Password autoComplete="new-password" readOnly={submitting} />
        </Form.Item>

        {error && <InlineAlert type="error" title={error} />}

        <SubmitButton busy={submitting}>Đặt lại mật khẩu</SubmitButton>
      </Form>
      <div className="auth-layout__footer">
        <Button type="link" onClick={onRequestCode}>Yêu cầu mã mới</Button>
        <Button type="link" onClick={() => navigate('/login', { state: { username } })}>
          Quay lại đăng nhập
        </Button>
      </div>
    </AuthLayout>
  );
}

/** Nút gửi của màn chưa đăng nhập: đang gửi thì chạy vạch tiến trình thay vì icon xoay (Design_Web §6.0). */
function SubmitButton({ busy, disabled, children }: { busy: boolean; disabled?: boolean; children: string }) {
  return (
    <Button
      type="primary"
      htmlType="submit"
      block
      className={`auth-submit${busy ? ' auth-submit--busy' : ''}`}
      aria-busy={busy}
      aria-disabled={busy || disabled}
      disabled={disabled}
    >
      {children}
    </Button>
  );
}

/** Chỉ lưu thời điểm chờ; không lưu OTP, mật khẩu hoặc bằng chứng xác minh. */
function resendKey(username: string) {
  return `msms:password-reset-wait:${username.trim().toLowerCase()}`;
}

function readResendAt(username: string) {
  try {
    const value = Number(sessionStorage.getItem(resendKey(username)));
    return Number.isFinite(value) ? Math.min(value, Date.now() + RESEND_SECONDS * 1000) : 0;
  } catch { return 0; }
}

function rememberResendAt(username: string, seconds: number) {
  const deadline = Date.now() + seconds * 1000;
  try { sessionStorage.setItem(resendKey(username), String(deadline)); } catch { /* Bộ nhớ trang vẫn giữ thời điểm chờ. */ }
  return deadline;
}

/** Theo mốc giờ thực, giữ đúng thời gian khi tab ngủ hoặc quay lại bước nhập tên. */
function useCountdown(until: number) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 250);
    return () => window.clearInterval(id);
  }, []);
  return Math.max(0, Math.min(RESEND_SECONDS, Math.ceil((until - now) / 1000)));
}
