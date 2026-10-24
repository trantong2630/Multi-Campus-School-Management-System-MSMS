import axios, { isAxiosError } from 'axios';
import type { UserInfo } from '@/features/iam/types';

// Access token chỉ giữ trong bộ nhớ trang. Refresh token nằm trong cookie HttpOnly do backend đặt: các tab dùng chung,
// đóng trình duyệt là mất, JavaScript không đọc được (BR-05).
let accessToken: string | null = null;

export const tokenStore = {
  get: () => accessToken,
  set: (token: string) => {
    accessToken = token;
  },
  clear: () => {
    accessToken = null;
  },
};

export const apiClient = axios.create({ baseURL: '/api' });

apiClient.interceptors.request.use((config) => {
  const token = tokenStore.get();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Access token ngắn hạn hết hạn thì đổi bằng refresh token rồi gọi lại; không đổi được (hết hạn, đã đăng xuất,
// tài khoản bị khóa) thì bỏ token, AuthProvider đưa về trang đăng nhập (BR-05).
export const AUTH_LOST_EVENT = 'msms:auth-lost';

let refreshing: Promise<RefreshedSession> | null = null;

interface RefreshedSession {
  token: string;
  user: UserInfo;
}

/** Đổi cookie refresh lấy access token mới. Nhiều nơi gọi cùng lúc thì dùng chung một lần gọi. */
export function refreshSession(): Promise<RefreshedSession> {
  refreshing ??= axios
    .post<RefreshedSession>('/api/auth/refresh', {})
    .then((r) => {
      tokenStore.set(r.data.token);
      return r.data;
    })
    .finally(() => {
      refreshing = null;
    });
  return refreshing;
}

apiClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    if (!isAxiosError(error) || error.response?.status !== 401 || !error.config) return Promise.reject(error);
    const url = error.config.url ?? '';
    // Các API dùng khi chưa đăng nhập: 401 ở đây là lỗi nghiệp vụ, không phải hết phiên.
    if (/\/auth\/(login|refresh|forgot-password|reset-password)/.test(url)) return Promise.reject(error);

    const config = error.config as typeof error.config & { _retried?: boolean };
    if (!config._retried) {
      try {
        const session = await refreshSession();
        config._retried = true;
        config.headers.Authorization = `Bearer ${session.token}`;
        return apiClient(config);
      } catch {
        // rơi xuống bước bỏ phiên bên dưới
      }
    }
    tokenStore.clear();
    window.dispatchEvent(new Event(AUTH_LOST_EVENT));
    return Promise.reject(error);
  },
);

/** Backend trả { code, message } tiếng Việt; FE chỉ hiện message (Conventions §3). */
export function getErrorMessage(error: unknown): string {
  if (isAxiosError(error)) {
    const message = (error.response?.data as { message?: string } | undefined)?.message;
    if (message) return message;
    if (!error.response) return 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.';
  }
  return 'Có lỗi xảy ra. Thử lại sau, nếu vẫn lỗi thì liên hệ quản trị.';
}
