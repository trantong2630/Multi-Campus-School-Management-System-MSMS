import type { ReactNode } from 'react';
import { fmtScore } from '../time';
import { bandColor, subjectVars } from './tone';
import { AiAvatar } from './MsmsAi';
import type { AiMood } from './MsmsAi';

/** Ô vuông bo tròn mang chữ cái đầu của môn, đứng thay cho icon trang trí. */
export function SubjectMark({ id, name, size = 44 }: { id: number; name: string; size?: number }) {
  return (
    <span className="sj-mark" style={{ ...subjectVars(id), width: size, height: size, fontSize: Math.round(size * 0.42) }} aria-hidden>
      {name.trim().charAt(0).toUpperCase()}
    </span>
  );
}

/** Vòng điểm: nét vòng đầy theo điểm trên thang 10, màu theo mức điểm. */
export function ScoreRing({ score, size = 148, label = '/10' }: { score: number | null; size?: number; label?: string }) {
  const stroke = Math.max(8, Math.round(size * 0.075));
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const pct = Math.max(0, Math.min(10, score ?? 0)) / 10;
  return (
    <div className="score-ring" style={{ width: size, height: size }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--c-neutralBg)" strokeWidth={stroke} />
        <circle
          cx={size / 2} cy={size / 2} r={r} fill="none" stroke={bandColor(score)} strokeWidth={stroke} strokeLinecap="round"
          strokeDasharray={`${c * pct} ${c}`} transform={`rotate(-90 ${size / 2} ${size / 2})`} className="score-ring__arc"
        />
      </svg>
      <div className="score-ring__text">
        <strong className="tabular" style={{ fontSize: Math.round(size * 0.3), color: bandColor(score) }}>{fmtScore(score)}</strong>
        <span>{label}</span>
      </div>
    </div>
  );
}

/** Dải ô nhỏ, mỗi ô một câu: xanh là đúng, san hô là sai. Thay cho thanh phần trăm khô khan. */
export function AnswerStrip({ correct, total }: { correct: number | null; total: number }) {
  const n = Math.min(total, 50);
  const right = correct === null ? 0 : Math.min(correct, n);
  return (
    <span className="answer-strip" aria-label={`Đúng ${right} trên ${total} câu`}>
      {Array.from({ length: n }, (_, i) => <span key={i} className={i < right ? 'is-right' : 'is-wrong'} />)}
    </span>
  );
}

/** Trạng thái rỗng có MSMS AI đứng cạnh, nói bằng giọng của AI. */
export function AiEmpty({ title, hint, mood = 'idle', children }: { title: string; hint?: string; mood?: AiMood; children?: ReactNode }) {
  return (
    <div className="ai-empty">
      <AiAvatar mood={mood} size={84} />
      <strong>{title}</strong>
      {hint && <p>{hint}</p>}
      {children}
    </div>
  );
}
