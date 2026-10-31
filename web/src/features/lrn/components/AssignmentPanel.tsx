import { useQuery } from '@tanstack/react-query';
import { Drawer, Skeleton } from 'antd';
import { Clock3, ListChecks, Search, Sparkles, UserRound } from 'lucide-react';
import { useMemo, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { PersonAvatar } from '@/shared/components/PersonAvatar';
import { formatDateTime } from '@/shared/lib/format';
import { lrnApi } from '../api';
import { durationText, fmtScore } from '../time';
import { AiBubble } from './MsmsAi';
import { bandColor, subjectVars } from './tone';
import { ResultView } from './ResultView';
import './lrn.css';

type Tab = 'students' | 'paper';
/** Bỏ dấu và chữ hoa để gõ "minh anh" vẫn ra "Đỗ Minh Anh". */
const fold = (v: string) => v.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase();

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
  const [search, setSearch] = useState('');
  const [student, setStudent] = useState<number | null>(null);

  const detail = useQuery({ queryKey: ['lrn', 'assignment', id], queryFn: () => lrnApi.assignments.get(id), retry: false });
  const results = useQuery({ queryKey: ['lrn', 'assignment-results', id], queryFn: () => lrnApi.assignments.results(id), retry: false });

  const rows = useMemo(() => {
    const key = fold(search.trim());
    const list = results.data?.rows ?? [];
    return key ? list.filter((s) => fold(`${s.studentName} ${s.studentCode}`).includes(key)) : list;
  }, [results.data, search]);

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
        <div className="ap__class">
          <div>
            <strong>Lớp {a.className}</strong>
            <span>{a.subjectName}</span>
          </div>
          <em>{a.isClosed ? 'Đã đóng' : 'Đang mở'}</em>
        </div>
        <h2>{a.title}</h2>
        <dl className="ap__meta">
          <div><dt><Clock3 size={14} />Hạn nộp</dt><dd>{formatDateTime(a.dueAt)}</dd></div>
          <div><dt><ListChecks size={14} />Đề bài</dt><dd>{a.questionCount} câu · {durationText(a.durationMinutes)}</dd></div>
          <div><dt><UserRound size={14} />Giao bởi</dt><dd>{a.assignedByName}</dd></div>
        </dl>
        {detail.data.instruction && <p className="ap__note">{detail.data.instruction}</p>}
      </header>

      <div className="ap__figs">
        <div><strong className="tabular">{r.submittedCount}<small>/{r.recipientCount}</small></strong><span>đã nộp · {pct}%</span><i><b style={{ width: `${pct}%` }} /></i></div>
        <div><strong className="tabular" style={{ color: bandColor(r.averageScore) }}>{fmtScore(r.averageScore)}</strong><span>điểm trung bình</span></div>
      </div>

      <div className="ap__tabs" role="tablist">
        <button type="button" role="tab" aria-selected={tab === 'students'} className={tab === 'students' ? 'is-on' : ''} onClick={() => setTab('students')}>Học sinh</button>
        <button type="button" role="tab" aria-selected={tab === 'paper'} className={tab === 'paper' ? 'is-on' : ''} onClick={() => setTab('paper')}>Đề bài</button>
        {tab === 'students' && (
          <label className="ap__search">
            <Search size={14} aria-hidden />
            <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Tìm học sinh" aria-label="Tìm học sinh" />
          </label>
        )}
      </div>

      {tab === 'students' ? (
        <ul className="roster">
          {rows.length === 0 && <li className="roster__none">Không tìm thấy học sinh nào.</li>}
          {rows.map((s) => (
            <li key={s.studentId}>
              <button type="button" disabled={!s.submitted} onClick={() => setStudent(s.studentId)} className={s.submitted ? '' : 'is-none'}>
                <span className="roster__who">
                  <PersonAvatar name={s.studentName} size={34} muted={!s.submitted} />
                  <span><strong>{s.studentName}</strong><em>{s.studentCode}</em></span>
                </span>
                <span className="roster__bar" aria-hidden>{s.submitted && <b style={{ width: `${(s.score ?? 0) * 10}%`, background: bandColor(s.score) }} />}</span>
                <span className="roster__score tabular" style={{ color: s.submitted ? bandColor(s.score) : undefined }}>
                  {s.submitted ? fmtScore(s.score) : a.isClosed ? 'Không nộp' : 'Chưa nộp'}
                </span>
                {s.analysisStatus === 'Done' || s.analysisStatus === 'Pending' ? (
                  <span className="chip chip--ai roster__ai"><Sparkles size={12} />{s.analysisStatus === 'Done' ? 'Có phân tích' : 'Đang phân tích'}</span>
                ) : <span />}
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
