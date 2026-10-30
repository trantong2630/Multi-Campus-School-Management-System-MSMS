import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { fmtScore } from '../time';
import { AnswerStrip } from './parts';
import { bandColor, subjectVars } from './tone';

export interface WallItem {
  assignmentId: number;
  title: string;
  subjectId: number;
  subjectName: string;
  className: string;
  /** Mốc dùng để sắp và ghi ngày: lúc nộp, hoặc hạn nộp với bài không nộp. */
  at: string;
  submitted: boolean;
  score: number | null;
  correctCount: number | null;
  questionCount: number;
}

interface Props {
  items: WallItem[];
  onOpen: (item: WallItem) => void;
  /** Câu mở đầu cho phần tóm tắt, ví dụ "Điểm trung bình của Minh". */
  averageLabel: string;
}

/**
 * Bức tường điểm: mỗi bài một ô với con số lớn, màu theo mức điểm và dải ô đúng/sai theo từng câu.
 * Học sinh và phụ huynh dùng chung. Chỉ hiện bộ lọc môn khi có từ hai môn trở lên.
 */
export function ScoreWall({ items, onOpen, averageLabel }: Props) {
  const [subjectId, setSubjectId] = useState<number | null>(null);
  const subjects = useMemo(() => {
    const m = new Map<number, string>();
    for (const i of items) m.set(i.subjectId, i.subjectName);
    return [...m.entries()];
  }, [items]);
  const shown = subjectId === null ? items : items.filter((i) => i.subjectId === subjectId);

  const scored = shown.filter((i) => i.submitted && i.score !== null);
  const avg = scored.length ? scored.reduce((s, i) => s + (i.score ?? 0), 0) / scored.length : null;
  const missed = shown.filter((i) => !i.submitted).length;

  return (
    <div className="wall">
      <div className="wall__summary">
        <div className="wall__avg">
          <span>{averageLabel}</span>
          <strong className="tabular" style={{ color: bandColor(avg) }}>{fmtScore(avg === null ? null : Math.round(avg * 100) / 100)}</strong>
        </div>
        <div className="wall__facts">
          <span><strong>{scored.length}</strong> bài đã có điểm</span>
          {missed > 0 && <span><strong>{missed}</strong> bài không nộp</span>}
        </div>
        {subjects.length > 1 && (
          <div className="wall__subjects" role="tablist" aria-label="Lọc theo môn">
            <button type="button" role="tab" aria-selected={subjectId === null} className={subjectId === null ? 'is-on' : ''} onClick={() => setSubjectId(null)}>Tất cả</button>
            {subjects.map(([id, name]) => (
              <button key={id} type="button" role="tab" aria-selected={subjectId === id} className={subjectId === id ? 'is-on' : ''} onClick={() => setSubjectId(id)}>{name}</button>
            ))}
          </div>
        )}
      </div>

      <div className="wall__grid">
        {shown.map((i) => (
          <button
            key={i.assignmentId} type="button" className={`tile${i.submitted ? '' : ' is-missed'}`} style={subjectVars(i.subjectId)}
            onClick={() => onOpen(i)}
          >
            <span className="tile__subject">{i.subjectName}</span>
            {i.submitted ? (
              <strong className="tile__score tabular" style={{ color: bandColor(i.score) }}>{fmtScore(i.score)}</strong>
            ) : (
              <strong className="tile__score tile__score--none">Không nộp</strong>
            )}
            <span className="tile__title">{i.title}</span>
            <span className="tile__foot">
              {i.submitted && <AnswerStrip correct={i.correctCount} total={i.questionCount} />}
              <span>{dayjs(i.at).format('DD/MM')} · {i.className}</span>
            </span>
          </button>
        ))}
      </div>
    </div>
  );
}
