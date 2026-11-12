import { useQueryClient } from '@tanstack/react-query';
import { DatePicker } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { FileDown, Send } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { message, modal } from '@/shared/lib/antdStatic';
import { downloadFile } from '@/shared/lib/download';
import { confirmAction } from '@/shared/lib/feedback';
import { lrnApi } from '../../api';
import { showDownloadError } from '../../downloadError';
import type { PerLevel } from '../../examPoints';
import { DIFFICULTIES } from '../../labels';
import type { ClassSubjectOption, PaperFormat, QuestionItem } from '../../types';
import '../lrn.css';

const stamp = (d: Dayjs) => d.format('YYYY-MM-DDTHH:mm:ss');

/** Mốc hạn nộp gợi ý: một cú bấm thay cho việc chọn ngày giờ. */
const DUE_PRESETS: { label: string; at: () => Dayjs }[] = [
  { label: 'Tối nay 21:00', at: () => dayjs().hour(21).minute(0).second(0) },
  { label: 'Ngày mai 21:00', at: () => dayjs().add(1, 'day').hour(21).minute(0).second(0) },
  { label: '3 ngày nữa', at: () => dayjs().add(3, 'day').hour(21).minute(0).second(0) },
];

interface Props {
  subjectId: number;
  grade: number;
  title: string;
  instruction: string;
  duration: number | null;
  lines: QuestionItem[];
  points: PerLevel;
  /** Các lớp giáo viên đang phụ trách môn này. */
  classes: ClassSubjectOption[];
  /** Gọi khi đã giao được cho ít nhất một lớp: từ đó trang khóa các bước soạn để mọi lớp nhận cùng một đề, và không hỏi "rời trang?" nữa. */
  onGiven: () => void;
}

/**
 * Bước 4: dùng đề vào việc gì. Giao cho lớp (cần lớp, giờ mở, hạn nộp) hoặc tải file để in (không cần gì thêm), hoặc cả hai.
 * Lớp và hạn nộp chỉ hỏi ở đây vì chỉ việc giao mới dùng đến.
 */
export function StepFinish({ subjectId, grade, title, instruction, duration, lines, points, classes, onGiven }: Props) {
  const navigate = useNavigate();
  const qc = useQueryClient();
  // Mặc định chọn các lớp cùng khối với đề; giáo viên bấm là chuyển sang danh sách tự chọn.
  const [override, setOverride] = useState<number[] | null>(null);
  const picked = override ?? classes.filter((c) => c.gradeLevel === grade).map((c) => c.classId);
  const [given, setGiven] = useState<number[]>([]);
  const [scheduled, setScheduled] = useState(false);
  const [openAt, setOpenAt] = useState<Dayjs | null>(null);
  const [dueAt, setDueAt] = useState<Dayjs | null>(() => DUE_PRESETS[1].at());
  const [saving, setSaving] = useState(false);
  const [withAnswers, setWithAnswers] = useState(true);
  const [exporting, setExporting] = useState<PaperFormat | null>(null);

  const sections = useMemo(() => DIFFICULTIES.filter((d) => lines.some((q) => q.difficulty === d.value))
    .map((d) => ({ difficulty: d.value, points: points[d.value] ?? 0 })), [lines, points]);
  const items = useMemo(() => lines.map((q) => ({ questionId: q.questionId })), [lines]);

  const toggle = (id: number) => setOverride(picked.includes(id) ? picked.filter((x) => x !== id) : [...picked, id]);

  const problems: string[] = [];
  if (picked.length === 0) problems.push('chọn ít nhất một lớp');
  if (!dueAt) problems.push('chọn hạn nộp');
  else if (!dueAt.isAfter(dayjs())) problems.push('chọn hạn nộp ở tương lai');
  else if (scheduled && openAt && !dueAt.isAfter(openAt)) problems.push('hạn nộp phải sau giờ mở bài');

  const chosen = classes.filter((c) => picked.includes(c.classId) && !given.includes(c.classId));

  const assign = () => {
    if (problems.length || !dueAt || chosen.length === 0) return;
    confirmAction({
      title: `Giao bài cho lớp ${chosen.map((c) => c.className).join(', ')}?`,
      content: (
        <div className="cfm">
          <dl>
            <dt>Đề</dt><dd>{title.trim()}</dd>
            <dt>Làm bài</dt><dd>{lines.length} câu · 10 điểm · {duration ? `${duration} phút` : 'không giới hạn thời gian'}</dd>
            {scheduled && openAt && (<><dt>Mở bài</dt><dd>{openAt.format('HH:mm DD/MM/YYYY')}</dd></>)}
            <dt>Hạn nộp</dt><dd>{dueAt.format('HH:mm DD/MM/YYYY')}</dd>
          </dl>
          <p className="cfm__warn">Giao rồi thì không sửa, không xóa, không gia hạn được. Học sinh nhận thông báo ngay.</p>
        </div>
      ),
      okText: 'Giao bài',
      onOk: async () => {
        setSaving(true);
        const failed: string[] = [];
        let firstId: number | null = null;
        for (const c of chosen) {
          try {
            const created = await lrnApi.assignments.create({
              subjectId, classId: c.classId, title: title.trim(), instruction: instruction.trim() || undefined,
              openAt: scheduled && openAt ? stamp(openAt) : undefined, dueAt: stamp(dueAt), durationMinutes: duration ?? undefined,
              items, sections,
            });
            firstId ??= created.assignment.assignmentId;
            setGiven((cur) => [...cur, c.classId]);
          } catch (e) { failed.push(`Lớp ${c.className}: ${getErrorMessage(e)}`); }
        }
        if (firstId !== null) onGiven();
        await qc.invalidateQueries({ queryKey: ['lrn', 'assignments'] });
        await qc.invalidateQueries({ queryKey: ['lrn', 'questions'] });
        setSaving(false);
        if (failed.length > 0) {
          modal.error({
            title: firstId === null ? 'Chưa giao được bài' : 'Một số lớp chưa giao được',
            content: <div style={{ whiteSpace: 'pre-line' }}>{failed.join('\n')}</div>, okText: 'Đóng',
          });
          if (firstId === null) return;
        }
        message.success('Đã giao bài. Học sinh đã nhận thông báo.');
        if (failed.length === 0) navigate(`/lrn/assigned?id=${firstId}`);
      },
    });
  };

  const exportFile = async (format: PaperFormat) => {
    setExporting(format);
    try {
      await downloadFile('/lrn/papers/export', undefined, 'post', {
        subjectId, title: title.trim(), instruction: instruction.trim() || undefined, durationMinutes: duration ?? undefined,
        items, sections, format, withAnswers,
      });
    } catch (e) { await showDownloadError(e); } finally { setExporting(null); }
  };

  return (
    <div className="xfinish">
      <header className="xfinish__head">
        <h2>{title.trim()}</h2>
      </header>
      {given.length > 0 && (
        <p className="xfinish__locked">
          Đề đã giao cho lớp {classes.filter((c) => given.includes(c.classId)).map((c) => c.className).join(', ')} nên không sửa nữa, để mọi lớp nhận cùng một đề. Giao tiếp cho các lớp còn lại ở đây.
        </p>
      )}
      <div className="xfinish__cards">
        <section className="xfinish__card">
          <h2><Send size={18} />Giao cho lớp</h2>
          <div className="when">
            <div className="when__row">
              <span>Lớp</span>
              <div className="when__ctl">
                <div className="pillset pillset--wrap">
                  {classes.map((c) => {
                    const done = given.includes(c.classId);
                    return (
                      <button key={c.classId} type="button" disabled={done} className={picked.includes(c.classId) || done ? 'is-on' : ''} aria-pressed={picked.includes(c.classId)} onClick={() => toggle(c.classId)}>
                        {c.className}{done ? ' · đã giao' : ''}
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>
            <div className="when__row">
              <span>Hẹn giờ giao</span>
              <div className="when__ctl">
                <div className="pillset">
                  <button type="button" className={!scheduled ? 'is-on' : ''} onClick={() => setScheduled(false)}>Ngay bây giờ</button>
                  <button type="button" className={scheduled ? 'is-on' : ''} onClick={() => { setScheduled(true); setOpenAt((v) => v ?? dayjs().add(1, 'hour').minute(0).second(0)); }}>Hẹn giờ</button>
                </div>
                {scheduled && <DatePicker showTime={{ format: 'HH:mm' }} format="DD/MM/YYYY HH:mm" value={openAt} onChange={setOpenAt} allowClear={false} placeholder="Giờ mở bài" />}
              </div>
            </div>
            <div className="when__row">
              <span>Hạn nộp</span>
              <div className="when__ctl">
                <div className="pillset pillset--wrap">
                  {DUE_PRESETS.map((p) => {
                    const at = p.at();
                    return <button key={p.label} type="button" className={dueAt?.isSame(at, 'minute') ? 'is-on' : ''} onClick={() => setDueAt(at)}>{p.label}</button>;
                  })}
                </div>
                <DatePicker showTime={{ format: 'HH:mm' }} format="DD/MM/YYYY HH:mm" value={dueAt} onChange={setDueAt} allowClear={false} placeholder="Hoặc chọn ngày giờ" />
              </div>
            </div>
          </div>
          <div className="xfinish__act">
            {problems.length > 0 && <p className="xfinish__todo">Còn thiếu: {problems.join(', ')}.</p>}
            <button type="button" className="xfinish__go" disabled={problems.length > 0 || saving || chosen.length === 0} onClick={assign}>
              {saving ? 'Đang giao…' : chosen.length > 1 ? `Giao bài cho ${chosen.length} lớp` : 'Giao bài'}
            </button>
          </div>
        </section>

        <section className="xfinish__card">
          <h2><FileDown size={18} />Tạo bản in đề</h2>
          <p className="xfinish__note">Tải file về để tự in hoặc chỉnh sửa. Không cần chọn lớp hay hạn nộp, và không giao bài cho học sinh.</p>
          <label className="xfinish__tick"><input type="checkbox" checked={withAnswers} onChange={(e) => setWithAnswers(e.target.checked)} />Kèm đáp án và giải thích (trang riêng cuối file)</label>
          <p className="xfinish__note">Word để chỉnh sửa được, PDF để in thẳng.</p>
          <div className="xfinish__act xfinish__files">
            <button type="button" className="xfile xfile--word" disabled={exporting !== null} onClick={() => void exportFile('docx')}><FileDown size={17} />{exporting === 'docx' ? 'Đang tạo…' : 'Tải Word (.docx)'}</button>
            <button type="button" className="xfile xfile--pdf" disabled={exporting !== null} onClick={() => void exportFile('pdf')}><FileDown size={17} />{exporting === 'pdf' ? 'Đang tạo…' : 'Tải PDF'}</button>
          </div>
        </section>
      </div>
    </div>
  );
}
