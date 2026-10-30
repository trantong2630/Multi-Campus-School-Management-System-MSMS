import { Check, X } from 'lucide-react';
import { useState } from 'react';
import { formatDateTime } from '@/shared/lib/format';
import { resultLine } from '../aiLines';
import type { Analysis, PracticeResult, ResultQuestion } from '../types';
import { fmtScore } from '../time';
import { AiAvatar, AiBubble, AiDots, MsmsAi, TypedText } from './MsmsAi';
import { ScoreRing } from './parts';

export type Viewer = 'student' | 'parent' | 'teacher';

const jump = (id: string) => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'center' });

function QuestionCard({ q, viewer }: { q: ResultQuestion; viewer: Viewer }) {
  const right = q.isCorrect === true;
  const pickedLabel = viewer === 'teacher' ? 'Học sinh chọn' : viewer === 'parent' ? 'Con chọn' : 'Bạn chọn';
  return (
    <article id={`q-${q.orderNo}`} className={`rv-q ${right ? 'is-right' : 'is-wrong'}`}>
      <header className="rv-q__head">
        <span className="rv-q__no" aria-label={right ? `Câu ${q.orderNo} đúng` : `Câu ${q.orderNo} sai`}>
          {right ? <Check size={16} strokeWidth={3} /> : <X size={16} strokeWidth={3} />}
        </span>
        <div className="rv-q__text">
          <span className="rv-q__label">Câu {q.orderNo}{q.isMultiple ? ' · chọn nhiều đáp án' : ''}</span>
          <p>{q.content}</p>
        </div>
        <span className="rv-q__pts tabular">{fmtScore(q.earnedPoints)}/{fmtScore(q.points)}</span>
      </header>
      <ul className="rv-opts">
        {q.options.map((o) => {
          const cls = o.isCorrect ? 'is-correct' : o.selected ? 'is-picked-wrong' : '';
          return (
            <li key={o.optionId} className={`rv-opt ${cls}${o.selected ? ' is-picked' : ''}`}>
              <b>{o.label}</b>
              <span>{o.content}</span>
              <em>
                {o.isCorrect && o.selected ? `Đúng · ${pickedLabel.toLowerCase()}` : o.isCorrect ? 'Đáp án đúng' : o.selected ? pickedLabel : ''}
              </em>
            </li>
          );
        })}
      </ul>
      {!q.options.some((o) => o.selected) && <p className="rv-q__skip">Câu này đã bỏ trống.</p>}
      {q.explanation && (
        <div className="rv-q__why">
          <strong>Giải thích</strong>
          <p>{q.explanation}</p>
        </div>
      )}
    </article>
  );
}

function AnalysisSection({ analysis }: { analysis: Analysis }) {
  // Phân tích lỗi thì để trống, không báo lỗi và không có nút thử lại (BR-23).
  if (analysis.status === 'Failed') return null;

  if (analysis.status === 'Pending') {
    return (
      <section id="rv-analysis" className="rv-ai rv-ai--wait">
        <AiAvatar mood="thinking" size={76} />
        <div>
          <h2>MSMS AI đang xem bài làm</h2>
          <p><TypedText text="Mình đang đối chiếu từng câu để tìm điểm mạnh và chỗ cần ôn thêm…" speed={28} /></p>
          <AiDots />
        </div>
      </section>
    );
  }

  return (
    <section id="rv-analysis" className="rv-ai">
      <header className="rv-ai__head">
        <AiAvatar mood="happy" size={64} />
        <div>
          <span className="rv-ai__kicker">MSMS AI phân tích</span>
          {analysis.summary && <p className="rv-ai__summary">{analysis.summary}</p>}
        </div>
      </header>

      {(analysis.strengths.length > 0 || analysis.weaknesses.length > 0) && (
        <div className="rv-ai__cols">
          {analysis.strengths.length > 0 && (
            <div className="rv-ai__col rv-ai__col--good">
              <h3>Điểm mạnh</h3>
              <ul>{analysis.strengths.map((s) => <li key={s}>{s}</li>)}</ul>
            </div>
          )}
          {analysis.weaknesses.length > 0 && (
            <div className="rv-ai__col rv-ai__col--bad">
              <h3>Cần cải thiện</h3>
              <ul>{analysis.weaknesses.map((s) => <li key={s}>{s}</li>)}</ul>
            </div>
          )}
        </div>
      )}

      {analysis.mistakes.length > 0 && (
        <div className="rv-ai__block">
          <h3>Các câu sai, vì sao</h3>
          <ul className="rv-ai__mistakes">
            {analysis.mistakes.map((m) => (
              <li key={m.questionNo}>
                <button type="button" onClick={() => jump(`q-${m.questionNo}`)} aria-label={`Đến câu ${m.questionNo}`}>Câu {m.questionNo}</button>
                <span>{m.why}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {analysis.suggestions.length > 0 && (
        <div className="rv-ai__block">
          <h3>Nên ôn thêm</h3>
          <ul className="rv-ai__tags">{analysis.suggestions.map((s) => <li key={s}>{s}</li>)}</ul>
        </div>
      )}
    </section>
  );
}

/**
 * Kết quả một bài làm: vòng điểm, lời của MSMS AI, dải câu đúng/sai để nhảy tới từng câu, đáp án và giải thích,
 * rồi khung phân tích AI ở cuối. Học sinh, phụ huynh và giáo viên dùng chung một giao diện.
 */
export function ResultView({ data, viewer }: { data: PracticeResult; viewer: Viewer }) {
  const [only, setOnly] = useState<'all' | 'wrong'>('all');
  const total = data.questions.length;
  const wrong = data.questions.filter((q) => q.isCorrect !== true);
  const shown = only === 'wrong' ? wrong : data.questions;
  const who = viewer === 'teacher' ? data.studentName : viewer === 'parent' ? data.studentName : null;

  if (!data.submitted) {
    return (
      <div className="rv">
        <section className="rv-hero rv-hero--none">
          <span className="rv-hero__badge">Không nộp</span>
          <div className="rv-hero__body">
            <span className="rv-hero__kicker">{data.subjectName} · {data.className}</span>
            <h1>{data.title}</h1>
            {who && <p className="rv-hero__who">{who}</p>}
            <p className="rv-hero__none">Không nộp: bài đã hết hạn mà chưa có bài làm, nên chưa có điểm và đáp án để xem.</p>
          </div>
        </section>
      </div>
    );
  }

  const mood = (data.score ?? 0) >= 6.5 ? 'happy' : 'idle';
  return (
    <div className="rv">
      <section className="rv-hero">
        <ScoreRing score={data.score} />
        <div className="rv-hero__body">
          <span className="rv-hero__kicker">{data.subjectName} · {data.className}</span>
          <h1>{data.title}</h1>
          {who && <p className="rv-hero__who">{who}</p>}
          <p className="rv-hero__meta">
            Đúng <strong className="tabular">{data.correctCount ?? 0}/{total}</strong> câu · nộp lúc {formatDateTime(data.submittedAt)}
          </p>
          {viewer !== 'teacher' && (
            <MsmsAi mood={mood} size={58} say={resultLine(data.score, data.correctCount, total)} />
          )}
        </div>
      </section>

      <nav className="rv-strip" aria-label="Các câu hỏi">
        <div className="rv-strip__dots">
          {data.questions.map((q) => (
            <button
              key={q.assignmentQuestionId} type="button" className={q.isCorrect ? 'is-right' : 'is-wrong'}
              onClick={() => { setOnly('all'); window.setTimeout(() => jump(`q-${q.orderNo}`), 30); }}
              aria-label={`Câu ${q.orderNo} ${q.isCorrect ? 'đúng' : 'sai'}`}
            >{q.orderNo}</button>
          ))}
        </div>
        {wrong.length > 0 && (
          <div className="rv-strip__toggle" role="tablist">
            <button type="button" role="tab" aria-selected={only === 'all'} className={only === 'all' ? 'is-on' : ''} onClick={() => setOnly('all')}>Tất cả {total}</button>
            <button type="button" role="tab" aria-selected={only === 'wrong'} className={only === 'wrong' ? 'is-on' : ''} onClick={() => setOnly('wrong')}>Chỉ câu sai {wrong.length}</button>
          </div>
        )}
      </nav>

      <div className="rv-list">
        {shown.map((q) => <QuestionCard key={q.assignmentQuestionId} q={q} viewer={viewer} />)}
        {shown.length === 0 && (
          <AiBubble tone="ok" tail="none">Không có câu nào sai. Làm tốt lắm!</AiBubble>
        )}
      </div>

      {data.analysis && <AnalysisSection analysis={data.analysis} />}
    </div>
  );
}
