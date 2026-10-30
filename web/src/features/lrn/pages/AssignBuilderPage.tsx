import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query';
import { DatePicker, Input, InputNumber } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { ArrowDown, ArrowUp, Check, Plus, X } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { message } from '@/shared/lib/antdStatic';
import { lrnApi } from '../api';
import { AiEmpty } from '../components/parts';
import { subjectVars } from '../components/tone';
import { useDebounced } from '../hooks';
import { DURATION_PRESETS } from '../labels';
import type { ClassSubjectOption, QuestionItem } from '../types';
import '../components/lrn.css';

interface Line { question: QuestionItem; points: number }

const stamp = (d: Dayjs) => d.format('YYYY-MM-DDTHH:mm:ss');

/** Mốc hạn nộp gợi ý: một cú bấm thay cho việc chọn ngày giờ. */
const DUE_PRESETS: { label: string; at: () => Dayjs }[] = [
  { label: 'Tối nay 21:00', at: () => dayjs().hour(21).minute(0).second(0) },
  { label: 'Ngày mai 21:00', at: () => dayjs().add(1, 'day').hour(21).minute(0).second(0) },
  { label: '3 ngày nữa', at: () => dayjs().add(3, 'day').hour(21).minute(0).second(0) },
  { label: '1 tuần nữa', at: () => dayjs().add(7, 'day').hour(21).minute(0).second(0) },
];

/**
 * Giao bài tập (LRN-02, BR-21): chọn lớp và môn mình phụ trách, nhặt câu hỏi Sẵn dùng của môn đó, đặt hạn nộp và thời lượng.
 * Bài giao ngay và không sửa, không xóa, không gia hạn được nữa.
 */
export function AssignBuilderPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const pairsQ = useQuery({ queryKey: ['lrn', 'class-subjects'], queryFn: lrnApi.assignments.classSubjects });
  const pairs = useMemo(() => pairsQ.data ?? [], [pairsQ.data]);

  const [pickedTarget, setTarget] = useState<ClassSubjectOption | null>(null);
  // Chỉ có một (lớp, môn) thì chọn sẵn.
  const target = pickedTarget ?? (pairs.length === 1 ? pairs[0] : null);
  const [lines, setLines] = useState<Line[]>([]);
  const [typedTitle, setTitle] = useState('');
  const [titleTouched, setTitleTouched] = useState(false);
  const [instruction, setInstruction] = useState('');
  const [openAt, setOpenAt] = useState<Dayjs | null>(null);
  const [scheduled, setScheduled] = useState(false);
  const [dueAt, setDueAt] = useState<Dayjs | null>(() => DUE_PRESETS[1].at());
  const [duration, setDuration] = useState<number | null>(null);
  const [customDuration, setCustomDuration] = useState(false);
  const [search, setSearch] = useState('');
  const [onlyMine, setOnlyMine] = useState(false);
  const [saving, setSaving] = useState(false);
  const dSearch = useDebounced(search);

  const title = titleTouched ? typedTitle : target ? `Rèn luyện ${target.subjectName} ${target.className}` : '';

  const bank = useInfiniteQuery({
    queryKey: ['lrn', 'questions', 'picker', target?.subjectId, dSearch, onlyMine],
    enabled: !!target, initialPageParam: 1,
    queryFn: ({ pageParam }) => lrnApi.questions.list({ subjectId: target!.subjectId, status: 'Ready', mine: onlyMine, search: dSearch || undefined, page: pageParam, pageSize: 12 }),
    getNextPageParam: (last, pages) => (pages.flatMap((p) => p.items).length < last.total ? pages.length + 1 : undefined),
  });
  const pool = useMemo(() => bank.data?.pages.flatMap((p) => p.items) ?? [], [bank.data]);
  const picked = useMemo(() => new Set(lines.map((l) => l.question.questionId)), [lines]);

  const pickTarget = (p: ClassSubjectOption) => {
    // Đổi sang môn khác thì các câu đã chọn không còn hợp lệ (bài chỉ gồm câu cùng môn).
    if (target && target.subjectId !== p.subjectId) setLines([]);
    setTarget(p);
  };

  const toggle = (q: QuestionItem) =>
    setLines((cur) => (cur.some((l) => l.question.questionId === q.questionId)
      ? cur.filter((l) => l.question.questionId !== q.questionId)
      : [...cur, { question: q, points: 1 }]));
  const move = (i: number, d: -1 | 1) => setLines((cur) => {
    const j = i + d;
    if (j < 0 || j >= cur.length) return cur;
    const next = [...cur];
    [next[i], next[j]] = [next[j], next[i]];
    return next;
  });
  const setPoints = (i: number, v: number | null) => setLines((cur) => cur.map((l, k) => (k === i ? { ...l, points: v ?? 1 } : l)));

  const totalPoints = lines.reduce((s, l) => s + l.points, 0);
  const problems: string[] = [];
  if (!target) problems.push('chọn lớp và môn');
  if (lines.length === 0) problems.push('chọn ít nhất một câu hỏi');
  if (!title.trim()) problems.push('đặt tên bài');
  if (!dueAt) problems.push('chọn hạn nộp');
  else if (!dueAt.isAfter(dayjs())) problems.push('chọn hạn nộp ở tương lai');
  else if (scheduled && openAt && !dueAt.isAfter(openAt)) problems.push('hạn nộp phải sau giờ mở bài');
  if (customDuration && (duration === null || duration < 1)) problems.push('nhập thời lượng làm bài');

  const submit = () => {
    if (problems.length || !target || !dueAt) return;
    confirmAction({
      title: 'Giao bài này cho lớp?',
      content: (
        <div className="confirm-list">
          <p><strong>{title.trim()}</strong> · {target.subjectName}, lớp {target.className}</p>
          <p>{lines.length} câu, hạn nộp {dueAt.format('HH:mm DD/MM/YYYY')}, {duration ? `giới hạn ${duration} phút` : 'không giới hạn thời gian làm'}.</p>
          <p>Bài đã giao <strong>không sửa, không xóa, không gia hạn</strong> được nữa. Học sinh trong lớp sẽ nhận thông báo ngay.</p>
        </div>
      ),
      okText: 'Giao bài',
      onOk: async () => {
        setSaving(true);
        try {
          const created = await lrnApi.assignments.create({
            subjectId: target.subjectId, classId: target.classId, title: title.trim(), instruction: instruction.trim() || undefined,
            openAt: scheduled && openAt ? stamp(openAt) : undefined, dueAt: stamp(dueAt), durationMinutes: duration ?? undefined,
            items: lines.map((l) => ({ questionId: l.question.questionId, points: l.points })),
          });
          await qc.invalidateQueries({ queryKey: ['lrn', 'assignments'] });
          await qc.invalidateQueries({ queryKey: ['lrn', 'questions'] });
          message.success('Đã giao bài. Học sinh đã nhận thông báo.');
          navigate(`/lrn/assigned?id=${created.assignment.assignmentId}`);
        } catch (e) { showError(e); } finally { setSaving(false); }
      },
    });
  };

  const bySubject = useMemo(() => {
    const m = new Map<number, ClassSubjectOption[]>();
    for (const p of pairs) m.set(p.subjectId, [...(m.get(p.subjectId) ?? []), p]);
    return [...m.values()];
  }, [pairs]);

  if (!pairsQ.isLoading && pairs.length === 0) {
    return (
      <div className="build">
        <AiEmpty title="Chưa có lớp nào để giao bài" hint="Thầy cô chỉ giao được bài cho lớp và môn mình phụ trách theo thời khóa biểu đã phát hành. Liên hệ hiệu phó nếu cần kiểm tra lại." />
      </div>
    );
  }

  return (
    <div className="build">
      <div className="build__steps">
        <h1>Giao bài tập</h1>

        <section className="step">
          <span className="step__no">1</span>
          <div className="step__body">
            <h2>Giao cho lớp nào?</h2>
            <div className="targets">
              {bySubject.map((group) => (
                <div key={group[0].subjectId} className="targets__row" style={subjectVars(group[0].subjectId)}>
                  <span className="targets__subject">{group[0].subjectName}</span>
                  {group.map((p) => (
                    <button
                      key={`${p.classId}-${p.subjectId}`} type="button"
                      className={target?.classId === p.classId && target?.subjectId === p.subjectId ? 'is-on' : ''} onClick={() => pickTarget(p)}
                    >{p.className}</button>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className={`step${target ? '' : ' is-locked'}`}>
          <span className="step__no">2</span>
          <div className="step__body">
            <h2>Chọn câu hỏi {target && <small>môn {target.subjectName}</small>}</h2>
            {target && (
              <>
                <div className="picker__bar">
                  <Input.Search allowClear placeholder="Tìm trong kho" value={search} onChange={(e) => setSearch(e.target.value)} />
                  <button type="button" className={`pill${onlyMine ? ' is-on' : ''}`} onClick={() => setOnlyMine(!onlyMine)} aria-pressed={onlyMine}>Chỉ câu của tôi</button>
                </div>
                {pool.length === 0 && !bank.isLoading ? (
                  <p className="picker__none">Kho chưa có câu Sẵn dùng nào của môn này. Soạn thêm ở mục Kho câu hỏi.</p>
                ) : (
                  <ul className="picker">
                    {pool.map((q) => {
                      const on = picked.has(q.questionId);
                      return (
                        <li key={q.questionId}>
                          <button type="button" className={on ? 'is-on' : ''} onClick={() => toggle(q)} aria-pressed={on}>
                            <span className="picker__check">{on ? <Check size={14} strokeWidth={3} /> : <Plus size={14} />}</span>
                            <span className="picker__text">{q.content}</span>
                            <span className="picker__tags">
                              {q.gradeLevel && <span className="chip">Khối {q.gradeLevel}</span>}
                              {q.topic && <span className="chip">{q.topic}</span>}
                              {q.isMultiple && <span className="chip chip--warn">Chọn nhiều</span>}
                            </span>
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
                {bank.hasNextPage && <button type="button" className="linkbtn" disabled={bank.isFetchingNextPage} onClick={() => void bank.fetchNextPage()}>Xem thêm câu hỏi</button>}
              </>
            )}
          </div>
        </section>

        <section className="step">
          <span className="step__no">3</span>
          <div className="step__body">
            <h2>Thời gian</h2>
            <div className="when">
              <div className="when__row">
                <span>Mở bài</span>
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
              <div className="when__row">
                <span>Thời gian làm</span>
                <div className="when__ctl">
                <div className="pillset pillset--wrap">
                  <button type="button" className={duration === null && !customDuration ? 'is-on' : ''} onClick={() => { setDuration(null); setCustomDuration(false); }}>Không giới hạn</button>
                  {DURATION_PRESETS.map((m) => (
                    <button key={m} type="button" className={duration === m && !customDuration ? 'is-on' : ''} onClick={() => { setDuration(m); setCustomDuration(false); }}>{m} phút</button>
                  ))}
                  <button type="button" className={customDuration ? 'is-on' : ''} onClick={() => { setCustomDuration(true); setDuration((d) => d ?? 20); }}>Tự nhập</button>
                </div>
                {customDuration && <InputNumber min={1} max={600} precision={0} value={duration} onChange={(v) => setDuration(v)} addonAfter="phút" />}
                </div>
              </div>
              {duration !== null && <p className="when__hint">Đồng hồ chạy từ lúc học sinh bấm Bắt đầu và tự nộp bài khi hết giờ, không muộn hơn hạn nộp.</p>}
            </div>
          </div>
        </section>
      </div>

      <aside className="sheet">
        <div className="sheet__inner">
          <h2>Bài sẽ giao</h2>
          <Input value={title} maxLength={200} placeholder="Tên bài tập" onChange={(e) => { setTitle(e.target.value); setTitleTouched(true); }} aria-label="Tên bài tập" />
          <Input.TextArea value={instruction} maxLength={500} rows={2} placeholder="Lời dặn cho học sinh (không bắt buộc)" onChange={(e) => setInstruction(e.target.value)} />

          {lines.length === 0 ? (
            <p className="sheet__empty">Chưa có câu nào. Bấm vào câu hỏi ở bên trái để thêm vào bài.</p>
          ) : (
            <ol className="sheet__lines">
              {lines.map((l, i) => (
                <li key={l.question.questionId}>
                  <span className="sheet__no">{i + 1}</span>
                  <span className="sheet__text" title={l.question.content}>{l.question.content}</span>
                  <InputNumber className="sheet__pts" size="small" min={0.25} max={10} step={0.25} value={l.points} onChange={(v) => setPoints(i, v)} aria-label={`Điểm câu ${i + 1}`} />
                  <span className="sheet__ctl">
                    <button type="button" aria-label="Lên" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp size={13} /></button>
                    <button type="button" aria-label="Xuống" disabled={i === lines.length - 1} onClick={() => move(i, 1)}><ArrowDown size={13} /></button>
                    <button type="button" aria-label="Bỏ câu này" onClick={() => toggle(l.question)}><X size={13} /></button>
                  </span>
                </li>
              ))}
            </ol>
          )}

          <div className="sheet__total">
            <span>{lines.length} câu · tổng {totalPoints.toLocaleString('vi-VN')} điểm</span>
            <small>Điểm học sinh quy về thang 10</small>
          </div>
          {problems.length > 0 && <p className="sheet__todo">Còn thiếu: {problems.join(', ')}.</p>}
          <button type="button" className="sheet__go" disabled={problems.length > 0 || saving} onClick={submit}>Giao bài</button>
        </div>
      </aside>
    </div>
  );
}
