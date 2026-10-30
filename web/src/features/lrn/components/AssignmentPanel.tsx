import { useQuery } from '@tanstack/react-query';
import { Drawer, Skeleton } from 'antd';
import { Clock3, ListChecks } from 'lucide-react';
import { useMemo, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { formatDateTime } from '@/shared/lib/format';
import { lrnApi } from '../api';
import { durationText, fmtScore } from '../time';
import { AiBubble } from './MsmsAi';
import { bandColor, subjectVars } from './tone';
import { ResultView } from './ResultView';
import './lrn.css';

type Tab = 'students' | 'paper';
type Sort = 'name' | 'low';

/** Một học sinh trong ngăn kéo: kết quả đầy đủ, hỏi lại vài giây một lần khi phân tích AI còn đang chạy. */
function StudentDrawer({ assignmentId, studentId, onClose }: { assignmentId: number; studentId: number | null; onClose: () => void }) {
  const q = useQuery({
    queryKey: ['lrn', 'student-result', assignmentId, studentId], enabled: studentId !== null, retry: false,
    queryFn: () => lrnApi.assignments.studentResult(assignmentId, studentId!),
    refetchInterval: (query) => (query.state.data?.analysis?.status === 'Pending' ? 3000 : false),
  });
  return (
    <Drawer open={studentId !== null} onClose={onClose} size={760} title={q.data?.studentName ?? 'Kết quả của học sinh'} destroyOnHidden>
      {q.isLoading ? <Skeleton active paragraph={{ rows: 8 }} /> : q.isError ? (
        <AiBubble tone="warn" tail="none">{getErrorMessage(q.error)}</AiBubble>
      ) : q.data ? <ResultView data={q.data} viewer="teacher" /> : null}
    </Drawer>
  );
}

/**
 * Chi tiết một bài đã giao (4.6.10, 4.6.17): tình hình nộp bài từng học sinh và đề bài. Chỉ đọc, dùng chung cho giáo viên phụ trách
 * và tổ trưởng. Bấm một học sinh đã nộp để xem đáp án và phân tích AI của em đó.
 */
export function AssignmentPanel({ id }: { id: number }) {
  const [tab, setTab] = useState<Tab>('students');
  const [sort, setSort] = useState<Sort>('name');
  const [student, setStudent] = useState<number | null>(null);

  const detail = useQuery({ queryKey: ['lrn', 'assignment', id], queryFn: () => lrnApi.assignments.get(id), retry: false });
  const results = useQuery({ queryKey: ['lrn', 'assignment-results', id], queryFn: () => lrnApi.assignments.results(id), retry: false });

  const rows = useMemo(() => {
    const list = [...(results.data?.rows ?? [])];
    if (sort === 'low') list.sort((a, b) => (a.score ?? 99) - (b.score ?? 99));
    return list;
  }, [results.data, sort]);

  if (detail.isLoading || results.isLoading) return <Skeleton active paragraph={{ rows: 8 }} />;
  if (detail.isError || !detail.data || !results.data) {
    return <AiBubble tone="warn" tail="none">{getErrorMessage(detail.error ?? results.error)}</AiBubble>;
  }

  const a = detail.data.assignment;
  const r = results.data;
  const pct = r.recipientCount ? Math.round((r.submittedCount / r.recipientCount) * 100) : 0;

  return (
    <div className="ap" style={subjectVars(a.subjectId)}>
      <header className="ap__head">
        <span className="chip chip--subject">{a.subjectName} · lớp {a.className}</span>
        <h2>{a.title}</h2>
        <p className="ap__meta">
          <span><Clock3 size={14} />Hạn nộp {formatDateTime(a.dueAt)}</span>
          <span><ListChecks size={14} />{a.questionCount} câu · {durationText(a.durationMinutes)}</span>
          <span>Giao bởi {a.assignedByName}</span>
        </p>
        {detail.data.instruction && <p className="ap__note">{detail.data.instruction}</p>}
      </header>

      <div className="ap__figs">
        <div><strong className="tabular">{r.submittedCount}<small>/{r.recipientCount}</small></strong><span>đã nộp · {pct}%</span><i><b style={{ width: `${pct}%` }} /></i></div>
        <div><strong className="tabular" style={{ color: bandColor(r.averageScore) }}>{fmtScore(r.averageScore)}</strong><span>điểm trung bình</span></div>
        <div><strong className="tabular">{a.isClosed ? 'Đã đóng' : 'Đang mở'}</strong><span>{a.isClosed ? 'quá hạn nộp' : 'còn nhận bài'}</span></div>
      </div>

      <div className="ap__tabs" role="tablist">
        <button type="button" role="tab" aria-selected={tab === 'students'} className={tab === 'students' ? 'is-on' : ''} onClick={() => setTab('students')}>Học sinh</button>
        <button type="button" role="tab" aria-selected={tab === 'paper'} className={tab === 'paper' ? 'is-on' : ''} onClick={() => setTab('paper')}>Đề bài</button>
        {tab === 'students' && (
          <span className="ap__sort">
            <button type="button" className={sort === 'name' ? 'is-on' : ''} onClick={() => setSort('name')}>Theo tên</button>
            <button type="button" className={sort === 'low' ? 'is-on' : ''} onClick={() => setSort('low')}>Điểm thấp trước</button>
          </span>
        )}
      </div>

      {tab === 'students' ? (
        <ul className="roster">
          {rows.map((s) => (
            <li key={s.studentId}>
              <button type="button" disabled={!s.submitted} onClick={() => setStudent(s.studentId)} className={s.submitted ? '' : 'is-none'}>
                <span className="roster__who"><strong>{s.studentName}</strong><em>{s.studentCode}</em></span>
                <span className="roster__bar" aria-hidden>{s.submitted && <b style={{ width: `${(s.score ?? 0) * 10}%`, background: bandColor(s.score) }} />}</span>
                <span className="roster__score tabular" style={{ color: s.submitted ? bandColor(s.score) : undefined }}>
                  {s.submitted ? fmtScore(s.score) : a.isClosed ? 'Không nộp' : 'Chưa nộp'}
                </span>
                <span className="roster__ai">{s.analysisStatus === 'Done' ? 'Có phân tích' : s.analysisStatus === 'Pending' ? 'Đang phân tích' : ''}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <ol className="paper">
          {detail.data.questions.map((q) => (
            <li key={q.assignmentQuestionId}>
              <header><b>Câu {q.orderNo}</b><span className="tabular">{fmtScore(q.points)} điểm{q.isMultiple ? ' · chọn nhiều' : ''}</span></header>
              <p>{q.content}</p>
              <ul>{q.options.map((o) => <li key={o.optionId} className={o.isCorrect ? 'is-correct' : ''}><b>{o.label}</b>{o.content}</li>)}</ul>
            </li>
          ))}
        </ol>
      )}

      <StudentDrawer assignmentId={id} studentId={student} onClose={() => setStudent(null)} />
    </div>
  );
}
