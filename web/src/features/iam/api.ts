import { apiClient, AUTH_REQUEST_TIMEOUT_MS, loginSession, logoutSession } from '@/app/apiClient';
import type { LoginResponse, OwnProfile, UserInfo } from './types';

export interface PasswordResetVerification {
  resetToken: string;
  expiresAt: string;
}

export interface UpdateProfileRequest {
  phoneNumber?: string;
  email?: string;
  address?: string;
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

  // IAM-04: hồ sơ cá nhân (4.1.27–4.1.30)
  profile: {
    get: () => apiClient.get<OwnProfile>('/iam/profile').then((r) => r.data),
    update: (body: UpdateProfileRequest) => apiClient.put<OwnProfile>('/iam/profile', body).then((r) => r.data),
    uploadAvatar: (file: File) => {
      const fd = new FormData();
      fd.append('file', file);
      return apiClient.post<OwnProfile>('/iam/profile/avatar', fd).then((r) => r.data);
    },
    removeAvatar: () => apiClient.delete<OwnProfile>('/iam/profile/avatar').then((r) => r.data),
    /** Ảnh cần Authorization nên tải dạng blob thay vì gắn thẳng vào <img src>; v đổi theo lần cập nhật để không dùng ảnh cũ. */
    avatar: (personId: number, version: string | number) =>
      apiClient.get<Blob>(`/iam/profile/avatar/${personId}`, { params: { v: version }, responseType: 'blob' })
        .then((r) => r.data),
  },
};
