import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { Pencil, Plus, Star, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatCard } from '@/shared/components/StatCard';
import { StatStrip } from '@/shared/components/StatStrip';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, toDateString } from '@/shared/lib/format';
import { schApi } from '../api';
import { useYears } from '../hooks/useCatalogs';
import { nextSemester, semesterAt, semesterProgress } from '../semester';
import type { AcademicYear, Semester } from '../types';
import '../components/school.css';

interface YearValues { name: string; range: [Dayjs, Dayjs] }
interface SemesterValues { start1: Dayjs; end1: Dayjs; start2: Dayjs; end2: Dayjs }

const DATE_FORMAT = 'DD/MM/YYYY';
const semesterText = (s?: Semester) => (s ? `${formatDate(s.startDate)} – ${formatDate(s.endDate)}` : null);

/** Phần trăm năm học đã trôi qua tính tới hôm nay. */
function yearElapsed(start: string, end: string, today: dayjs.Dayjs): number {
  const total = dayjs(end).diff(dayjs(start), 'day') + 1;
  return Math.min(100, Math.max(0, ((today.diff(dayjs(start), 'day') + 1) / Math.max(1, total)) * 100));
}

export function YearsPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const { data, isLoading } = useYears();
  const navigate = useNavigate();
  const current = data?.find((y) => y.isCurrent);
  const holidays = useQuery({
    queryKey: ['sch', 'holidays', current?.academicYearId, undefined],
    queryFn: () => schApi.holidays.list({ yearId: current!.academicYearId }),
    enabled: !!current,
  });
  const today = dayjs().startOf('day');
  const semester = semesterAt(current, today);
  const upcomingSemester = nextSemester(current, today);
  const progress = semester ? semesterProgress(semester, today) : null;
  // Ngày nghỉ cấp trường và cấp phân hiệu cùng ngày chỉ tính một lần.
  const upcoming = [...new Set((holidays.data ?? []).map((h) => h.holidayDate.slice(0, 10)).filter((d) => !dayjs(d).isBefore(today)))].sort();
  const nextHoliday = holidays.data?.find((h) => h.holidayDate.slice(0, 10) === upcoming[0]);
  const canOpenHolidays = !!user && isPathAllowed('/sch/holidays', user);
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

  const remove = useMutation({
    mutationFn: (y: AcademicYear) => schApi.years.remove(y.academicYearId),
    onSuccess: () => { void refresh(); message.success('Đã xóa năm học.'); },
    onError: (e) => showError(e),
  });

  const askRemove = (y: AcademicYear) => confirmAction({
    title: `Xóa năm học ${y.name}?`,
    content: `Sẽ xóa năm học này và ${y.semesters.length} học kỳ đã khai. Chỉ xóa được khi chưa có lớp, ngày nghỉ hay thời khóa biểu nào gắn vào; không khôi phục được.`,
    okText: 'Xóa năm học', danger: true,
    onOk: () => remove.mutateAsync(y).catch(() => undefined),
  });

  const askSetCurrent = (y: AcademicYear) => {
    const previous = data?.find((x) => x.isCurrent);
    confirmAction({
      title: `Đặt ${y.name} làm năm học hiện hành?`,
      content: (
        <>
          {previous ? <p>Năm học hiện hành sẽ đổi từ <b>{previous.name}</b> sang <b>{y.name}</b>.</p> : null}
          <p>Các màn hình vận hành sẽ mặc định lọc theo năm học này.</p>
        </>
      ),
      okText: 'Đặt hiện hành',
      onOk: () => setCurrent.mutateAsync(y).catch(() => undefined),
    });
  };

  const saveYear = async (v: YearValues) => {
    const body = { name: v.name, startDate: toDateString(v.range[0]), endDate: toDateString(v.range[1]) };
    if (editing === 'new') await schApi.years.create(body);
    else if (editing) await schApi.years.update(editing.academicYearId, body);
    await refresh();
    message.success('Đã lưu năm học.');
  };

  // Mốc kết thúc muộn nhất do backend trả về (SemesterRules): giao diện chỉ hiển thị và khóa ô chọn ngày theo mốc đó.
  const latestOf = (no: 1 | 2) =>
    semesterOf ? dayjs(no === 1 ? semesterOf.semester1LatestEnd : semesterOf.semester2LatestEnd) : undefined;
  const outsideYear = (d: Dayjs, no: 1 | 2) => !semesterOf || d.isBefore(semesterOf.startDate, 'day') || d.isAfter(latestOf(no), 'day');

  const saveSemesters = async (v: SemesterValues) => {
    await schApi.years.configureSemesters(semesterOf!.academicYearId, {
      semester1: { startDate: toDateString(v.start1), endDate: toDateString(v.end1) },
      semester2: { startDate: toDateString(v.start2), endDate: toDateString(v.end2) },
    });
    await refresh();
    message.success('Đã lưu hai học kỳ.');
  };

  const sem = (y: AcademicYear, no: number) => y.semesters.find((s) => s.semesterNo === no);
  const semesterCell = (y: AcademicYear, no: number) => {
    const text = semesterText(sem(y, no));
    if (!canWrite) return text ?? <span className="muted">Chưa khai</span>;
    return text
      ? <Button type="text" size="small" icon={<Pencil size={14} />} title="Sửa học kỳ" onClick={() => setSemesterOf(y)}>{text}</Button>
      : <Button size="small" icon={<Plus size={14} />} onClick={() => setSemesterOf(y)}>Khai báo học kỳ</Button>;
  };
  const range = (a: string, b: string): [Dayjs, Dayjs] => [dayjs(a), dayjs(b)];

  return (
    <>
      <PageHeader
        title="Năm học & Học kỳ"
        description="Đúng một năm học hiện hành. Mỗi năm có hai học kỳ nằm trong năm học, không chồng nhau."
        extra={canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm năm học</Button>}
      />
      {!isLoading && (
        <StatStrip>
          <StatCard label="Năm học hiện hành" value={current?.name ?? '—'}
            meter={current ? yearElapsed(current.startDate, current.endDate, today) : null}
            hint={current ? `${formatDate(current.startDate)} – ${formatDate(current.endDate)}` : 'Chưa đặt năm học hiện hành'} />
          <StatCard label="Học kỳ hiện tại" value={semester?.name ?? 'Ngoài học kỳ'}
            meter={progress ? (progress.week / progress.total) * 100 : null}
            hint={progress ? `Tuần ${progress.week} / ${progress.total}`
              : upcomingSemester ? `${upcomingSemester.name} bắt đầu ${formatDate(upcomingSemester.startDate)}` : undefined} />
          <StatCard label="Còn lại của học kỳ" value={progress ? `${progress.total - progress.week} tuần` : '—'}
            hint={semester ? `Kết thúc ${formatDate(semester.endDate)}` : undefined} />
          <StatCard label="Ngày nghỉ còn lại trong năm" value={current ? upcoming.length : '—'}
            hint={nextHoliday ? `Gần nhất ${formatDate(nextHoliday.holidayDate)}${nextHoliday.reason ? ` · ${nextHoliday.reason}` : ''}` : undefined}
            onClick={canOpenHolidays ? () => navigate('/sch/holidays') : undefined} />
        </StatStrip>
      )}
      <Card className="table-card">
        <DataTable<AcademicYear>
          rowKey="academicYearId" loading={isLoading} dataSource={data}
          emptyText="Chưa có năm học nào."
          columns={[
            { title: 'Năm học', dataIndex: 'name', width: 130, render: (v: string) => <strong className="tabular">{v}</strong> },
            { title: 'Khoảng thời gian', key: 'range', width: 220, className: 'tabular', render: (_: unknown, y: AcademicYear) => `${formatDate(y.startDate)} – ${formatDate(y.endDate)}` },
            { title: 'Học kỳ 1', key: 's1', className: 'tabular', render: (_: unknown, y: AcademicYear) => semesterCell(y, 1) },
            { title: 'Học kỳ 2', key: 's2', className: 'tabular', render: (_: unknown, y: AcademicYear) => semesterCell(y, 2) },
            {
              title: 'Hiện hành', dataIndex: 'isCurrent', width: 170, align: 'center',
              render: (v: boolean, y: AcademicYear) => v
                ? <StatusBadge domain="catalog" value="Active" />
                : canWrite
                  ? <Button size="small" icon={<Star size={14} />} onClick={() => askSetCurrent(y)}>Đặt hiện hành</Button>
                  : <span className="muted">—</span>,
            },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 110, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, y: AcademicYear) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa năm học', icon: <Pencil size={16} />, onClick: () => setEditing(y) },
                  {
                    key: 'delete', label: 'Xóa năm học', icon: <Trash2 size={16} />, danger: true,
                    hidden: y.isCurrent && (data?.length ?? 0) > 1, onClick: () => askRemove(y),
                  },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      <FormModal<YearValues>
        variant="drawer" width={640}
        open={editing !== null} title={editing === 'new' ? 'Thêm năm học' : 'Sửa năm học'}
        form={yearForm} onClose={() => setEditing(null)} onSubmit={saveYear}
        initialValues={editing && editing !== 'new' ? { name: editing.name, range: range(editing.startDate, editing.endDate) } : undefined}
      >
        <div className="form-grid">
          <Form.Item label="Tên năm học" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên năm học, ví dụ 2026-2027.' }]}>
            <Input maxLength={20} placeholder="2026-2027" />
          </Form.Item>
          <Form.Item label="Từ ngày – đến ngày" name="range" style={{ gridColumn: '1 / -1' }} rules={[{ required: true, message: 'Chọn ngày bắt đầu và ngày kết thúc năm học.' }]}>
            <DatePicker.RangePicker format={DATE_FORMAT} style={{ width: '100%' }} />
          </Form.Item>
        </div>
      </FormModal>

      <FormModal<SemesterValues>
        open={semesterOf !== null} title={`Học kỳ của năm học ${semesterOf?.name ?? ''}`}
        form={semForm} onClose={() => setSemesterOf(null)} onSubmit={saveSemesters}
        initialValues={semesterOf ? {
          start1: dayjs(sem(semesterOf, 1)?.startDate ?? semesterOf.startDate),
          end1: sem(semesterOf, 1) ? dayjs(sem(semesterOf, 1)!.endDate) : latestOf(1),
          start2: sem(semesterOf, 2) ? dayjs(sem(semesterOf, 2)!.startDate) : latestOf(1)?.add(1, 'day'),
          end2: sem(semesterOf, 2) ? dayjs(sem(semesterOf, 2)!.endDate) : latestOf(2),
        } : undefined}
      >
        {([1, 2] as const).map((no) => (
          <div key={no} className="semester-row">
            <Form.Item
              label={`Học kỳ ${no} — ngày bắt đầu`} name={`start${no}`}
              dependencies={no === 2 ? ['end1'] : undefined}
              rules={[
                { required: true, message: `Chọn ngày bắt đầu học kỳ ${no}.` },
                ...(no === 2 ? [({ getFieldValue }: { getFieldValue: (name: string) => Dayjs | undefined }) => ({
                  validator: (_: unknown, value?: Dayjs) => {
                    const end1 = getFieldValue('end1');
                    return !value || !end1 || value.isAfter(end1, 'day')
                      ? Promise.resolve()
                      : Promise.reject(new Error('Học kỳ 2 phải bắt đầu sau ngày kết thúc học kỳ 1.'));
                  },
                })] : []),
              ]}
            >
              <DatePicker format={DATE_FORMAT} style={{ width: '100%' }} disabledDate={(d) => outsideYear(d, no)} />
            </Form.Item>
            <Form.Item
              label={`Học kỳ ${no} — ngày kết thúc`} name={`end${no}`} dependencies={[`start${no}`]}
              extra={<>Muộn nhất: <b className="tabular">{latestOf(no)?.format(DATE_FORMAT)}</b></>}
              rules={[
                { required: true, message: `Chọn ngày kết thúc học kỳ ${no}.` },
                ({ getFieldValue }: { getFieldValue: (name: string) => Dayjs | undefined }) => ({
                  validator: (_: unknown, value?: Dayjs) => {
                    const start = getFieldValue(`start${no}`);
                    return !value || !start || value.isAfter(start, 'day')
                      ? Promise.resolve()
                      : Promise.reject(new Error(`Học kỳ ${no} phải kết thúc sau ngày bắt đầu.`));
                  },
                }),
              ]}
            >
              <DatePicker
                format={DATE_FORMAT} style={{ width: '100%' }} disabledDate={(d) => outsideYear(d, no)}
                onChange={no === 1 ? (d) => { if (d && !semForm.getFieldValue('start2')) semForm.setFieldValue('start2', d.add(1, 'day')); } : undefined}
              />
            </Form.Item>
          </div>
        ))}
      </FormModal>
    </>
  );
}
