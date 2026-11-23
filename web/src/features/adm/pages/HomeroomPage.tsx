import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { admApi } from '../api';
import { Count, Section, Seg, Tile } from '../components/BoardKit';
import { ATTENDANCE_ALERT, SUBMISSION_ALERT, pct } from '../components/dashboardFormat';
import { AssignmentPanel, MixPanel, RateTile, TrendPanel } from '../components/leadership';
import '../components/board.css';

/**
 * Tình hình lớp chủ nhiệm: bốn thẻ số (học sinh vắng liền gắn trên thẻ Chuyên cần), nhóm Chuyên cần (lớp so với khối, cơ cấu điểm danh)
 * và nhóm Rèn luyện (nộp bài 8 tuần, theo môn).
 */
export function HomeroomPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const classId = params.get('classId') ? Number(params.get('classId')) : undefined;
  const q = useQuery({ queryKey: ['adm', 'homeroom', classId], queryFn: () => admApi.homeroom(classId) });
  const d = q.data;
  const go = (path: string) => (user && isPathAllowed(path, user) ? () => navigate(path) : undefined);

  if (q.isLoading || !d) return <Skeleton active paragraph={{ rows: 10 }} />;

  const leave = go('/par/leave-requests');

  return (
    <div className="bd">
      <header className="bd-head">
        <h1>Tình hình lớp {d.className}</h1>
        {d.classes.length > 1 && (
          <div className="bd-head__tools">
            <Seg label="Chọn lớp" value={d.classId} onChange={(v) => setParams({ classId: String(v) })}
              options={d.classes.map((c) => ({ value: c.id, label: `Lớp ${c.name}` }))} />
          </div>
        )}
      </header>

      <div className="bd-tiles">
        <Tile hue="mint" label="Sĩ số hôm nay" unit={d.presentToday === null ? ' học sinh' : undefined}
          value={d.presentToday === null ? <Count value={d.studentCount} /> : <><Count value={d.presentToday} /><small>/{d.studentCount}</small></>}
          foot={d.todaySessions === 0 ? 'hôm nay lớp không có tiết' : d.presentToday === null ? 'chưa tiết nào được điểm danh' : 'học sinh có mặt'} />
        <RateTile hue="sky" label={d.gradeRate === null ? 'Chuyên cần' : `Chuyên cần · khối ${pct(d.gradeRate)}`} k={d.attendance} higherIsBetter empty="chưa có điểm danh"
          alert={d.streakStudents ? { text: `${d.streakStudents} học sinh vắng liền`, onClick: go('/sch/homeroom-students') } : null} />
        <Tile hue="lilac" label="Đơn nghỉ chờ duyệt" value={<Count value={d.pendingLeave} />} unit=" đơn" onClick={leave}
          foot={d.pendingLeave ? 'bấm để duyệt' : 'không có đơn đang chờ'} />
        <RateTile hue="aqua" label="Nộp bài rèn luyện" k={d.submission} higherIsBetter empty="chưa có bài hết hạn" />
      </div>

      <Section title="Chuyên cần" />
      <div className="bd-row bd-row--21">
        <TrendPanel title="Chuyên cần 8 tuần" meta="lớp so với trung bình khối" weekStarts={d.weekStarts} weekNo={d.weekNo} values={d.attendance.weeks}
          name={`Lớp ${d.className}`} base={{ name: `Khối ${d.gradeLevel}`, values: d.gradeWeeks }} threshold={ATTENDANCE_ALERT} />
        <MixPanel mix={d.mix} />
      </div>

      <Section title="Rèn luyện" />
      <div className="bd-row bd-row--21">
        <TrendPanel title="Tỷ lệ nộp bài 8 tuần" meta="theo tuần hết hạn" weekStarts={d.weekStarts} weekNo={d.weekNo}
          values={d.submission.weeks} name={`Lớp ${d.className}`} threshold={SUBMISSION_ALERT} />
        <AssignmentPanel a={d.assignments} groupLabel="môn" rows={d.subjects.map((s) => ({ key: String(s.subjectId), label: s.subjectName, value: s.rate }))} />
      </div>
    </div>
  );
}
