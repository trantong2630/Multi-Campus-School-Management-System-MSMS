import { useQuery } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Select } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { FileDown } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { attApi } from '@/features/att/api';
import { scdApi } from '@/features/scd/api';
import { PageHeader } from '@/shared/components/PageHeader';
import { message } from '@/shared/lib/antdStatic';
import { blobErrorMessage } from '@/shared/lib/download';
import { toDateString } from '@/shared/lib/format';
import { admApi } from '../api';
import './exports.css';

type Range = [Dayjs, Dayjs];

/** Khoảng ngày chung của các mục xuất, khớp ExportRules.MaxRangeDays ở Backend. */
const MAX_RANGE_DAYS = 186;

function RangeField({ value, onChange }: { value: Range; onChange: (v: Range) => void }) {
  return (
    <Form.Item label="Khoảng ngày" extra={`Tối đa ${MAX_RANGE_DAYS} ngày mỗi lần.`}>
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

/** 4.9.10: chủ nhiệm xuất lớp mình, Hiệu phó xuất lớp thuộc phân hiệu mình (spec 5.2). */
function AttendanceExport() {
  const classes = useQuery({ queryKey: ['att', 'classes'], queryFn: attApi.classes });
  const [picked, setPicked] = useState<number | undefined>();
  const [range, setRange] = useState<Range>(() => [dayjs().startOf('month'), dayjs()]);
  const { busy, run } = useDownload();
  const options = classes.data ?? [];
  const campusCount = new Set(options.map((c) => c.campusId)).size;
  const classId = picked ?? (options.length === 1 ? options[0].classId : undefined);

  return (
    <Card title="Điểm danh theo lớp">
      <p className="exports-hint">
        Mỗi dòng là một học sinh ở một tiết, kèm nhãn cuối (có phép hoặc không phép tính theo đơn xin nghỉ đã duyệt).
        Chỉ gồm tiết Đã xếp lịch tính tới hôm nay.
      </p>
      <Form layout="vertical" requiredMark={false}>
        <Form.Item label="Lớp">
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn lớp" loading={classes.isLoading} value={classId} onChange={setPicked}
            options={options.map((c) => ({ value: c.classId, label: campusCount > 1 ? `${c.name} — ${c.campusName}` : c.name }))}
          />
        </Form.Item>
        <RangeField value={range} onChange={setRange} />
        <Button
          type="primary" icon={<FileDown size={16} />} loading={busy} disabled={classId === undefined}
          onClick={() => run(() => admApi.exportAttendance(classId!, toDateString(range[0]), toDateString(range[1])))}
        >
          Xuất Excel
        </Button>
      </Form>
    </Card>
  );
}

/** 4.9.11: Hiệu phó xuất thời khóa biểu hiện hành của phân hiệu mình, đọc từ lịch dạy (BR-14). */
function TimetableExport() {
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [picked, setPicked] = useState<number | undefined>();
  const [range, setRange] = useState<Range>(() => [dayjs().startOf('week'), dayjs().endOf('week')]);
  const { busy, run } = useDownload();
  const options = campuses.data ?? [];
  const campusId = picked ?? (options.length === 1 ? options[0].campusId : undefined);

  return (
    <Card title="Thời khóa biểu theo phân hiệu">
      <p className="exports-hint">
        Lấy từ lịch dạy nên có cả tiết đổi giáo viên, tiết bù, tiết Đã hủy và Chờ xử lý. Mỗi dòng là một tiết, kèm trạng thái và nguồn tiết.
      </p>
      <Form layout="vertical" requiredMark={false}>
        <Form.Item label="Phân hiệu">
          <Select
            placeholder="Chọn phân hiệu" loading={campuses.isLoading} value={campusId} onChange={setPicked}
            options={options.map((c) => ({ value: c.campusId, label: c.name }))}
          />
        </Form.Item>
        <RangeField value={range} onChange={setRange} />
        <Button
          type="primary" icon={<FileDown size={16} />} loading={busy} disabled={campusId === undefined}
          onClick={() => run(() => admApi.exportTimetable(campusId!, toDateString(range[0]), toDateString(range[1])))}
        >
          Xuất Excel
        </Button>
      </Form>
    </Card>
  );
}

/** ADM-03 Xuất dữ liệu (spec 5.2): chỉ hiện mục người dùng có quyền xuất. */
export function ExportsPage() {
  const { user } = useAuth();
  const leadsCampus = user?.accountType === 'VicePrincipal' && !!user.scope?.ledCampusIds.length;
  const isHomeroom = user?.accountType === 'Teacher' && !!user.scope?.homeroomClassIds.length;

  return (
    <>
      <PageHeader title="Xuất dữ liệu" description="Tải file Excel trong phạm vi đang phụ trách. File sinh ngay khi bấm, hệ thống không lưu lại." />
      <div className="exports-grid">
        {(leadsCampus || isHomeroom) && <AttendanceExport />}
        {leadsCampus && <TimetableExport />}
      </div>
    </>
  );
}
