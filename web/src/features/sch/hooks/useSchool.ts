import { useQuery } from '@tanstack/react-query';
import { schApi } from '../api';
import { schoolLogoUrl } from '../api';

export const DEFAULT_SCHOOL_NAME = 'MSMS';

/**
 * Thông tin nhận diện của trường. Màn hình đăng nhập cũng gọi nên không báo lỗi bằng hộp thoại:
 * chưa lấy được thì giao diện dùng tên và biểu tượng mặc định.
 */
export function useSchool() {
  const query = useQuery({
    queryKey: ['school'],
    queryFn: schApi.school.get,
    staleTime: 5 * 60 * 1000,
    retry: false,
    meta: { silent: true },
  });
  const profile = query.data;
  return {
    ...query,
    profile,
    name: profile?.name ?? DEFAULT_SCHOOL_NAME,
    hasName: !!profile?.name,
    logoSrc: profile?.hasLogo ? schoolLogoUrl(profile.logoVersion) : null,
  };
}
