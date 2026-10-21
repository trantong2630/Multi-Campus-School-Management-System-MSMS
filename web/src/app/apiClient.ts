import axios, { isAxiosError } from 'axios';

const TOKEN_KEY = 'msms.token';

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
};

export const apiClient = axios.create({ baseURL: '/api' });

apiClient.interceptors.request.use((config) => {
  const token = tokenStore.get();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Phiên hết hạn hoặc tài khoản bị khóa giữa phiên (BR-05): bỏ token, AuthProvider đưa về trang đăng nhập.
export const AUTH_LOST_EVENT = 'msms:auth-lost';
apiClient.interceptors.response.use(
  (res) => res,
  (error) => {
    if (isAxiosError(error) && error.response?.status === 401 && !error.config?.url?.includes('/auth/login')) {
      tokenStore.clear();
      window.dispatchEvent(new Event(AUTH_LOST_EVENT));
    }
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
