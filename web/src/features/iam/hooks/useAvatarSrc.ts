import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { iamApi } from '../api';

/**
 * Đường dẫn object URL của ảnh đại diện (4.1.29). Ảnh cần access token nên tải dạng blob thay vì gắn thẳng vào <img src>.
 * `version` là giá trị đổi mỗi lần đổi ảnh (tên file hoặc avatarVersion); null nghĩa là chưa có ảnh → trả null.
 */
export function useAvatarSrc(personId: number | undefined, version: string | number | null | undefined) {
  const enabled = personId !== undefined && version != null;
  const photo = useQuery({
    queryKey: ['iam', 'avatar', personId, version],
    queryFn: () => iamApi.profile.avatar(personId!, version!),
    enabled,
    staleTime: Infinity,
    retry: false,
  });
  const blob = enabled ? photo.data : undefined;

  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!blob) { setUrl(null); return; }
    const u = URL.createObjectURL(blob);
    setUrl(u);
    return () => URL.revokeObjectURL(u);
  }, [blob]);

  return { src: url, loading: enabled && photo.isLoading };
}
