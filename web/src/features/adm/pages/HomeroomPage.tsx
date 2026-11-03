import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { admApi } from '../api';
import { Count, Donut, Empty, HBars, Seg, Tile } from '../components/BoardKit';
import { fmt } from '../components/dashboardFormat';
import '../components/board.css';
import './views.css';

/** Vắng không phép từ ngần này buổi trong 30 ngày thì đưa vào danh sách cần theo dõi. */
const WATCH_ABSENCES = 3;
type Filter = 'all' | 'absent';

/**
 * Tình hình lớp chủ nhiệm: sĩ số và chuyên cần, điểm danh, liên kết phụ huynh, học sinh vắng nhiều
 * và danh sách học sinh (phạm vi chủ nhiệm, Business Spec §2.3).
 */
export function HomeroomPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<Filter>('all');
  const classId = params.get('classId') ? Number(params.get('classId')) : undefined;
  const q = useQuery({ queryKey: ['adm', 'homeroom', classId], queryFn: () => admApi.homeroom(classId) });
  const d = q.data;
  const can = (path: string) => (user && isPathAllowed(path, user) ? path : undefined);

  if (q.isLoading || !d) return <Skeleton active paragraph={{ rows: 10 }} />;

  const a = d.attendance;
  const marked = a.present + a.late + a.absentExcused + a.absentUnexcused;
  const frequent = d.students.filter((s) => s.absentUnexcused >= WATCH_ABSENCES);
  const shown = filter === 'absent' ? frequent : d.students;
  const absentees = d.students.filter((s) => s.absentExcused + s.absentUnexcused > 0)
    .sort((x, y) => y.absentUnexcused - x.absentUnexcused || (y.absentExcused + y.absentUnexcused) - (x.absentExcused + x.absentUnexcused))
    .slice(0, 6);

  return (
    <div className="bd">
      <header className="bd-head">
        <div>
          <h1>Lớp chủ nhiệm {d.className}</h1>
          <p>{[d.campusName, `Khối ${d.gradeLevel}`, d.academicYearName && `Năm học ${d.academicYearName}`].filter(Boolean).join(' · ')}</p>
        </div>
        {d.classes.length > 1 && (
          <Seg label="Chọn lớp" value={d.classId} onChange={(v) => { setFilter('all'); setParams({ classId: String(v) }); }}
            options={d.classes.map((c) => ({ value: c.id, label: `Lớp ${c.name}` }))} />
        )}
      </header>

      <div className="v-tiles4">
        <Tile hue="mint" label="Sĩ số" value={d.studentCount} hint={`${d.maleCount} nam · ${d.femaleCount} nữ`} />
        <Tile hue="sky" label="Vắng không phép" value={<Count value={a.absentUnexcused} />} tone={a.absentUnexcused ? 'warn' : undefined} hint={marked ? `${fmt(a.absentExcused)} vắng có phép · 4 tuần` : 'chưa có điểm danh'} />
        <Tile hue="sun" label="Đơn nghỉ chờ duyệt" value={d.pendingLeave} tone={d.pendingLeave ? 'warn' : undefined}
          hint={`${d.leaveLast30Days} đơn trong 30 ngày`} onClick={can('/par/leave-requests') ? () => navigate('/par/leave-requests') : undefined} />
        <Tile hue="lilac" label="Phụ huynh chưa có tài khoản" value={d.parentsWithoutAccount} tone={d.parentsWithoutAccount ? 'warn' : undefined}
          hint={d.studentsWithoutParent ? `${d.studentsWithoutParent} học sinh chưa liên kết` : 'đã liên kết đủ'} />
      </div>

      <div className="bd-split">
        <section className="bd-panel" aria-label="Điểm danh">
          <div className="bd-panel__title"><h2>Điểm danh 4 tuần</h2></div>
          {marked === 0 ? <Empty>Chưa có lượt điểm danh nào.</Empty> : (
            <Donut total={marked} caption="lượt" segments={[
              { key: 'p', label: 'Có mặt', value: a.present, tone: 'good' },
              { key: 'l', label: 'Muộn', value: a.late, tone: 'soft' },
              { key: 'e', label: 'Vắng có phép', value: a.absentExcused, tone: 'warn' },
              { key: 'u', label: 'Vắng không phép', value: a.absentUnexcused, tone: 'low' },
            ]} />
          )}
        </section>
        <section className="bd-panel" aria-label="Học sinh vắng nhiều">
          <div className="bd-panel__title"><h2>Học sinh vắng nhiều nhất, 30 ngày</h2></div>
          {absentees.length === 0 ? <Empty>Không có học sinh nào vắng.</Empty> : (
            <HBars items={absentees.map((s) => ({
              key: String(s.studentId), label: s.fullName, sub: `${s.absentUnexcused} không phép · ${s.absentExcused} có phép`,
              value: s.absentUnexcused + s.absentExcused, display: `${s.absentUnexcused + s.absentExcused} buổi`,
              tone: s.absentUnexcused >= WATCH_ABSENCES ? 'low' : 'warn',
            }))} />
          )}
        </section>
      </div>

      <section className="bd-panel" aria-label="Danh sách học sinh">
        <div className="bd-panel__title">
          <h2>Danh sách học sinh</h2>
          <Seg label="Lọc danh sách" value={filter} onChange={setFilter}
            options={[{ value: 'all', label: `Cả lớp (${d.studentCount})` }, { value: 'absent', label: `Vắng nhiều (${frequent.length})` }]} />
        </div>
        {shown.length === 0 ? <Empty>{d.students.length === 0 ? 'Lớp chưa có học sinh ghi danh.' : 'Không có học sinh nào khớp.'}</Empty> : (
          <>
            <div className="v-trow v-trow--head v-trow--stu" aria-hidden><span>STT</span><span>Học sinh</span><span>Vắng 30 ngày</span><span>Phụ huynh</span><span>Đơn nghỉ</span></div>
            <ul className="v-table">
              {shown.map((s, i) => (
                <li key={s.studentId}>
                  <div className="v-trow v-trow--stu">
                    <span className="v-trow__num tabular">{i + 1}</span>
                    <span className="v-trow__name"><strong>{s.fullName}</strong><span className="tabular">{s.personCode}</span></span>
                    <span className={`v-trow__num tabular${s.absentUnexcused >= WATCH_ABSENCES ? ' bd-text--low' : ''}`}>{s.absentUnexcused + s.absentExcused === 0 ? '—' : `${s.absentUnexcused + s.absentExcused} buổi`}</span>
                    {s.parentName
                      ? <span className="v-trow__name"><strong>{s.parentName}</strong><span className="tabular">{s.parentPhone ?? 'Chưa có số điện thoại'}</span></span>
                      : <span className="bd-text--warn">Chưa liên kết</span>}
                    <span className="v-trow__num tabular">{s.pendingLeave > 0 ? <em className="v-badge">{s.pendingLeave} chờ duyệt</em> : s.leaveCount || '—'}</span>
                  </div>
                </li>
              ))}
            </ul>
          </>
        )}
      </section>

      {d.teachers.length > 0 && (
        <section className="bd-panel" aria-label="Giáo viên bộ môn">
          <div className="bd-panel__title"><h2>Giáo viên bộ môn</h2></div>
          <div className="bd-chips">
            {d.teachers.map((t) => <span key={`${t.subjectName}-${t.teacherName}`} className="bd-chip"><b>{t.subjectName}</b>{t.teacherName}</span>)}
          </div>
        </section>
      )}
    </div>
  );
}
