import { useQuery } from '@tanstack/react-query';
import { Dropdown, Skeleton } from 'antd';
import dayjs from 'dayjs';
import { ArrowRight, ChevronDown, Filter, Lock } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { formatDateTime } from '@/shared/lib/format';
import { todoLine } from '../aiLines';
import { lrnApi } from '../api';
import { MsmsAi } from '../components/MsmsAi';
import { AiEmpty } from '../components/parts';
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

  // Bài vừa qua hạn khi trang đang mở không còn là việc cần làm, dù danh sách chưa tải lại từ máy chủ.
  const todo = useMemo(
    () => (list.data ?? []).filter((a) => isTodo(a) && dayjs(a.dueAt).valueOf() > nowMs).sort((a, b) => a.dueAt.localeCompare(b.dueAt)),
    [list.data, nowMs],
  );

  const subjects = useMemo(() => {
    const map = new Map<number, { id: number; name: string; count: number }>();
    for (const a of todo) {
      const cur = map.get(a.subjectId);
      map.set(a.subjectId, { id: a.subjectId, name: a.subjectName, count: (cur?.count ?? 0) + 1 });
    }
    return [...map.values()];
  }, [todo]);
  const [subjectId, setSubjectId] = useState<number | null>(null);
  const activeSubject = subjects.some((s) => s.id === subjectId) ? subjectId : null;
  const shown = useMemo(() => (activeSubject == null ? todo : todo.filter((a) => a.subjectId === activeSubject)), [todo, activeSubject]);

  const groups = useMemo(() => {
    const map = new Map<string, MyAssignmentItem[]>();
    for (const a of shown) {
      const key = dayLabel(a.dueAt, nowMs);
      map.set(key, [...(map.get(key) ?? []), a]);
    }
    return [...map.entries()];
  }, [shown, nowMs]);

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

      <div className="todo__bar">
        <span>{shown.length} bài{activeSubject != null ? ` · ${subjects.find((s) => s.id === activeSubject)?.name}` : ''}</span>
        <Dropdown
          trigger={['click']} placement="bottomRight"
          menu={{
            selectable: true,
            selectedKeys: [String(activeSubject ?? 'all')],
            onClick: ({ key }) => setSubjectId(key === 'all' ? null : Number(key)),
            items: [
              { key: 'all', label: `Tất cả môn (${todo.length})` },
              ...subjects.map((s) => ({ key: String(s.id), label: `${s.name} (${s.count})` })),
            ],
          }}
        >
          <button type="button" className={`todo__filterbtn${activeSubject != null ? ' is-on' : ''}`}>
            <Filter size={16} />Lọc theo môn<ChevronDown size={14} />
          </button>
        </Dropdown>
      </div>

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
                  const m = /(\d+) (\S+)$/.exec(hint.text);
                  const stub = m ? { big: m[1], unit: m[2] } : { big: '!', unit: 'quá hạn' };
                  return (
                    <div key={a.assignmentId} className={`ticket ticket--${locked ? 'locked' : hint.tone}`} style={subjectVars(a.subjectId)}>
                      <button type="button" className="task" disabled={locked} onClick={() => navigate(`/lrn/todo/${a.assignmentId}`)}>
                        <span className="task__stub">
                          {locked ? <Lock size={26} /> : <>
                            <b>{stub.big}</b>
                            <small>{stub.unit}</small>
                          </>}
                        </span>
                        <span className="task__body">
                          <span className="task__subject">{a.subjectName}<em>{a.className}</em></span>
                          <strong className="task__title">{a.title}</strong>
                          <span className="task__meta">
                            {a.questionCount} câu <i /> {durationText(a.durationMinutes)}
                            {locked && <> <i /> Mở lúc {formatDateTime(a.openAt)}</>}
                          </span>
                        </span>
                        {!locked && (
                          <span className="task__go" aria-label={a.state === 'InProgress' ? 'Làm tiếp' : 'Làm bài'}>
                            {a.state === 'InProgress' && <em>Làm tiếp</em>}<ArrowRight size={20} />
                          </span>
                        )}
                      </button>
                    </div>
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
