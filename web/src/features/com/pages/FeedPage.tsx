import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { Button, Empty, Skeleton } from 'antd';
import { useEffect, useRef } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { PageHeader } from '@/shared/components/PageHeader';
import { formatDateTime } from '@/shared/lib/format';
import { postVisibilityLabel } from '@/shared/lib/labels';
import { postApi } from '../api';
import { PostImage } from '../components/PostImage';
import type { PostListItem } from '../types';
import '../components/posts.css';

const PAGE_SIZE = 8;

const initial = (name: string) => name.trim().split(/\s+/).pop()?.[0]?.toUpperCase() ?? '?';

/** Một bài trên dòng thời gian: tải đủ nội dung và ảnh, chỉ đọc (không cảm xúc, không bình luận). */
function FeedCard({ item, showScope }: { item: PostListItem; showScope: boolean }) {
  const detail = useQuery({ queryKey: ['com', 'post', item.postId], queryFn: () => postApi.get(item.postId) });
  const p = detail.data;
  const images = p?.images ?? [];

  return (
    <article className="feed-card">
      <header className="feed-card__head">
        <span className="feed-card__avatar" aria-hidden>{initial(item.authorName)}</span>
        <div className="feed-card__who">
          <span className="feed-card__author">{item.authorName}</span>
          <span className="feed-card__time">
            {formatDateTime(item.createdAt)}
            {showScope && <span className="post-scope feed-card__scope">{postVisibilityLabel[item.visibility]}</span>}
          </span>
        </div>
      </header>
      <h3 className="feed-card__title">{item.title}</h3>
      {detail.isLoading ? <Skeleton active paragraph={{ rows: 3 }} title={false} /> : (
        <p className="feed-card__content">{p?.content ?? item.excerpt}</p>
      )}
      {images.length > 0 && (
        <div className={`feed-gallery feed-gallery--${Math.min(images.length, 4)}`}>
          {images.slice(0, 4).map((img, i) => (
            <div key={img.postImageId} className="feed-gallery__cell">
              <PostImage postId={item.postId} imageId={img.postImageId} alt={`Ảnh ${i + 1} của bài ${item.title}`} />
              {i === 3 && images.length > 4 && <span className="feed-gallery__more">+{images.length - 4}</span>}
            </div>
          ))}
        </div>
      )}
    </article>
  );
}

/** COM-02: bảng tin dạng dòng thời gian, lướt xuống để tải thêm. Không tạo thông báo (BR-27). */
export function FeedPage() {
  const { user } = useAuth();
  const isStaff = user?.accountType !== 'Student' && user?.accountType !== 'Parent';
  const sentinel = useRef<HTMLDivElement>(null);

  const feed = useInfiniteQuery({
    queryKey: ['com', 'posts', 'feed'],
    initialPageParam: 1,
    queryFn: ({ pageParam }) => postApi.list({ page: pageParam, pageSize: PAGE_SIZE, status: 'Posted' }),
    getNextPageParam: (last, all) => (all.reduce((n, pg) => n + pg.items.length, 0) < last.total ? all.length + 1 : undefined),
  });
  const items = feed.data?.pages.flatMap((pg) => pg.items) ?? [];
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = feed;

  useEffect(() => {
    const el = sentinel.current;
    if (!el || !hasNextPage) return;
    const io = new IntersectionObserver((entries) => {
      if (entries[0].isIntersecting && !isFetchingNextPage) void fetchNextPage();
    }, { rootMargin: '400px' });
    io.observe(el);
    return () => io.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage, items.length]);

  return (
    <>
      <PageHeader title="Bảng tin" description="Các bài đăng mới nhất của nhà trường." />
      <div className="feed">
        {feed.isLoading && <Skeleton active avatar paragraph={{ rows: 5 }} />}
        {!feed.isLoading && items.length === 0 && (
          <div className="feed-card"><Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có bài đăng nào." /></div>
        )}
        {items.map((it) => <FeedCard key={it.postId} item={it} showScope={isStaff} />)}
        <div ref={sentinel} className="feed__end">
          {isFetchingNextPage && <Skeleton active paragraph={{ rows: 2 }} />}
          {hasNextPage && !isFetchingNextPage && <Button type="link" onClick={() => void fetchNextPage()}>Xem thêm</Button>}
          {!hasNextPage && items.length > 0 && <span className="muted">Bạn đã xem hết bài đăng.</span>}
        </div>
      </div>
    </>
  );
}
