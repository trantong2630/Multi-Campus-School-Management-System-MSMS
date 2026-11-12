import { useQuery } from '@tanstack/react-query';
import { InputNumber } from 'antd';
import { ArrowDown, ArrowUp, X } from 'lucide-react';
import { lrnApi } from '../../api';
import { cents, fmtPoints, perQuestionText, pointsPerQuestion, suggestByDifficulty, suggestEvenly, TOTAL_CENTS } from '../../examPoints';
import type { PerLevel } from '../../examPoints';
import { DIFFICULTIES } from '../../labels';
import type { Difficulty, QuestionItem } from '../../types';
import { DifficultyTag } from '../DifficultyTag';
import '../lrn.css';

interface Props {
  lines: QuestionItem[];
  onLines: (next: QuestionItem[]) => void;
  counts: PerLevel;
  points: PerLevel;
  onPoints: (next: PerLevel) => void;
}

/** Một câu trong bản xem trước: đúng như học sinh sẽ thấy, thêm đáp án đúng đánh dấu để giáo viên kiểm tra. */
function PreviewLine({ q, no, pts, first, last, onMove, onRemove }: {
  q: QuestionItem; no: number; pts: number; first: boolean; last: boolean; onMove: (d: -1 | 1) => void; onRemove: () => void;
}) {
  const detail = useQuery({ queryKey: ['lrn', 'questions', 'detail', q.questionId], queryFn: () => lrnApi.questions.get(q.questionId) });
  return (
    <li className="xprev__q">
      <header>
        <b>Câu {no}</b>
        <DifficultyTag value={q.difficulty} short />
        <span className="xprev__pts tabular">{fmtPoints(pts)} điểm</span>
        {q.isMultiple && <span className="chip chip--warn">Chọn nhiều</span>}
        <span className="xprev__ctl">
          <button type="button" aria-label="Lên" disabled={first} onClick={() => onMove(-1)}><ArrowUp size={14} /></button>
          <button type="button" aria-label="Xuống" disabled={last} onClick={() => onMove(1)}><ArrowDown size={14} /></button>
          <button type="button" aria-label="Bỏ câu này khỏi đề" onClick={onRemove}><X size={14} /></button>
        </span>
      </header>
      <p>{q.content}</p>
      <ul>
        {(detail.data?.options ?? []).map((o) => (
          <li key={o.optionId} className={o.isCorrect ? 'is-correct' : ''}><b>{o.label}</b>{o.content}</li>
        ))}
      </ul>
    </li>
  );
}

/** Bước 3: đặt điểm cho từng phần (độ khó) rồi xem lại cả đề, đổi thứ tự hoặc bỏ câu ngay tại đây. */
export function StepScore({ lines, onLines, counts, points, onPoints }: Props) {
  const levels = DIFFICULTIES.filter((d) => (counts[d.value] ?? 0) > 0);
  const sum = levels.reduce((s, d) => s + cents(points[d.value] ?? 0), 0);
  const perQuestion = pointsPerQuestion(lines, points);
  const set = (d: Difficulty, v: number | null) => onPoints({ ...points, [d]: v ?? 0 });
  const move = (i: number, d: -1 | 1) => {
    const j = i + d;
    if (j < 0 || j >= lines.length) return;
    const next = [...lines];
    [next[i], next[j]] = [next[j], next[i]];
    onLines(next);
  };

  return (
    <div className="xscore">
      <section className="xscale">
        <header>
          <h2>Thang điểm theo phần</h2>
          <span className="xscale__tools">
            <button type="button" className="linkbtn" onClick={() => onPoints(suggestByDifficulty(counts))} title="Phần dễ nhiều điểm hơn, câu khó điểm cao hơn mỗi câu">Gợi ý theo độ khó</button>
            <button type="button" className="linkbtn" onClick={() => onPoints(suggestEvenly(counts))} title="Mỗi câu một điểm bằng nhau">Chia đều</button>
          </span>
        </header>
        <div className="xscale__cards">
          {levels.map((d) => {
            const n = counts[d.value] ?? 0;
            const p = points[d.value] ?? 0;
            return (
              <div key={d.value} className="xscale__card">
                <DifficultyTag value={d.value} />
                <span className="xscale__n">{n} câu</span>
                <InputNumber min={0} max={10} step={0.25} precision={2} value={p} onChange={(v) => set(d.value, v)} aria-label={`Tổng điểm phần ${d.label}`} addonAfter="điểm" />
                <small>{p > 0 ? `mỗi câu ${perQuestionText(p, n)} điểm` : 'chưa có điểm'}</small>
              </div>
            );
          })}
        </div>
        <div className={`xscale__sum ${sum === TOTAL_CENTS ? 'is-ok' : 'is-bad'}`}>
          <span>Tổng điểm</span>
          <strong className="tabular">{fmtPoints(sum / 100)} / 10</strong>
          {sum !== TOTAL_CENTS && <em>{sum < TOTAL_CENTS ? `Còn thiếu ${fmtPoints((TOTAL_CENTS - sum) / 100)} điểm` : `Đang dư ${fmtPoints((sum - TOTAL_CENTS) / 100)} điểm`}</em>}
        </div>
      </section>

      <section className="xprev">
        <h2>Xem lại đề <small>{lines.length} câu</small></h2>
        <ol>
          {lines.map((q, i) => (
            <PreviewLine key={q.questionId} q={q} no={i + 1} pts={perQuestion.get(q.questionId) ?? 0} first={i === 0} last={i === lines.length - 1}
              onMove={(d) => move(i, d)} onRemove={() => onLines(lines.filter((l) => l.questionId !== q.questionId))} />
          ))}
        </ol>
      </section>
    </div>
  );
}
