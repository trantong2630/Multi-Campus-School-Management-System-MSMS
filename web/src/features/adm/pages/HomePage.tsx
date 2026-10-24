import { useQuery } from '@tanstack/react-query';
import { Card, Skeleton } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { Globe, Mail, MapPin, Phone } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { schApi } from '@/features/sch/api';
import { SchoolLogo } from '@/features/sch/components/SchoolLogo';
import { useSchool } from '@/features/sch/hooks/useSchool';
import type { AcademicYear, Holiday, PeriodSlot } from '@/features/sch/types';
import { scdApi } from '@/features/scd/api';
import type { MySession } from '@/features/scd/types';
import { HeroBanner } from '@/shared/components/HeroBanner';
import { formatTime, sessionOfDayLabel } from '@/shared/lib/format';
import './home.css';

/** Trang chủ chung cho mọi loại tài khoản: lời chào, hôm nay của trường và năm học. Không có số liệu thống kê (số liệu ở Dashboard). */
const STAFF = ['SchoolAdmin', 'Principal', 'VicePrincipal', 'Teacher'];

function greetingOf(now: Dayjs) {
  const h = now.hour();
  if (h < 11) return 'Chào buổi sáng';
  if (h < 13) return 'Chào buổi trưa';
  if (h < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

/** Đồng hồ phút: đủ để vạch "đang diễn ra" tự chuyển tiết mà không làm render dày. */
function useNow() {
  const [now, setNow] = useState(() => dayjs());
  useEffect(() => {
    const id = window.setInterval(() => setNow(dayjs()), 30_000);
    return () => window.clearInterval(id);
  }, []);
  return now;
}

export function HomePage() {
  const { user } = useAuth();
  const school = useSchool();
  const now = useNow();
  const isStaff = !!user && STAFF.includes(user.accountType);
  const today = now.format('YYYY-MM-DD');

  const years = useQuery({ queryKey: ['sch', 'years'], queryFn: schApi.years.list, enabled: isStaff });
  const year = years.data?.find((y) => y.isCurrent) ?? years.data?.[0];
  const holidays = useQuery({
    queryKey: ['sch', 'holidays', year?.academicYearId, undefined],
    queryFn: () => schApi.holidays.list({ yearId: year!.academicYearId }),
    enabled: !!year,
  });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list, enabled: isStaff });
  const mine = useQuery({ queryKey: ['scd', 'mine', today, today], queryFn: () => scdApi.mine(today, today), enabled: isStaff });

  if (!user) return null;
  const given = user.fullName.trim().split(/\s+/).at(-1) ?? user.fullName;
  const context = [school.hasName ? school.name : null, year && `Năm học ${year.name}`].filter(Boolean).join(' · ');

  // Bento 3 cột: lời chào (ô nhấn xanh) + năm học ở hàng trên; hôm nay + ngày nghỉ ở hàng dưới.
  return (
    <div className="home-bento">
      <section className={`bento-tile bento-tile--accent home-bento__greet${isStaff ? '' : ' home-bento__greet--wide'}`}>
        <HeroBanner
          eyebrow={context || undefined}
          title={`${greetingOf(now)}, ${given}`}
          description={now.format('dddd, DD/MM/YYYY')}
          extra={<span className="home-crest" aria-hidden><SchoolLogo size={104} /></span>}
        />
      </section>

      {isStaff ? (
        <>
          <YearCard now={now} year={year} loading={years.isLoading} />
          <TodayCard
            now={now} loading={slots.isLoading}
            slots={slots.data ?? []} sessions={mine.data ?? []} holidays={holidays.data ?? []}
            showMine={user.accountType !== 'SchoolAdmin'}
          />
          <HolidaysCard now={now} holidays={holidays.data ?? []} loading={years.isLoading || holidays.isLoading} />
        </>
      ) : (
        <SchoolCard />
      )}
    </div>
  );
}

type SlotState = 'done' | 'current' | 'next' | 'later';

function TodayCard({ now, slots, sessions, holidays, loading, showMine }: {
  now: Dayjs; slots: PeriodSlot[]; sessions: MySession[]; holidays: Holiday[]; loading: boolean; showMine: boolean;
}) {
  const today = now.format('YYYY-MM-DD');
  const clock = now.format('HH:mm:ss');
  const active = useMemo(
    () => slots.filter((s) => s.isActive).sort((a, b) => a.startTime.localeCompare(b.startTime)),
    [slots],
  );
  const offToday = holidays.find((h) => h.holidayDate.slice(0, 10) === today && h.campusId === null);
  const isSunday = now.day() === 0;

  const nextIndex = active.findIndex((s) => s.startTime > clock);
  const stateOf = (s: PeriodSlot, i: number): SlotState => {
    if (s.endTime <= clock) return 'done';
    if (s.startTime <= clock) return 'current';
    if (i === nextIndex && !active.some((x) => x.startTime <= clock && x.endTime > clock)) return 'next';
    return 'later';
  };
  const mineAt = (s: PeriodSlot) =>
    sessions.find((m) => m.sessionOfDay === s.sessionOfDay && m.periodNo === s.periodNo && m.status !== 'Cancelled');

  return (
    <Card className="home-card home-bento__today" title="Hôm nay" extra={<span className="home-clock tabular">{now.format('HH:mm')}</span>}>
      {loading ? (
        <Skeleton active title={false} paragraph={{ rows: 6 }} />
      ) : isSunday || offToday ? (
        <div className="home-rest">
          <b>Hôm nay trường nghỉ</b>
          <span>{offToday?.reason ?? 'Chủ nhật'}</span>
        </div>
      ) : active.length === 0 ? (
        <div className="home-rest"><span>Trường chưa khai khung giờ tiết.</span></div>
      ) : (
        <ol className="home-periods">
          {active.map((s, i) => {
            const state = stateOf(s, i);
            const m = showMine ? mineAt(s) : undefined;
            const firstOfSession = i === 0 || active[i - 1].sessionOfDay !== s.sessionOfDay;
            return (
              <li key={s.periodSlotId} className={`home-period home-period--${state}`}>
                {firstOfSession && <span className="home-period__session">{sessionOfDayLabel(s.sessionOfDay)}</span>}
                <span className="home-period__time tabular">{formatTime(s.startTime)}<i>{formatTime(s.endTime)}</i></span>
                <span className="home-period__node" aria-hidden />
                <span className="home-period__what">
                  <span className="home-period__no">Tiết {s.periodNo}</span>
                  {m && <span className="home-period__lesson"><b>{m.className}</b> · {m.subjectName}</span>}
                </span>
                {state === 'current' && <span className="home-period__tag">Đang diễn ra</span>}
                {state === 'next' && <span className="home-period__tag home-period__tag--next">Tiếp theo</span>}
              </li>
            );
          })}
        </ol>
      )}
    </Card>
  );
}

function YearCard({ now, year, loading }: { now: Dayjs; year?: AcademicYear; loading: boolean }) {
  const today = dayjs(now.format('YYYY-MM-DD'));
  const semester = year?.semesters.find((s) => !today.isBefore(dayjs(s.startDate)) && !today.isAfter(dayjs(s.endDate)));
  const nextSemester = year?.semesters.find((s) => today.isBefore(dayjs(s.startDate)));

  let progress: { week: number; total: number } | null = null;
  if (semester) {
    const start = dayjs(semester.startDate);
    const days = dayjs(semester.endDate).diff(start, 'day') + 1;
    const total = Math.ceil(days / 7);
    progress = { week: Math.min(Math.floor(today.diff(start, 'day') / 7) + 1, total), total };
  }

  return (
    <Card className="home-card home-bento__year" title="Năm học" extra={year && <span className="home-year tabular">{year.name}</span>}>
      {loading ? (
        <Skeleton active title={false} paragraph={{ rows: 4 }} />
      ) : !year ? (
        <div className="home-rest"><span>Chưa có năm học nào.</span></div>
      ) : (
        <div className="home-term">
          <TermDonut today={today} semester={semester ?? nextSemester} progress={progress} />
          <div className="home-term__text">
            <b>{semester?.name ?? (nextSemester ? `Sắp tới: ${nextSemester.name}` : 'Ngoài học kỳ')}</b>
            <span className="home-term__sub tabular">
              {semester
                ? `${dayjs(semester.startDate).format('DD/MM')} → ${dayjs(semester.endDate).format('DD/MM/YYYY')}`
                : nextSemester ? `Bắt đầu ${dayjs(nextSemester.startDate).format('DD/MM/YYYY')}` : `Năm học ${year.name}`}
            </span>
          </div>
        </div>
      )}
    </Card>
  );
}

const DONUT = { size: 160, r: 68, stroke: 12 };

/** Tọa độ trên vòng, 0° ở đỉnh, chiều kim đồng hồ. */
function polar(deg: number) {
  const rad = ((deg - 90) * Math.PI) / 180;
  const c = DONUT.size / 2;
  return { x: c + DONUT.r * Math.cos(rad), y: c + DONUT.r * Math.sin(rad) };
}

/**
 * Vòng tiến độ của học kỳ đang diễn ra (hoặc sắp tới): cả vòng là học kỳ, phần đã qua tô đặc, phần còn lại mờ.
 */
function TermDonut({ today, semester, progress }: {
  today: Dayjs; semester?: AcademicYear['semesters'][number]; progress: { week: number; total: number } | null;
}) {
  let elapsed = 0;
  if (semester) {
    const start = dayjs(semester.startDate);
    const span = dayjs(semester.endDate).diff(start, 'day') + 1;
    elapsed = Math.min(Math.max((today.diff(start, 'day') + 1) / span, 0), 1);
  }
  const end = polar(elapsed * 359.99);

  return (
    <div className="home-donut">
      <svg viewBox={`0 0 ${DONUT.size} ${DONUT.size}`} width={DONUT.size} height={DONUT.size} role="img"
           aria-label={progress ? `Tuần ${progress.week} trên ${progress.total}` : 'Ngoài học kỳ'}>
        <circle cx={DONUT.size / 2} cy={DONUT.size / 2} r={DONUT.r} fill="none" strokeWidth={DONUT.stroke}
                stroke="var(--c-green600)" strokeOpacity={0.14} />
        {elapsed > 0 && (
          <path d={`M ${DONUT.size / 2} ${DONUT.size / 2 - DONUT.r} A ${DONUT.r} ${DONUT.r} 0 ${elapsed > 0.5 ? 1 : 0} 1 ${end.x} ${end.y}`}
                fill="none" stroke="var(--c-green600)" strokeWidth={DONUT.stroke} strokeLinecap="round" />
        )}
      </svg>
      <span className="home-ring">
        {progress
          ? <><small>Tuần</small><b className="tabular">{progress.week}</b><small className="tabular">/ {progress.total}</small></>
          : <small>—</small>}
      </span>
    </div>
  );
}

function HolidaysCard({ now, holidays, loading }: { now: Dayjs; holidays: Holiday[]; loading: boolean }) {
  const todayKey = now.format('YYYY-MM-DD');

  // Ngày nghỉ sắp tới: gộp các dòng cùng ngày cùng lý do (nghỉ cấp trường lẫn phân hiệu), lấy bốn ngày gần nhất.
  const upcoming = useMemo(() => {
    const today = dayjs(todayKey);
    const seen = new Set<string>();
    return holidays
      .filter((h) => !dayjs(h.holidayDate).isBefore(today))
      .sort((a, b) => a.holidayDate.localeCompare(b.holidayDate))
      .filter((h) => {
        const k = `${h.holidayDate.slice(0, 10)}|${h.reason ?? ''}`;
        if (seen.has(k)) return false;
        seen.add(k);
        return true;
      })
      .slice(0, 4);
  }, [holidays, todayKey]);

  return (
    <Card className="home-card home-bento__holidays" title="Ngày nghỉ sắp tới">
      {loading ? (
        <Skeleton active title={false} paragraph={{ rows: 4 }} />
      ) : upcoming.length === 0 ? (
        <div className="home-rest"><span>Không còn ngày nghỉ nào trong năm học.</span></div>
      ) : (
        <ul className="home-holidays">
          {upcoming.map((h) => {
            const d = dayjs(h.holidayDate);
            const left = d.diff(dayjs(todayKey), 'day');
            return (
              <li key={h.holidayId}>
                <span className="home-date tabular"><b>{d.format('DD')}</b>{d.format('[Th]M')}</span>
                <span className="home-holiday__text">
                  <span>{h.reason ?? 'Ngày nghỉ'}</span>
                  {h.campusName && <span className="home-holiday__campus">{h.campusName}</span>}
                </span>
                <span className="home-holiday__left tabular">{left === 0 ? 'Hôm nay' : `còn ${left} ngày`}</span>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
}

/** Học sinh và phụ huynh: chưa đọc được lịch năm học, nên chỉ hiện thông tin liên hệ của trường (API công khai). */
function SchoolCard() {
  const { profile, name } = useSchool();
  const rows = [
    { icon: MapPin, value: profile?.address },
    { icon: Phone, value: profile?.phoneNumber },
    { icon: Mail, value: profile?.email },
    { icon: Globe, value: profile?.website },
  ].filter((r) => r.value);
  return (
    <Card className="home-card home-card--school home-bento__school" title={name}>
      {rows.length === 0 ? (
        <span className="home-term__sub">Trường chưa cập nhật thông tin liên hệ.</span>
      ) : (
        <ul className="home-contact">
          {rows.map(({ icon: Icon, value }) => <li key={value}><Icon size={16} /> {value}</li>)}
        </ul>
      )}
    </Card>
  );
}
