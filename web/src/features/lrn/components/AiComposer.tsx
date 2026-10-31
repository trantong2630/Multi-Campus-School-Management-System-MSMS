import { useQueryClient } from '@tanstack/react-query';
import { AutoComplete, Input, Select } from 'antd';
import { Minus, Plus } from 'lucide-react';
import { useEffect, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { composeSteps } from '../aiLines';
import { lrnApi } from '../api';
import { GRADES, TEXTBOOK } from '../labels';
import { topicSuggestions } from '../textbookToc';
import { AiAvatar, AiBubble, AiDots, TypedText } from './MsmsAi';
import type { AiMood, AiTone } from './MsmsAi';
import './lrn.css';

interface Props {
  subjects: { subjectId: number; name: string }[];
  defaultSubjectId?: number;
  /** Gọi sau khi sinh xong để trang chuyển sang xem các câu nháp. */
  onDone: () => void;
}

type Phase = { kind: 'idle' } | { kind: 'working' } | { kind: 'done'; text: string } | { kind: 'error'; text: string };

/** Quầy soạn câu hỏi của MSMS AI (4.6.5): nói chủ đề, AI trả về câu Nháp để giáo viên kiểm tra rồi mới dùng. */
export function AiComposer({ subjects, defaultSubjectId, onDone }: Props) {
  const qc = useQueryClient();
  const [pickedSubject, setSubjectId] = useState<number | undefined>();
  const subjectId = pickedSubject ?? defaultSubjectId;
  const [grade, setGrade] = useState<number>(6);
  const [topic, setTopic] = useState('');
  const [count, setCount] = useState(5);
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const [step, setStep] = useState(0);

  useEffect(() => {
    if (phase.kind !== 'working') return;
    const t = setInterval(() => setStep((s) => Math.min(s + 1, 4)), 5000);
    return () => clearInterval(t);
  }, [phase.kind]);

  const lessons = topicSuggestions(subjects.find((s) => s.subjectId === subjectId)?.name, grade).map((t) => ({ value: t }));
  const ready =subjectId !== undefined && topic.trim().length > 0;
  const working = phase.kind === 'working';

  const run = async () => {
    if (!ready || working) return;
    setStep(0);
    setPhase({ kind: 'working' });
    try {
      const r = await lrnApi.questions.generate({ subjectId: subjectId!, gradeLevel: grade, topic: topic.trim(), textbook: TEXTBOOK, count });
      await qc.invalidateQueries({ queryKey: ['lrn', 'questions'] });
      setPhase({
        kind: 'done',
        text: r.discarded > 0
          ? `Xong rồi! Mình soạn được ${r.drafts.length} câu (bỏ ${r.discarded} câu chưa đạt). Thầy cô xem lại ở tab Nháp nhé.`
          : `Xong rồi! ${r.drafts.length} câu mới đang ở tab Nháp, thầy cô xem lại rồi duyệt nhé.`,
      });
      setTopic('');
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
            <><TypedText text={composeSteps(topic.trim() || 'chủ đề này')[step]} speed={22} /> <AiDots /></>
          ) : phase.kind === 'done' || phase.kind === 'error' ? phase.text : 'Thầy cô muốn mình soạn câu hỏi về chủ đề gì?'}
        </AiBubble>
      </div>

      <div className="composer__form">
        <label>Môn học
          <Select
            placeholder="Chọn môn" value={subjectId} onChange={setSubjectId} disabled={working} showSearch optionFilterProp="label"
            options={subjects.map((s) => ({ value: s.subjectId, label: s.name }))}
          />
        </label>
        <div className="composer__field">
          <span>Khối</span>
          <div className="pillset" role="radiogroup" aria-label="Khối">
            {GRADES.map((g) => (
              <button key={g} type="button" role="radio" aria-checked={grade === g} className={grade === g ? 'is-on' : ''} disabled={working} onClick={() => setGrade(g)}>{g}</button>
            ))}
          </div>
        </div>
        <label>Chủ đề
          <AutoComplete
            value={topic} options={lessons} disabled={working} onChange={setTopic}
            filterOption={(input, o) => (o?.value ?? '').toLowerCase().includes(input.trim().toLowerCase())}
          >
            <Input maxLength={150} placeholder={lessons.length > 0 ? 'Gõ để tìm bài hoặc nhập chủ đề tự do' : 'Ví dụ: Phân số, Quang hợp'} />
          </AutoComplete>
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
