import { useQuery } from '@tanstack/react-query';
import { Card, DatePicker } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { PageHeader } from '@/shared/components/PageHeader';
import { formatDate, toDateString } from '@/shared/lib/format';
import { schApi } from '../api';
import { defaultRosterDate, rosterColumns } from '../components/rosterColumns';
import '../components/school.css';
import { useCurrentYear } from '../hooks/useCatalogs';
import type { AcademicYear, RosterItem, SchoolClass } from '../types';

const bornInMonth = (dob: string | null, month: number) => !!dob && dayjs(dob).month() === month;

/** Danh sách học sinh của một lớp chủ nhiệm tại một ngày, chỉ xem. */
function ClassStudents({ cls, year }: { cls: SchoolClass; year: AcademicYear }) {
  const [picked, setPicked] = useState<Dayjs | null>(null);
  const date = picked ?? defaultRosterDate(year.startDate);
  const dateKey = toDateString(date);
  const roster = useQuery({
    queryKey: ['sch', 'roster', cls.classId, dateKey],
    queryFn: () => schApi.enrollments.roster(cls.classId, dateKey),
  });

  const students = roster.data?.students ?? [];
  const male = students.filter((s) => s.gender === 'Male').length;
  const female = students.filter((s) => s.gender === 'Female').length;
  const month = date.month();
  const birthdays = students.filter((s) => bornInMonth(s.dateOfBirth, month))
    .sort((a, b) => dayjs(a.dateOfBirth).date() - dayjs(b.dateOfBirth).date());
  const anyEnded = students.some((s) => s.endDate !== null);

  // Cột Kết thúc chỉ có ý nghĩa khi có em đã kết thúc ghi danh; còn lại em nào cũng học tới hết năm.
  const columns: ColumnsType<RosterItem> = [
    { title: 'STT', key: 'no', width: 64, align: 'center', className: 'tabular', render: (_: unknown, __: RosterItem, i: number) => i + 1 },
    ...rosterColumns
      .filter((c) => anyEnded || !('dataIndex' in c && c.dataIndex === 'endDate'))
      .map((c) => ('dataIndex' in c && c.dataIndex === 'dateOfBirth'
        ? {
          ...c, width: 180,
          render: (v: string | null) => (
            <span className="dob-cell">
              {formatDate(v)}
              {bornInMonth(v, month) && <span className="dob-cell__tag">Sinh nhật tháng {month + 1}</span>}
            </span>
          ),
        }
        : c)),
  ];

  return (
    <>
      <section className="homeroom-hero">
        <div className="homeroom-hero__top">
          <div className="homeroom-hero__title">
            <h2><span>Lớp chủ nhiệm</span> <b>{cls.name}</b></h2>
            <p>
              <span>{cls.campusName}</span>
              <span>Năm học {cls.academicYearName}</span>
            </p>
          </div>
          <label className="homeroom-hero__date">
            <span>Xem tại ngày</span>
            <DatePicker
              format="DD/MM/YYYY" allowClear={false} value={date}
              disabledDate={(d) => d.isBefore(year.startDate, 'day') || d.isAfter(year.endDate, 'day')}
              onChange={(d) => d && setPicked(d)}
            />
          </label>
        </div>
        <div className="homeroom-hero__facts">
          <div className="homeroom-fact">
            <span className="homeroom-fact__label">Sĩ số</span>
            <b className="homeroom-fact__value tabular">{roster.data ? students.length : '—'}</b>
          </div>
          <div className="homeroom-fact">
            <span className="homeroom-fact__label">Nam · Nữ</span>
            <div className="homeroom-fact__gender">
              <div className="gender-split" aria-hidden>
                <i className="gender-split__male" style={{ flexGrow: male }} />
                <i className="gender-split__female" style={{ flexGrow: female }} />
              </div>
              <span className="tabular">{male} nam · {female} nữ</span>
            </div>
          </div>
          <div className="homeroom-fact">
            <span className="homeroom-fact__label">Sinh nhật tháng {month + 1}</span>
            {birthdays.length === 0
              ? <span className="homeroom-fact__empty">Không có</span>
              : (
                <ul className="birthday-list">
                  {birthdays.map((s) => (
                    <li key={s.enrollmentId}><b className="tabular">{dayjs(s.dateOfBirth).format('DD/MM')}</b> {s.fullName}</li>
                  ))}
                </ul>
              )}
          </div>
        </div>
      </section>
      <Card className="table-card">
        <DataTable<RosterItem>
          rowKey="enrollmentId" loading={roster.isLoading} dataSource={roster.data?.students} pagination={false}
          emptyText="Lớp chưa có học sinh nào có ghi danh hiệu lực tại ngày này."
          columns={columns}
        />
      </Card>
    </>
  );
}

/**
 * SCH-05 Danh sách học sinh (UC-SCH-09): giáo viên chủ nhiệm xem học sinh của lớp mình chủ nhiệm.
 * Chỉ xem; School Admin, Hiệu trưởng, Hiệu phó xem ở màn Lớp & Ghi danh.
 */
export function HomeroomStudentsPage() {
  const { user } = useAuth();
  const { current, isLoading: yearsLoading } = useCurrentYear();
  const [pickedId, setPickedId] = useState<number | null>(null);

  const classes = useQuery({
    queryKey: ['sch', 'classes', current?.academicYearId, undefined, false],
    queryFn: () => schApi.classes.list({ yearId: current!.academicYearId }),
    enabled: current !== undefined,
  });

  // API trả cả lớp chủ nhiệm lẫn lớp đang dạy của giáo viên; màn này chỉ dành cho lớp chủ nhiệm.
  const homeroomIds = user?.scope?.homeroomClassIds ?? [];
  const mine = (classes.data ?? []).filter((c) => homeroomIds.includes(c.classId));
  const selected = mine.find((c) => c.classId === pickedId) ?? mine[0];
  const loading = yearsLoading || classes.isLoading;

  return (
    <>
      <PageHeader
        title="Danh sách học sinh"
        extra={mine.length > 1 && (
          <div className="homeroom-switch" role="tablist" aria-label="Chọn lớp chủ nhiệm">
            {mine.map((c) => (
              <button
                key={c.classId} type="button" role="tab" aria-selected={c.classId === selected?.classId}
                className={c.classId === selected?.classId ? 'is-active' : undefined}
                onClick={() => setPickedId(c.classId)}
              >
                {c.name}
              </button>
            ))}
          </div>
        )}
      />
      {loading && <Card loading />}
      {!loading && (!current || !selected) && (
        <Card><p className="muted">Không có lớp chủ nhiệm nào ở năm học hiện hành.</p></Card>
      )}
      {!loading && current && selected && <ClassStudents key={selected.classId} cls={selected} year={current} />}
    </>
  );
}
