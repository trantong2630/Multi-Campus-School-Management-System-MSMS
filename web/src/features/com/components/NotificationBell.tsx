import { useState } from 'react';
import { Badge, Button, Empty, Popover, Spin } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bell, CheckCheck } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { formatDateTime } from '@/shared/lib/format';
import { showError } from '@/shared/lib/feedback';
import { modal } from '@/shared/lib/antdStatic';
import {
  notificationEmpty, notificationLoadMore, notificationMarkAll, notificationOpenErrorTitle, notificationTitle, notificationTypeLabel,
} from '@/shared/lib/labels';
import { notificationApi } from '../api';
import { NOTIFICATION_KEY, useRealtime } from '../hooks/useRealtime';
import type { NotificationItem } from '../types';
import './notification.css';

const PAGE = 20;
// Dự phòng khi SignalR không nối được: làm mới chậm (COM-4.8.10 áp dụng tương tự cho web).
const FALLBACK_POLL_MS = 60_000;

/** Chuông thông báo ở góc phải header (Conventions §4.7): số chưa đọc, danh sách, mở kiểm quyền lại (BR-26). */
export function NotificationBell() {
  useRealtime();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [pageSize, setPageSize] = useState(PAGE);

  const unread = useQuery({
    queryKey: [...NOTIFICATION_KEY, 'unread'],
    queryFn: notificationApi.unreadCount,
    refetchInterval: FALLBACK_POLL_MS,
    meta: { silent: true },
  });
  const list = useQuery({
    queryKey: [...NOTIFICATION_KEY, 'list', pageSize],
    queryFn: () => notificationApi.list(pageSize),
    enabled: open,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEY });
  const markAll = useMutation({ mutationFn: notificationApi.markAllRead, onSuccess: refresh, onError: (e) => showError(e) });
  const openItem = useMutation({
    mutationFn: (item: NotificationItem) => notificationApi.open(item.notificationId),
    onSuccess: (result) => {
      void refresh();
      if (result.canAccess && result.targetRoute) {
        setOpen(false);
        navigate(result.targetRoute);
      } else {
        modal.error({ title: notificationOpenErrorTitle, content: result.reasonNotAllowed ?? 'Bạn không còn quyền xem nội dung này.', okText: 'Đóng' });
      }
    },
    onError: (e) => showError(e, notificationOpenErrorTitle),
  });

  const items = list.data?.items ?? [];
  const hasMore = (list.data?.total ?? 0) > items.length;

  const content = (
    <div className="notif-panel">
      <div className="notif-panel__head">
        <span className="notif-panel__title">{notificationTitle}</span>
        <Button type="link" size="small" icon={<CheckCheck size={14} />} disabled={!unread.data} loading={markAll.isPending} onClick={() => markAll.mutate()}>
          {notificationMarkAll}
        </Button>
      </div>
      <Spin spinning={list.isLoading}>
        {items.length === 0 && !list.isLoading ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={notificationEmpty} />
        ) : (
          <ul className="notif-list">
            {items.map((n) => (
              <li key={n.notificationId}>
                <button type="button" className={n.isRead ? 'notif-item' : 'notif-item notif-item--unread'} onClick={() => openItem.mutate(n)}>
                  <span className="notif-item__type">{notificationTypeLabel[n.notificationType] ?? n.notificationType}</span>
                  <span className="notif-item__title">{n.title}</span>
                  {n.body && <span className="notif-item__body">{n.body}</span>}
                  <span className="notif-item__time">{formatDateTime(n.createdAt)}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Spin>
      {hasMore && (
        <Button type="link" block loading={list.isFetching} onClick={() => setPageSize((s) => s + PAGE)}>{notificationLoadMore}</Button>
      )}
    </div>
  );

  return (
    <Popover content={content} trigger="click" placement="bottomRight" open={open} onOpenChange={setOpen} arrow={false} styles={{ container: { padding: 0 } }}>
      <button type="button" className="header-bell" aria-label={notificationTitle}>
        <Badge count={unread.data ?? 0} size="small" overflowCount={99}>
          <Bell size={18} />
        </Badge>
      </button>
    </Popover>
  );
}
