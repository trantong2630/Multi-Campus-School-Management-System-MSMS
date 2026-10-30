import axios, { CanceledError, isAxiosError } from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import type { LoginResponse } from '@/features/iam/types';
import { publishSessionEnd, sessionIdFromToken } from './authSync.ts';

// Access token chỉ giữ trong bộ nhớ trang. Refresh token nằm trong cookie HttpOnly do backend đặt: các tab dùng chung,
// đóng trình duyệt là mất, JavaScript không đọc được (BR-05).
let accessToken: string | null = null;
let generation = 0;
const endedSessions = new Set<string>();

function rememberEndedSession(sessionId: string | null) {
  if (!sessionId) return;
  endedSessions.delete(sessionId);
  endedSessions.add(sessionId);
  if (endedSessions.size > 64) endedSessions.delete(endedSessions.values().next().value!);
}

function ensureSessionNotEnded(token: string) {
  const sessionId = sessionIdFromToken(token);
  if (sessionId && endedSessions.has(sessionId)) throw new CanceledError('Phiên đăng nhập đã kết thúc.');
}

export const tokenStore = {
  get: () => accessToken,
  set: (token: string) => {
    generation++;
    accessToken = token;
  },
  clear: () => {
    generation++;
    accessToken = null;
  },
  generation: () => generation,
  isCurrent: (value: number) => generation === value,
  sessionId: () => sessionIdFromToken(accessToken),
};

export const apiClient = axios.create({ baseURL: '/api' });

interface SessionRequestConfig extends InternalAxiosRequestConfig {
  _authGeneration?: number;
  _retried?: boolean;
}

apiClient.interceptors.request.use((config: SessionRequestConfig) => {
  config._authGeneration ??= generation;
  const token = tokenStore.get();
  if (token && config.headers.Authorization === undefined) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Access token ngắn hạn hết hạn thì đổi bằng refresh token rồi gọi lại; không đổi được (hết hạn, đã đăng xuất,
// tài khoản bị khóa) thì bỏ token, AuthProvider đưa về trang đăng nhập (BR-05).
export const AUTH_LOST_EVENT = 'msms:auth-lost';
export const AUTH_REQUEST_TIMEOUT_MS = 10_000;

let refreshing: { generation: number; promise: Promise<LoginResponse> } | null = null;
let ending: { generation: number; promise: Promise<void> } | null = null;

class SessionChangedError extends Error {
  constructor() { super('Phiên đăng nhập đã thay đổi. Tải lại trang để tiếp tục.'); }
}

function ensureCurrent(expectedGeneration: number) {
  if (generation !== expectedGeneration) throw new CanceledError('Phiên đăng nhập đã thay đổi.');
}

function isEnding() { return ending?.generation === generation; }

function withSessionLock<T>(action: () => Promise<T>): Promise<T> {
  const locks = typeof navigator === 'undefined' ? undefined : navigator.locks;
  if (!locks) return action();
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), AUTH_REQUEST_TIMEOUT_MS);
  return locks.request('msms:refresh-session', { signal: controller.signal }, () => {
    clearTimeout(timer);
    return action();
  }).finally(() => clearTimeout(timer));
}

async function exchangeRefresh(expectedGeneration: number, expectedSessionId: string | null): Promise<LoginResponse> {
  ensureCurrent(expectedGeneration);
  const response = await axios.post<LoginResponse>('/api/auth/refresh', {}, {
    withCredentials: true, timeout: AUTH_REQUEST_TIMEOUT_MS,
  });
  ensureCurrent(expectedGeneration);
  ensureSessionNotEnded(response.data.token);
  if (expectedSessionId && sessionIdFromToken(response.data.token) !== expectedSessionId) throw new SessionChangedError();
  accessToken = response.data.token;
  return response.data;
}

export function loginSession(send: () => Promise<LoginResponse>): Promise<LoginResponse> {
  const expectedGeneration = ++generation;
  return withSessionLock(async () => {
    ensureCurrent(expectedGeneration);
    const response = await send();
    ensureCurrent(expectedGeneration);
    ensureSessionNotEnded(response.token);
    accessToken = response.token;
    return response;
  });
}

function endCurrentSession(expectedGeneration: number) {
  if (!tokenStore.isCurrent(expectedGeneration)) return;
  tokenStore.clear();
  if (typeof window !== 'undefined') window.dispatchEvent(new Event(AUTH_LOST_EVENT));
}

export function endMatchingSession(sessionId: string) {
  // Một tab mới có thể đang đợi refresh và chưa có access token để đối chiếu.
  rememberEndedSession(sessionId);
  if (tokenStore.sessionId() === sessionId) endCurrentSession(generation);
}

function isUnauthorized(error: unknown) { return isAxiosError(error) && error.response?.status === 401; }

/** Đăng xuất chỉ hoàn tất khi server đã thu hồi phiên hoặc xác nhận phiên không còn hợp lệ. */
export function logoutSession(send: (token: string | null) => Promise<{ status: number }>): Promise<void> {
  if (ending?.generation === generation) return ending.promise;
  const expectedGeneration = ++generation;
  let sessionId = tokenStore.sessionId();
  const finish = () => {
    rememberEndedSession(sessionId);
    endCurrentSession(expectedGeneration);
    publishSessionEnd(sessionId);
  };
  const sendOnce = async () => {
    ensureCurrent(expectedGeneration);
    const response = await send(accessToken);
    if (response.status !== 204) throw new Error('Phản hồi đăng xuất không hợp lệ.');
  };
  const operation = withSessionLock(async () => {
    try {
      await sendOnce();
    } catch (error) {
      if (!isUnauthorized(error)) throw error;
      // Access token hết hạn chưa có nghĩa là phiên kết thúc: đổi bằng cookie rồi thử đúng một lần.
      try {
        const refreshed = await exchangeRefresh(expectedGeneration, sessionId);
        sessionId ??= sessionIdFromToken(refreshed.token);
      } catch (refreshError) {
        if (!isUnauthorized(refreshError)) throw refreshError;
        ensureCurrent(expectedGeneration);
        finish();
        return;
      }
      try { await sendOnce(); } catch (retryError) {
        if (!isUnauthorized(retryError)) throw retryError;
        ensureCurrent(expectedGeneration);
      }
    }
    finish();
  });
  const pending = { generation: expectedGeneration, promise: operation };
  ending = pending;
  pending.promise = operation.finally(() => { if (ending === pending) ending = null; });
  return pending.promise;
}

/** Đổi cookie refresh lấy access token mới. Nhiều nơi gọi cùng lúc thì dùng chung một lần gọi. */
export function refreshSession(): Promise<LoginResponse> {
  if (isEnding()) return Promise.reject(new CanceledError('Đang đăng xuất.'));
  if (refreshing?.generation === generation) return refreshing.promise;
  const expectedGeneration = generation;
  const sessionId = tokenStore.sessionId();
  const operation = withSessionLock(() => exchangeRefresh(expectedGeneration, sessionId));
  const pending = { generation: expectedGeneration, promise: operation };
  refreshing = pending;
  pending.promise = operation.finally(() => { if (refreshing === pending) refreshing = null; });
  return pending.promise;
}

apiClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    if (!isAxiosError(error) || error.response?.status !== 401 || !error.config) return Promise.reject(error);
    const url = error.config.url ?? '';
    // Các API dùng khi chưa đăng nhập: 401 ở đây là lỗi nghiệp vụ, không phải hết phiên.
    if (/\/auth\/(login|logout|refresh|forgot-password|reset-password)/.test(url)) return Promise.reject(error);

    const config = error.config as SessionRequestConfig;
    if (config._authGeneration !== generation || isEnding()) return Promise.reject(error);
    if (!config._retried) {
      try {
        const session = await refreshSession();
        ensureCurrent(config._authGeneration!);
        if (isEnding()) return Promise.reject(error);
        config._retried = true;
        config.headers.Authorization = `Bearer ${session.token}`;
        return apiClient(config);
      } catch (refreshError) {
        if (config._authGeneration !== generation || isEnding()) return Promise.reject(error);
        if (!isUnauthorized(refreshError) && !(refreshError instanceof SessionChangedError)) return Promise.reject(refreshError);
      }
    }
    endCurrentSession(config._authGeneration!);
    return Promise.reject(error);
  },
);

/** Backend trả { code, message } tiếng Việt; FE chỉ hiện message (Conventions §3). */
export function getErrorMessage(error: unknown): string {
  if (error instanceof SessionChangedError) return error.message;
  if (isAxiosError(error)) {
    const message = (error.response?.data as { message?: string } | undefined)?.message;
    if (message) return message;
    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT')
      return 'Máy chủ phản hồi quá lâu. Vui lòng thử lại.';
    if (!error.response) return 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.';
  }
  return 'Có lỗi xảy ra. Thử lại sau, nếu vẫn lỗi thì liên hệ quản trị.';
}
