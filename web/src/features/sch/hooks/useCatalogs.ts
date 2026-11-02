import { useQuery } from '@tanstack/react-query';
import { schApi } from '../api';

/** Danh mục dùng ở nhiều màn hình (bộ lọc, ô chọn). Mọi màn hình lấy qua đây để dùng chung cache. */
export const useCampuses = () => useQuery({ queryKey: ['sch', 'campuses'], queryFn: schApi.campuses.list });
export const useYears = () => useQuery({ queryKey: ['sch', 'years'], queryFn: schApi.years.list });
export const useDepartments = () => useQuery({ queryKey: ['sch', 'departments'], queryFn: schApi.departments.list });
export const usePeriodSlots = () => useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });

/** Năm học hiện hành, hoặc năm đầu tiên nếu chưa đánh dấu. */
export function useCurrentYear() {
  const years = useYears();
  const current = years.data?.find((y) => y.isCurrent) ?? years.data?.[0];
  return { ...years, current };
}

/** Thống kê cấu trúc của màn Thiết lập. Cùng tiền tố ['sch'] nên sửa danh mục xong là tự làm mới. */
export const useCampusStats = () => useQuery({ queryKey: ['sch', 'stats', 'campuses'], queryFn: schApi.stats.campuses });
export const useCatalogStats = () => useQuery({ queryKey: ['sch', 'stats', 'catalog'], queryFn: schApi.stats.catalog });
export const useClassStats = () => useQuery({ queryKey: ['sch', 'stats', 'classes'], queryFn: schApi.stats.classes });
