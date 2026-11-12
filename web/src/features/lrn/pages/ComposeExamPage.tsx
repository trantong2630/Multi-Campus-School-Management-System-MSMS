import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Check, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useBlocker } from 'react-router-dom';
import { modal } from '@/shared/lib/antdStatic';
import { lrnApi } from '../api';
import { StepFinish } from '../components/exam/StepFinish';
import { StepInfo } from '../components/exam/StepInfo';
import type { SubjectChoice } from '../components/exam/StepInfo';
import { StepPick } from '../components/exam/StepPick';
import { StepScore } from '../components/exam/StepScore';
import { AiEmpty } from '../components/parts';
import { cents, countByLevel, fmtPoints, suggestByDifficulty, TOTAL_CENTS } from '../examPoints';
import type { PerLevel } from '../examPoints';
import { DIFFICULTIES } from '../labels';
import type { QuestionItem } from '../types';
import '../components/lrn.css';

const STEPS = ['Thông tin', 'Chọn câu', 'Điểm và xem đề', 'Hoàn tất'] as const;

/**
 * Soạn đề (LRN-02, BR-21): một trang rộng bốn bước. Dựng đề một lần (thông tin, câu hỏi, thang điểm theo phần), rồi mới chọn dùng đề vào việc gì:
 * giao cho lớp hoặc tải file để in. Đề chưa lưu ở đâu cho tới khi giao; rời trang là mất phần đang soạn.
 */
export function ComposeExamPage() {
  const pairsQ = useQuery({ queryKey: ['lrn', 'class-subjects'], queryFn: lrnApi.assignments.classSubjects });
  const pairs = useMemo(() => pairsQ.data ?? [], [pairsQ.data]);
  const subjects = useMemo<SubjectChoice[]>(() => {
    const m = new Map<number, string>();
    for (const p of pairs) m.set(p.subjectId, p.subjectName);
    return [...m].map(([subjectId, name]) => ({ subjectId, name })).sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  }, [pairs]);

  const [step, setStep] = useState(0);
  const [pickedSubject, setPickedSubject] = useState<number | null>(null);
  // Chỉ có một môn thì chọn sẵn.
  const subjectId = pickedSubject ?? (subjects.length === 1 ? subjects[0].subjectId : null);
  const subject = subjects.find((s) => s.subjectId === subjectId) ?? null;
  const [pickedGrade, setPickedGrade] = useState<number | null>(null);
  const grade = pickedGrade ?? pairs.find((p) => p.subjectId === subjectId)?.gradeLevel ?? 6;
  const [typedTitle, setTitle] = useState('');
  const [titleTouched, setTitleTouched] = useState(false);
  const title = titleTouched ? typedTitle : subject ? `Rèn luyện ${subject.name} khối ${grade}` : '';
  const [instruction, setInstruction] = useState('');
  const [duration, setDuration] = useState<number | null>(null);
  const [customDuration, setCustomDuration] = useState(false);
  const [lines, setLines] = useState<QuestionItem[]>([]);
  // Điểm do giáo viên tự sửa; chỉ có hiệu lực khi tập các phần có câu không đổi, đổi thì quay về gợi ý.
  const [custom, setCustom] = useState<{ key: string; points: PerLevel } | null>(null);

  const counts = useMemo(() => countByLevel(lines), [lines]);
  const activeLevels = DIFFICULTIES.filter((d) => (counts[d.value] ?? 0) > 0);
  const levelsKey = activeLevels.map((d) => `${d.value}:${counts[d.value]}`).join(',');
  const suggested = useMemo(() => suggestByDifficulty(counts), [counts]);
  const points: PerLevel = custom && custom.key === levelsKey ? custom.points : suggested;
  const sumCents = activeLevels.reduce((s, d) => s + cents(points[d.value] ?? 0), 0);

  const pickSubject = (id: number) => {
    // Đổi môn thì các câu đã chọn không còn hợp lệ (đề chỉ gồm câu cùng môn).
    if (id !== subjectId) { setLines([]); setPickedGrade(null); }
    setPickedSubject(id);
  };

  const missing: string[][] = [
    [
      ...(subjectId === null ? ['chọn môn'] : []),
      ...(!title.trim() ? ['đặt tên đề'] : []),
      ...(customDuration && (duration === null || duration < 1) ? ['nhập thời gian làm bài'] : []),
    ],
    lines.length === 0 ? ['chọn ít nhất một câu hỏi'] : [],
    lines.length === 0 ? ['chọn ít nhất một câu hỏi']
      : activeLevels.some((d) => (points[d.value] ?? 0) <= 0) ? ['nhập điểm cho mọi phần']
        : sumCents !== TOTAL_CENTS ? ['tổng điểm các phần phải bằng 10'] : [],
    [],
  ];
  const valid = (n: number) => missing[n].length === 0;
  // Bước đi tới được: mọi bước trước nó đã đủ.
  const reachable = (n: number) => STEPS.slice(0, n).every((_, i) => valid(i));

  // Đề chưa lưu ở đâu: rời trang (đóng tab, bấm menu khác) khi đã chọn câu thì hỏi lại. Giao xong thì không hỏi.
  const finished = useRef(false);
  // Đã giao cho ít nhất một lớp: khóa các bước soạn, vì các lớp còn lại phải nhận đúng đề đó.
  const [locked, setLocked] = useState(false);
  const dirty = lines.length > 0;
  const blocker = useBlocker(({ currentLocation, nextLocation }) => dirty && !finished.current && currentLocation.pathname !== nextLocation.pathname);
  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    modal.confirm({
      title: 'Rời trang soạn đề?', centered: true, okText: 'Rời trang', cancelText: 'Ở lại soạn tiếp',
      content: 'Đề đang soạn chưa được giao hay lưu ở đâu. Rời trang là mất phần đã chọn.',
      onOk: () => blocker.proceed(), onCancel: () => blocker.reset(),
    });
  }, [blocker]);
  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => { if (!finished.current) e.preventDefault(); };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  if (!pairsQ.isLoading && pairs.length === 0) {
    return (
      <div className="exam">
        <AiEmpty title="Chưa có lớp nào để giao bài" hint="Thầy cô chỉ soạn đề và giao bài cho lớp và môn mình phụ trách theo thời khóa biểu đã phát hành. Liên hệ hiệu phó nếu cần kiểm tra lại." />
      </div>
    );
  }

  return (
    <div className="exam">
      <header className="exam__head">
        <h1>Soạn đề</h1>
        <ol className="xsteps" aria-label="Các bước soạn đề">
          {STEPS.map((label, i) => {
            const done = i < step && valid(i);
            const can = reachable(i) && !(locked && i < 3);
            return (
              <li key={label} className={`${i === step ? 'is-current' : ''}${done ? ' is-done' : ''}`}>
                <button type="button" disabled={!can} aria-current={i === step ? 'step' : undefined} onClick={() => setStep(i)}>
                  <span className="xsteps__no">{done ? <Check size={15} strokeWidth={3} /> : i + 1}</span>
                  <span>{label}</span>
                </button>
              </li>
            );
          })}
        </ol>
      </header>

      <div className="exam__body">
        <div hidden={step !== 0}>
          <StepInfo
            subjects={subjects} subjectId={subjectId} onSubject={pickSubject} grade={grade} onGrade={setPickedGrade}
            title={title} onTitle={(v) => { setTitle(v); setTitleTouched(true); }} instruction={instruction} onInstruction={setInstruction}
            duration={duration} onDuration={setDuration} customDuration={customDuration} onCustomDuration={setCustomDuration}
          />
        </div>
        <div hidden={step !== 1}>
          {subjectId !== null ? <StepPick subjectId={subjectId} grade={grade} lines={lines} onLines={setLines} /> : <p className="picker__none">Chọn môn ở bước Thông tin trước.</p>}
        </div>
        <div hidden={step !== 2}>
          <StepScore lines={lines} onLines={setLines} counts={counts} points={points} onPoints={(p) => setCustom({ key: levelsKey, points: p })} />
        </div>
        {subject && (
          <div hidden={step !== 3}>
            <StepFinish
              key={subject.subjectId} onGiven={() => { finished.current = true; setLocked(true); }}
            subjectId={subject.subjectId} grade={grade} title={title} instruction={instruction} duration={duration}
            lines={lines} points={points} classes={pairs.filter((p) => p.subjectId === subject.subjectId)}
            />
          </div>
        )}
      </div>

      <footer className="exam__foot">
        {step > 0 && !locked ? <button type="button" className="exam__back" onClick={() => setStep(step - 1)}><ArrowLeft size={17} />Quay lại</button> : <span />}
        {step === 1 && lines.length > 0 && (
          <span className="exam__tray" aria-live="polite">
            <span className="exam__count">Đã chọn <b className="tabular">{lines.length}</b> câu</span>
            <button type="button" className="exam__clear" aria-label="Bỏ chọn hết các câu" title="Bỏ chọn hết" onClick={() => setLines([])}><Trash2 size={16} />Bỏ chọn hết</button>
          </span>
        )}
        {step === 2 && <span className="exam__tray"><strong className="tabular">{fmtPoints(sumCents / 100)} / 10 điểm</strong></span>}
        {step < 3 ? (
          <span className="exam__next">
            {missing.slice(0, step + 1).flat().length > 0 && <span className="exam__todo">Còn thiếu: {missing.slice(0, step + 1).flat().join(', ')}</span>}
            <button type="button" className="exam__go" disabled={!reachable(step + 1)} onClick={() => setStep(step + 1)}>Tiếp tục<ArrowRight size={17} /></button>
          </span>
        ) : <span />}
      </footer>
    </div>
  );
}
