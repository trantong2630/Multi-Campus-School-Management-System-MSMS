import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { schApi } from '@/features/sch/api';

/** Giá trị trễ một nhịp để ô gõ chữ không gọi API sau mỗi phím. */
export function useDebounced<T>(value: T, ms = 400): T {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

/** Môn học đang hoạt động, dùng chung cache với màn hình Tổ chuyên môn & Môn học. */
export function useSubjects() {
  const q = useQuery({ queryKey: ['sch', 'subjects', undefined], queryFn: () => schApi.subjects.list() });
  return { ...q, active: q.data?.filter((s) => s.isActive) ?? [] };
}

/** Giờ hiện tại theo máy cộng độ lệch đã biết, cập nhật theo nhịp. */
export function useNow(offsetMs = 0, tickMs = 1000) {
  const [tick, setTick] = useState(() => Date.now());
  useEffect(() => {
    const t = setInterval(() => setTick(Date.now()), tickMs);
    return () => clearInterval(t);
  }, [tickMs]);
  return tick + offsetMs;
}
