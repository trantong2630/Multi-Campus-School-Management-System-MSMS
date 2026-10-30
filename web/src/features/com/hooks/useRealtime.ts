import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import * as signalR from '@microsoft/signalr';
import { tokenStore } from '@/app/apiClient';
import { useAuth } from '@/app/AuthProvider';

export const NOTIFICATION_KEY = ['com', 'notifications'] as const;

/**
 * COM-4.8.9: một kết nối SignalR cho phiên đăng nhập hiện tại, dựng lại khi đổi phiên (access token đổi theo phiên).
 * Có thông báo mới thì làm mới chuông; nối lại sau khi mất kết nối thì tải lại phần đã lỡ qua REST.
 */
export function useRealtime() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const sessionId = tokenStore.sessionId();
  const accountId = user?.accountId;

  useEffect(() => {
    if (!accountId || !sessionId) return;
    const refresh = () => void queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEY });

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/signalr', { accessTokenFactory: () => tokenStore.get() ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('NotificationCreated', refresh);
    // Tin nhắn mới cũng sinh thông báo; ConversationService đẩy NotificationCreated riêng nên chỉ cần làm mới chuông.
    connection.onreconnected(refresh);
    connection.start().catch(() => { /* Mất kết nối: chuông vẫn tự làm mới theo chu kỳ chậm (polling dự phòng). */ });

    return () => { void connection.stop(); };
  }, [accountId, sessionId, queryClient]);
}
