export interface NotificationItem {
  notificationId: number;
  recipientPersonId: number;
  notificationType: string;
  title: string;
  body: string | null;
  refEntityType: string | null;
  refEntityId: number | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
}

export interface NotificationList {
  items: NotificationItem[];
  total: number;
  unreadCount: number;
}

export interface OpenNotificationResult {
  notificationId: number;
  isRead: boolean;
  canAccess: boolean;
  targetRoute: string | null;
  refEntityType: string | null;
  refEntityId: number | null;
  reasonNotAllowed: string | null;
}
