import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import type { CSSProperties } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { schApi } from '@/features/sch/api';
import { SchoolLogo } from '@/features/sch/components/SchoolLogo';
import { nextSemester as nextSemesterOf, semesterAt, semesterProgress } from '@/features/sch/semester';
import type { AcademicYear, Holiday, PeriodSlot } from '@/features/sch/types';
import { scdApi } from '@/features/scd/api';
import type { MySession } from '@/features/scd/types';
import { formatTime, sessionOfDayLabel } from '@/shared/lib/format';
import { Count } from '../components/BoardKit';
import './home.css';

/** Trang chủ chung cho mọi loại tài khoản: lời chào, hôm nay của trường và năm học. Không có số liệu thống kê (số liệu ở Dashboard). */

function greetingOf(now: Dayjs) {
  const h = now.hour();
  if (h < 11) return 'Chào buổi sáng';
  if (h < 13) return 'Chào buổi trưa';
  if (h < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

/** Bầu trời của khối đầu trang đổi theo giờ: sáng sớm, ban ngày, hoàng hôn, ban đêm. */
function skyOf(now: Dayjs) {
  const h = now.hour() + now.minute() / 60;
  if (h < 5 || h >= 19) return 'night';
  if (h < 9) return 'dawn';
  if (h < 16.5) return 'day';
  return 'dusk';
}

/** Đồng hồ phút: đủ để mặt trời và vạch "bây giờ" tự dịch chuyển mà không làm render dày. */
function useNow() {
  const [now, setNow] = useState(() => dayjs());
  useEffect(() => {
    const id = window.setInterval(() => setNow(dayjs()), 30_000);
    return () => window.clearInterval(id);
  }, []);
  return now;
}

/** "HH:mm[:ss]" → số phút trong ngày. */
const minutesOf = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));
const stagger = (i: number): CSSProperties => ({ ['--i' as string]: i });

type SlotState = 'done' | 'current' | 'next' | 'later';

/** Các tiết đang dùng hôm nay, trạng thái từng tiết và tiết của chính người dùng. */
function useDay(now: Dayjs, slots: PeriodSlot[], sessions: MySession[], holidays: Holiday[], showMine: boolean) {
  const today = now.format('YYYY-MM-DD');
  const clock = now.format('HH:mm:ss');
  const active = useMemo(
    () => slots.filter((s) => s.isActive).sort((a, b) => a.startTime.localeCompare(b.startTime)),
    [slots],
  );
  const offToday = holidays.find((h) => h.holidayDate.slice(0, 10) === today && h.campusId === null);
  const off = now.day() === 0 || !!offToday;
  const nextIndex = active.findIndex((s) => s.startTime > clock);
  const currentIndex = active.findIndex((s) => s.startTime <= clock && s.endTime > clock);
  const stateOf = (i: number): SlotState => {
    const s = active[i];
    if (s.endTime <= clock) return 'done';
    if (s.startTime <= clock) return 'current';
    if (i === nextIndex && currentIndex < 0) return 'next';
    return 'later';
  };
  const mineAt = (s: PeriodSlot) => showMine
    ? sessions.find((m) => m.sessionOfDay === s.sessionOfDay && m.periodNo === s.periodNo && m.status !== 'Cancelled')
    : undefined;
  const focusIndex = currentIndex >= 0 ? currentIndex : nextIndex;
  return { active, off, offReason: offToday?.reason ?? 'Chủ nhật', stateOf, mineAt, currentIndex, focusIndex };
}
type Day = ReturnType<typeof useDay>;

export function HomePage() {
  const { user } = useAuth();
  const now = useNow();
  const today = now.format('YYYY-MM-DD');

  const years = useQuery({ queryKey: ['sch', 'years'], queryFn: schApi.years.list });
  const year = years.data?.find((y) => y.isCurrent) ?? years.data?.[0];
  const holidays = useQuery({
    queryKey: ['sch', 'holidays', year?.academicYearId, undefined],
    queryFn: () => schApi.holidays.list({ yearId: year!.academicYearId }),
    enabled: !!year,
  });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });
  const mine = useQuery({ queryKey: ['scd', 'mine', today, today], queryFn: () => scdApi.mine(today, today), enabled: user?.accountType === 'Teacher' });
  const day = useDay(now, slots.data ?? [], mine.data ?? [], holidays.data ?? [], user?.accountType !== 'SchoolAdmin');

  if (!user) return null;
  const given = user.fullName.trim().split(/\s+/).at(-1) ?? user.fullName;

  return (
    <div className="hm">
      <section className={`hm-hero hm-hero--${skyOf(now)}`} aria-label="Lời chào">
        <i className="hm-cloud hm-cloud--a" aria-hidden />
        <i className="hm-cloud hm-cloud--b" aria-hidden />
        <div className="hm-hero__text">
          <span className="hm-crest" style={stagger(0)} aria-hidden><SchoolLogo size={136} /></span>
          <div className="hm-hero__words">
            <h1 className="hm-hero__title" style={stagger(1)}>{greetingOf(now)},<br />{given}</h1>
          </div>
        </div>
        <DayDial now={now} day={day} loading={slots.isLoading} />
      </section>

      <div className="hm-grid">
        <TodayCard now={now} day={day} loading={slots.isLoading} />
        <YearCard now={now} year={year} loading={years.isLoading} />
        <HolidaysCard now={now} holidays={holidays.data ?? []} loading={years.isLoading || holidays.isLoading} />
      </div>
    </div>
  );
}

const DIAL = { size: 300, c: 150, arc: 116, num: 90 };
/** Điểm trên mặt đồng hồ: t = 0 ở vị trí 12 giờ, đi theo chiều kim đồng hồ, t = 1 là một vòng. */
function dialPoint(t: number, r: number) {
  const a = t * 2 * Math.PI;
  return { x: DIAL.c + r * Math.sin(a), y: DIAL.c - r * Math.cos(a) };
}
function dialPath(t1: number, t2: number, r: number) {
  const a = dialPoint(t1, r);
  const b = dialPoint(t2, r);
  return `M ${a.x.toFixed(2)} ${a.y.toFixed(2)} A ${r} ${r} 0 ${t2 - t1 > 0.5 ? 1 : 0} 1 ${b.x.toFixed(2)} ${b.y.toFixed(2)}`;
}

/**
 * Đồng hồ kim thường (12 giờ): các tiết nằm trên mặt đồng hồ đúng vị trí giờ của chúng, nên buổi sáng nằm ở
 * khoảng 7–11 giờ và buổi chiều ở khoảng 1–6 giờ, chỗ giữa là khoảng nghỉ trống. Tiết nào chưa tới thì mờ,
 * đến giờ tiết đó mới đậm lên. Bên cạnh là giờ số và một câu nói đang học tiết nào.
 */
function DayDial({ now, day, loading }: { now: Dayjs; day: Day; loading: boolean }) {
  const { active, off, offReason, stateOf, mineAt, currentIndex, focusIndex } = day;
  if (loading) return <div className="hm-arc"><Skeleton active title={false} paragraph={{ rows: 4 }} /></div>;
  if (off || active.length === 0) {
    return (
      <div className="hm-arc hm-arc--rest">
        <span className="hm-clock tabular">{now.format('HH:mm')}</span>
        <b>{off ? `Hôm nay trường nghỉ · ${offReason}` : 'Trường chưa khai khung giờ tiết'}</b>
      </div>
    );
  }

  const focus = focusIndex >= 0 ? active[focusIndex] : undefined;
  const lesson = focus ? mineAt(focus) : undefined;
  const live = currentIndex >= 0;
  const line = !focus ? 'Hôm nay đã hết tiết' : live ? `Đang học tiết ${focus.periodNo}` : `Tiết ${focus.periodNo} lúc ${formatTime(focus.startTime)}`;
  const hourDeg = ((now.hour() % 12) + now.minute() / 60) * 30;
  const minuteDeg = now.minute() * 6;

  return (
    <div className="hm-arc">
      <svg className="hm-dial" viewBox={`0 0 ${DIAL.size} ${DIAL.size}`} role="img" aria-label={`Đồng hồ ${now.format('HH:mm')}`}>
        <circle cx={DIAL.c} cy={DIAL.c} r={138} className="hm-dial__face" />
        {Array.from({ length: 12 }, (_, i) => {
          const n = i === 0 ? 12 : i;
          const p = dialPoint(i / 12, DIAL.num);
          return <text key={n} x={p.x} y={p.y + 5} textAnchor="middle" className="hm-dial__num">{n}</text>;
        })}
        {active.map((sl, i) => {
          const t1 = (minutesOf(sl.startTime) % 720) / 720;
          let t2 = (minutesOf(sl.endTime) % 720) / 720;
          if (t2 <= t1) t2 += 1;
          return (
            <path
              key={sl.periodSlotId}
              d={dialPath(t1, t2, DIAL.arc)}
              className={`hm-dial__seg hm-dial__seg--${stateOf(i) === 'current' ? 'current' : 'idle'}`}
              style={stagger(i)}
            >
              <title>{`${sessionOfDayLabel(sl.sessionOfDay)} · Tiết ${sl.periodNo} · ${formatTime(sl.startTime)}–${formatTime(sl.endTime)}`}</title>
            </path>
          );
        })}
        <g className="hm-dial__hand" style={{ ['--rot' as string]: `${hourDeg}deg` }}>
          <line x1={DIAL.c} y1={DIAL.c + 8} x2={DIAL.c} y2={DIAL.c - 52} className="hm-dial__hour" />
        </g>
        <g className="hm-dial__hand" style={{ ['--rot' as string]: `${minuteDeg}deg` }}>
          <line x1={DIAL.c} y1={DIAL.c + 12} x2={DIAL.c} y2={DIAL.c - 76} className="hm-dial__minute" />
        </g>
        <circle cx={DIAL.c} cy={DIAL.c} r={6} className="hm-dial__pin" />
      </svg>
      <div className="hm-arc__centre">
        <span className="hm-clock tabular" aria-label={`Bây giờ là ${now.format('HH:mm')}`}>{now.format('HH:mm')}</span>
        <b className={live ? 'is-live' : ''}>{line}</b>
        {lesson && <span>{lesson.className} · {lesson.subjectName}</span>}
      </div>
    </div>
  );
}

/** Hôm nay: trục thời gian dọc của các tiết trong ngày, giáo viên thấy thêm lớp · môn của chính mình ở từng tiết. */
/** Hôm nay: đầu thẻ là tờ lịch ngày hôm nay và số tiết, bên dưới là trục thời gian dọc; giáo viên thấy thêm lớp · môn của chính mình ở từng tiết. */
function TodayCard({ now, day, loading }: { now: Dayjs; day: Day; loading: boolean }) {
  const { active, off, offReason, stateOf, mineAt } = day;
  return (
    <section className="hm-card hm-today" style={stagger(3)} aria-label="Hôm nay">
      <header className="hm-campus">
        <span className="hm-campus__mark">
          <b>{now.format('dddd').replace(/(^|\s)\S/g, (c) => c.toUpperCase())}</b>
          <i className="tabular">{now.format('DD/MM')}</i>
        </span>
      </header>
      {loading ? (
        <Skeleton active title={false} paragraph={{ rows: 6 }} />
      ) : off ? (
        <div className="hm-rest"><b>Hôm nay trường nghỉ</b><span>{offReason}</span></div>
      ) : active.length === 0 ? (
        <div className="hm-rest"><span>Trường chưa khai khung giờ tiết.</span></div>
      ) : (
        <ol className="hm-periods">
          {active.map((sl, i) => {
            const state = stateOf(i);
            const m = mineAt(sl);
            const first = i === 0 || active[i - 1].sessionOfDay !== sl.sessionOfDay;
            return (
              <li key={sl.periodSlotId} className={`hm-period hm-period--${state}`} style={stagger(i)}>
                {first && <span className="hm-period__session">{sessionOfDayLabel(sl.sessionOfDay)}</span>}
                <span className="hm-period__time tabular">{formatTime(sl.startTime)}<i>{formatTime(sl.endTime)}</i></span>
                <span className="hm-period__node" aria-hidden />
                <span className="hm-period__what">
                  <b>Tiết {sl.periodNo}</b>
                  {m && <span><strong>{m.className}</strong> · {m.subjectName}</span>}
                </span>
                {state === 'current' && <em className="hm-period__tag">Đang diễn ra</em>}
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

/** Năm học: học kỳ là một dãy ô tuần, tuần đã qua tô xanh, tuần này màu cam và cao hơn. */
function YearCard({ now, year, loading }: { now: Dayjs; year?: AcademicYear; loading: boolean }) {
  const today = dayjs(now.format('YYYY-MM-DD'));
  const semester = semesterAt(year, today);
  const nextSemester = nextSemesterOf(year, today);
  const progress = semester ? semesterProgress(semester, today) : null;
  const shown = semester ?? nextSemester;
  const total = shown ? semesterProgress(shown, dayjs(shown.endDate)).total : 0;

  return (
    <section className="hm-card hm-year" style={stagger(4)} aria-label="Năm học">
      <h2 className="hm-card__title">Năm học{year && <span className="hm-card__aside tabular">{year.name}</span>}</h2>
      {loading ? (
        <Skeleton active title={false} paragraph={{ rows: 3 }} />
      ) : !year ? (
        <div className="hm-rest"><span>Chưa có năm học nào.</span></div>
      ) : (
        <>
          <div className="hm-term">
            <b>{semester?.name ?? (nextSemester ? `Sắp tới: ${nextSemester.name}` : 'Ngoài học kỳ')}</b>
            {progress && (
              <span className="hm-term__week tabular">Tuần <strong><Count value={progress.week} /></strong> / {progress.total}</span>
            )}
          </div>
          {shown && (
            <>
              <ol className="hm-weeks" aria-label={progress ? `Tuần ${progress.week} trên ${progress.total}` : 'Ngoài học kỳ'}>
                {Array.from({ length: total }, (_, i) => (
                  <li
                    key={i}
                    className={progress ? (i + 1 < progress.week ? 'is-done' : i + 1 === progress.week ? 'is-now' : '') : ''}
                    style={stagger(i)}
                  />
                ))}
              </ol>
              <div className="hm-term__dates tabular">
                <span>{dayjs(shown.startDate).format('DD/MM')}</span>
                <span>{dayjs(shown.endDate).format('DD/MM/YYYY')}</span>
              </div>
            </>
          )}
        </>
      )}
    </section>
  );
}

/** Lịch của tháng có ngày nghỉ gần nhất: ngày đó tô cam đặc, các ngày nghỉ khác trong tháng tô cam nhạt, hôm nay có vòng viền. */
function MiniMonth({ month, campus, firstKey, todayKey, offDays }: { month: Dayjs; campus?: string | null; firstKey: string; todayKey: string; offDays: Holiday[] }) {
  const offSet = new Set(offDays.map((h) => h.holidayDate.slice(0, 10)));
  const lead = (month.startOf('month').day() + 6) % 7;
  const cells: (number | null)[] = [...Array<null>(lead).fill(null), ...Array.from({ length: month.daysInMonth() }, (_, i) => i + 1)];
  return (
    <div className="hm-cal" aria-label={month.format('[Tháng] M/YYYY')}>
      <div className="hm-cal__head"><b>{month.format('[Tháng] M')}</b>{campus && <span>{campus}</span>}</div>
      <ol className="hm-cal__grid">
        {['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'].map((d) => <li key={d} className="hm-cal__dow">{d}</li>)}
        {cells.map((d, i) => {
          if (d === null) return <li key={`e${i}`} />;
          const key = month.date(d).format('YYYY-MM-DD');
          const cls = key === firstKey ? 'is-first' : offSet.has(key) ? 'is-off' : '';
          return <li key={key} className={`${cls}${key === todayKey ? ' is-today' : ''}`}>{d}</li>;
        })}
      </ol>
    </div>
  );
}

/** Ngày nghỉ sắp tới: ngày gần nhất là một con số đếm ngược lớn, những ngày sau là dòng gọn. */
function HolidaysCard({ now, holidays, loading }: { now: Dayjs; holidays: Holiday[]; loading: boolean }) {
  const todayKey = now.format('YYYY-MM-DD');

  // Gộp các dòng cùng ngày cùng lý do (nghỉ cấp trường lẫn phân hiệu), lấy bốn ngày gần nhất.
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
      .slice(0, 3);
  }, [holidays, todayKey]);

  const [first, ...rest] = upcoming;
  const left = (h: Holiday) => dayjs(h.holidayDate).diff(dayjs(todayKey), 'day');

  return (
    <section className="hm-card hm-holidays" style={stagger(5)} aria-label="Ngày nghỉ sắp tới">
      <h2 className="hm-card__title">Ngày nghỉ sắp tới</h2>
      {loading ? (
        <Skeleton active title={false} paragraph={{ rows: 3 }} />
      ) : !first ? (
        <div className="hm-rest"><span>Không còn ngày nghỉ nào trong năm học.</span></div>
      ) : (
        <>
          <div className="hm-holiday">
            <div className="hm-next">
              <b className="hm-next__n tabular">{left(first) === 0 ? 'Nay' : <Count value={left(first)} />}</b>
              <span className="hm-next__text">
                <b>{left(first) === 0 ? 'là' : 'ngày nữa là'} {first.reason && first.reason !== 'Ngày nghỉ' ? first.reason : 'ngày nghỉ'}</b>
              </span>
            </div>
            <MiniMonth month={dayjs(first.holidayDate)} campus={first.campusName} firstKey={first.holidayDate.slice(0, 10)} todayKey={todayKey} offDays={holidays} />
          </div>
          {rest.length > 0 && (
            <ul className="hm-later">
              {rest.map((h, i) => (
                <li key={h.holidayId} style={stagger(i)}>
                  <span className="tabular">{dayjs(h.holidayDate).format('DD/MM')}</span>
                  <span>{h.reason ?? 'Ngày nghỉ'}{h.campusName ? ` · ${h.campusName}` : ''}</span>
                  <em className="tabular">{left(h)} ngày</em>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}
