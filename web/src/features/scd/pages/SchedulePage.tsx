import { useQuery } from '@tanstack/react-query';
import { Card, DatePicker, Select } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { formatDate, formatTime, sessionOfDayLabel, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import type { DaySchedule, MySession, SessionCell } from '../types';
import '../components/scd.css';

/** Lịch dạy là tiết thực tế theo ngày, có điều chỉnh tay — nguồn sự thật của thời khóa biểu hiện hành (BR-14). */
export function SchedulePage() {
  const { user } = useAuth();
  return user?.accountType === 'Teacher' ? <MySchedule /> : <CampusDaySchedule />;
}

function CampusDaySchedule() {
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number | undefined>();
  const [date, setDate] = useState<Dayjs>(dayjs());
  const effectiveCampus = campusId ?? campuses.data?.[0]?.campusId;
  const day = useQuery({
    queryKey: ['scd', 'day', effectiveCampus, toDateString(date)],
    queryFn: () => scdApi.day(effectiveCampus!, toDateString(date)),
    enabled: effectiveCampus !== undefined,
  });

  return (
    <>
      <PageHeader title="Lịch dạy" description="Lịch dạy thực tế của phân hiệu theo ngày. Tiết đã hủy gạch ngang, tiết chờ xử lý nền vàng." />
      <FilterBar>
        <Select
          placeholder="Phân hiệu" value={effectiveCampus} onChange={setCampusId}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
        <DatePicker format="DD/MM/YYYY" allowClear={false} value={date} onChange={(d) => d && setDate(d)} />
        <span className="muted">{weekdayOfDate(date)}</span>
      </FilterBar>
      {campuses.isSuccess && campuses.data.length === 0 && (
        <InlineAlert type="warning" title="Chưa có phân hiệu nào để xem" description="Hiệu phó cần được gán phụ trách phân hiệu ở Hồ sơ → Trách nhiệm." />
      )}
      {day.data?.isHoliday && (
        <InlineAlert type="info" title={`Ngày nghỉ: ${day.data.holidayReason}`} description="Ngày nghỉ không có tiết dạy." />
      )}
      <Card className="table-card">
        {day.isLoading ? <DataTable<object> rowKey="x" loading dataSource={[]} columns={[]} /> : day.data && <DayGrid day={day.data} />}
      </Card>
    </>
  );
}

function DayGrid({ day }: { day: DaySchedule }) {
  if (day.classes.length === 0) {
    return <DataTable<object> rowKey="x" dataSource={[]} columns={[]} emptyText="Phân hiệu chưa có lớp nào trong năm học của ngày này." />;
  }
  const empty = day.rows.every((r) => r.cells.every((c) => c === null));
  return (
    <>
      {empty && !day.isHoliday && (
        <InlineAlert type="info" title="Ngày này chưa có tiết nào" description="Chủ nhật, ngày chưa phát hành thời khóa biểu hoặc ngày đã qua trước lần phát hành." />
      )}
      <div className="tt-scroll">
        <table className="tt-grid">
          <thead>
            <tr>
              <th className="tt-sticky tt-col-session" style={{ left: 0 }}>Buổi</th>
              <th className="tt-sticky" style={{ left: 64, minWidth: 110 }}>Tiết</th>
              {day.classes.map((c) => <th key={c.classId} className="tt-class">{c.name}</th>)}
            </tr>
          </thead>
          <tbody>
            {day.rows.map((row) => (
              <tr key={row.periodSlotId}>
                <td className="tt-sticky tt-col-session" style={{ left: 0 }}>{sessionOfDayLabel(row.sessionOfDay)}</td>
                <td className="tt-sticky tabular" style={{ left: 64, minWidth: 110 }}>
                  {row.periodNo}
                  <span className="tt-time">{formatTime(row.startTime)}–{formatTime(row.endTime)}</span>
                </td>
                {row.cells.map((cell, i) => <SessionTd key={day.classes[i].classId} cell={cell} />)}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function SessionTd({ cell }: { cell: SessionCell | null }) {
  if (!cell) return <td className="tt-cell tt-cell--empty" />;
  const cls = cell.status === 'Cancelled' ? 'tt-cell--cancelled' : cell.status === 'Pending' ? 'tt-cell--pending' : 'tt-cell--filled';
  return (
    <td className={`tt-cell ${cls}`} title={`${cell.subjectName} — ${cell.teacherName}`}>
      <div className="tt-subject">{cell.subjectAbbr}</div>
      <div className="tt-teacher">{cell.teacherAbbr}</div>
    </td>
  );
}

function MySchedule() {
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().startOf('week').add(1, 'day'), dayjs().startOf('week').add(7, 'day')]);
  const sessions = useQuery({
    queryKey: ['scd', 'mine', toDateString(range[0]), toDateString(range[1])],
    queryFn: () => scdApi.mine(toDateString(range[0]), toDateString(range[1])),
  });

  return (
    <>
      <PageHeader title="Lịch dạy của tôi" description="Gồm tiết bù và tiết được phân dạy thay; tiết chờ xử lý và đã hủy hiện rõ trạng thái." />
      <FilterBar>
        <DatePicker.RangePicker
          format="DD/MM/YYYY" allowClear={false} value={range}
          onChange={(v) => v?.[0] && v[1] && setRange([v[0], v[1]])}
        />
      </FilterBar>
      <Card className="table-card">
        <DataTable<MySession>
          rowKey="sessionId" loading={sessions.isLoading} dataSource={sessions.data}
          emptyText="Không có tiết dạy nào trong khoảng ngày này."
          columns={[
            { title: 'Ngày', dataIndex: 'date', width: 120, className: 'tabular', render: (v: string) => formatDate(v) },
            { title: 'Thứ', dataIndex: 'date', key: 'weekday', width: 80, render: (v: string) => weekdayOfDate(v) },
            { title: 'Tiết', key: 'slot', width: 190, className: 'tabular', render: (_: unknown, s: MySession) => `${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo} (${formatTime(s.startTime)}–${formatTime(s.endTime)})` },
            { title: 'Lớp', dataIndex: 'className', width: 100 },
            { title: 'Môn', dataIndex: 'subjectName' },
            { title: 'Phân hiệu', dataIndex: 'campusName', width: 170 },
            { title: 'Trạng thái', dataIndex: 'status', width: 150, align: 'center', render: (v: string) => <StatusBadge domain="session" value={v} /> },
          ]}
        />
      </Card>
    </>
  );
}
