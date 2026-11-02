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

export type PostVisibility = 'Internal' | 'School';
export type PostStatus = 'Posted' | 'Hidden';

export interface PostActions {
  canEdit: boolean;
  canHide: boolean;
  canUnhide: boolean;
  canDelete: boolean;
}

export interface PostListItem extends PostActions {
  postId: number;
  title: string;
  excerpt: string;
  visibility: PostVisibility;
  status: PostStatus;
  authorPersonId: number;
  authorName: string;
  createdAt: string;
  updatedAt: string | null;
  coverImageId: number | null;
  imageCount: number;
}

export interface PostImageRef {
  postImageId: number;
  orderNo: number;
}

export interface PostDetail extends PostActions {
  postId: number;
  title: string;
  content: string;
  visibility: PostVisibility;
  status: PostStatus;
  authorPersonId: number;
  authorName: string;
  createdAt: string;
  updatedAt: string | null;
  images: PostImageRef[];
  hiddenByName: string | null;
  hiddenAt: string | null;
  hiddenReason: string | null;
}

export interface PostList {
  items: PostListItem[];
  total: number;
}

export interface PostInput {
  title: string;
  content: string;
  visibility: PostVisibility;
}
