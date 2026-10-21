import dayjs from 'dayjs';

/** Ngày dd/MM/yyyy, giờ 24h HH:mm (Conventions §3). Nhận chuỗi ISO từ Backend. */
export const formatDate = (value?: string | null) => (value ? dayjs(value).format('DD/MM/YYYY') : '—');
export const formatDateTime = (value?: string | null) => (value ? dayjs(value).format('DD/MM/YYYY HH:mm') : '—');
export const formatTime = (value?: string | null) => (value ? value.slice(0, 5) : '—');

const WEEKDAYS = ['CN', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];
/** Thứ theo cách gọi của trường: 2..7 = Thứ 2..Thứ 7 (khớp DayOfWeek của Backend), 1 = Chủ nhật. */
export const weekdayLabel = (vnDay: number) => (vnDay === 1 ? 'CN' : `Thứ ${vnDay}`);
export const weekdayOfDate = (value: string | dayjs.Dayjs) => WEEKDAYS[dayjs(value).day()];

export const sessionOfDayLabel = (value: string) => (value === 'Morning' ? 'Sáng' : 'Chiều');

/** Giờ gửi lên Backend luôn có giây (HH:mm:ss). */
export const toTimeString = (value: dayjs.Dayjs) => value.format('HH:mm:ss');
export const toDateString = (value: dayjs.Dayjs) => value.format('YYYY-MM-DD');
