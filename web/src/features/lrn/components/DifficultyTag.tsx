import type { Difficulty } from '../types';
import { DIFFICULTIES, difficultyLabel } from '../labels';

/** Bốn vạch tăng dần như cột sóng: số vạch tô đậm là mức độ (Nhận biết 1 vạch, Vận dụng cao 4 vạch); chưa phân loại là bốn vạch rỗng. */
const BARS = [0, 1, 2, 3];
const levelOf = (v: Difficulty | null | undefined) => DIFFICULTIES.findIndex((x) => x.value === v) + 1;

function Bars({ value }: { value: Difficulty | null | undefined }) {
  const n = levelOf(value);
  return <span className="dbars" aria-hidden>{BARS.map((i) => <b key={i} className={i < n ? 'is-on' : ''} />)}</span>;
}
import './lrn.css';

/** Nhãn độ khó: một chấm màu đậm dần theo mức kèm tên; `short` dùng tên viết tắt ở chỗ hẹp. null là chưa phân loại. */
export function DifficultyTag({ value, short = false }: { value: Difficulty | null | undefined; short?: boolean }) {
  const d = DIFFICULTIES.find((x) => x.value === value);
  return (
    <span className={`dtag dtag--${value ?? 'none'}`} title={d ? `${d.label}: ${d.hint}` : 'Chưa phân loại độ khó'}>
      <Bars value={value} />{short && d ? d.short : difficultyLabel(value)}
    </span>
  );
}

/** Chọn một trong bốn độ khó, dùng như ô của Form.Item. */
export function DifficultyPicker({ value, onChange, disabled }: { value?: Difficulty; onChange?: (v: Difficulty) => void; disabled?: boolean }) {
  return (
    <div className="dpick" role="radiogroup" aria-label="Độ khó">
      {DIFFICULTIES.map((d) => (
        <button
          key={d.value} type="button" role="radio" aria-checked={value === d.value} disabled={disabled}
          className={value === d.value ? 'is-on' : ''} onClick={() => onChange?.(d.value)}
        >
          <span className="dtag"><Bars value={d.value} />{d.label}</span>
          <small>{d.hint}</small>
        </button>
      ))}
    </div>
  );
}
