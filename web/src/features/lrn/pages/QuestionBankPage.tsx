import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Input, Select } from 'antd';
import { Check, ChevronDown, EyeOff, Pencil, Plus, Trash2 } from 'lucide-react';
import { useMemo, useState } from 'react';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { lrnApi } from '../api';
import { AiComposer } from '../components/AiComposer';
import { AiEmpty } from '../components/parts';
import { subjectVars } from '../components/tone';
import { QuestionEditor } from '../components/QuestionEditor';
import { useDebounced, useSubjects } from '../hooks';
import { GRADE_OPTIONS } from '../labels';
import type { QuestionDetail, QuestionItem, QuestionQuery } from '../types';
import '../components/lrn.css';

type Tab = 'all' | 'mine' | 'draft';
const PAGE = 15;

/** Câu hỏi mở rộng: lấy chi tiết (có đáp án) khi giáo viên bấm vào một dòng. */
function Expanded({ id }: { id: number }) {
  const q = useQuery({ queryKey: ['lrn', 'questions', 'detail', id], queryFn: () => lrnApi.questions.get(id) });
  if (!q.data) return <div className="qrow__detail qrow__detail--wait">Đang tải…</div>;
  return (
    <div className="qrow__detail">
      <ul>
        {q.data.options.map((o) => (
          <li key={o.optionId} className={o.isCorrect ? 'is-correct' : ''}><b>{o.label}</b><span>{o.content}</span>{o.isCorrect && <em>Đáp án đúng</em>}</li>
        ))}
      </ul>
      {q.data.explanation && <p className="qrow__why"><strong>Giải thích: </strong>{q.data.explanation}</p>}
    </div>
  );
}

/**
 * Kho câu hỏi (LRN-01, BR-20): dùng chung toàn trường, chỉ người tạo sửa; câu đã nằm trong bài tập chỉ còn ẩn khỏi kho.
 * Bên trái là quầy soạn của MSMS AI, bên phải là danh sách; nháp AI nằm riêng ở tab Nháp cho tới khi giáo viên duyệt.
 */
export function QuestionBankPage() {
  const qc = useQueryClient();
  const subjects = useSubjects();
  const [tab, setTab] = useState<Tab>('all');
  const [subjectId, setSubjectId] = useState<number | undefined>();
  const [gradeLevel, setGradeLevel] = useState<number | undefined>();
  const [search, setSearch] = useState('');
  const dSearch = useDebounced(search);
  const [open, setOpen] = useState<number | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<QuestionDetail | null>(null);

  const base: QuestionQuery = { subjectId, gradeLevel, search: dSearch || undefined, mine: tab !== 'all', status: tab === 'draft' ? 'Draft' : tab === 'all' ? 'Ready' : undefined };
  const list = useInfiniteQuery({
    queryKey: ['lrn', 'questions', 'list', base],
    initialPageParam: 1,
    queryFn: ({ pageParam }) => lrnApi.questions.list({ ...base, page: pageParam, pageSize: PAGE }),
    getNextPageParam: (last, pages) => (pages.flatMap((p) => p.items).length < last.total ? pages.length + 1 : undefined),
  });
  const items = useMemo(() => list.data?.pages.flatMap((p) => p.items) ?? [], [list.data]);
  const total = list.data?.pages[0]?.total ?? 0;

  const draftCount = useQuery({ queryKey: ['lrn', 'questions', 'draft-count'], queryFn: () => lrnApi.questions.list({ mine: true, status: 'Draft', pageSize: 1 }) });

  const refresh = () => qc.invalidateQueries({ queryKey: ['lrn', 'questions'] });
  const confirmDraft = useMutation({
    mutationFn: (id: number) => lrnApi.questions.confirm(id),
    onSuccess: () => { void refresh(); message.success('Đã duyệt: câu hỏi đã vào kho, dùng được cho bài tập.'); },
    onError: (e) => showError(e),
  });
  const hide = useMutation({
    mutationFn: (id: number) => lrnApi.questions.hide(id),
    onSuccess: () => { void refresh(); message.success('Đã ẩn câu hỏi khỏi kho. Các bài tập cũ không bị ảnh hưởng.'); },
    onError: (e) => showError(e),
  });
  const removeDraft = useMutation({
    mutationFn: (id: number) => lrnApi.questions.removeDraft(id),
    onSuccess: () => { void refresh(); message.success('Đã xóa câu nháp.'); },
    onError: (e) => showError(e),
  });

  const edit = async (id: number) => {
    try { setEditing(await lrnApi.questions.get(id)); setFormOpen(true); } catch (e) { showError(e); }
  };

  const drafts = draftCount.data?.total ?? 0;
  const tabs: { key: Tab; label: string }[] = [
    { key: 'all', label: 'Kho chung' }, { key: 'mine', label: 'Của tôi' }, { key: 'draft', label: drafts > 0 ? `Nháp · ${drafts}` : 'Nháp' },
  ];

  return (
    <div className="bank">
      <aside className="bank__studio">
        <AiComposer subjects={subjects.active} defaultSubjectId={subjectId} onDone={() => setTab('draft')} />
      </aside>

      <section className="bank__main">
        <header className="bank__head">
          <h1>Kho câu hỏi</h1>
          <button type="button" className="linkbtn" onClick={() => { setEditing(null); setFormOpen(true); }}><Plus size={16} />Soạn tay</button>
        </header>

        <div className="bank__tabs" role="tablist">
          {tabs.map((t) => (
            <button key={t.key} type="button" role="tab" aria-selected={tab === t.key} className={tab === t.key ? 'is-on' : ''} onClick={() => setTab(t.key)}>{t.label}</button>
          ))}
          <span className="bank__filters">
            <Input.Search allowClear placeholder="Tìm câu hỏi" value={search} onChange={(e) => setSearch(e.target.value)} style={{ width: 190 }} />
            <Select allowClear placeholder="Môn" value={subjectId} onChange={setSubjectId} style={{ width: 130 }} showSearch optionFilterProp="label"
              options={subjects.active.map((s) => ({ value: s.subjectId, label: s.name }))} />
            <Select allowClear placeholder="Khối" value={gradeLevel} onChange={setGradeLevel} style={{ width: 92 }} options={GRADE_OPTIONS} />
          </span>
        </div>

        {list.isLoading ? <div className="bank__empty">Đang tải…</div> : items.length === 0 ? (
          <AiEmpty
            title={tab === 'draft' ? 'Chưa có câu nháp nào' : 'Chưa có câu hỏi nào'}
            hint={tab === 'draft' ? 'Nhờ MSMS AI soạn ở khung bên trái, các câu mới sẽ nằm ở đây chờ thầy cô duyệt.' : 'Thử bỏ bớt bộ lọc, hoặc nhờ MSMS AI soạn câu mới.'}
          />
        ) : (
          <ul className="qlist">
            {items.map((q: QuestionItem) => {
              const expanded = open === q.questionId;
              return (
                <li key={q.questionId} className={`qrow${expanded ? ' is-open' : ''}${q.status === 'Draft' ? ' is-draft' : ''}`} style={subjectVars(q.subjectId)}>
                  <button type="button" className="qrow__main" onClick={() => setOpen(expanded ? null : q.questionId)} aria-expanded={expanded}>
                    <span className="qrow__text">{q.content}</span>
                    <span className="qrow__chips">
                      <span className="chip chip--subject">{q.subjectName}</span>
                      {q.gradeLevel && <span className="chip">Khối {q.gradeLevel}</span>}
                      {q.topic && <span className="chip">{q.topic}</span>}
                      {q.isMultiple && <span className="chip chip--warn">Chọn nhiều</span>}
                      {q.isAiGenerated && <span className="chip chip--ai">MSMS AI soạn</span>}
                      {q.isLocked && <span className="chip">Đã dùng trong bài tập</span>}
                      {!q.isMine && <span className="chip chip--quiet">{q.createdByName}</span>}
                    </span>
                    <ChevronDown size={18} className="qrow__chev" />
                  </button>
                  {q.isMine && (
                    <span className="qrow__acts">
                      {q.status === 'Draft' && (
                        <button type="button" className="act act--ok" onClick={() => confirmDraft.mutate(q.questionId)}><Check size={15} />Duyệt</button>
                      )}
                      {!q.isLocked && <button type="button" className="act" aria-label="Sửa câu hỏi" onClick={() => void edit(q.questionId)}><Pencil size={15} /></button>}
                      {q.status === 'Ready' && !q.isHidden && (
                        <button
                          type="button" className="act" aria-label="Ẩn khỏi kho"
                          onClick={() => confirmAction({
                            title: 'Ẩn câu hỏi khỏi kho?',
                            content: 'Giáo viên khác không còn thấy câu này để đưa vào bài mới. Các bài đã có câu này không bị ảnh hưởng. Muốn dùng lại thì phải soạn câu mới.',
                            okText: 'Ẩn khỏi kho', onOk: () => hide.mutateAsync(q.questionId).catch(() => undefined),
                          })}
                        ><EyeOff size={15} /></button>
                      )}
                      {q.status === 'Draft' && (
                        <button
                          type="button" className="act act--danger" aria-label="Xóa câu nháp"
                          onClick={() => confirmAction({
                            title: 'Xóa câu nháp?', content: 'Câu nháp sẽ bị xóa hẳn, không khôi phục được.', okText: 'Xóa', danger: true,
                            onOk: () => removeDraft.mutateAsync(q.questionId).catch(() => undefined),
                          })}
                        ><Trash2 size={15} /></button>
                      )}
                    </span>
                  )}
                  {expanded && <Expanded id={q.questionId} />}
                </li>
              );
            })}
          </ul>
        )}

        {items.length > 0 && (
          <footer className="bank__more">
            <span>Đang hiện {items.length}/{total} câu</span>
            {list.hasNextPage && <button type="button" className="linkbtn" disabled={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()}>Xem thêm</button>}
          </footer>
        )}
      </section>

      <QuestionEditor open={formOpen} editing={editing} subjects={subjects.active} defaultSubjectId={subjectId} onClose={() => setFormOpen(false)} />
    </div>
  );
}
