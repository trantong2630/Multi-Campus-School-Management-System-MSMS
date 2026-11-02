import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import type { AcademicYear, Semester } from './types';

/** Học kỳ chứa ngày `today` (tính theo ngày, không theo giờ). */
export function semesterAt(year: AcademicYear | undefined, today: Dayjs): Semester | undefined {
  const d = today.startOf('day');
  return year?.semesters.find((s) => !d.isBefore(dayjs(s.startDate)) && !d.isAfter(dayjs(s.endDate)));
}

/** Học kỳ kế tiếp chưa bắt đầu. */
export function nextSemester(year: AcademicYear | undefined, today: Dayjs): Semester | undefined {
  const d = today.startOf('day');
  return year?.semesters.find((s) => d.isBefore(dayjs(s.startDate)));
}

/** Đang ở tuần thứ mấy của học kỳ, trên tổng số tuần (tuần 1 bắt đầu từ ngày đầu học kỳ). */
export function semesterProgress(semester: Semester, today: Dayjs): { week: number; total: number } {
  const start = dayjs(semester.startDate);
  const total = Math.ceil((dayjs(semester.endDate).diff(start, 'day') + 1) / 7);
  return { week: Math.min(Math.floor(today.startOf('day').diff(start, 'day') / 7) + 1, total), total };
}
