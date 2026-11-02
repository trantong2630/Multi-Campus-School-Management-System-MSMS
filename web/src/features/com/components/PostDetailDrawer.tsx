import { useQuery } from '@tanstack/react-query';
import { Button, Drawer, Skeleton } from 'antd';
import { Eye, EyeOff, Pencil, Trash2 } from 'lucide-react';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { formatDateTime } from '@/shared/lib/format';
import { postVisibilityLabel } from '@/shared/lib/labels';
import { postApi } from '../api';
import type { PostDetail } from '../types';
import { PostImage } from './PostImage';
import './posts.css';

interface Props {
  postId: number | null;
  onClose: () => void;
  onEdit: (post: PostDetail) => void;
  onHide: (post: PostDetail) => void;
  onUnhide: (post: PostDetail) => void;
  onDelete: (post: PostDetail) => void;
}

/** Xem một bài đăng đầy đủ: ảnh, nội dung, người đăng; bài bị ẩn kèm người ẩn, thời điểm và lý do (BR-27). */
export function PostDetailDrawer({ postId, onClose, onEdit, onHide, onUnhide, onDelete }: Props) {
  const q = useQuery({
    queryKey: ['com', 'post', postId],
    queryFn: () => postApi.get(postId!),
    enabled: postId !== null,
  });
  const p = q.data;

  return (
    <Drawer
      open={postId !== null} size={640} onClose={onClose} title={p?.title ?? 'Bài đăng'} mask={{ closable: false }} destroyOnHidden
      footer={p && (p.canEdit || p.canHide || p.canUnhide || p.canDelete) && (
        <div className="form-drawer__footer">
          <Button onClick={onClose}>Đóng</Button>
          {p.canDelete && <Button danger icon={<Trash2 size={16} />} onClick={() => onDelete(p)}>Xóa</Button>}
          {p.canHide && <Button icon={<EyeOff size={16} />} onClick={() => onHide(p)}>Ẩn bài</Button>}
          {p.canUnhide && <Button icon={<Eye size={16} />} onClick={() => onUnhide(p)}>Hiện lại</Button>}
          {p.canEdit && <Button type="primary" icon={<Pencil size={16} />} onClick={() => onEdit(p)}>Sửa</Button>}
        </div>
      )}
    >
      {q.isLoading || !p ? <Skeleton active paragraph={{ rows: 8 }} /> : (
        <article className="post-detail">
          <div className="post-meta">
            <span className="post-scope">{postVisibilityLabel[p.visibility]}</span>
            {p.status === 'Hidden' && <StatusBadge domain="post" value={p.status} />}
            <span className="muted">{p.authorName} · {formatDateTime(p.createdAt)}{p.updatedAt ? ` · đã sửa ${formatDateTime(p.updatedAt)}` : ''}</span>
          </div>
          {p.status === 'Hidden' && (
            <InlineAlert
              type="warning"
              title={`Bài đã được ẩn${p.hiddenByName ? ` bởi ${p.hiddenByName}` : ''}${p.hiddenAt ? ` lúc ${formatDateTime(p.hiddenAt)}` : ''}`}
              description={p.hiddenReason ? `Lý do: ${p.hiddenReason}` : undefined}
            />
          )}
          {p.images.length > 0 && (
            <div className="post-gallery">
              {p.images.map((img, i) => (
                <PostImage key={img.postImageId} postId={p.postId} imageId={img.postImageId} alt={`Ảnh ${i + 1} của bài ${p.title}`} className={i === 0 ? 'post-gallery__cover' : ''} />
              ))}
            </div>
          )}
          <p className="post-content">{p.content}</p>
        </article>
      )}
    </Drawer>
  );
}
