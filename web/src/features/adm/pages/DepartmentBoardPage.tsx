import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { admApi } from '../api';
import { Count, Donut, Empty, HBars, Tile } from '../components/BoardKit';
import type { DonutSegment } from '../components/BoardKit';
import { fmt, pct } from '../components/dashboardFormat';
import '../components/board.css';
import './views.css';

const TONES: DonutSegment['tone'][] = ['good', 'mint', 'warn', 'ink', 'soft', 'low'];

/**
 * Tình hình tổ chuyên môn (tổ trưởng): quy mô tổ, các môn của tổ, tải dạy từng thầy cô, nợ điểm danh của tổ và chuyên cần các tiết thuộc môn của tổ.
 * Bài tập nằm ở Tiến độ bài tập rèn luyện.
 */
export function DepartmentBoardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const q = useQuery({ queryKey: ['adm', 'department-board'], queryFn: admApi.departmentBoard });
  const d = q.data;
  const go = (path: string) => (user && isPathAllowed(path, user) ? () => navigate(path) : undefined);

  if (q.isLoading || !d) return <Skeleton active paragraph={{ rows: 10 }} />;

  const a = d.attendance;
  const marked = a.present + a.late + a.absentExcused + a.absentUnexcused;
  const teaching = d.members.filter((m) => m.periodsPerWeek > 0);
  const totalPeriods = teaching.reduce((n, m) => n + m.periodsPerWeek, 0);
  const byLoad = [...d.members].sort((x, y) => y.periodsPerWeek - x.periodsPerWeek);
  const owing = d.members.filter((m) => m.unmarkedSessions > 0).sort((x, y) => y.unmarkedSessions - x.unmarkedSessions);

  return (
    <div className="bd">
      <header className="bd-head">
        <div>
          <h1>Tình hình tổ chuyên môn</h1>
          <p>{d.semesterName}</p>
        </div>
      </header>

      <div className="v-tiles4">
        <div className="bd-tile bd-tile--mint bd-tile--donut">
          <span>Môn của tổ</span>
          <b className="tabular"><Count value={d.subjectNames.length} /></b>
          <small>{d.subjectNames.join(', ') || 'chưa có môn'}</small>
          <svg className="bd-tile__donut" viewBox="0 0 64 64" role="img" aria-label={d.subjectNames.join(', ')}>
            <g transform="rotate(-90 32 32)" fill="none" strokeWidth="11">
              <circle className="bd-donut__bg" cx="32" cy="32" r="24" />
              {d.subjectNames.map((n, i) => {
                const c = 2 * Math.PI * 24, len = c / d.subjectNames.length;
                return <circle key={n} className={`bd-donut__seg--${TONES[i % TONES.length]}`} cx="32" cy="32" r="24"
                  strokeDasharray={`${Math.max(len - (d.subjectNames.length > 1 ? 2 : 0), 1)} ${c}`} strokeDashoffset={-i * len} />;
              })}
            </g>
          </svg>
        </div>
        <Tile hue="sky" label="Vắng không phép" value={<Count value={a.absentUnexcused} />} tone={a.absentUnexcused ? 'warn' : undefined} hint={marked ? `${fmt(a.absentExcused)} vắng có phép · 4 tuần` : 'chưa có điểm danh'} />
        <Tile hue="sun" label="Thầy cô vắng hôm nay" value={<Count value={d.absentToday} />} tone={d.absentToday ? 'warn' : undefined}
          hint={d.sessionsToday ? `${d.sessionsToday} tiết hôm nay` : 'không có tiết hôm nay'} onClick={go('/scd/schedule')} />
        <Tile hue="lilac" label="Tiết chưa điểm danh" value={<Count value={a.unmarkedSessions} />} tone={a.unmarkedSessions ? 'warn' : undefined} hint="tiết đã qua, 4 tuần" onClick={a.unmarkedSessions ? go('/att') : undefined} />
      </div>

      <div className="bd-split">
        <section className="bd-panel" aria-label="Tải dạy">
          <div className="bd-panel__title"><h2>Tiết dạy mỗi tuần</h2><span className="tabular">{fmt(totalPeriods)} tiết</span></div>
          {byLoad.length === 0 ? <Empty>Tổ chưa có thầy cô nào.</Empty> : (
            <HBars items={byLoad.map((m) => ({ key: String(m.staffId), label: m.name, sub: m.absentToday ? 'Vắng hôm nay' : undefined, value: m.periodsPerWeek, display: `${m.periodsPerWeek} tiết`, tone: m.absentToday ? 'warn' : 'good' }))} />
          )}
        </section>
        <section className="bd-panel" aria-label="Điểm danh">
          <div className="bd-panel__title"><h2>{owing.length ? 'Tiết chưa điểm danh theo thầy cô' : 'Điểm danh 4 tuần'}</h2><span className="tabular">{pct(a.rate)}</span></div>
          {owing.length > 0 ? (
            <Donut total={a.unmarkedSessions} caption="tiết" segments={owing.map((m, i) => ({ key: String(m.staffId), label: m.name, value: m.unmarkedSessions, tone: TONES[i % TONES.length] }))} />
          ) : marked === 0 ? <Empty>Chưa có lượt điểm danh nào.</Empty> : (
            <Donut total={marked} caption="lượt" segments={[
              { key: 'p', label: 'Có mặt', value: a.present, tone: 'good' },
              { key: 'l', label: 'Muộn', value: a.late, tone: 'soft' },
              { key: 'e', label: 'Vắng có phép', value: a.absentExcused, tone: 'warn' },
              { key: 'u', label: 'Vắng không phép', value: a.absentUnexcused, tone: 'low' },
            ]} />
          )}
        </section>
      </div>

      <section className="bd-panel" aria-label="Thầy cô">
        <div className="bd-panel__title"><h2>Thầy cô trong tổ</h2><span className="tabular">{d.members.length} thầy cô · {fmt(totalPeriods)} tiết mỗi tuần</span></div>
        {d.members.length === 0 ? <Empty>Tổ chưa có thầy cô nào.</Empty> : (
          <>
            <div className="v-trow v-trow--head v-trow--teach" aria-hidden><span>Thầy cô</span><span>Phân hiệu</span><span>Môn và lớp</span><span>Chưa điểm danh</span></div>
            <ul className="v-table">
              {d.members.map((m) => (
                <li key={m.staffId}>
                  <div className="v-trow v-trow--teach">
                    <span className="v-trow__name"><strong>{m.name}{m.absentToday && <em className="v-badge" style={{ marginLeft: 8 }}>Vắng hôm nay</em>}</strong><span>{m.periodsPerWeek} tiết mỗi tuần</span></span>
                    <span className="v-trow__name"><span>{m.inDepartment ? (m.campusName ?? 'Chưa gắn phân hiệu') : `Tổ khác · ${m.campusName ?? ''}`}</span></span>
                    <span className="bd-chips">
                      {m.teaching.length === 0 ? <span className="bd-row__count">Chưa dạy môn của tổ</span>
                        : m.teaching.map((t) => <span key={t.subjectName} className="bd-chip"><b>{t.subjectName}</b>{t.classNames.join(', ')}</span>)}
                    </span>
                    <span className={`v-trow__num tabular${m.unmarkedSessions ? ' bd-text--warn' : ''}`}>{m.unmarkedSessions || '—'}</span>
                  </div>
                </li>
              ))}
            </ul>
          </>
        )}
      </section>
    </div>
  );
}
