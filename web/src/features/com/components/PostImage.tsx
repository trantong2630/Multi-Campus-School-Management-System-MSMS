import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { postApi } from '../api';
import './posts.css';

function useObjectUrl(blob: Blob | undefined) {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!blob) { setUrl(null); return; }
    const u = URL.createObjectURL(blob);
    setUrl(u);
    return () => URL.revokeObjectURL(u);
  }, [blob]);
  return url;
}

interface Props {
  postId: number;
  imageId: number;
  alt: string;
  className?: string;
}

/** Ảnh bài đăng: tải kèm access token rồi hiện từ blob, vì bài Nội bộ không được công khai theo đường dẫn. */
export function PostImage({ postId, imageId, alt, className = '' }: Props) {
  const q = useQuery({
    queryKey: ['com', 'post-image', postId, imageId],
    queryFn: () => postApi.image(postId, imageId),
    staleTime: Infinity,
  });
  const url = useObjectUrl(q.data);
  return url
    ? <img src={url} alt={alt} className={`post-img ${className}`} />
    : <span className={`post-img post-img--empty ${className}`} aria-hidden />;
}

/** Ảnh vừa chọn từ máy, chưa tải lên. */
export function LocalImage({ file, alt, className = '' }: { file: File; alt: string; className?: string }) {
  const url = useObjectUrl(file);
  return url ? <img src={url} alt={alt} className={`post-img ${className}`} /> : <span className={`post-img post-img--empty ${className}`} aria-hidden />;
}
