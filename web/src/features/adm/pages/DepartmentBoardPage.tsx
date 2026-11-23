import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { admApi } from '../api';
import { Count, HBars, Panel, Section, Tile, WeekStack } from '../components/BoardKit';
import { SUBMISSION_ALERT, weekLabel } from '../components/dashboardFormat';
import { AssignmentPanel, CountTile, RateTile, TrendPanel } from '../components/leadership';
import '../components/board.css';

/**
 * Tình hình tổ chuyên môn (tổ trưởng): bốn thẻ số (thầy cô vắng hôm nay, thầy cô chưa giao bài gắn trên thẻ liên quan),
 * nhóm Giảng dạy (tiết bị ảnh hưởng theo tuần, tải dạy) và nhóm Rèn luyện (nộp bài 8 tuần, theo môn).
 */
export function DepartmentBoardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const q = useQuery({ queryKey: ['adm', 'department-board'], queryFn: admApi.departmentBoard });
  const d = q.data;
  const go = (path: string) => (user && isPathAllowed(path, user) ? () => navigate(path) : undefined);

  if (q.isLoading || !d) return <Skeleton active paragraph={{ rows: 10 }} />;

  const labels = d.weekStarts.map((_, i) => weekLabel(d.weekStarts, i, d.weekNo));
  const names = d.departments.map((t) => t.name.replace(/^tổ\s+/i, '')).join(', ');
  const teaching = d.members.filter((m) => m.periodsPerWeek > 0);
  const idle = teaching.filter((m) => m.assignmentCount === 0).length;

  return (
    <div className="bd">
      <header className="bd-head">
        <h1>Tình hình {names ? `tổ ${names}` : 'tổ chuyên môn'}</h1>
      </header>

      <div className="bd-tiles">
        <CountTile hue="lilac" label="Tiết bị ảnh hưởng" k={d.affected} unit=" tiết"
          alert={d.absentToday.length ? { text: `Vắng hôm nay: ${d.absentToday.join(', ')}`, onClick: go('/scd/schedule') } : null} />
        <Tile hue="sun" label="Tiết chưa điểm danh" value={<Count value={d.unmarkedSessions} />} unit=" tiết" onClick={go('/att')} foot="tiết đã qua trong 4 tuần" />
        <RateTile hue="aqua" label="Nộp bài rèn luyện" k={d.submission} higherIsBetter empty="chưa có bài hết hạn"
          alert={idle ? { text: `${idle} thầy cô chưa giao bài trong 4 tuần` } : null} />
        <Tile hue="peach" label="Bài đang mở" value={<Count value={d.assignments.open} />} unit=" bài"
          foot={d.assignments.dueSoon ? `${d.assignments.dueSoon} bài hết hạn trong 7 ngày` : undefined} />
      </div>

      <Section title="Giảng dạy" />
      <div className="bd-row bd-row--21">
        <Panel title="Tiết bị ảnh hưởng theo tuần" meta="số trên cột là giáo viên vắng"><WeekStack labels={labels} weeks={d.absenceWeeks} height={190} /></Panel>
        <Panel title="Tải dạy" meta={d.averagePeriods === null ? undefined : `vạch đứt: trung bình ${d.averagePeriods.toLocaleString('vi-VN')} tiết`}>
          {teaching.length === 0 ? <p className="bd-empty">Chưa có thời khóa biểu đã phát hành cho các môn của tổ.</p> : (
            <HBars mark={d.averagePeriods === null ? undefined : { value: d.averagePeriods, label: 'Trung bình tổ' }}
              items={teaching.map((m) => ({ key: String(m.staffId), label: m.name, sub: m.absentToday ? 'Vắng hôm nay' : `Lớp ${m.classNames.join(', ')}`,
                value: m.periodsPerWeek, display: `${m.periodsPerWeek} tiết`, tone: m.absentToday ? 'warn' : 'good' }))} />
          )}
        </Panel>
      </div>

      <Section title="Rèn luyện" />
      <div className="bd-row bd-row--21">
        <TrendPanel title="Tỷ lệ nộp bài 8 tuần" meta="môn của tổ · theo tuần hết hạn" weekStarts={d.weekStarts} weekNo={d.weekNo}
          values={d.submission.weeks} name="Tỷ lệ nộp" threshold={SUBMISSION_ALERT} />
        <AssignmentPanel a={d.assignments} groupLabel="môn" rows={d.subjects.map((s) => ({ key: String(s.id), label: s.name, value: s.rate }))} />
      </div>
    </div>
  );
}
