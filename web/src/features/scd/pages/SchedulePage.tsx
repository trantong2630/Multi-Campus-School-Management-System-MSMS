import { useQuery } from '@tanstack/react-query';
import { Button, Card, DatePicker, Segmented, Select } from 'antd';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { InlineAlert } from '@/shared/components/InlineAlert';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { Fragment, useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { PageHeader } from '@/shared/components/PageHeader';
import { schApi } from '@/features/sch/api';
import { formatTime, sessionOfDayLabel, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import { subjectColorStyle } from '../subjectColor';
import type { DaySchedule, MySession, SessionCell } from '../types';
import '../components/scd.css';

/** Lịch dạy là tiết thực tế theo ngày, có điều chỉnh tay — nguồn sự thật của thời khóa biểu hiện hành (BR-14). */
export function SchedulePage() {
  const { user } = useAuth();
  return user?.accountType === 'Teacher' ? <MySchedule /> : <CampusSchedule />;
}

type ViewMode = 'day' | 'class' | 'teacher';

/** Hiệu trưởng và hiệu phó: xem cả phân hiệu theo ngày, hoặc xem tuần của một lớp / một giáo viên. */
function CampusSchedule() {
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number | undefined>();
  const [mode, setMode] = useState<ViewMode>('day');
  const effectiveCampus = campusId ?? campuses.data?.[0]?.campusId;

  const modeSwitch = (
    <Segmented
      value={mode} onChange={(v) => setMode(v as ViewMode)}
      options={[{ value: 'day', label: 'Theo ngày' }, { value: 'class', label: 'Theo lớp' }, { value: 'teacher', label: 'Theo giáo viên' }]}
    />
  );
  const campusSelect = (
    <Select
      placeholder="Phân hiệu" value={effectiveCampus} onChange={setCampusId}
      options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
    />
  );
  const lead = <>{modeSwitch}{campusSelect}</>;

  return (
    <>
      <PageHeader
        title="Lịch dạy"
        description={mode === 'day'
          ? 'Lịch dạy thực tế của phân hiệu theo ngày. Tiết đã hủy gạch ngang, tiết chờ xử lý nền vàng.'
          : 'Lịch dạy theo tuần của một lớp hoặc một giáo viên. Tiết đã hủy gạch ngang, tiết chờ xử lý nền vàng.'}
      />
      {campuses.isSuccess && campuses.data.length === 0 && (
        <InlineAlert type="warning" title="Chưa có phân hiệu nào để xem" description="Hiệu phó cần được gán phụ trách phân hiệu ở Hồ sơ → Trách nhiệm." />
      )}
      {effectiveCampus !== undefined && (mode === 'day'
        ? <CampusDayView campusId={effectiveCampus} lead={lead} />
        : <EntityWeekView key={`${mode}-${effectiveCampus}`} mode={mode} campusId={effectiveCampus} lead={lead} />)}
    </>
  );
}

function CampusDayView({ campusId, lead }: { campusId: number; lead: React.ReactNode }) {
  const [date, setDate] = useState<Dayjs>(dayjs());
  const day = useQuery({
    queryKey: ['scd', 'day', campusId, toDateString(date)],
    queryFn: () => scdApi.day(campusId, toDateString(date)),
  });

  return (
    <>
      <FilterBar>
        {lead}
        <DatePicker format="DD/MM/YYYY" allowClear={false} value={date} onChange={(d) => d && setDate(d)} />
        <span className="muted">{weekdayOfDate(date)}</span>
      </FilterBar>
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
  const state = cell.status === 'Cancelled' ? 'tt-chip--cancelled' : cell.status === 'Pending' ? 'tt-chip--pending' : '';
  return (
    <td className="tt-cell" title={`${cell.subjectName} — ${cell.teacherName ?? 'Chờ phân công giáo viên'}`}>
      <div className={`tt-chip ${state}`} style={subjectColorStyle(cell.subjectId)}>
        <div className="tt-subject">{cell.subjectAbbr}</div>
        <div className="tt-teacher">{cell.teacherAbbr ?? 'Chờ GV'}</div>
      </div>
    </td>
  );
}

const STATUS_NOTE: Record<MySession['status'], string | null> = { Scheduled: null, Pending: 'Chờ xử lý', Cancelled: 'Đã hủy' };

type WeekSession = MySession & { teacherName?: string | null };

/** Lưới tuần dùng chung: hàng là Buổi × Tiết, cột là Thứ 2 → Thứ 7 của tuần đang chọn. `detail` quyết định dòng phụ của mỗi ô. */
function WeekGrid({ monday, sessions, loading, detail }: {
  monday: Dayjs; sessions: WeekSession[] | undefined; loading: boolean; detail: (s: WeekSession) => string;
}) {
  const days = Array.from({ length: 6 }, (_, i) => monday.add(i, 'day'));
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });
  // Khung đã ngừng dùng vẫn hiện nếu tuần này còn tiết nằm ở khung đó.
  const used = new Set(sessions?.map((s) => s.periodSlotId));
  const rows = (slots.data ?? [])
    .filter((p) => p.isActive || used.has(p.periodSlotId))
    .sort((a, b) => (a.sessionOfDay === b.sessionOfDay ? a.periodNo - b.periodNo : a.sessionOfDay === 'Morning' ? -1 : 1));
  const today = dayjs();

  if (slots.isLoading || loading) return <DataTable<object> rowKey="x" loading dataSource={[]} columns={[]} />;
  return (
    <div className="tt-scroll">
      <table className="tt-grid tt-week">
        <thead>
          <tr>
            <th className="tt-sticky tt-col-session" style={{ left: 0 }}>Buổi</th>
            <th className="tt-sticky" style={{ left: 64, minWidth: 110 }}>Tiết</th>
            {days.map((d) => (
              <th key={d.valueOf()} className={`tt-class${d.isSame(today, 'day') ? ' tt-today' : ''}`}>
                {weekdayOfDate(d)}
                <span className="tt-time">{d.format('DD/MM')}</span>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((slot, i) => {
            const newSession = i > 0 && rows[i - 1].sessionOfDay !== slot.sessionOfDay;
            const firstOfSession = i === 0 || newSession;
            return (
              <Fragment key={slot.periodSlotId}>
                {newSession && <tr className="tt-band"><td colSpan={2 + days.length} /></tr>}
                <tr>
                  {firstOfSession && (
                    <td className="tt-sticky tt-col-session" style={{ left: 0 }} rowSpan={rows.filter((r) => r.sessionOfDay === slot.sessionOfDay).length}>
                      {sessionOfDayLabel(slot.sessionOfDay)}
                    </td>
                  )}
                  <td className="tt-sticky tabular" style={{ left: 64, minWidth: 110 }}>
                    {slot.periodNo}
                    <span className="tt-time">{formatTime(slot.startTime)}–{formatTime(slot.endTime)}</span>
                  </td>
                  {days.map((d) => (
                    <WeekTd
                      key={d.valueOf()} detail={detail}
                      sessions={(sessions ?? []).filter((s) => s.periodSlotId === slot.periodSlotId && s.date === toDateString(d))}
                    />
                  ))}
                </tr>
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function WeekNav({ monday, onChange }: { monday: Dayjs; onChange: (m: Dayjs) => void }) {
  const end = monday.add(5, 'day');
  return (
    <>
      <Button icon={<ChevronLeft size={16} />} aria-label="Tuần trước" onClick={() => onChange(monday.subtract(1, 'week'))} />
      <DatePicker
        picker="week" allowClear={false} value={monday}
        format={() => `${monday.format('DD/MM')} – ${end.format('DD/MM/YYYY')}`}
        onChange={(d) => d && onChange(d.startOf('week'))}
      />
      <Button icon={<ChevronRight size={16} />} aria-label="Tuần sau" onClick={() => onChange(monday.add(1, 'week'))} />
      <Button disabled={monday.isSame(dayjs().startOf('week'), 'day')} onClick={() => onChange(dayjs().startOf('week'))}>Tuần này</Button>
    </>
  );
}

/** Lịch dạy của giáo viên đang đăng nhập. */
function MySchedule() {
  const [monday, setMonday] = useState<Dayjs>(() => dayjs().startOf('week'));
  const from = toDateString(monday);
  const to = toDateString(monday.add(5, 'day'));
  const sessions = useQuery({ queryKey: ['scd', 'mine', from, to], queryFn: () => scdApi.mine(from, to) });

  return (
    <>
      <PageHeader title="Lịch dạy của tôi" description="Gồm tiết bù và tiết được phân dạy thay. Tiết đã hủy gạch ngang, tiết chờ xử lý nền vàng." />
      <FilterBar><WeekNav monday={monday} onChange={setMonday} /></FilterBar>
      {sessions.isSuccess && sessions.data.length === 0 && (
        <InlineAlert type="info" title="Tuần này không có tiết dạy nào" description="Tuần nghỉ, tuần chưa phát hành thời khóa biểu hoặc bạn chưa được phân công dạy." />
      )}
      <Card className="table-card">
        <WeekGrid monday={monday} sessions={sessions.data} loading={sessions.isLoading} detail={(s) => s.className} />
      </Card>
    </>
  );
}

/** Lịch tuần của một lớp hoặc một giáo viên trong phân hiệu đang chọn. */
function EntityWeekView({ mode, campusId, lead }: { mode: 'class' | 'teacher'; campusId: number; lead: React.ReactNode }) {
  const isClass = mode === 'class';
  const [monday, setMonday] = useState<Dayjs>(() => dayjs().startOf('week'));
  const [entityId, setEntityId] = useState<number | undefined>();
  const from = toDateString(monday);
  const to = toDateString(monday.add(5, 'day'));

  const options = useQuery({
    queryKey: ['scd', isClass ? 'classes' : 'teachers', campusId],
    queryFn: async () => isClass
      ? (await scdApi.classes(campusId)).map((c) => ({ value: c.classId, label: c.name }))
      : (await scdApi.teachers(campusId)).map((t) => ({ value: t.teacherId, label: t.abbr ? `${t.name} (${t.abbr})` : t.name })),
  });
  const sessions = useQuery({
    queryKey: ['scd', 'by', mode, entityId, from, to],
    queryFn: () => (isClass ? scdApi.byClass(entityId!, from, to) : scdApi.byTeacher(entityId!, from, to)),
    enabled: entityId !== undefined,
  });

  return (
    <>
      <FilterBar>
        {lead}
        <Select
          showSearch optionFilterProp="label" style={{ minWidth: 220 }} value={entityId} onChange={setEntityId}
          placeholder={isClass ? 'Chọn lớp' : 'Chọn giáo viên'} loading={options.isLoading} options={options.data}
        />
        <WeekNav monday={monday} onChange={setMonday} />
      </FilterBar>
      {entityId === undefined ? (
        <InlineAlert type="info" title={isClass ? 'Chọn một lớp để xem lịch' : 'Chọn một giáo viên để xem lịch'} description="Lịch hiện theo tuần, Thứ 2 đến Thứ 7." />
      ) : (
        <>
          {sessions.isSuccess && sessions.data.length === 0 && (
            <InlineAlert type="info" title="Tuần này không có tiết nào" description="Tuần nghỉ hoặc tuần chưa phát hành thời khóa biểu." />
          )}
          <Card className="table-card">
            <WeekGrid
              monday={monday} sessions={sessions.data} loading={sessions.isLoading}
              detail={(s) => (isClass ? s.teacherName ?? 'Chờ GV' : `${s.className} · ${s.campusName}`)}
            />
          </Card>
        </>
      )}
    </>
  );
}

function WeekTd({ sessions, detail }: { sessions: WeekSession[]; detail: (s: WeekSession) => string }) {
  if (sessions.length === 0) return <td className="tt-cell tt-cell--empty" />;
  // Một khung có thể có tiết Đã hủy kèm tiết bù hoặc dạy thay: tiết còn hiệu lực đứng trước.
  const ordered = [...sessions].sort((a, b) => (a.status === 'Cancelled' ? 1 : 0) - (b.status === 'Cancelled' ? 1 : 0));
  return (
    <td className="tt-cell">
      <div className="tt-stack">
        {ordered.map((s) => {
          const state = s.status === 'Cancelled' ? 'tt-chip--cancelled' : s.status === 'Pending' ? 'tt-chip--pending' : '';
          const note = STATUS_NOTE[s.status];
          return (
            <div key={s.sessionId} className={`tt-chip ${state}`} style={subjectColorStyle(s.subjectId)} title={`${s.subjectName} — ${s.className} — ${s.campusName}`}>
              <div className="tt-subject">{s.subjectName}</div>
              <div className="tt-teacher">{detail(s)}{note && ` · ${note}`}</div>
            </div>
          );
        })}
      </div>
    </td>
  );
}
