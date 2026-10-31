import { apiClient } from '@/app/apiClient';
import type { NotificationList, OpenNotificationResult, PostDetail, PostInput, PostList } from './types';

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

const filesForm = (files: File[], fields: Record<string, string> = {}) => {
  const form = new FormData();
  Object.entries(fields).forEach(([k, v]) => form.append(k, v));
  files.forEach((f) => form.append('files', f));
  return form;
};

export const postApi = {
  list: (params: { page: number; pageSize: number; visibility?: string; status?: string }) =>
    apiClient.get<PostList>('/com/posts', { params }).then((r) => r.data),

  get: (id: number) => apiClient.get<PostDetail>(`/com/posts/${id}`).then((r) => r.data),

  /** Ảnh cần Authorization nên tải dạng blob thay vì gắn thẳng vào <img src> (bài Nội bộ không công khai). */
  image: (postId: number, imageId: number) =>
    apiClient.get<Blob>(`/com/posts/${postId}/images/${imageId}`, { responseType: 'blob' }).then((r) => r.data),

  create: (input: PostInput, files: File[]) =>
    apiClient.post<PostDetail>('/com/posts', filesForm(files, { ...input })).then((r) => r.data),

  update: (id: number, input: PostInput) => apiClient.put<PostDetail>(`/com/posts/${id}`, input).then((r) => r.data),

  addImages: (id: number, files: File[]) =>
    apiClient.post<PostDetail>(`/com/posts/${id}/images`, filesForm(files)).then((r) => r.data),

  removeImage: (id: number, imageId: number) =>
    apiClient.delete<PostDetail>(`/com/posts/${id}/images/${imageId}`).then((r) => r.data),

  hide: (id: number, reason?: string) => apiClient.post<PostDetail>(`/com/posts/${id}/hide`, { reason }).then((r) => r.data),

  unhide: (id: number) => apiClient.post<PostDetail>(`/com/posts/${id}/unhide`).then((r) => r.data),

  remove: (id: number) => apiClient.delete(`/com/posts/${id}`).then((r) => r.data),
};
