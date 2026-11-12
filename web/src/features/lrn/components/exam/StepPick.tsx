import { useInfiniteQuery } from '@tanstack/react-query';
import { ConfigProvider, Input } from 'antd';
import { Check, ChevronDown, ListChecks, Plus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { FILTER_THEME } from '@/shared/components/FilterBar';
import { lrnApi } from '../../api';
import { useDebounced } from '../../hooks';
import { DIFFICULTIES, GRADES } from '../../labels';
import type { Difficulty, QuestionItem } from '../../types';
import { DifficultyTag } from '../DifficultyTag';
import { ExamMatrix } from '../ExamMatrix';
import { QuestionPreview } from '../QuestionPreview';
import '../lrn.css';

interface Props {
  subjectId: number;
  grade: number;
  lines: QuestionItem[];
  onLines: (next: QuestionItem[]) => void;
}

/**
 * Bước 2: chọn câu cho đề, bằng tay (lọc theo khối, độ khó, chủ đề) hoặc dựng theo ma trận bài × độ khó. Cả hai cùng ghi vào một danh sách
 * câu của đề; câu chưa phân loại độ khó bị khóa vì đề chấm điểm theo phần độ khó.
 */
export function StepPick({ subjectId, grade, lines, onLines }: Props) {
  const [mode, setMode] = useState<'pick' | 'matrix'>('matrix');
  const [search, setSearch] = useState('');
  const dSearch = useDebounced(search);
  // undefined: theo khối của đề; null: mọi khối; số: khối do giáo viên chọn ở danh sách.
  const [pickedGrade, setPickedGrade] = useState<number | null | undefined>(undefined);
  const [difficulty, setDifficulty] = useState<Difficulty | null>(null);
  const [peekId, setPeekId] = useState<number | null>(null);
  const listGrade = pickedGrade === undefined ? grade : pickedGrade ?? undefined;

  const bank = useInfiniteQuery({
    queryKey: ['lrn', 'questions', 'picker', subjectId, listGrade, dSearch, difficulty],
    enabled: mode === 'pick', initialPageParam: 1,
    queryFn: ({ pageParam }) => lrnApi.questions.list({ subjectId, status: 'Ready', gradeLevel: listGrade, difficulty: difficulty ?? undefined, topic: dSearch || undefined, page: pageParam, pageSize: 15 }),
    getNextPageParam: (last, pages) => (pages.flatMap((p) => p.items).length < last.total ? pages.length + 1 : undefined),
  });
  const pool = useMemo(() => bank.data?.pages.flatMap((p) => p.items) ?? [], [bank.data]);
  const picked = useMemo(() => new Set(lines.map((q) => q.questionId)), [lines]);

  const toggle = (q: QuestionItem) => {
    if (picked.has(q.questionId)) onLines(lines.filter((l) => l.questionId !== q.questionId));
    else if (q.difficulty !== null) onLines([...lines, q]);
  };

  return (
    <div className="xpick">
      <div className="xpick__bar">
        <div className="modeset" role="tablist" aria-label="Cách chọn câu">
          <button type="button" role="tab" aria-selected={mode === 'matrix'} className={mode === 'matrix' ? 'is-on' : ''} onClick={() => setMode('matrix')}>Theo ma trận đề</button>
          <button type="button" role="tab" aria-selected={mode === 'pick'} className={mode === 'pick' ? 'is-on' : ''} onClick={() => setMode('pick')}>Chọn tay</button>
        </div>
        {mode === 'pick' && (
          <div className="xpick__filters">
            <ConfigProvider theme={FILTER_THEME}><Input.Search allowClear placeholder="Tìm theo chủ đề, ví dụ: Phân số" value={search} onChange={(e) => setSearch(e.target.value)} style={{ width: 260 }} /></ConfigProvider>
            <div className="picker__grades" role="group" aria-label="Lọc theo khối">
              <span>Khối</span>
              {GRADES.map((g) => (
                <button key={g} type="button" className={`pill${listGrade === g ? ' is-on' : ''}`} aria-pressed={listGrade === g} onClick={() => setPickedGrade(listGrade === g ? null : g)}>{g}</button>
              ))}
            </div>
            <div className="picker__grades" role="group" aria-label="Lọc theo độ khó">
              <span>Độ khó</span>
              {DIFFICULTIES.map((d) => (
                <button key={d.value} type="button" className={`pill${difficulty === d.value ? ' is-on' : ''}`} aria-pressed={difficulty === d.value} onClick={() => setDifficulty(difficulty === d.value ? null : d.value)}>{d.label}</button>
              ))}
            </div>
          </div>
        )}
      </div>

      {mode === 'matrix' ? (
        <ExamMatrix key={`${subjectId}-${grade}`} subjectId={subjectId} grade={grade} onPick={(qs) => onLines(qs)} />
      ) : pool.length === 0 && !bank.isLoading ? (
        <p className="picker__none">Không có câu Sẵn dùng nào khớp. Thử đổi khối, bỏ bớt bộ lọc, hoặc soạn thêm ở mục Kho câu hỏi.</p>
      ) : (
        <>
          <ul className="picker">
            {pool.map((q) => {
              const on = picked.has(q.questionId);
              const unsorted = q.difficulty === null;
              return (
                <li key={q.questionId} className={`picker__item${on ? ' is-on' : ''}${peekId === q.questionId ? ' is-peek' : ''}${unsorted ? ' is-locked' : ''}`}>
                  <button type="button" className="picker__check" aria-pressed={on} disabled={unsorted && !on}
                    title={unsorted ? 'Câu chưa có độ khó nên chưa đưa vào đề được. Người soạn cần chọn độ khó ở Kho câu hỏi.' : undefined}
                    aria-label={unsorted ? 'Câu chưa phân loại độ khó, chưa chọn được' : on ? 'Bỏ chọn câu này' : 'Chọn câu này'} onClick={() => toggle(q)}>
                    {on ? <Check size={18} strokeWidth={3} /> : <Plus size={18} />}
                  </button>
                  <div className="picker__card">
                    <button type="button" className="picker__body" aria-expanded={peekId === q.questionId} onClick={() => setPeekId(peekId === q.questionId ? null : q.questionId)}>
                      <span className="picker__text">{q.content}</span>
                      <span className="picker__tags">
                        <DifficultyTag value={q.difficulty} short />
                        {q.gradeLevel && <span className="tagtext">Khối {q.gradeLevel}</span>}
                        {q.topic && <span className="tagtext">{q.topic}</span>}
                        {q.isMultiple && <span className="chip chip--warn"><ListChecks size={12} />Chọn nhiều</span>}
                      </span>
                      <ChevronDown size={18} className="picker__chev" aria-hidden />
                    </button>
                    {peekId === q.questionId && <QuestionPreview id={q.questionId} />}
                  </div>
                </li>
              );
            })}
          </ul>
          <div className="picker__foot">
            <span className="tabular">Đang hiện {pool.length}/{bank.data?.pages[0]?.total ?? pool.length} câu</span>
            {bank.hasNextPage && <button type="button" className="linkbtn" disabled={bank.isFetchingNextPage} onClick={() => void bank.fetchNextPage()}>Xem thêm câu hỏi</button>}
          </div>
        </>
      )}
    </div>
  );
}
