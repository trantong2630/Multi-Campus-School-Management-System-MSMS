import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Input, Select } from 'antd';
import { Minus, Plus } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { composeSteps } from '../aiLines';
import { lrnApi } from '../api';
import { GRADES, TEXTBOOK, pagesText } from '../labels';
import { AiAvatar, AiBubble, AiDots, TypedText } from './MsmsAi';
import type { AiMood, AiTone } from './MsmsAi';
import './lrn.css';

interface Props {
  subjects: { subjectId: number; name: string }[];
  defaultSubjectId?: number;
  /** Gọi sau khi sinh xong để trang chuyển sang xem các câu nháp. */
  onDone: () => void;
}

/** Một bài trong ô chọn: label là chữ thuần cho ô tìm kiếm, title và pages để vẽ dòng bài. */
interface LessonOption { value: number; label: string; title: string; pages: string }

type Phase = { kind: 'idle' } | { kind: 'working' } | { kind: 'done'; text: string } | { kind: 'error'; text: string };

/** Quầy soạn câu hỏi của MSMS AI (4.6.5): chọn bài trong sách, AI đọc các trang của bài rồi trả về câu Nháp để giáo viên kiểm tra rồi mới dùng. */
export function AiComposer({ subjects, defaultSubjectId, onDone }: Props) {
  const qc = useQueryClient();
  const [pickedSubject, setSubjectId] = useState<number | undefined>();
  const subjectId = pickedSubject ?? defaultSubjectId;
  const [grade, setGrade] = useState<number>(6);
  const [lessonId, setLessonId] = useState<number | undefined>();
  const [note, setNote] = useState('');
  const [count, setCount] = useState(5);
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const [step, setStep] = useState(0);

  useEffect(() => {
    if (phase.kind !== 'working') return;
    const t = setInterval(() => setStep((s) => Math.min(s + 1, 4)), 5000);
    return () => clearInterval(t);
  }, [phase.kind]);

  const lessons = useQuery({
    queryKey: ['lrn', 'textbook-lessons', subjectId, grade],
    queryFn: () => lrnApi.textbooks.lessons(subjectId!, grade),
    enabled: subjectId !== undefined,
  });
  // Nhóm bài theo chương để danh sách dễ tìm; "Luyện tập chung" lặp tên nên luôn kèm số trang.
  const lessonOptions = useMemo(() => {
    const groups = new Map<string, LessonOption[]>();
    for (const l of lessons.data ?? []) {
      const list = groups.get(l.chapter) ?? [];
      // label là chữ thuần để ô tìm kiếm lọc; hiển thị thật do optionRender và labelRender vẽ.
      list.push({ value: l.lessonId, label: `${l.title} ${pagesText(l.startPage, l.endPage)}`, title: l.title, pages: pagesText(l.startPage, l.endPage) });
      groups.set(l.chapter, list);
    }
    return [...groups].map(([label, options]) => ({ label, options }));
  }, [lessons.data]);
  const lesson = lessons.data?.find((l) => l.lessonId === lessonId);
  const noBook = lessons.isSuccess && lessons.data.length === 0;

  const ready = subjectId !== undefined && lessonId !== undefined;
  const working = phase.kind === 'working';

  const pickSubject = (id: number) => { setSubjectId(id); setLessonId(undefined); };
  const pickGrade = (g: number) => { setGrade(g); setLessonId(undefined); };

  const run = async () => {
    if (!ready || working) return;
    setStep(0);
    setPhase({ kind: 'working' });
    try {
      const r = await lrnApi.questions.generate({ subjectId: subjectId!, gradeLevel: grade, lessonId: lessonId!, note: note.trim() || undefined, count });
      await qc.invalidateQueries({ queryKey: ['lrn', 'questions'] });
      setPhase({
        kind: 'done',
        text: r.discarded > 0
          ? `Xong rồi! Mình soạn được ${r.drafts.length} câu (bỏ ${r.discarded} câu chưa đạt). Thầy cô xem lại ở tab Nháp nhé.`
          : `Xong rồi! ${r.drafts.length} câu mới đang ở tab Nháp, thầy cô xem lại rồi duyệt nhé.`,
      });
      setNote('');
      onDone();
    } catch (e) {
      setPhase({ kind: 'error', text: `${getErrorMessage(e)}` });
    }
  };

  const mood: AiMood = working ? 'thinking' : phase.kind === 'done' ? 'happy' : phase.kind === 'error' ? 'warn' : 'idle';
  const tone: AiTone = phase.kind === 'done' ? 'ok' : phase.kind === 'error' ? 'warn' : 'plain';

  return (
    <section className="composer" aria-label="MSMS AI soạn câu hỏi">
      <div className="composer__top">
        <AiAvatar mood={mood} size={84} />
        <AiBubble tone={tone} tail="left">
          {working ? (
            <><TypedText text={composeSteps(lesson?.title ?? 'bài này')[step]} speed={22} /> <AiDots /></>
          ) : phase.kind === 'done' || phase.kind === 'error' ? phase.text : 'Thầy cô muốn mình soạn câu hỏi từ bài nào trong sách?'}
        </AiBubble>
      </div>

      <div className="composer__form">
        <label>Môn học
          <Select
            placeholder="Chọn môn" value={subjectId} onChange={pickSubject} disabled={working} showSearch optionFilterProp="label"
            options={subjects.map((s) => ({ value: s.subjectId, label: s.name }))}
          />
        </label>
        <div className="composer__field">
          <span>Khối</span>
          <div className="pillset" role="radiogroup" aria-label="Khối">
            {GRADES.map((g) => (
              <button key={g} type="button" role="radio" aria-checked={grade === g} className={grade === g ? 'is-on' : ''} disabled={working} onClick={() => pickGrade(g)}>{g}</button>
            ))}
          </div>
        </div>
        <label>Bài học
          <Select
            placeholder={noBook ? 'Chưa có sách cho môn và khối này' : 'Chọn bài trong sách'} value={lessonId} onChange={setLessonId}
            disabled={working || subjectId === undefined || noBook} loading={lessons.isFetching} showSearch optionFilterProp="label"
            options={lessonOptions} notFoundContent="Không có bài phù hợp" popupClassName="lessonpick"
            optionRender={(o) => { const d = o.data as Partial<LessonOption>; return d.title && d.pages ? <LessonLine title={d.title} pages={d.pages} /> : o.label; }}
            labelRender={() => (lesson ? <LessonLine title={lesson.title} pages={pagesText(lesson.startPage, lesson.endPage)} /> : null)}
          />
        </label>
        <label>Gợi ý thêm (không bắt buộc)
          <Input value={note} maxLength={150} disabled={working || lessonId === undefined} placeholder="Ví dụ: tập trung số La Mã" onChange={(e) => setNote(e.target.value)} />
        </label>
        <div className="composer__field">
          <span>Bộ sách</span>
          <strong className="composer__book">{TEXTBOOK}</strong>
        </div>
        <div className="composer__field">
          <span>Số câu</span>
          <div className="stepper">
            <button type="button" aria-label="Bớt một câu" disabled={working || count <= 1} onClick={() => setCount(count - 1)}><Minus size={16} /></button>
            <strong className="tabular">{count}</strong>
            <button type="button" aria-label="Thêm một câu" disabled={working || count >= 10} onClick={() => setCount(count + 1)}><Plus size={16} /></button>
          </div>
        </div>
      </div>

      <button type="button" className="composer__go" disabled={!ready || working} onClick={() => void run()}>
        {working ? 'MSMS AI đang soạn…' : 'Nhờ MSMS AI soạn'}
      </button>
      <p className="composer__foot">Câu do AI soạn luôn ở dạng nháp. Chỉ câu thầy cô xác nhận mới đưa vào bài tập được.</p>
    </section>
  );
}

/** Một dòng bài trong ô chọn: tên bài bên trái (rút gọn khi hẹp), trang in nằm trong viên thuốc bên phải. */
function LessonLine({ title, pages }: { title: string; pages: string }) {
  return (
    <span className="lessonline">
      <span className="lessonline__title">{title}</span>
      <span className="lessonline__pg">{pages}</span>
    </span>
  );
}
