import { Input, InputNumber } from 'antd';
import { SubjectMark } from '../parts';
import { subjectVars } from '../tone';
import { DURATION_PRESETS, GRADES } from '../../labels';
import '../lrn.css';

export interface SubjectChoice { subjectId: number; name: string }

interface Props {
  subjects: SubjectChoice[];
  subjectId: number | null;
  onSubject: (id: number) => void;
  grade: number;
  onGrade: (g: number) => void;
  title: string;
  onTitle: (v: string) => void;
  instruction: string;
  onInstruction: (v: string) => void;
  duration: number | null;
  onDuration: (v: number | null) => void;
  customDuration: boolean;
  onCustomDuration: (v: boolean) => void;
}

/** Bước 1: đề thuộc môn nào, khối nào, tên gì, làm bao lâu. Chưa hỏi lớp hay hạn nộp: những thứ đó chỉ cần khi giao. */
export function StepInfo(p: Props) {
  return (
    <div className="xinfo">
      <section>
        <h2>Môn học</h2>
        <div className="xsubjects">
          {p.subjects.map((s) => (
            <button key={s.subjectId} type="button" className={p.subjectId === s.subjectId ? 'is-on' : ''} style={subjectVars(s.subjectId)} onClick={() => p.onSubject(s.subjectId)}>
              <SubjectMark id={s.subjectId} name={s.name} size={36} />
              <span>{s.name}</span>
            </button>
          ))}
        </div>
      </section>

      <section>
        <h2>Khối</h2>
        <div className="pillset" role="radiogroup" aria-label="Khối">
          {GRADES.map((g) => (
            <button key={g} type="button" role="radio" aria-checked={p.grade === g} className={p.grade === g ? 'is-on' : ''} onClick={() => p.onGrade(g)}>Khối {g}</button>
          ))}
        </div>
        <p className="xhint">Kho câu hỏi và ma trận ở bước sau lấy theo khối này.</p>
      </section>

      <section>
        <h2>Tên đề</h2>
        <Input value={p.title} maxLength={200} placeholder="Ví dụ: Kiểm tra 15 phút - Chương 2" onChange={(e) => p.onTitle(e.target.value)} aria-label="Tên đề" />
        <Input.TextArea className="xinfo__note" value={p.instruction} maxLength={500} rows={2} placeholder="Lời dặn cho học sinh (không bắt buộc)" onChange={(e) => p.onInstruction(e.target.value)} />
      </section>

      <section>
        <h2>Thời gian làm bài</h2>
        <div className="pillset pillset--wrap">
          <button type="button" className={p.duration === null && !p.customDuration ? 'is-on' : ''} onClick={() => { p.onDuration(null); p.onCustomDuration(false); }}>Không giới hạn</button>
          {DURATION_PRESETS.map((m) => (
            <button key={m} type="button" className={p.duration === m && !p.customDuration ? 'is-on' : ''} onClick={() => { p.onDuration(m); p.onCustomDuration(false); }}>{m} phút</button>
          ))}
          <button type="button" className={p.customDuration ? 'is-on' : ''} onClick={() => { p.onCustomDuration(true); p.onDuration(p.duration ?? 20); }}>Tự nhập</button>
          {p.customDuration && <InputNumber min={1} max={600} precision={0} value={p.duration} onChange={(v) => p.onDuration(v)} addonAfter="phút" />}
        </div>
      </section>
    </div>
  );
}
