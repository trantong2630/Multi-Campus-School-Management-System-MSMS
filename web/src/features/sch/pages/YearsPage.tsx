import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input, InputNumber } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { CalendarRange, Pencil, Plus, Star } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, toDateString } from '@/shared/lib/format';
import { schApi } from '../api';
import { useYears } from '../hooks/useCatalogs';
import type { AcademicYear, Semester } from '../types';
import '../components/school.css';

interface YearValues { name: string; range: [Dayjs, Dayjs] }
interface SemesterValues { start1: Dayjs; weeks1: number; start2: Dayjs; weeks2: number }

// Số tuần thực học mặc định của THCS theo khung kế hoạch năm học của Bộ: HK1 18 tuần, HK2 17 tuần.
const DEFAULT_WEEKS = { 1: 18, 2: 17 } as const;
/** Ngày kết thúc = ngày bắt đầu + số tuần × 7 − 1. */
const endOf = (start?: Dayjs | null, weeks?: number | null) => (start && weeks ? start.add(weeks * 7 - 1, 'day') : undefined);
const weeksOf = (s: Semester) => Math.max(1, Math.round((dayjs(s.endDate).diff(dayjs(s.startDate), 'day') + 1) / 7));

const DATE_FORMAT = 'DD/MM/YYYY';
const semesterText = (s?: Semester) => (s ? `${formatDate(s.startDate)} – ${formatDate(s.endDate)}` : null);

export function YearsPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const { data, isLoading } = useYears();
  const [yearForm] = Form.useForm<YearValues>();
  const [semForm] = Form.useForm<SemesterValues>();
  const [editing, setEditing] = useState<AcademicYear | 'new' | null>(null);
  const [semesterOf, setSemesterOf] = useState<AcademicYear | null>(null);

  const refresh = () => qc.invalidateQueries({ queryKey: ['sch'] });
  const setCurrent = useMutation({
    mutationFn: (y: AcademicYear) => schApi.years.setCurrent(y.academicYearId),
    onSuccess: () => { void refresh(); message.success('Đã đặt năm học hiện hành.'); },
    onError: (e) => showError(e),
  });

  const saveYear = async (v: YearValues) => {
    const body = { name: v.name, startDate: toDateString(v.range[0]), endDate: toDateString(v.range[1]) };
    if (editing === 'new') await schApi.years.create(body);
    else if (editing) await schApi.years.update(editing.academicYearId, body);
    await refresh();
    message.success('Đã lưu năm học.');
  };

  const start1 = Form.useWatch('start1', semForm);
  const weeks1 = Form.useWatch('weeks1', semForm);
  const start2 = Form.useWatch('start2', semForm);
  const weeks2 = Form.useWatch('weeks2', semForm);
  const end1 = endOf(start1, weeks1);
  const end2 = endOf(start2, weeks2);

  // Chưa chọn ngày bắt đầu HK2 thì gợi ý ngày liền sau HK1; đã chọn rồi thì không tự đổi nữa.
  useEffect(() => {
    if (end1 && !semForm.getFieldValue('start2')) semForm.setFieldValue('start2', end1.add(1, 'day'));
  }, [end1, semForm]);

  const saveSemesters = async (v: SemesterValues) => {
    await schApi.years.configureSemesters(semesterOf!.academicYearId, {
      semester1: { startDate: toDateString(v.start1), endDate: toDateString(endOf(v.start1, v.weeks1)!) },
      semester2: { startDate: toDateString(v.start2), endDate: toDateString(endOf(v.start2, v.weeks2)!) },
    });
    await refresh();
    message.success('Đã lưu hai học kỳ.');
  };

  const sem = (y: AcademicYear, no: number) => y.semesters.find((s) => s.semesterNo === no);
  const range = (a: string, b: string): [Dayjs, Dayjs] => [dayjs(a), dayjs(b)];

  return (
    <>
      <PageHeader
        title="Năm học & Học kỳ"
        description="Đúng một năm học hiện hành. Mỗi năm có hai học kỳ nằm trong năm học, không chồng nhau."
        extra={canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm năm học</Button>}
      />
      <Card className="table-card">
        <DataTable<AcademicYear>
          rowKey="academicYearId" loading={isLoading} dataSource={data}
          emptyText="Chưa có năm học nào."
          columns={[
            { title: 'Năm học', dataIndex: 'name', width: 130, render: (v: string) => <strong className="tabular">{v}</strong> },
            { title: 'Khoảng thời gian', key: 'range', width: 220, className: 'tabular', render: (_: unknown, y: AcademicYear) => `${formatDate(y.startDate)} – ${formatDate(y.endDate)}` },
            { title: 'Học kỳ 1', key: 's1', className: 'tabular', render: (_: unknown, y: AcademicYear) => semesterText(sem(y, 1)) ?? <span className="muted">Chưa khai</span> },
            { title: 'Học kỳ 2', key: 's2', className: 'tabular', render: (_: unknown, y: AcademicYear) => semesterText(sem(y, 2)) ?? <span className="muted">Chưa khai</span> },
            { title: 'Hiện hành', dataIndex: 'isCurrent', width: 130, align: 'center', render: (v: boolean) => v ? <StatusBadge domain="catalog" value="Active" /> : <span className="muted">—</span> },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 130, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, y: AcademicYear) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa năm học', icon: <Pencil size={16} />, onClick: () => setEditing(y) },
                  { key: 'sem', label: 'Khai báo hai học kỳ', icon: <CalendarRange size={16} />, onClick: () => setSemesterOf(y) },
                  {
                    key: 'current', label: 'Đặt làm năm học hiện hành', icon: <Star size={16} />, hidden: y.isCurrent,
                    onClick: () => confirmAction({
                      title: `Đặt ${y.name} làm năm học hiện hành?`,
                      content: 'Các màn hình vận hành sẽ mặc định lọc theo năm học này. Năm học hiện hành trước đó sẽ bỏ đánh dấu.',
                      onOk: () => setCurrent.mutateAsync(y).catch(() => undefined),
                    }),
                  },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      <FormModal<YearValues>
        open={editing !== null} title={editing === 'new' ? 'Thêm năm học' : 'Sửa năm học'}
        form={yearForm} onClose={() => setEditing(null)} onSubmit={saveYear}
        initialValues={editing && editing !== 'new' ? { name: editing.name, range: range(editing.startDate, editing.endDate) } : undefined}
      >
        <Form.Item label="Tên năm học" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên năm học, ví dụ 2026-2027.' }]}>
          <Input maxLength={20} placeholder="2026-2027" />
        </Form.Item>
        <Form.Item label="Từ ngày – đến ngày" name="range" rules={[{ required: true, message: 'Chọn ngày bắt đầu và ngày kết thúc năm học.' }]}>
          <DatePicker.RangePicker format={DATE_FORMAT} style={{ width: '100%' }} />
        </Form.Item>
      </FormModal>

      <FormModal<SemesterValues>
        open={semesterOf !== null} title={`Học kỳ của năm học ${semesterOf?.name ?? ''}`}
        form={semForm} onClose={() => setSemesterOf(null)} onSubmit={saveSemesters}
        initialValues={semesterOf ? {
          start1: sem(semesterOf, 1) ? dayjs(sem(semesterOf, 1)!.startDate) : undefined,
          weeks1: sem(semesterOf, 1) ? weeksOf(sem(semesterOf, 1)!) : DEFAULT_WEEKS[1],
          start2: sem(semesterOf, 2) ? dayjs(sem(semesterOf, 2)!.startDate) : undefined,
          weeks2: sem(semesterOf, 2) ? weeksOf(sem(semesterOf, 2)!) : DEFAULT_WEEKS[2],
        } : undefined}
      >
        {([1, 2] as const).map((no) => {
          const end = no === 1 ? end1 : end2;
          return (
            <div key={no} className="semester-row">
              <Form.Item label={`Học kỳ ${no} — ngày bắt đầu`} name={`start${no}`} rules={[{ required: true, message: `Chọn ngày bắt đầu học kỳ ${no}.` }]}>
                <DatePicker format={DATE_FORMAT} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Số tuần" name={`weeks${no}`} rules={[{ required: true, message: 'Nhập số tuần.' }]}>
                <InputNumber min={1} max={30} precision={0} style={{ width: '100%' }} />
              </Form.Item>
              <div className="semester-row__end">
                <span>Kết thúc</span>
                <b className="tabular">{end ? end.format(DATE_FORMAT) : '—'}</b>
              </div>
            </div>
          );
        })}
        <p className="semester-total">
          Tổng <b className="tabular">{(weeks1 ?? 0) + (weeks2 ?? 0)}</b> tuần. THCS thường là 18 + 17 = 35 tuần; nghỉ Tết giữa kỳ thì lùi ngày bắt đầu hoặc tăng số tuần.
        </p>
      </FormModal>
    </>
  );
}
