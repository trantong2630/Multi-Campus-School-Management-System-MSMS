import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { PersonCell } from '@/shared/components/PersonCell';
import { formatDate } from '@/shared/lib/format';
import { enrollmentCloseReasonLabel } from '@/shared/lib/labels';
import type { RosterItem } from '../types';

const GENDER_LABELS: Record<string, string> = { Male: 'Nam', Female: 'Nữ', Other: 'Khác' };
export const genderText = (v: string | null) => (v ? GENDER_LABELS[v] ?? v : '—');

/** Mặc định xem theo hôm nay; lớp của năm học chưa bắt đầu thì xem theo ngày bắt đầu năm học để thấy các em đã được xếp. */
export function defaultRosterDate(yearStart?: string): Dayjs {
  const today = dayjs();
  return yearStart && today.isBefore(yearStart, 'day') ? dayjs(yearStart) : today;
}

/** Cột chung của danh sách lớp (SCH-05): màn Xếp lớp và màn Danh sách lớp. */
export const rosterColumns: ColumnsType<RosterItem> = [
  { title: 'Học sinh', dataIndex: 'fullName', width: 240, render: (v: string, r: RosterItem) => <PersonCell name={v} sub={r.personCode} /> },
  { title: 'Ngày sinh', dataIndex: 'dateOfBirth', width: 104, className: 'tabular', render: (v: string | null) => formatDate(v) },
  { title: 'Giới tính', dataIndex: 'gender', width: 84, render: (v: string | null) => genderText(v) },
  { title: 'Bắt đầu', dataIndex: 'startDate', width: 112, className: 'tabular', render: (v: string) => formatDate(v) },
  {
    title: 'Kết thúc', dataIndex: 'endDate', width: 124, className: 'tabular',
    render: (v: string | null, r: RosterItem) => v
      ? (
        <>
          {formatDate(v)}
          {r.reason && <div className="roster-reason">{enrollmentCloseReasonLabel[r.reason] ?? r.reason}</div>}
        </>
      )
      : <span className="muted">Hết năm học</span>,
  },
];
