import { useQuery } from '@tanstack/react-query';
import { Button, DatePicker, Form, Segmented, Select } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { FileDown } from 'lucide-react';
import type { ReactNode } from 'react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { attApi } from '@/features/att/api';
import { scdApi } from '@/features/scd/api';
import { useCurrentYear, useYears } from '@/features/sch/hooks/useCatalogs';
import { PageHeader } from '@/shared/components/PageHeader';
import { message } from '@/shared/lib/antdStatic';
import { blobErrorMessage } from '@/shared/lib/download';
import { toDateString } from '@/shared/lib/format';
import { admApi } from '../api';
import './exports.css';

type Range = [Dayjs, Dayjs];

function RangeField({ value, onChange }: { value: Range; onChange: (v: Range) => void }) {
  return (
    <Form.Item label="Khoảng ngày">
      <DatePicker.RangePicker
        format="DD/MM/YYYY" allowClear={false} value={value} style={{ width: '100%' }}
        onChange={(v) => v && v[0] && v[1] && onChange([v[0], v[1]])}
      />
    </Form.Item>
  );
}

function useDownload() {
  const [busy, setBusy] = useState(false);
  const run = async (task: () => Promise<void>) => {
    setBusy(true);
    try { await task(); }
    catch (e) { message.error(await blobErrorMessage(e) ?? 'Không xuất được file. Thử lại sau.'); }
    finally { setBusy(false); }
  };
  return { busy, run };
}

/** Một mục xuất là một hàng: tên và mô tả bên trái, các trường chọn ở giữa, nút tải bên phải. */
function ExportCard({ title, hint, busy, disabled, onExport, children }: {
  title: string; hint: string; busy: boolean; disabled: boolean; onExport: () => void; children: ReactNode;
}) {
  return (
    <section className="export-row">
      <header className="export-row__info">
        <h2 className="export-row__title">{title}</h2>
        <p className="export-row__hint">{hint}</p>
      </header>
      <Form layout="vertical" requiredMark={false} className="export-row__fields">{children}</Form>
      <Button type="primary" size="large" icon={<FileDown size={16} />} loading={busy} disabled={disabled} onClick={onExport} className="export-row__btn">
        Xuất Excel
      </Button>
    </section>
  );
}

/** 4.9.12: chủ nhiệm xuất lớp mình, Hiệu phó xuất lớp thuộc phân hiệu mình, Hiệu trưởng xuất mọi lớp (spec 5.2). */
function AttendanceExport() {
  const classes = useQuery({ queryKey: ['att', 'classes'], queryFn: attApi.classes });
  const [picked, setPicked] = useState<number | undefined>();
  const [range, setRange] = useState<Range>(() => [dayjs().startOf('month'), dayjs()]);
  const { busy, run } = useDownload();
  const options = classes.data ?? [];
  const campusCount = new Set(options.map((c) => c.campusId)).size;
  const classId = picked ?? (options.length === 1 ? options[0].classId : undefined);

  return (
    <ExportCard
      title="Điểm danh theo lớp" hint="Từng tiết của cả lớp, kèm nhãn có phép hoặc không phép."
      busy={busy} disabled={classId === undefined}
      onExport={() => run(() => admApi.exportAttendance(classId!, toDateString(range[0]), toDateString(range[1])))}
    >
      <Form.Item label="Lớp">
        <Select
          showSearch optionFilterProp="label" placeholder="Chọn lớp" loading={classes.isLoading} value={classId} onChange={setPicked}
          options={options.map((c) => ({ value: c.classId, label: campusCount > 1 ? `${c.name} — ${c.campusName}` : c.name }))}
        />
      </Form.Item>
      <RangeField value={range} onChange={setRange} />
    </ExportCard>
  );
}

/** 4.9.13: Hiệu phó xuất thời khóa biểu hiện hành của phân hiệu mình, Hiệu trưởng xuất mọi phân hiệu; đọc từ lịch dạy (BR-14). */
function TimetableExport() {
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [picked, setPicked] = useState<number | undefined>();
  const [range, setRange] = useState<Range>(() => [dayjs().startOf('week'), dayjs().endOf('week')]);
  const { busy, run } = useDownload();
  const options = campuses.data ?? [];
  const campusId = picked ?? (options.length === 1 ? options[0].campusId : undefined);

  return (
    <ExportCard
      title="Thời khóa biểu theo phân hiệu" hint="Lịch dạy thực tế của phân hiệu, gồm cả các điều chỉnh tay."
      busy={busy} disabled={campusId === undefined}
      onExport={() => run(() => admApi.exportTimetable(campusId!, toDateString(range[0]), toDateString(range[1])))}
    >
      <Form.Item label="Phân hiệu">
        <Select
          placeholder="Chọn phân hiệu" loading={campuses.isLoading} value={campusId} onChange={setPicked}
          options={options.map((c) => ({ value: c.campusId, label: c.name }))}
        />
      </Form.Item>
      <RangeField value={range} onChange={setRange} />
    </ExportCard>
  );
}

const KIND_OPTIONS = [
  { value: 'campus', label: 'Phân hiệu' },
  { value: 'department', label: 'Tổ trưởng' },
  { value: 'homeroom', label: 'Chủ nhiệm' },
];

/** Danh sách trách nhiệm của một nhóm (PPL-04): Hiệu trưởng toàn trường, Hiệu phó trong phân hiệu mình phụ trách. */
function ResponsibilityExport() {
  const [kind, setKind] = useState('campus');
  const years = useYears();
  const { current } = useCurrentYear();
  const [pickedYear, setPickedYear] = useState<number | undefined>();
  const { busy, run } = useDownload();
  const yearId = pickedYear ?? current?.academicYearId;

  return (
    <ExportCard
      title="Trách nhiệm" hint="Ai đang phụ trách phân hiệu, tổ và lớp, kèm người sắp nhận."
      busy={busy} disabled={kind === 'homeroom' && yearId === undefined}
      onExport={() => run(() => admApi.exportResponsibilities(kind, kind === 'homeroom' ? yearId : undefined))}
    >
      <Form.Item label="Nhóm trách nhiệm">
        <Segmented block value={kind} onChange={(v) => setKind(String(v))} options={KIND_OPTIONS} />
      </Form.Item>
      {kind === 'homeroom' ? (
        <Form.Item label="Năm học">
          <Select value={yearId} onChange={setPickedYear} options={years.data?.map((y) => ({ value: y.academicYearId, label: y.name }))} />
        </Form.Item>
      ) : (
        <p className="export-row__note">Xuất theo thời điểm hiện tại.</p>
      )}
    </ExportCard>
  );
}

/** ADM-03 Xuất dữ liệu (spec 5.2): chỉ hiện mục người dùng có quyền xuất. */
export function ExportsPage() {
  const { user } = useAuth();
  const isPrincipal = user?.accountType === 'Principal';
  const leadsCampus = user?.accountType === 'VicePrincipal' && !!user.scope?.ledCampusIds.length;
  const isHomeroom = user?.accountType === 'Teacher' && !!user.scope?.homeroomClassIds.length;

  return (
    <>
      <PageHeader title="Xuất dữ liệu" />
      <div className="exports-grid">
        {(isPrincipal || leadsCampus || isHomeroom) && <AttendanceExport />}
        {(isPrincipal || leadsCampus) && <TimetableExport />}
        {(isPrincipal || leadsCampus) && <ResponsibilityExport />}
      </div>
    </>
  );
}
