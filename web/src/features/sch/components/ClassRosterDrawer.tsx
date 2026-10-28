import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, DatePicker, Form, Modal, Radio, Select } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { ArrowRightLeft, LogOut, UserPlus } from 'lucide-react';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PersonCell } from '@/shared/components/PersonCell';
import { RowActions } from '@/shared/components/RowActions';
import { formatDate, toDateString } from '@/shared/lib/format';
import { enrollmentCloseReasonLabel } from '@/shared/lib/labels';
import { schApi } from '../api';
import './school.css';
import { useYears } from '../hooks/useCatalogs';
import type { RosterItem, SchoolClass } from '../types';

interface Props {
  cls: SchoolClass | null;
  canWrite: boolean;
  onClose: () => void;
}

interface EnrollValues { studentId: number; startDate: Dayjs }
interface TransferValues { toClassId: number; effectiveDate: Dayjs }
interface CloseValues { reason: string }

const CLOSE_REASONS = [
  { value: 'ClassTransfer', label: 'Chuyển lớp (chưa xếp lớp mới)' },
  { value: 'SchoolTransfer', label: 'Chuyển trường' },
  { value: 'Graduated', label: 'Tốt nghiệp' },
];

/** SCH-05: danh sách học sinh của lớp tại một ngày, kèm ghi danh, chuyển lớp, kết thúc ghi danh (4.2.29–4.2.32). */
export function ClassRosterDrawer({ cls, canWrite, onClose }: Props) {
  const qc = useQueryClient();
  const years = useYears();
  const [date, setDate] = useState<Dayjs>(dayjs());
  const [enrolling, setEnrolling] = useState(false);
  const [transferring, setTransferring] = useState<RosterItem | null>(null);
  const [closing, setClosing] = useState<RosterItem | null>(null);
  const [candidateSearch, setCandidateSearch] = useState('');
  const [enrollForm] = Form.useForm<EnrollValues>();
  const [transferForm] = Form.useForm<TransferValues>();
  const [closeForm] = Form.useForm<CloseValues>();

  const year = years.data?.find((y) => y.academicYearId === cls?.academicYearId);
  const outOfYear = (d: Dayjs) => !year || d.isBefore(year.startDate, 'day') || d.isAfter(year.endDate, 'day');
  const defaultStart = () => {
    const today = dayjs();
    return year && today.isBefore(year.startDate, 'day') ? dayjs(year.startDate) : today;
  };

  const dateKey = toDateString(date);
  const roster = useQuery({
    queryKey: ['sch', 'roster', cls?.classId, dateKey],
    queryFn: () => schApi.enrollments.roster(cls!.classId, dateKey),
    enabled: cls !== null,
  });
  const candidates = useQuery({
    queryKey: ['sch', 'candidates', cls?.academicYearId, candidateSearch],
    queryFn: () => schApi.enrollments.candidates(cls!.academicYearId, candidateSearch),
    enabled: enrolling && cls !== null,
  });
  const otherClasses = useQuery({
    queryKey: ['sch', 'classes', cls?.academicYearId, undefined, false],
    queryFn: () => schApi.classes.list({ yearId: cls!.academicYearId }),
    enabled: transferring !== null && cls !== null,
  });

  const refresh = async () => {
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['sch', 'roster'] }),
      qc.invalidateQueries({ queryKey: ['sch', 'classes'] }),
      qc.invalidateQueries({ queryKey: ['sch', 'candidates'] }),
    ]);
  };

  const enroll = async (v: EnrollValues) => {
    await schApi.enrollments.enroll(cls!.classId, { studentId: v.studentId, startDate: toDateString(v.startDate) });
    await refresh();
    message.success('Đã ghi danh học sinh vào lớp.');
  };
  const transfer = async (v: TransferValues) => {
    await schApi.enrollments.transfer(transferring!.enrollmentId, { toClassId: v.toClassId, effectiveDate: toDateString(v.effectiveDate) });
    await refresh();
    message.success('Đã chuyển lớp cho học sinh.');
  };
  const closeEnrollment = async (v: CloseValues) => {
    await schApi.enrollments.close(closing!.enrollmentId, v);
    await refresh();
    message.success('Đã kết thúc ghi danh.');
  };

  const reason = Form.useWatch('reason', closeForm);

  return (
    <>
      <Modal
        open={cls !== null} width={720} onCancel={onClose} destroyOnHidden mask={{ closable: false }}
        title={cls ? `Lớp ${cls.name} · ${cls.campusName} · Năm học ${cls.academicYearName}` : ''}
        footer={<Button onClick={onClose}>Đóng</Button>}
      >
        <div className="roster-toolbar">
          <label htmlFor="roster-date">Xem tại ngày</label>
          <DatePicker id="roster-date" format="DD/MM/YYYY" allowClear={false} value={date} onChange={(d) => d && setDate(d)} />
          <span className="muted tabular">{roster.data ? `${roster.data.students.length} học sinh` : ''}</span>
          {canWrite && cls?.isActive && (
            <Button type="primary" icon={<UserPlus size={16} />} onClick={() => setEnrolling(true)}>Ghi danh học sinh</Button>
          )}
        </div>
        <DataTable<RosterItem>
          rowKey="enrollmentId" loading={roster.isLoading} dataSource={roster.data?.students} scroll={{}}
          emptyText="Lớp chưa có học sinh nào có ghi danh hiệu lực tại ngày này."
          columns={[
            { title: 'Học sinh', dataIndex: 'fullName', render: (v: string, r: RosterItem) => <PersonCell name={v} sub={r.personCode} /> },
            { title: 'Ngày sinh', dataIndex: 'dateOfBirth', width: 112, className: 'tabular', render: (v: string | null) => formatDate(v) },
            { title: 'Bắt đầu', dataIndex: 'startDate', width: 132, className: 'tabular', render: (v: string) => formatDate(v) },
            {
              title: 'Kết thúc', dataIndex: 'endDate', width: 132, className: 'tabular',
              render: (v: string | null, r: RosterItem) => v
                ? (
                  <>
                    {formatDate(v)}
                    {r.reason && <div className="roster-reason">{enrollmentCloseReasonLabel[r.reason] ?? r.reason}</div>}
                  </>
                )
                : <span className="muted">Hết năm học</span>,
            },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 96, align: 'center' as const,
              render: (_: unknown, r: RosterItem) => (
                <RowActions actions={[
                  { key: 'transfer', label: 'Chuyển lớp', icon: <ArrowRightLeft size={16} />, hidden: r.endDate !== null, onClick: () => setTransferring(r) },
                  { key: 'close', label: 'Kết thúc ghi danh', icon: <LogOut size={16} />, danger: true, hidden: r.endDate !== null, onClick: () => setClosing(r) },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Modal>

      <FormModal<EnrollValues>
        open={enrolling} title="Ghi danh học sinh" form={enrollForm} okText="Ghi danh"
        onClose={() => { setEnrolling(false); setCandidateSearch(''); }} onSubmit={enroll}
        initialValues={{ startDate: defaultStart() }}
      >
        <Form.Item
          label="Học sinh" name="studentId" rules={[{ required: true, message: 'Chọn học sinh cần ghi danh.' }]}
          extra="Chỉ hiện học sinh đang học và chưa có ghi danh còn hiệu lực trong năm học này. Gõ tên, mã hoặc số định danh để tìm."
        >
          <Select
            showSearch filterOption={false} placeholder="Tìm học sinh" loading={candidates.isFetching}
            onSearch={setCandidateSearch} notFoundContent="Không có học sinh phù hợp."
            options={candidates.data?.map((s) => ({
              value: s.studentId, label: `${s.fullName} · ${s.personCode}${s.dateOfBirth ? ` · ${formatDate(s.dateOfBirth)}` : ''}`,
            }))}
          />
        </Form.Item>
        <Form.Item
          label="Ngày hiệu lực" name="startDate" rules={[{ required: true, message: 'Chọn ngày bắt đầu ghi danh.' }]}
          extra={year ? `Phải nằm trong năm học ${year.name}. Ghi danh kéo dài tới hết năm học.` : undefined}
        >
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={outOfYear} />
        </Form.Item>
      </FormModal>

      <FormModal<TransferValues>
        open={transferring !== null} title={`Chuyển lớp: ${transferring?.fullName ?? ''}`} form={transferForm} okText="Chuyển lớp"
        onClose={() => setTransferring(null)} onSubmit={transfer}
        initialValues={{ effectiveDate: defaultStart() }}
      >
        <InlineAlert
          type="info"
          title="Ghi danh ở lớp hiện tại kết thúc vào ngày liền trước ngày hiệu lực. Điểm danh và dữ liệu đã có ở lớp cũ được giữ nguyên."
        />
        <Form.Item label="Chuyển đến lớp" name="toClassId" rules={[{ required: true, message: 'Chọn lớp chuyển đến.' }]}>
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn lớp" loading={otherClasses.isLoading}
            options={otherClasses.data?.filter((c) => c.isActive && c.classId !== cls?.classId)
              .map((c) => ({ value: c.classId, label: `${c.name} · ${c.campusName}` }))}
          />
        </Form.Item>
        <Form.Item
          label="Ngày hiệu lực" name="effectiveDate" rules={[{ required: true, message: 'Chọn ngày hiệu lực.' }]}
          extra={transferring ? `Phải sau ngày bắt đầu ghi danh hiện tại (${formatDate(transferring.startDate)}).` : undefined}
        >
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={outOfYear} />
        </Form.Item>
      </FormModal>

      <FormModal<CloseValues>
        open={closing !== null} title={`Kết thúc ghi danh: ${closing?.fullName ?? ''}`} form={closeForm} okText="Kết thúc ghi danh"
        onClose={() => setClosing(null)} onSubmit={closeEnrollment}
        initialValues={{ reason: 'ClassTransfer' }}
      >
        <Form.Item label="Lý do" name="reason" rules={[{ required: true, message: 'Chọn lý do kết thúc ghi danh.' }]}>
          <Radio.Group options={CLOSE_REASONS} className="roster-reasons" />
        </Form.Item>
        <InlineAlert
          type="warning"
          title={reason === 'SchoolTransfer' || reason === 'Graduated'
            ? `Sẽ kết thúc 1 ghi danh ở lớp ${cls?.name} và khóa 1 tài khoản học sinh ngay lập tức. Học sinh chuyển sang trạng thái không còn học.`
            : `Sẽ kết thúc 1 ghi danh ở lớp ${cls?.name}. Học sinh chưa thuộc lớp nào cho tới khi được ghi danh hoặc chuyển vào lớp mới.`}
        />
        <p className="muted">Ngày kết thúc là hôm nay ({dayjs().format('DD/MM/YYYY')}).</p>
      </FormModal>
    </>
  );
}
