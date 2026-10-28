import { apiClient, AUTH_REQUEST_TIMEOUT_MS } from '@/app/apiClient';
import type { LoginResponse, UserInfo } from './types';

export const iamApi = {
  login: (username: string, password: string) =>
    apiClient.post<LoginResponse>('/auth/login', { username, password }, { timeout: AUTH_REQUEST_TIMEOUT_MS }).then((r) => r.data),
  me: () => apiClient.get<UserInfo>('/auth/me').then((r) => r.data),
  logout: () => apiClient.post('/auth/logout'),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post('/auth/change-password', { currentPassword, newPassword }),
  // Quên mật khẩu (BR-04): luôn trả cùng một kết quả, không tiết lộ tài khoản có tồn tại hay có email không.
  requestPasswordReset: (username: string) => apiClient.post('/auth/forgot-password', { username }),
  resetPassword: (username: string, code: string, newPassword: string) =>
    apiClient.post('/auth/reset-password', { username, code, newPassword }),
};
