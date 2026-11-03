import { useQuery } from '@tanstack/react-query';
import { Select, Skeleton } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { scdApi } from '@/features/scd/api';
import { admApi } from '../api';
import { Columns, Count, Donut, Empty, Meter, Seg, Tile, Trend, toneOfRate } from '../components/BoardKit';
import type { DonutSegment } from '../components/BoardKit';
import { fmt, pct } from '../components/dashboardFormat';
import '../components/board.css';
import './views.css';

const ALL = 0;
const GRADE_TONES: DonutSegment['tone'][] = ['good', 'mint', 'warn', 'ink', 'soft', 'low'];

/**
 * Tình hình toàn trường (Hiệu trưởng): bốn thẻ số, xu hướng chuyên cần và so sánh phân hiệu,
 * cơ cấu học sinh theo khối cạnh những việc cần chú ý, rồi bảng các phân hiệu.
 */
export function PrincipalBoardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number>(ALL);
  const [grade, setGrade] = useState<number>(ALL);
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
  const marked = a.present + a.late + a.absentExcused + a.absentUnexcused;
  const trend = d.weeks.map((w) => ({ weekStart: w.weekStart, count: 0, rate: w.rate }));
  const gradeSegments: DonutSegment[] = d.byGrade.map((g, i) => ({ key: String(g.gradeLevel), label: `Khối ${g.gradeLevel}`, value: g.studentCount, tone: GRADE_TONES[i % GRADE_TONES.length] }));
  const maxStudents = Math.max(1, ...d.campuses.map((c) => c.studentCount));
  const todo = [
    { key: 'unmarked', n: a.unmarkedSessions, text: 'Tiết đã qua chưa điểm danh', to: go('/att') },
    { key: 'pending', n: att.pendingSessions, text: 'Tiết chờ xử lý do giáo viên vắng', to: go('/scd/absences') },
    { key: 'homeroom', n: att.classesWithoutHomeroom, text: 'Lớp chưa có chủ nhiệm', to: go('/ppl/responsibilities') },
  ].filter((r) => r.n > 0);

  return (
    <div className="bd">
      <header className="bd-head">
        <div>
          <h1>Tình hình toàn trường</h1>
          <p>{[d.academicYearName && `Năm học ${d.academicYearName}`, d.semesterName].filter(Boolean).join(' · ')}</p>
        </div>
        <div className="bd-head__tools">
          {list.length > 1 && (
            <Seg label="Chọn phân hiệu" value={campusId} onChange={setCampusId}
              options={[{ value: ALL, label: 'Cả trường' }, ...list.map((c) => ({ value: c.campusId, label: c.name }))]} />
          )}
          {d.gradeOptions.length > 1 && (
            <Select value={grade} onChange={setGrade} style={{ width: 120 }}
              options={[{ value: ALL, label: 'Mọi khối' }, ...d.gradeOptions.map((g) => ({ value: g, label: `Khối ${g}` }))]} />
          )}
        </div>
      </header>

      <div className="v-tiles4">
        <Tile hue="mint" label="Vắng không phép" value={<Count value={a.absentUnexcused} />} tone={a.absentUnexcused ? 'warn' : undefined} hint={marked ? `${fmt(a.absentExcused)} vắng có phép · 4 tuần` : 'chưa có điểm danh'} />
        <Tile hue="sky" label="Học sinh" value={<Count value={d.studentCount} />} hint={`${d.campusCount} phân hiệu · ${d.classCount} lớp`} onClick={go('/ppl/students')} />
        <Tile hue="sun" label="Cán bộ" value={<Count value={d.staffCount} />} onClick={go('/ppl/staff')} />
        <Tile hue="lilac" label="Giáo viên vắng hôm nay" value={<Count value={att.teacherAbsencesToday} />} tone={att.teacherAbsencesToday ? 'warn' : undefined}
          hint={a.todaySessions ? `${a.todayMarkedSessions}/${a.todaySessions} tiết đã điểm danh` : 'không có tiết hôm nay'} onClick={go('/scd/absences')} />
      </div>

      <div className="bd-split">
        <section className="bd-panel" aria-label="Xu hướng">
          <div className="bd-panel__title"><h2>Chuyên cần theo tuần</h2></div>
          {d.weeks.some((w) => w.rate !== null) ? <Trend points={trend} rate2Label="Chuyên cần theo tuần" quiet height={210} /> : <Empty>Chưa có điểm danh trong 8 tuần qua.</Empty>}
        </section>
        <section className="bd-panel" aria-label="Theo phân hiệu">
          <div className="bd-panel__title"><h2>Chuyên cần theo phân hiệu</h2></div>
          {d.campuses.some((c) => c.attendanceRate !== null)
            ? <Columns height={170} items={d.campuses.map((c) => ({ key: String(c.campusId), label: c.name, value: c.attendanceRate, sub: `${fmt(c.studentCount)} học sinh` }))} />
            : <Empty>Chưa có điểm danh để so sánh.</Empty>}
        </section>
      </div>

      <div className="bd-split">
        <section className="bd-panel" aria-label="Học sinh theo khối">
          <div className="bd-panel__title"><h2>Học sinh theo khối</h2></div>
          {gradeSegments.length === 0 ? <Empty>Chưa có khối nào.</Empty> : <Donut segments={gradeSegments} total={d.studentCount} caption="học sinh" />}
        </section>
        <section className="bd-panel" aria-label="Cần chú ý">
          <div className="bd-panel__title"><h2>Cần chú ý</h2></div>
          {todo.length === 0 ? <p className="v-calm">Không có việc nào tồn đọng.</p> : (
            <ul className="v-todo">
              {todo.map((r) => {
                const inner = (<><span className="v-todo__n tabular">{r.n}</span><span>{r.text}</span>{r.to ? <span className="v-todo__go">→</span> : <span />}</>);
                return <li key={r.key}>{r.to ? <button type="button" onClick={r.to}>{inner}</button> : <div>{inner}</div>}</li>;
              })}
            </ul>
          )}
        </section>
      </div>

      <section className="bd-panel" aria-label="Các phân hiệu">
        <div className="bd-panel__title"><h2>Các phân hiệu</h2></div>
        <div className="v-trow v-trow--head" aria-hidden><span>Phân hiệu</span><span>Học sinh</span><span /><span>Chuyên cần</span><span>Lớp</span></div>
        <ul className="v-table">
          {d.campuses.map((c) => (
            <li key={c.campusId}>
              <button type="button" className="v-trow" onClick={() => setCampusId(c.campusId === campusId ? ALL : c.campusId)} aria-label={`Xem riêng ${c.name}`}>
                <span className="v-trow__name"><strong>{c.name}</strong><span>{c.leaders.length ? c.leaders.join(', ') : 'Chưa có hiệu phó phụ trách'}</span></span>
                <Meter value={(c.studentCount / maxStudents) * 100} tone="good" height={10} />
                <b className="v-trow__num tabular">{fmt(c.studentCount)}</b>
                <b className={`v-trow__rate tabular bd-text--${toneOfRate(c.attendanceRate)}`}>{pct(c.attendanceRate)}</b>
                <span className="v-trow__num tabular">{c.classCount}</span>
              </button>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
