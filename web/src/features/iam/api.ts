import { apiClient, AUTH_REQUEST_TIMEOUT_MS, loginSession, logoutSession } from '@/app/apiClient';
import type { LoginResponse, UserInfo } from './types';

export interface PasswordResetVerification {
  resetToken: string;
  expiresAt: string;
}

export const iamApi = {
  login: (username: string, password: string) =>
    loginSession(() => apiClient.post<LoginResponse>('/auth/login', { username, password }, {
      timeout: AUTH_REQUEST_TIMEOUT_MS, headers: { Authorization: '' },
    }).then((r) => r.data)),
  me: () => apiClient.get<UserInfo>('/auth/me').then((r) => r.data),
  logout: () => logoutSession((token) => apiClient.post('/auth/logout', {}, {
    timeout: AUTH_REQUEST_TIMEOUT_MS, headers: { Authorization: token ? `Bearer ${token}` : '' },
  })),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post('/auth/change-password', { currentPassword, newPassword }),
  // Quên mật khẩu (BR-04): luôn trả cùng một kết quả, không tiết lộ tài khoản có tồn tại hay có email không.
  requestPasswordReset: (username: string) => apiClient.post('/auth/forgot-password', { username }, {
    timeout: 20_000, headers: { Authorization: '' },
  }).then((response) => {
    const retryAfter = Number(response.headers['retry-after']);
    return Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : 60;
  }),
  verifyPasswordReset: (username: string, code: string) =>
    apiClient.post<PasswordResetVerification>('/auth/verify-reset-code', { username, code }, {
      timeout: AUTH_REQUEST_TIMEOUT_MS, headers: { Authorization: '' },
    }).then((response) => response.data),
  resetPassword: (username: string, resetToken: string, newPassword: string) =>
    apiClient.post('/auth/reset-password', { username, resetToken, newPassword }, {
      timeout: AUTH_REQUEST_TIMEOUT_MS, headers: { Authorization: '' },
    }),
};
