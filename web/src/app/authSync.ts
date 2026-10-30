import type { QueryClient } from '@tanstack/react-query';

const channelName = 'msms:session-ended';

/** Chỉ dùng sid để đồng bộ UI; backend vẫn xác thực token và phiên. */
export function sessionIdFromToken(token: string | null): string | null {
  if (!token) return null;
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const sid: unknown = JSON.parse(atob(payload)).sid;
    return typeof sid === 'string' && sid.length > 0 ? sid : null;
  } catch {
    return null;
  }
}

/** Hủy truy vấn trước khi bỏ cache để dữ liệu phiên cũ không xuất hiện sau khi đổi tài khoản. */
export function clearSessionQueries(client: QueryClient) {
  void client.cancelQueries();
  client.clear();
}

function readSessionId(value: unknown): string | null {
  if (!value || typeof value !== 'object') return null;
  const message = value as { sessionId?: unknown; eventId?: unknown };
  return typeof message.sessionId === 'string' && typeof message.eventId === 'string' ? message.sessionId : null;
}

export function publishSessionEnd(sessionId: string | null) {
  if (!sessionId || typeof window === 'undefined') return;
  // Không truyền hay lưu access/refresh token. eventId chỉ để mỗi lần gửi có một giá trị khác nhau.
  let eventId: string;
  try { eventId = crypto.randomUUID(); } catch { eventId = `${Date.now()}-${Math.random().toString(36).slice(2)}`; }
  const message = { sessionId, eventId };
  try {
    if (typeof BroadcastChannel !== 'undefined') {
      const channel = new BroadcastChannel(channelName);
      try { channel.postMessage(message); } finally { channel.close(); }
      return;
    }
  } catch {
    // Trình duyệt hạn chế BroadcastChannel vẫn có thể dùng sự kiện storage.
  }
  try {
    window.localStorage.setItem(channelName, JSON.stringify(message));
    window.localStorage.removeItem(channelName);
  } catch {
    // Tab khác vẫn bị backend từ chối ở request kế tiếp nếu trình duyệt chặn cả hai cơ chế.
  }
}

export function subscribeSessionEnd(onEnded: (sessionId: string) => void): () => void {
  const receive = (value: unknown) => {
    const sessionId = readSessionId(value);
    if (sessionId) onEnded(sessionId);
  };
  try {
    if (typeof BroadcastChannel !== 'undefined') {
      const channel = new BroadcastChannel(channelName);
      channel.onmessage = (event: MessageEvent<unknown>) => receive(event.data);
      return () => channel.close();
    }
  } catch {
    // Dùng storage khi BroadcastChannel không khả dụng.
  }
  const onStorage = (event: StorageEvent) => {
    if (event.key !== channelName || !event.newValue) return;
    try { receive(JSON.parse(event.newValue)); } catch { /* Bỏ qua dữ liệu không đúng định dạng. */ }
  };
  window.addEventListener('storage', onStorage);
  return () => window.removeEventListener('storage', onStorage);
}
