import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useMemo } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { lrnApi } from '../api';
import { AssignmentPanel } from '../components/AssignmentPanel';
import { AiEmpty } from '../components/parts';
import { subjectVars } from '../components/tone';
import { useNow } from '../hooks';
import { dueHint } from '../time';
import type { AssignmentItem } from '../types';
import '../components/lrn.css';

/** Một vòng tròn nhỏ: phần đã nộp trên tổng số học sinh của bài. */
function Pie({ done, total }: { done: number; total: number }) {
  const r = 15;
  const c = 2 * Math.PI * r;
  const pct = total ? done / total : 0;
  return (
    <span className="pie" aria-label={`${done} trên ${total} học sinh đã nộp`}>
      <svg width="40" height="40" viewBox="0 0 40 40" aria-hidden>
        <circle cx="20" cy="20" r={r} fill="none" stroke="var(--c-neutralBg)" strokeWidth="5" />
        <circle cx="20" cy="20" r={r} fill="none" stroke="var(--c-green500)" strokeWidth="5" strokeLinecap="round" strokeDasharray={`${c * pct} ${c}`} transform="rotate(-90 20 20)" />
      </svg>
      <b className="tabular">{done}</b>
    </span>
  );
}

/**
 * Bài tập đã giao (giáo viên): danh sách bên trái, chi tiết bài đang chọn bên phải. Mã bài nằm trên đường dẫn (?id=) để chia sẻ được
 * và để giữ chỗ khi vừa giao xong. Bài đã giao chỉ xem, không sửa, không xóa (BR-21).
 */
export function AssignedPage() {
  const [params, setParams] = useSearchParams();
  const nowMs = useNow(0, 60_000);
  const list = useQuery({ queryKey: ['lrn', 'assignments'], queryFn: lrnApi.assignments.list });
  const items = useMemo(() => list.data ?? [], [list.data]);
  const open = items.filter((a) => !a.isClosed);
  const closed = items.filter((a) => a.isClosed);

  const wanted = Number(params.get('id')) || null;
  const selected = items.find((a) => a.assignmentId === wanted) ?? open[0] ?? closed[0];

  const card = (a: AssignmentItem) => (
    <li key={a.assignmentId}>
      <button
        type="button" className={`aitem${selected?.assignmentId === a.assignmentId ? ' is-on' : ''}`} style={subjectVars(a.subjectId)}
        onClick={() => setParams({ id: String(a.assignmentId) }, { replace: true })}
      >
        <Pie done={a.submittedCount} total={a.recipientCount} />
        <span className="aitem__text">
          <strong>{a.title}</strong>
          <span>{a.subjectName} · lớp {a.className}</span>
        </span>
        <span className={`aitem__due aitem__due--${a.isClosed ? 'late' : dueHint(a.dueAt, nowMs).tone}`}>{a.isClosed ? 'Đã đóng' : dueHint(a.dueAt, nowMs).text}</span>
      </button>
    </li>
  );

  return (
    <div className="assigned">
      <aside className="assigned__list">
        <header>
          <h1>Bài tập đã giao</h1>
          <Link to="/lrn/assign" className="linkbtn">+ Giao bài mới</Link>
        </header>
        {list.isLoading ? <Skeleton active paragraph={{ rows: 6 }} /> : items.length === 0 ? (
          <AiEmpty title="Chưa giao bài nào" hint="Giao bài đầu tiên ở mục Giao bài tập." />
        ) : (
          <>
            {open.length > 0 && <><h3>Đang mở · {open.length}</h3><ul>{open.map(card)}</ul></>}
            {closed.length > 0 && <><h3>Đã đóng · {closed.length}</h3><ul>{closed.map(card)}</ul></>}
          </>
        )}
      </aside>
      <section className="assigned__detail">
        {selected ? <AssignmentPanel key={selected.assignmentId} id={selected.assignmentId} /> : !list.isLoading && (
          <AiEmpty title="Chọn một bài để xem" hint="Điểm từng học sinh và phân tích AI sẽ hiện ở đây." />
        )}
      </section>
    </div>
  );
}
