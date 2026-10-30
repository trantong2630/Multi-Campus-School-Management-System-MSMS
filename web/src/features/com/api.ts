import { apiClient } from '@/app/apiClient';
import type { NotificationList, OpenNotificationResult } from './types';

export const notificationApi = {
  list: (pageSize = 20) =>
    apiClient.get<NotificationList>('/com/notifications', { params: { page: 1, pageSize } }).then((r) => r.data),

  unreadCount: () =>
    apiClient.get<{ unreadCount: number }>('/com/notifications/unread-count').then((r) => r.data.unreadCount),

  /** Mở thông báo: backend đánh dấu đã đọc và kiểm lại quyền trên bản ghi nguồn (BR-26). */
  open: (id: number) =>
    apiClient.post<OpenNotificationResult>(`/com/notifications/${id}/open`).then((r) => r.data),

  markRead: (id: number) => apiClient.put(`/com/notifications/${id}/read`).then((r) => r.data),

  markAllRead: () => apiClient.put('/com/notifications/read-all').then((r) => r.data),
};
