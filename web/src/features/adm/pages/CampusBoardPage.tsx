import { useQuery } from '@tanstack/react-query';
import { Select, Skeleton } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { scdApi } from '@/features/scd/api';
import { admApi } from '../api';
import { AiAssistant } from '../components/AiAssistant';
import { aiPrompts } from '../components/aiPrompts';
import { Count, OutcomeTag, Panel, Section, Tile } from '../components/BoardKit';
import { ATTENDANCE_ALERT, SUBMISSION_ALERT } from '../components/dashboardFormat';
import { AssignmentPanel, MixPanel, RatePanel, RateTile, TrendPanel, WeekdayPanel } from '../components/leadership';
import '../components/board.css';
import './views.css';

const ALL = 0;
const ORDER: Record<string, number> = { Pending: 0, Cancelled: 1, Substituted: 2 };

/**
 * Tình hình phân hiệu (Hiệu phó): cùng khung với màn Hiệu trưởng, thiên về điều hành trong ngày: thẻ tiết hôm nay bị ảnh hưởng
 * và tiết chưa điểm danh, khối Hôm nay cạnh đường chuyên cần.
 */
export function CampusBoardPage() {
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

  const today = [...d.today].sort((a, b) => ORDER[a.outcome] - ORDER[b.outcome] || a.startTime.localeCompare(b.startTime));
  const open = today.filter((s) => s.outcome === 'Pending').length;
  const absences = go('/scd/absences');
  // Một phân hiệu (phụ trách một, hoặc đang lọc một) thì tiêu đề ghi luôn tên.
  const title = list.find((c) => c.campusId === campusId)?.name ?? (list.length === 1 ? list[0].name : 'phân hiệu');

  return (
    <div className="bd bd--ai">
      <header className="bd-head">
        <h1>Tình hình {title}</h1>
        <div className="bd-head__tools">
          {list.length > 1 && (
            <Select value={campusId} onChange={setCampusId} style={{ width: 220 }} aria-label="Chọn phân hiệu" popupMatchSelectWidth={false}
              options={[{ value: ALL, label: 'Mọi phân hiệu phụ trách' }, ...list.map((c) => ({ value: c.campusId, label: c.name }))]} />
          )}
          {d.gradeOptions.length > 1 && (
            <Select value={grade} onChange={setGrade} style={{ width: 120 }} aria-label="Chọn khối"
              options={[{ value: ALL, label: 'Mọi khối' }, ...d.gradeOptions.map((g) => ({ value: g, label: `Khối ${g}` }))]} />
          )}
        </div>
      </header>

      <div className="bd-tiles">
        <Tile hue="lilac" label="Tiết hôm nay bị ảnh hưởng" value={<Count value={today.length} />} unit=" tiết" onClick={absences}
          foot={today.length === 0 ? 'mọi tiết đều có người dạy' : open ? 'cần xếp người dạy thay' : 'đã xử lý xong'}
          alert={d.alerts.uncoveredSessions ? { text: `${d.alerts.uncoveredSessions} tiết chưa có người dạy`, onClick: absences } : null} />
        <RateTile hue="sky" label="Chuyên cần" k={d.attendance} higherIsBetter empty="chưa có điểm danh"
          alert={d.alerts.streakStudents ? { text: `${d.alerts.streakStudents} học sinh vắng liền`, onClick: go('/att') } : null} />
        <Tile hue="sun" label="Tiết chưa điểm danh" value={<Count value={d.alerts.unmarkedSessions} />} unit=" tiết" onClick={go('/att')}
          foot="tiết đã qua trong 4 tuần" />
        <RateTile hue="aqua" label="Nộp bài rèn luyện" k={d.submission} higherIsBetter empty="chưa có bài hết hạn" />
      </div>

      <Section title="Chuyên cần" />
      <div className="bd-row bd-row--21">
        <TrendPanel title="Chuyên cần 8 tuần" weekStarts={d.weekStarts} weekNo={d.weekNo} values={d.attendance.weeks} name={title === 'phân hiệu' ? 'Phân hiệu' : title} threshold={ATTENDANCE_ALERT} />
        <Panel title="Hôm nay" meta={today.length ? `${today.length} tiết bị ảnh hưởng` : undefined}>
          {today.length === 0 ? <p className="bd-calm"><i aria-hidden>✓</i>Mọi tiết hôm nay đều có giáo viên đứng lớp.</p> : (
            <ul className="v-today">
              {today.map((s) => (
                <li key={s.sessionId} className={s.outcome === 'Pending' ? 'is-open' : ''}>
                  <span className="v-today__time tabular"><b>Tiết {s.periodNo}</b>{s.startTime.slice(0, 5)}</span>
                  <span className="v-today__what"><strong>{s.className} · {s.subjectName}</strong>
                    <small>{s.absentTeacher ? `${s.absentTeacher} vắng` : 'Tiết bị hủy'}{s.teacher ? ` → ${s.teacher}` : ''}</small>
                  </span>
                  <OutcomeTag outcome={s.outcome} />
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
      <div className="bd-row bd-row--auto">
        <MixPanel mix={d.mix} />
        <RatePanel title="Theo khối" rows={d.grades} threshold={ATTENDANCE_ALERT} empty="Chưa có khối nào có học sinh." />
        <WeekdayPanel values={d.weekdays} />
      </div>

      <Section title="Rèn luyện" />
      <div className="bd-row bd-row--21">
        <TrendPanel title="Tỷ lệ nộp bài 8 tuần" meta="theo tuần hết hạn" weekStarts={d.weekStarts} weekNo={d.weekNo}
          values={d.submission.weeks} name="Tỷ lệ nộp" threshold={SUBMISSION_ALERT} />
        <AssignmentPanel a={d.assignments} groupLabel="khối" rows={d.submissionGrades.map((g) => ({ key: String(g.id), label: g.name, value: g.rate }))} />
      </div>

      <AiAssistant {...aiPrompts('campus')} />
    </div>
  );
}
