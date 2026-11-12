import { useQuery } from '@tanstack/react-query';
import { InputNumber } from 'antd';
import { useMemo, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { message } from '@/shared/lib/antdStatic';
import { showError } from '@/shared/lib/feedback';
import { lrnApi } from '../api';
import { DIFFICULTIES, difficultyLabel } from '../labels';
import type { Difficulty, MatrixRow, PickShortage, QuestionItem } from '../types';
import { AiEmpty } from './parts';
import './lrn.css';

const FIELD: Record<Difficulty, Exclude<keyof MatrixRow, 'topic'>> = { Recognize: 'recognize', Understand: 'understand', Apply: 'apply', HighApply: 'highApply' };
const cellKey = (topic: string, d: Difficulty) => `${topic}\u0000${d}`;

interface Props {
  subjectId: number;
  grade: number;
  /** Nhận các câu vừa bốc để đưa vào phiếu "Bài sẽ giao" (thay các câu đang chọn). */
  onPick: (questions: QuestionItem[]) => void;
}

/**
 * Ma trận đề: hàng là chủ đề (bài), cột là bốn mức độ. Giáo viên điền số câu cần lấy ở mỗi ô (số nhỏ bên cạnh là số câu Sẵn dùng
 * của kho), bấm bốc thì hệ thống chọn ngẫu nhiên đúng số câu đó. Chỉ là gợi ý: giáo viên đổi câu thoải mái ở phiếu bên phải.
 */
export function ExamMatrix({ subjectId, grade, onPick }: Props) {
  const matrix = useQuery({ queryKey: ['lrn', 'matrix', subjectId, grade], queryFn: () => lrnApi.questions.matrix(subjectId, grade) });
  const [cells, setCells] = useState<Record<string, number>>({});
  const [shortages, setShortages] = useState<PickShortage[]>([]);
  const [busy, setBusy] = useState(false);
  const rows = useMemo(() => matrix.data ?? [], [matrix.data]);

  const colTotal = (d: Difficulty) => rows.reduce((s, r) => s + (cells[cellKey(r.topic, d)] ?? 0), 0);
  const rowTotal = (r: MatrixRow) => DIFFICULTIES.reduce((s, d) => s + (cells[cellKey(r.topic, d.value)] ?? 0), 0);
  const total = DIFFICULTIES.reduce((s, d) => s + colTotal(d.value), 0);

  const set = (topic: string, d: Difficulty, v: number | null) => setCells((cur) => ({ ...cur, [cellKey(topic, d)]: v ?? 0 }));

  const run = async () => {
    const list = rows.flatMap((r) => DIFFICULTIES.map((d) => ({ topic: r.topic, difficulty: d.value, count: cells[cellKey(r.topic, d.value)] ?? 0 }))).filter((c) => c.count > 0);
    setBusy(true);
    try {
      const res = await lrnApi.questions.pick(subjectId, grade, list);
      setShortages(res.shortages);
      onPick(res.questions);
      message.success(`Đã chọn ${res.questions.length} câu. Xem lại và đổi câu ở phiếu bên phải.`);
    } catch (e) { showError(e); } finally { setBusy(false); }
  };

  if (matrix.isLoading) return <p className="picker__none">Đang tải ma trận…</p>;
  if (matrix.isError) return <p className="picker__none">{getErrorMessage(matrix.error)}</p>;
  if (rows.length === 0) {
    return <AiEmpty title="Kho chưa có câu nào để dựng ma trận" hint={`Chưa có câu Sẵn dùng đã phân loại độ khó cho môn và Khối ${grade} này. Soạn thêm hoặc phân loại câu cũ ở mục Kho câu hỏi.`} />;
  }

  return (
    <div className="mx">
      <div className="mx__scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Bài / chủ đề</th>
              {DIFFICULTIES.map((d) => <th key={d.value} scope="col" title={d.hint}>{d.label}</th>)}
              <th scope="col">Tổng</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.topic}>
                <td title={r.topic}>{r.topic}</td>
                {DIFFICULTIES.map((d) => {
                  const have = r[FIELD[d.value]];
                  return (
                    <td key={d.value}>
                      <span className={`mx__cell${have === 0 ? ' is-empty' : ''}`}>
                        <InputNumber
                          size="small" min={0} max={have} precision={0} controls={false} disabled={have === 0} placeholder="0"
                          value={cells[cellKey(r.topic, d.value)] || null} onChange={(v) => set(r.topic, d.value, v)}
                          aria-label={`${r.topic}, ${difficultyLabel(d.value)}: số câu cần lấy, kho có ${have}`}
                        />
                        <small>/{have}</small>
                      </span>
                    </td>
                  );
                })}
                <td className="tabular">{rowTotal(r) || '–'}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td>Cơ cấu đề</td>
              {DIFFICULTIES.map((d) => (
                <td key={d.value} className="tabular">{colTotal(d.value) || '–'}{total > 0 && colTotal(d.value) > 0 && <small> · {Math.round((colTotal(d.value) / total) * 100)}%</small>}</td>
              ))}
              <td className="tabular">{total || '–'}</td>
            </tr>
          </tfoot>
        </table>
      </div>
      <div className="mx__foot">
        <button type="button" className="mx__go" disabled={total === 0 || busy} onClick={() => void run()}>{busy ? 'Đang sinh đề…' : total > 0 ? `Sinh đề ngẫu nhiên (${total} câu)` : 'Sinh đề ngẫu nhiên'}</button>
      </div>
      {shortages.length > 0 && (
        <p className="mx__short">
          Kho không đủ câu ở {shortages.length} ô: {shortages.map((s) => `${s.topic} (${difficultyLabel(s.difficulty)}) cần ${s.wanted}, có ${s.available}`).join('; ')}. Đã lấy hết số câu có.
        </p>
      )}
    </div>
  );
}
