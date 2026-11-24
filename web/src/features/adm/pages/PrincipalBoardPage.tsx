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
import { Section } from '../components/BoardKit';
import { ATTENDANCE_ALERT, SUBMISSION_ALERT } from '../components/dashboardFormat';
import { AssignmentPanel, CountTile, MixPanel, RatePanel, RateTile, TrendPanel, WeekdayPanel } from '../components/leadership';
import '../components/board.css';

const ALL = 0;

/**
 * Tình hình toàn trường (Hiệu trưởng): bốn thẻ số (việc tồn đọng gắn trên thẻ liên quan), nhóm Chuyên cần và nhóm Rèn luyện.
 * Biểu đồ đường chỉ một đường; so sánh nhiều phân hiệu, khối dùng thanh ngang một màu. Phân tích sâu để MSMS AI.
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

  const al = d.alerts;

  return (
    <div className="bd bd--ai">
      <header className="bd-head">
        <h1>Tình hình toàn trường</h1>
        <div className="bd-head__tools">
          {list.length > 1 && (
            <Select value={campusId} onChange={setCampusId} style={{ width: 200 }} aria-label="Chọn phân hiệu" popupMatchSelectWidth={false}
              options={[{ value: ALL, label: 'Cả trường' }, ...list.map((c) => ({ value: c.campusId, label: c.name }))]} />
          )}
          {d.gradeOptions.length > 1 && (
            <Select value={grade} onChange={setGrade} style={{ width: 120 }} aria-label="Chọn khối"
              options={[{ value: ALL, label: 'Mọi khối' }, ...d.gradeOptions.map((g) => ({ value: g, label: `Khối ${g}` }))]} />
          )}
        </div>
      </header>

      <div className="bd-tiles">
        <RateTile hue="sky" label="Chuyên cần" k={d.attendance} higherIsBetter empty="chưa có điểm danh"
          alert={al.streakStudents ? { text: `${al.streakStudents} học sinh vắng liền`, onClick: go('/att') } : null} />
        <RateTile hue="peach" label="Vắng không phép" k={d.unexcused} higherIsBetter={false} empty="chưa có điểm danh" />
        <CountTile hue="lilac" label="Tiết bị ảnh hưởng" k={d.affected} unit=" tiết" onClick={go('/scd/absences')}
          alert={al.uncoveredSessions ? { text: `${al.uncoveredSessions} tiết chưa có người dạy`, onClick: go('/scd/absences') } : null} />
        <RateTile hue="aqua" label="Nộp bài rèn luyện" k={d.submission} higherIsBetter empty="chưa có bài hết hạn" />
      </div>

      <Section title="Chuyên cần" />
      <div className="bd-row bd-row--21">
        <TrendPanel title="Chuyên cần 8 tuần" weekStarts={d.weekStarts} weekNo={d.weekNo} values={d.attendance.weeks}
          name={campusId ? (list.find((c) => c.campusId === campusId)?.name ?? 'Phân hiệu') : 'Toàn trường'} threshold={ATTENDANCE_ALERT} />
        <MixPanel mix={d.mix} />
      </div>
      <div className="bd-row bd-row--auto">
        {d.campuses.length > 1 && <RatePanel title="Theo phân hiệu" rows={d.campuses} threshold={ATTENDANCE_ALERT} empty="" />}
        <RatePanel title="Theo khối" rows={d.grades} threshold={ATTENDANCE_ALERT} empty="Chưa có khối nào có học sinh." />
        <WeekdayPanel values={d.weekdays} />
      </div>

      <Section title="Rèn luyện" />
      <div className="bd-row bd-row--21">
        <TrendPanel title="Tỷ lệ nộp bài 8 tuần" meta="theo tuần hết hạn" weekStarts={d.weekStarts} weekNo={d.weekNo}
          values={d.submission.weeks} name="Tỷ lệ nộp" threshold={SUBMISSION_ALERT} />
        <AssignmentPanel a={d.assignments} groupLabel="khối" rows={d.submissionGrades.map((g) => ({ key: String(g.id), label: g.name, value: g.rate }))} />
      </div>

      <AiAssistant {...aiPrompts('school')} />
    </div>
  );
}
