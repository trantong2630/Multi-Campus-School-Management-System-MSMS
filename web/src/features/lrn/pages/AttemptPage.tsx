import { useQueryClient } from '@tanstack/react-query';
import { Button, Modal } from 'antd';
import dayjs from 'dayjs';
import { ArrowLeft, ArrowRight, Check, ChevronDown, ChevronUp, X } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate, useParams } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { showError } from '@/shared/lib/feedback';
import { aiLines } from '../aiLines';
import { lrnApi } from '../api';
import { AiAvatar, AiBubble } from '../components/MsmsAi';
import type { AiMood, AiTone } from '../components/MsmsAi';
import { subjectVars } from '../components/tone';
import { useNow } from '../hooks';
import type { AnswerInput, Attempt } from '../types';
import { durationText, formatClock } from '../time';
import { OPTION_LABELS } from '../labels';
import '../components/lrn.css';

type Answers = Record<number, number[]>;
interface Line { text: string; tone: AiTone; mood: AiMood; key: number }

const toInputs = (a: Answers): AnswerInput[] => Object.entries(a).map(([id, optionIds]) => ({ assignmentQuestionId: Number(id), optionIds }));

/**
 * Làm bài (4.6.12–4.6.14): toàn màn hình, mỗi lần một câu. Bài có thời lượng có bước Bắt đầu, đồng hồ đếm ngược nằm trong
 * bong bóng của MSMS AI ở góc phải dưới. Hết hạn thực tế thì tự nộp; Backend vẫn là nơi quyết định hạn.
 */
export function AttemptPage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [data, setData] = useState<Attempt | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fetchedAt, setFetchedAt] = useState(0);
  const [answers, setAnswers] = useState<Answers>({});
  const [idx, setIdx] = useState(0);
  const [starting, setStarting] = useState(false);
  const [reviewing, setReviewing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [saveFailed, setSaveFailed] = useState(false);
  const [line, setLine] = useState<Line | null>(null);
  const [dockOpen, setDockOpen] = useState(true);
  const lineKey = useRef(0);
  const dirty = useRef(false);
  const saving = useRef(false);
  const answersRef = useRef<Answers>({});
  const submittedRef = useRef(false);
  const fired = useRef({ start: false, half: false, all: false, minute: false, timeUp: false });
  const nowMs = useNow(0, 1000);

  const say = useCallback((text: string, tone: AiTone = 'plain', mood: AiMood = 'idle') => {
    lineKey.current += 1;
    setLine({ text, tone, mood, key: lineKey.current });
    setDockOpen(true);
  }, []);

  const load = useCallback((a: Attempt) => {
    setFetchedAt(Date.now());
    setData(a);
    const init: Answers = {};
    for (const x of a.answers) if (x.optionIds.length) init[x.assignmentQuestionId] = x.optionIds;
    answersRef.current = init;
    setAnswers(init);
  }, []);

  useEffect(() => {
    let alive = true;
    lrnApi.student.open(id).then((a) => { if (alive) load(a); }).catch((e: unknown) => {
      if (!alive) return;
      const msg = getErrorMessage(e);
      // Đã nộp hoặc đã hết hạn: không còn gì để làm, chuyển sang xem kết quả.
      if (/đã nộp|quá hạn|hết giờ/i.test(msg)) navigate(`/lrn/done/${id}`, { replace: true });
      else setError(msg);
    });
    return () => { alive = false; };
  }, [id, load, navigate]);

  const questions = useMemo(() => data?.questions ?? [], [data]);
  const total = questions.length;
  const answered = questions.filter((q) => (answers[q.assignmentQuestionId] ?? []).length > 0).length;
  const timed = data?.durationMinutes != null;

  // Còn bao lâu: khoảng cách giữa hạn thực tế và giờ máy chủ lúc tải, trừ đi thời gian đã trôi trên máy này.
  const remaining = data
    ? Date.parse(data.deadline) - Date.parse(data.serverNow) - (nowMs - fetchedAt)
    : Number.POSITIVE_INFINITY;

  // ---- lưu nháp ----
  const flush = useCallback(async () => {
    if (!dirty.current || saving.current || submittedRef.current) return;
    saving.current = true;
    dirty.current = false;
    try {
      const r = await lrnApi.student.saveDraft(id, toInputs(answersRef.current));
      setSavedAt(r.savedAt);
      setSaveFailed(false);
    } catch {
      dirty.current = true;
      setSaveFailed(true);
    } finally {
      saving.current = false;
    }
  }, [id]);

  useEffect(() => {
    const t = setInterval(() => { void flush(); }, 12_000);
    const onHide = () => { if (document.visibilityState === 'hidden') void flush(); };
    document.addEventListener('visibilitychange', onHide);
    return () => { clearInterval(t); document.removeEventListener('visibilitychange', onHide); };
  }, [flush]);

  const choose = useCallback((qid: number, optionId: number, multiple: boolean) => {
    setAnswers((prev) => {
      const cur = prev[qid] ?? [];
      const next = multiple
        ? (cur.includes(optionId) ? cur.filter((x) => x !== optionId) : [...cur, optionId])
        : (cur[0] === optionId ? [] : [optionId]);
      const merged = { ...prev, [qid]: next };
      answersRef.current = merged;
      dirty.current = true;
      return merged;
    });
  }, []);

  useEffect(() => {
    if (!dirty.current) return;
    const t = setTimeout(() => { void flush(); }, 1500);
    return () => clearTimeout(t);
  }, [answers, flush]);

  // ---- nộp bài ----
  const submit = useCallback(async (auto: boolean) => {
    if (submittedRef.current) return;
    submittedRef.current = true;
    setSubmitting(true);
    try {
      await lrnApi.student.submit(id, toInputs(answersRef.current));
      say(aiLines.submitted(), 'ok', 'happy');
      await qc.invalidateQueries({ queryKey: ['lrn', 'my-assignments'] });
      navigate(`/lrn/done/${id}`, { replace: true });
    } catch (e) {
      submittedRef.current = false;
      setSubmitting(false);
      if (auto) { navigate(`/lrn/done/${id}`, { replace: true }); return; }
      showError(e, 'Chưa nộp được bài');
    }
  }, [id, navigate, qc, say]);

  // ---- MSMS AI nói theo diễn biến ----
  useEffect(() => {
    if (!data?.started || fired.current.start) return;
    fired.current.start = true;
    say(aiLines.start(), 'plain', 'happy');
  }, [data?.started, say]);

  useEffect(() => {
    if (!data?.started || total === 0) return;
    if (!fired.current.half && answered >= Math.ceil(total / 2) && answered < total) {
      fired.current.half = true;
      say(aiLines.mid(answered, total), 'ok', 'happy');
    }
    if (!fired.current.all && answered === total) {
      fired.current.all = true;
      say(aiLines.allDone(), 'ok', 'happy');
    }
  }, [answered, total, data?.started, say]);

  useEffect(() => {
    if (!data?.started || submittedRef.current) return;
    if (timed && remaining <= 60_000 && remaining > 0 && !fired.current.minute) {
      fired.current.minute = true;
      say(aiLines.lastMinute(), 'danger', 'warn');
    }
    if (remaining <= 0 && !fired.current.timeUp) {
      fired.current.timeUp = true;
      say(aiLines.timeUp(), 'danger', 'warn');
      void submit(true);
    }
  }, [remaining, timed, data?.started, say, submit]);

  // Lời nhắn tự tắt sau vài giây; riêng báo gấp còn 1 phút thì ở lại tới khi có lời khác.
  useEffect(() => {
    if (!line || line.tone === 'danger') return;
    const t = setTimeout(() => setLine(null), 7000);
    return () => clearTimeout(t);
  }, [line]);

  // ---- bàn phím ----
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (reviewing || !data?.started || (e.target as HTMLElement)?.closest('input,textarea')) return;
      if (e.key === 'ArrowRight') setIdx((i) => Math.min(i + 1, total - 1));
      else if (e.key === 'ArrowLeft') setIdx((i) => Math.max(i - 1, 0));
      else {
        const k = OPTION_LABELS.findIndex((l) => l.toLowerCase() === e.key.toLowerCase());
        const n = k >= 0 ? k : ['1', '2', '3', '4'].indexOf(e.key);
        const q = questions[idx];
        if (n >= 0 && q?.options[n]) choose(q.assignmentQuestionId, q.options[n].optionId, q.isMultiple);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [reviewing, data?.started, total, questions, idx, choose]);

  const start = async () => {
    setStarting(true);
    try { load(await lrnApi.student.start(id)); } catch (e) { showError(e, 'Chưa bắt đầu được'); } finally { setStarting(false); }
  };

  const leave = async () => { await flush(); navigate('/lrn/todo'); };

  const q = questions[idx];
  const urgent = timed && remaining <= 60_000;
  const content = (() => {
    if (error) {
      return (
        <div className="focus__center">
          <AiAvatar mood="idle" size={96} />
          <AiBubble tone="warn" tail="none">{error}</AiBubble>
          <Button type="primary" onClick={() => navigate('/lrn/todo')}>Về danh sách bài</Button>
        </div>
      );
    }
    if (!data) return <div className="focus__center"><AiAvatar mood="thinking" size={96} /></div>;

    if (!data.started) {
      return (
        <div className="focus__center focus__intro" style={subjectVars(data.subjectId)}>
          <AiAvatar mood="happy" size={120} />
          <span className="focus__kicker">{data.subjectName} · {data.className}</span>
          <h1>{data.title}</h1>
          <p>
            Bài có <strong>{data.questionCount} câu</strong> và giới hạn <strong>{durationText(data.durationMinutes)}</strong>.
            Đồng hồ chỉ bắt đầu chạy khi bạn bấm nút dưới đây, và sẽ vẫn chạy nếu bạn thoát ra.
          </p>
          {data.instruction && <p className="focus__note">{data.instruction}</p>}
          <div className="focus__introbtns">
            <Button size="large" onClick={() => navigate('/lrn/todo')}>Để sau</Button>
            <Button size="large" type="primary" loading={starting} onClick={() => void start()}>Bắt đầu làm bài</Button>
          </div>
        </div>
      );
    }

    return (
      <>
        <header className="focus__bar">
          <button type="button" className="focus__exit" onClick={() => void leave()} aria-label="Thoát, bài đang làm được giữ lại">
            <X size={18} /> Thoát
          </button>
          <div className="focus__title">
            <strong>{data.title}</strong>
            <span>{data.subjectName} · {timed ? durationText(data.durationMinutes) : `Hạn nộp ${dayjs(data.dueAt).format('HH:mm DD/MM')}`}</span>
          </div>
          <div className="focus__progress" aria-label={`Đã làm ${answered} trên ${total} câu`}>
            <span className="tabular">{answered}/{total}</span>
            <i><b style={{ width: `${total ? (answered / total) * 100 : 0}%` }} /></i>
          </div>
          <span className={`focus__save${saveFailed ? ' is-bad' : ''}`}>
            {saveFailed ? 'Chưa lưu được, đang thử lại' : savedAt ? `Đã lưu ${dayjs(savedAt).format('HH:mm:ss')}` : 'Tự động lưu'}
          </span>
          <Button type="primary" onClick={() => setReviewing(true)} loading={submitting}>Nộp bài</Button>
        </header>

        <main className="focus__stage">
          {q && (
            <article className="qcard" key={q.assignmentQuestionId} style={subjectVars(data.subjectId)}>
              <div className="qcard__meta">
                <span className="qcard__no">Câu {q.orderNo}<small>/{total}</small></span>
                <span className="qcard__tag">{q.isMultiple ? 'Chọn nhiều đáp án' : 'Chọn một đáp án'}</span>
                <span className="qcard__pts">{q.points.toLocaleString('vi-VN')} điểm</span>
              </div>
              {idx === 0 && data.instruction && <p className="focus__note">{data.instruction}</p>}
              <h2 className="qcard__text">{q.content}</h2>
              <ul className="qcard__opts">
                {q.options.map((o, n) => {
                  const on = (answers[q.assignmentQuestionId] ?? []).includes(o.optionId);
                  return (
                    <li key={o.optionId}>
                      <button
                        type="button" className={`qopt${on ? ' is-on' : ''}${q.isMultiple ? ' is-multi' : ''}`}
                        aria-pressed={on} onClick={() => choose(q.assignmentQuestionId, o.optionId, q.isMultiple)}
                      >
                        <b>{o.label}</b>
                        <span>{o.content}</span>
                        <i aria-hidden>{on && <Check size={16} strokeWidth={3} />}</i>
                        <kbd>{n + 1}</kbd>
                      </button>
                    </li>
                  );
                })}
              </ul>
              <div className="qcard__nav">
                <Button icon={<ArrowLeft size={16} />} disabled={idx === 0} onClick={() => setIdx(idx - 1)}>Câu trước</Button>
                {idx < total - 1
                  ? <Button type="primary" onClick={() => setIdx(idx + 1)}>Câu sau <ArrowRight size={16} /></Button>
                  : <Button type="primary" onClick={() => setReviewing(true)}>Xem lại và nộp</Button>}
              </div>
            </article>
          )}
          <nav className="qdots" aria-label="Chuyển câu">
            {questions.map((x, n) => (
              <button
                key={x.assignmentQuestionId} type="button" onClick={() => setIdx(n)}
                className={`${n === idx ? 'is-current ' : ''}${(answers[x.assignmentQuestionId] ?? []).length ? 'is-done' : ''}`}
                aria-label={`Câu ${x.orderNo}${(answers[x.assignmentQuestionId] ?? []).length ? ', đã làm' : ''}`}
              >{x.orderNo}</button>
            ))}
          </nav>
        </main>

        <aside className={`dock${dockOpen ? '' : ' is-min'}`} aria-live="polite">
          {dockOpen && line && <AiBubble tone={line.tone} tail="bottom-right" key={line.key} className="dock__bubble">{line.text}</AiBubble>}
          <div className="dock__row">
            {timed && (
              <div className={`dock__clock${urgent ? ' is-urgent' : ''}`} role="timer" aria-label="Thời gian còn lại">
                <span>Còn lại</span>
                <strong className="tabular">{formatClock(Math.max(0, remaining))}</strong>
              </div>
            )}
            <button
              type="button" className="dock__ai" onClick={() => setDockOpen((v) => !v)}
              aria-label={dockOpen ? 'Thu nhỏ MSMS AI' : 'Mở MSMS AI'}
            >
              <AiAvatar mood={line?.mood ?? 'idle'} size={64} />
              <span className="dock__toggle">{dockOpen ? <ChevronDown size={12} /> : <ChevronUp size={12} />}</span>
            </button>
          </div>
        </aside>

        <Modal
          open={reviewing} onCancel={() => setReviewing(false)} footer={null} centered width={520} destroyOnHidden
          title="Xem lại trước khi nộp"
        >
          <div className="review">
            <AiBubble tone={answered < total ? 'warn' : 'ok'} tail="none">
              {answered < total ? aiLines.unanswered(total - answered) : aiLines.allDone()}
            </AiBubble>
            <div className="review__grid">
              {questions.map((x, n) => {
                const done = (answers[x.assignmentQuestionId] ?? []).length > 0;
                return (
                  <button key={x.assignmentQuestionId} type="button" className={done ? 'is-done' : 'is-empty'} onClick={() => { setIdx(n); setReviewing(false); }}>
                    {x.orderNo}
                  </button>
                );
              })}
            </div>
            <p className="review__hint">Ô trống là câu chưa làm. Mỗi bài chỉ nộp một lần, nộp xong không sửa được.</p>
            <div className="review__btns">
              <Button onClick={() => setReviewing(false)}>Làm tiếp</Button>
              <Button type="primary" loading={submitting} onClick={() => { setReviewing(false); void submit(false); }}>Nộp bài</Button>
            </div>
          </div>
        </Modal>
      </>
    );
  })();

  return createPortal(<div className="focus">{content}</div>, document.body);
}
