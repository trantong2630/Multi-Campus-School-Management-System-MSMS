import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { ArrowRight, Clock3, ListChecks } from 'lucide-react';
import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { formatDateTime } from '@/shared/lib/format';
import { todoLine } from '../aiLines';
import { lrnApi } from '../api';
import { MsmsAi } from '../components/MsmsAi';
import { AiEmpty, SubjectMark } from '../components/parts';
import { subjectVars } from '../components/tone';
import { useNow } from '../hooks';
import type { MyAssignmentItem } from '../types';
import { dayLabel, dueHint, durationText } from '../time';
import '../components/lrn.css';

const isTodo = (a: MyAssignmentItem) => a.state === 'Open' || a.state === 'InProgress' || a.state === 'NotOpen';

/**
 * Bài tập được giao (học sinh): những bài chưa nộp còn hạn, xếp theo hạn nộp gần nhất và nhóm theo ngày. Bấm một bài là vào làm ngay;
 * bài có giới hạn thời gian sẽ có bước Bắt đầu ở trang làm bài (4.6.12).
 */
export function TodoPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const nowMs = useNow(0, 30_000);
  const list = useQuery({ queryKey: ['lrn', 'my-assignments'], queryFn: lrnApi.student.list });

  const todo = useMemo(
    () => (list.data ?? []).filter(isTodo).sort((a, b) => a.dueAt.localeCompare(b.dueAt)),
    [list.data],
  );

  const groups = useMemo(() => {
    const map = new Map<string, MyAssignmentItem[]>();
    for (const a of todo) {
      const key = dayLabel(a.dueAt, nowMs);
      map.set(key, [...(map.get(key) ?? []), a]);
    }
    return [...map.entries()];
  }, [todo, nowMs]);

  const first = todo.find((a) => a.state !== 'NotOpen');
  const firstHint = first ? dueHint(first.dueAt, nowMs) : null;
  const urgent = firstHint?.tone === 'soon';
  const greeting = `Chào ${user?.fullName.split(' ').slice(-1)[0] ?? 'bạn'}!`;

  return (
    <div className="todo">
      <header className="todo__hero">
        <div>
          <h1>Bài tập được giao</h1>
          <p>{greeting} {todo.length > 0 ? `Bạn có ${todo.length} bài đang chờ.` : 'Hôm nay bạn rảnh rồi.'}</p>
        </div>
        <MsmsAi
          mood={todo.length === 0 ? 'happy' : urgent ? 'warn' : 'idle'} tone={urgent ? 'warn' : 'plain'} size={88}
          say={todoLine({ total: todo.length, soonest: first && firstHint ? { title: first.title, hint: firstHint.text } : undefined })}
        />
      </header>

      {list.isLoading ? <Skeleton active paragraph={{ rows: 5 }} /> : groups.length === 0 ? (
        <AiEmpty mood="happy" title="Không có bài nào cần làm" hint="Khi giáo viên giao bài mới, mình sẽ báo cho bạn ở đây." />
      ) : (
        <div className="todo__timeline">
          {groups.map(([label, items]) => (
            <section key={label} className="todo__day">
              <h2 className={`todo__daylabel${label === 'Hôm nay' ? ' is-today' : ''}`}><span>{label}</span></h2>
              <div className="todo__items">
                {items.map((a) => {
                  const hint = dueHint(a.dueAt, nowMs);
                  const locked = a.state === 'NotOpen';
                  return (
                    <button
                      key={a.assignmentId} type="button" className={`task${locked ? ' is-locked' : ''}`} style={subjectVars(a.subjectId)}
                      disabled={locked} onClick={() => navigate(`/lrn/todo/${a.assignmentId}`)}
                    >
                      <SubjectMark id={a.subjectId} name={a.subjectName} size={52} />
                      <span className="task__main">
                        <strong>{a.title}</strong>
                        <span className="task__meta">
                          {a.subjectName} · {a.className}
                          <span><ListChecks size={14} />{a.questionCount} câu</span>
                          <span><Clock3 size={14} />{durationText(a.durationMinutes)}</span>
                        </span>
                      </span>
                      <span className="task__side">
                        {locked ? (
                          <span className="task__due">Mở lúc {formatDateTime(a.openAt)}</span>
                        ) : (
                          <>
                            <span className={`task__due task__due--${hint.tone}`}>{hint.text}</span>
                            <span className="task__go">{a.state === 'InProgress' ? 'Làm tiếp' : 'Làm bài'}<ArrowRight size={16} /></span>
                          </>
                        )}
                      </span>
                    </button>
                  );
                })}
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}
