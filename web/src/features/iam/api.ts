import { apiClient } from '@/app/apiClient';
import type { LoginResponse, UserInfo } from './types';

export const iamApi = {
  login: (username: string, password: string) =>
    apiClient.post<LoginResponse>('/auth/login', { username, password }).then((r) => r.data),
  me: () => apiClient.get<UserInfo>('/auth/me').then((r) => r.data),
  logout: () => apiClient.post('/auth/logout'),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post('/auth/change-password', { currentPassword, newPassword }),
};
