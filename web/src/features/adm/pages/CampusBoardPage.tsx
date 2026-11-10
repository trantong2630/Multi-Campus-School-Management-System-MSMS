import { useQuery } from '@tanstack/react-query';
import { Select, Skeleton } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { scdApi } from '@/features/scd/api';
import { admApi } from '../api';
import { Columns, Count, Empty, HBars, Meter, Tile, Trend, toneOfRate } from '../components/BoardKit';
import { fmt, pct } from '../components/dashboardFormat';
import '../components/board.css';
import './views.css';

const ALL = 0;
const PAGE = 8;

/**
 * Tình hình phân hiệu (Hiệu phó): bốn thẻ số của hôm nay, xu hướng và chuyên cần theo thứ,
 * lớp nào đang nợ điểm danh, rồi bảng các lớp. Mỗi thẻ số bấm được sang màn xử lý.
 */
export function CampusBoardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number>(ALL);
  const [grade, setGrade] = useState<number>(ALL);
  const [more, setMore] = useState(false);
  const q = useQuery({
    queryKey: ['adm', 'dashboard', campusId || undefined, grade || undefined],
    queryFn: () => admApi.dashboard(campusId || undefined, grade || undefined),
    placeholderData: (prev) => prev,
  });
  const d = q.data;
  const list = campuses.data ?? [];
  const go = (path: string) => (user && isPathAllowed(path, user) ? () => navigate(path) : undefined);

  if (!d) return <Skeleton active paragraph={{ rows: 10 }} />;

  const a = d.attendance;
  const att = d.attention;
  const leave = att.pendingLeaveRequests ?? 0;
  const trend = d.weeks.map((w) => ({ weekStart: w.weekStart, count: 0, rate: w.rate }));
  const owing = d.classes.filter((c) => c.unmarkedSessions > 0).sort((x, y) => y.unmarkedSessions - x.unmarkedSessions).slice(0, 8);
  const sorted = [...d.classes].sort((x, y) => x.gradeLevel - y.gradeLevel || x.className.localeCompare(y.className, 'vi', { numeric: true }));
  const rows = more ? sorted : sorted.slice(0, PAGE);

  return (
    <div className="bd">
      <header className="bd-head">
        <div>
          <h1>Tình hình phân hiệu</h1>
          <p>{[d.campuses.length === 1 ? d.campuses[0].name : null, `${fmt(d.studentCount)} học sinh`, `${fmt(d.classCount)} lớp`, `${fmt(d.staffCount)} cán bộ`].filter(Boolean).join(' · ')}</p>
        </div>
        <div className="bd-head__tools">
          {list.length > 1 && (
            <Select value={campusId} onChange={setCampusId} style={{ width: 200 }}
              options={[{ value: ALL, label: 'Mọi phân hiệu phụ trách' }, ...list.map((c) => ({ value: c.campusId, label: c.name }))]} />
          )}
          {d.gradeOptions.length > 1 && (
            <Select value={grade} onChange={setGrade} style={{ width: 120 }}
              options={[{ value: ALL, label: 'Mọi khối' }, ...d.gradeOptions.map((g) => ({ value: g, label: `Khối ${g}` }))]} />
          )}
        </div>
      </header>

      <div className="v-tiles4">
        <Tile hue="mint" label="Tiết hôm nay" value={a.todaySessions ? `${a.todayMarkedSessions}/${a.todaySessions}` : '0'} hint={a.todaySessions ? 'đã điểm danh' : 'không có tiết dạy'} onClick={go('/scd/schedule')} />
        <Tile hue="sun" label="Giáo viên vắng" value={<Count value={att.teacherAbsencesToday} />} tone={att.teacherAbsencesToday ? 'warn' : undefined}
          hint={att.pendingSessions ? `${att.pendingSessions} tiết chờ dạy thay` : 'không có tiết chờ'} onClick={go('/scd/absences')} />
        <Tile hue="sky" label="Đơn nghỉ chờ duyệt" value={<Count value={leave} />} tone={leave ? 'warn' : undefined} />
        <Tile hue="lilac" label="Tiết chưa điểm danh" value={<Count value={a.unmarkedSessions} />} tone={a.unmarkedSessions ? 'warn' : undefined} hint="tiết đã qua, 4 tuần" onClick={go('/att')} />
      </div>

      <div className="bd-split">
        <section className="bd-panel" aria-label="Xu hướng">
          <div className="bd-panel__title"><h2>Chuyên cần theo tuần</h2><span className="tabular">{pct(a.rate)}</span></div>
          {d.weeks.some((w) => w.rate !== null) ? <Trend points={trend} rate2Label="Chuyên cần theo tuần" quiet height={210} /> : <Empty>Chưa có điểm danh trong 8 tuần qua.</Empty>}
        </section>
        <section className="bd-panel" aria-label="Theo thứ">
          <div className="bd-panel__title"><h2>Chuyên cần theo thứ</h2></div>
          {d.weekdays.some((w) => w.rate !== null)
            ? <Columns height={170} items={d.weekdays.map((w) => ({ key: String(w.day), label: `Thứ ${w.day + 1}`, value: w.rate }))} />
            : <Empty>Chưa có điểm danh để so sánh.</Empty>}
        </section>
      </div>

      <section className="bd-panel" aria-label="Chưa điểm danh theo lớp">
        <div className="bd-panel__title"><h2>Tiết chưa điểm danh theo lớp</h2><span className="tabular">{fmt(a.unmarkedSessions)} tiết</span></div>
        {owing.length === 0 ? <p className="v-calm">Không lớp nào nợ điểm danh.</p> : (
          <HBars items={owing.map((c) => ({ key: String(c.classId), label: `Lớp ${c.className}`, sub: c.homeroom ?? 'Chưa có chủ nhiệm', value: c.unmarkedSessions, display: `${c.unmarkedSessions} tiết`, tone: 'warn' }))} />
        )}
      </section>

      <section className="bd-panel" aria-label="Các lớp">
        <div className="bd-panel__title"><h2>Các lớp</h2><span className="tabular">{d.classes.length} lớp</span></div>
        {rows.length === 0 ? <Empty>Chưa có lớp nào.</Empty> : (
          <>
            <div className="v-trow v-trow--head" aria-hidden><span>Lớp</span><span>Chuyên cần</span><span /><span>Sĩ số</span><span>Chưa ĐD</span></div>
            <ul className="v-table">
              {rows.map((c) => (
                <li key={c.classId}>
                  <div className="v-trow">
                    <span className="v-trow__name"><strong>Lớp {c.className}</strong><span>{c.homeroom ?? 'Chưa có chủ nhiệm'}</span></span>
                    <Meter value={c.attendanceRate} height={10} />
                    <b className={`v-trow__rate tabular bd-text--${toneOfRate(c.attendanceRate)}`}>{pct(c.attendanceRate)}</b>
                    <span className="v-trow__num tabular">{c.studentCount}</span>
                    <span className={`v-trow__num tabular${c.unmarkedSessions ? ' bd-text--warn' : ''}`}>{c.unmarkedSessions || '—'}</span>
                  </div>
                </li>
              ))}
            </ul>
            {sorted.length > PAGE && <button type="button" className="bd-more" onClick={() => setMore(!more)}>{more ? 'Thu gọn' : `Xem cả ${sorted.length} lớp`}</button>}
          </>
        )}
      </section>
    </div>
  );
}
