import { useQueryClient } from '@tanstack/react-query';
import { Form, Input, Segmented, Select } from 'antd';
import { Check } from 'lucide-react';
import { FormModal } from '@/shared/components/FormModal';
import { message } from '@/shared/lib/antdStatic';
import { lrnApi } from '../api';
import { GRADES, OPTION_LABELS } from '../labels';
import type { OptionInput, QuestionDetail, SaveQuestionInput } from '../types';
import './lrn.css';

interface Values {
  subjectId: number;
  gradeLevel: number;
  topic: string;
  content: string;
  options: OptionInput[];
  explanation?: string;
}

const EMPTY_OPTIONS: OptionInput[] = OPTION_LABELS.map((label) => ({ label, content: '', isCorrect: false }));

/** Bốn lựa chọn A–D: gõ nội dung, bấm ô tròn để đánh dấu đáp án đúng (1 đến 3 đáp án, BR-20). */
function OptionsEditor({ value = EMPTY_OPTIONS, onChange }: { value?: OptionInput[]; onChange?: (v: OptionInput[]) => void }) {
  const patch = (i: number, p: Partial<OptionInput>) => onChange?.(value.map((o, idx) => (idx === i ? { ...o, ...p } : o)));
  const correct = value.filter((o) => o.isCorrect).length;
  return (
    <div className="oe">
      {value.map((o, i) => (
        <div key={o.label} className={`oe__row${o.isCorrect ? ' is-correct' : ''}`}>
          <button
            type="button" className="oe__mark" aria-pressed={o.isCorrect} aria-label={`Đặt ${o.label} là đáp án đúng`}
            onClick={() => patch(i, { isCorrect: !o.isCorrect })}
          >{o.isCorrect ? <Check size={16} strokeWidth={3} /> : o.label}</button>
          <Input
            variant="borderless" value={o.content} maxLength={500} placeholder={`Nội dung lựa chọn ${o.label}`} aria-label={`Lựa chọn ${o.label}`}
            onChange={(e) => patch(i, { content: e.target.value })}
          />
        </div>
      ))}
      <p className="oe__hint">
        {correct === 0 ? 'Bấm vào chữ cái để chọn ít nhất 1 đáp án đúng.' : correct > 1 ? `${correct} đáp án đúng: học sinh sẽ được chọn nhiều đáp án.` : '1 đáp án đúng.'}
      </p>
    </div>
  );
}

interface Props {
  open: boolean;
  /** null = soạn mới; có giá trị = sửa (kể cả sửa nháp AI). */
  editing: QuestionDetail | null;
  subjects: { subjectId: number; name: string }[];
  defaultSubjectId?: number;
  onClose: () => void;
}

export function QuestionEditor({ open, editing, subjects, defaultSubjectId, onClose }: Props) {
  const qc = useQueryClient();
  const [form] = Form.useForm<Values>();

  const save = async (v: Values) => {
    const body: SaveQuestionInput = {
      subjectId: v.subjectId, gradeLevel: v.gradeLevel, topic: v.topic, content: v.content, explanation: v.explanation, options: v.options,
    };
    if (editing) await lrnApi.questions.update(editing.questionId, body);
    else await lrnApi.questions.create(body);
    await qc.invalidateQueries({ queryKey: ['lrn', 'questions'] });
    message.success(editing ? 'Đã lưu câu hỏi.' : 'Đã thêm câu hỏi vào kho.');
  };

  const initial: Partial<Values> = editing
    ? {
      subjectId: editing.subjectId, gradeLevel: editing.gradeLevel ?? undefined, topic: editing.topic ?? '', content: editing.content,
      explanation: editing.explanation ?? undefined,
      options: OPTION_LABELS.map((label) => {
        const o = editing.options.find((x) => x.label === label);
        return { label, content: o?.content ?? '', isCorrect: o?.isCorrect ?? false };
      }),
    }
    : { subjectId: defaultSubjectId, gradeLevel: 6, options: EMPTY_OPTIONS };

  return (
    <FormModal<Values>
      open={open} title={editing ? (editing.status === 'Draft' ? 'Sửa câu nháp' : 'Sửa câu hỏi') : 'Soạn câu hỏi'}
      variant="drawer" width={620} form={form} onClose={onClose} onSubmit={save} initialValues={initial}
    >
      <Form.Item name="subjectId" label="Môn học" rules={[{ required: true, message: 'Chọn môn học cho câu hỏi.' }]}>
        <Select placeholder="Chọn môn" options={subjects.map((s) => ({ value: s.subjectId, label: s.name }))} showSearch optionFilterProp="label" />
      </Form.Item>
      <Form.Item name="gradeLevel" label="Khối" rules={[{ required: true, message: 'Chọn khối của câu hỏi.' }]}>
        <Segmented options={GRADES.map((g) => ({ value: g, label: `Khối ${g}` }))} />
      </Form.Item>
      <Form.Item name="topic" label="Chủ đề" rules={[{ required: true, whitespace: true, message: 'Nhập chủ đề để dễ tìm lại câu hỏi.' }]}>
        <Input maxLength={150} placeholder="Ví dụ: Phân số, Quang hợp, Thì hiện tại đơn" />
      </Form.Item>
      <Form.Item name="content" label="Nội dung câu hỏi" rules={[{ required: true, whitespace: true, message: 'Nhập nội dung câu hỏi.' }]}>
        <Input.TextArea rows={4} maxLength={2000} showCount placeholder="Câu có nhiều đáp án đúng nên nêu rõ trong nội dung, ví dụ: “Chọn các số chẵn”." />
      </Form.Item>
      <Form.Item
        name="options" label="Bốn lựa chọn"
        rules={[{
          validator: (_, v: OptionInput[] | undefined) => {
            const list = v ?? [];
            const empty = list.find((o) => !o.content.trim());
            if (empty) return Promise.reject(new Error(`Lựa chọn ${empty.label} chưa có nội dung. Nhập nội dung rồi lưu lại.`));
            const n = list.filter((o) => o.isCorrect).length;
            if (n < 1) return Promise.reject(new Error('Chọn ít nhất 1 đáp án đúng.'));
            if (n > 3) return Promise.reject(new Error('Một câu có tối đa 3 đáp án đúng.'));
            return Promise.resolve();
          },
        }]}
      >
        <OptionsEditor />
      </Form.Item>
      <Form.Item name="explanation" label="Giải thích (không bắt buộc)">
        <Input.TextArea rows={3} maxLength={2000} showCount placeholder="Học sinh xem được giải thích ngay sau khi nộp bài." />
      </Form.Item>
    </FormModal>
  );
}
