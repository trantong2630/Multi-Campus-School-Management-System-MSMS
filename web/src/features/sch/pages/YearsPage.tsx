import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { CalendarRange, Pencil, Plus, Star } from 'lucide-react';
import { useState } from 'react';
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

interface YearValues { name: string; range: [Dayjs, Dayjs] }
interface SemesterValues { sem1: [Dayjs, Dayjs]; sem2: [Dayjs, Dayjs] }

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

  const saveSemesters = async (v: SemesterValues) => {
    await schApi.years.configureSemesters(semesterOf!.academicYearId, {
      semester1: { startDate: toDateString(v.sem1[0]), endDate: toDateString(v.sem1[1]) },
      semester2: { startDate: toDateString(v.sem2[0]), endDate: toDateString(v.sem2[1]) },
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
          ...(sem(semesterOf, 1) ? { sem1: range(sem(semesterOf, 1)!.startDate, sem(semesterOf, 1)!.endDate) } : {}),
          ...(sem(semesterOf, 2) ? { sem2: range(sem(semesterOf, 2)!.startDate, sem(semesterOf, 2)!.endDate) } : {}),
        } : undefined}
      >
        <Form.Item label="Học kỳ 1" name="sem1" rules={[{ required: true, message: 'Chọn khoảng thời gian học kỳ 1.' }]}>
          <DatePicker.RangePicker format={DATE_FORMAT} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Học kỳ 2" name="sem2" rules={[{ required: true, message: 'Chọn khoảng thời gian học kỳ 2.' }]}>
          <DatePicker.RangePicker format={DATE_FORMAT} style={{ width: '100%' }} />
        </Form.Item>
      </FormModal>
    </>
  );
}
