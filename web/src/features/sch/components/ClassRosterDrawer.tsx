import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, DatePicker, Form, Input, Modal, Radio, Select, Table } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { ArrowRightLeft, LogOut, Search, UserPlus } from 'lucide-react';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PersonCell } from '@/shared/components/PersonCell';
import { RowActions } from '@/shared/components/RowActions';
import { showError } from '@/shared/lib/feedback';
import { formatDate, toDateString } from '@/shared/lib/format';
import { enrollmentCloseReasonLabel } from '@/shared/lib/labels';
import { schApi } from '../api';
import './school.css';
import { useYears } from '../hooks/useCatalogs';
import type { RosterItem, SchoolClass, StudentCandidate } from '../types';

const GENDER_LABELS: Record<string, string> = { Male: 'Nam', Female: 'Nữ', Other: 'Khác' };
const genderText = (v: string | null) => (v ? GENDER_LABELS[v] ?? v : '—');

interface Props {
  cls: SchoolClass | null;
  canWrite: boolean;
  onClose: () => void;
}

interface TransferValues { toClassId: number; effectiveDate: Dayjs }
interface CloseValues { reason: string }

const CLOSE_REASONS = [
  { value: 'ClassTransfer', label: 'Chuyển lớp (chưa xếp lớp mới)' },
  { value: 'SchoolTransfer', label: 'Chuyển trường' },
  { value: 'Graduated', label: 'Tốt nghiệp' },
];

/**
 * SCH-05: danh sách học sinh của lớp tại một ngày, kèm ghi danh, chuyển lớp, kết thúc ghi danh (4.2.29–4.2.32).
 * Ghi danh chọn nhiều em từ bảng; chuyển lớp và kết thúc ghi danh làm được cho cả nhóm đã tích hoặc từng em ở cột thao tác.
 */
export function ClassRosterDrawer({ cls, canWrite, onClose }: Props) {
  const qc = useQueryClient();
  const years = useYears();
  const [picked, setPicked] = useState<Dayjs | null>(null);
  const [enrolling, setEnrolling] = useState(false);
  const [transferring, setTransferring] = useState<RosterItem[] | null>(null);
  const [closing, setClosing] = useState<RosterItem[] | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const [candidateSearch, setCandidateSearch] = useState('');
  const [chosen, setChosen] = useState<number[]>([]);
  const [enrollDate, setEnrollDate] = useState<Dayjs | null>(null);
  const [enrollSaving, setEnrollSaving] = useState(false);
  const [transferForm] = Form.useForm<TransferValues>();
  const [closeForm] = Form.useForm<CloseValues>();

  const year = years.data?.find((y) => y.academicYearId === cls?.academicYearId);
  const outOfYear = (d: Dayjs) => !year || d.isBefore(year.startDate, 'day') || d.isAfter(year.endDate, 'day');
  const defaultStart = () => {
    const today = dayjs();
    return year && today.isBefore(year.startDate, 'day') ? dayjs(year.startDate) : today;
  };

  // Mặc định xem theo hôm nay; lớp của năm học chưa bắt đầu thì xem theo ngày bắt đầu năm học để thấy các em đã được xếp.
  const date = picked ?? (year && dayjs().isBefore(year.startDate, 'day') ? dayjs(year.startDate) : dayjs());
  const dateKey = toDateString(date);
  const roster = useQuery({
    queryKey: ['sch', 'roster', cls?.classId, dateKey],
    queryFn: () => schApi.enrollments.roster(cls!.classId, dateKey),
    enabled: cls !== null,
  });
  const candidates = useQuery({
    queryKey: ['sch', 'candidates', cls?.academicYearId, candidateSearch],
    queryFn: () => schApi.enrollments.candidates(cls!.academicYearId, candidateSearch, 200),
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

  const closeEnroll = () => { setEnrolling(false); setCandidateSearch(''); setChosen([]); setEnrollDate(null); };
  const enrollMany = async () => {
    setEnrollSaving(true);
    try {
      await schApi.enrollments.enrollMany(cls!.classId, { studentIds: chosen, startDate: toDateString(enrollDate ?? defaultStart()) });
      await refresh();
      message.success(`Đã ghi danh ${chosen.length} học sinh vào lớp.`);
      closeEnroll();
    } catch (e) {
      showError(e);
    } finally {
      setEnrollSaving(false);
    }
  };
  const transfer = async (v: TransferValues) => {
    await schApi.enrollments.transferMany({ enrollmentIds: transferring!.map((r) => r.enrollmentId), toClassId: v.toClassId, effectiveDate: toDateString(v.effectiveDate) });
    await refresh();
    setSelected([]);
    message.success(`Đã chuyển lớp cho ${transferring!.length} học sinh.`);
  };
  const closeEnrollment = async (v: CloseValues) => {
    await schApi.enrollments.closeMany({ enrollmentIds: closing!.map((r) => r.enrollmentId), reason: v.reason });
    await refresh();
    setSelected([]);
    message.success(`Đã kết thúc ghi danh của ${closing!.length} học sinh.`);
  };

  const reason = Form.useWatch('reason', closeForm);
  const openRows = roster.data?.students.filter((r) => selected.includes(r.enrollmentId)) ?? [];
  const candidateIds = new Set((candidates.data ?? []).map((c) => c.studentId));
  const closeCount = closing?.length ?? 1;
  const latestStart = transferring?.reduce((m, r) => (r.startDate > m ? r.startDate : m), transferring[0].startDate);

  return (
    <>
      <Modal
        open={cls !== null} width={720} onCancel={onClose} destroyOnHidden mask={{ closable: false }}
        title={cls ? `Lớp ${cls.name} · ${cls.campusName} · Năm học ${cls.academicYearName}` : ''}
        footer={<Button onClick={onClose}>Đóng</Button>}
      >
        <div className="roster-toolbar">
          <label htmlFor="roster-date">Xem tại ngày</label>
          <DatePicker id="roster-date" format="DD/MM/YYYY" allowClear={false} value={date} onChange={(d) => { if (d) { setPicked(d); setSelected([]); } }} />
          <span className="muted tabular">{roster.data ? `${roster.data.students.length} học sinh` : ''}</span>
          {canWrite && cls?.isActive && (
            <Button type="primary" icon={<UserPlus size={16} />} onClick={() => setEnrolling(true)}>Ghi danh học sinh</Button>
          )}
        </div>
        {canWrite && openRows.length > 0 && (
          <div className="roster-bulkbar">
            <span><b>{openRows.length}</b> học sinh được chọn</span>
            <Button icon={<ArrowRightLeft size={16} />} onClick={() => setTransferring(openRows)}>Chuyển lớp</Button>
            <Button danger icon={<LogOut size={16} />} onClick={() => setClosing(openRows)}>Kết thúc ghi danh</Button>
            <Button type="link" onClick={() => setSelected([])}>Bỏ chọn</Button>
          </div>
        )}
        <DataTable<RosterItem>
          rowKey="enrollmentId" loading={roster.isLoading} dataSource={roster.data?.students} scroll={{}}
          emptyText="Lớp chưa có học sinh nào có ghi danh hiệu lực tại ngày này."
          rowSelection={canWrite ? {
            selectedRowKeys: selected,
            onChange: (k) => setSelected(k as number[]),
            getCheckboxProps: (r) => ({ disabled: r.endDate !== null }),
          } : undefined}
          columns={[
            { title: 'Học sinh', dataIndex: 'fullName', render: (v: string, r: RosterItem) => <PersonCell name={v} sub={r.personCode} /> },
            { title: 'Ngày sinh', dataIndex: 'dateOfBirth', width: 112, className: 'tabular', render: (v: string | null) => formatDate(v) },
            { title: 'Giới tính', dataIndex: 'gender', width: 96, render: (v: string | null) => genderText(v) },
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
                  { key: 'transfer', label: 'Chuyển lớp', icon: <ArrowRightLeft size={16} />, hidden: r.endDate !== null, onClick: () => setTransferring([r]) },
                  { key: 'close', label: 'Kết thúc ghi danh', icon: <LogOut size={16} />, danger: true, hidden: r.endDate !== null, onClick: () => setClosing([r]) },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Modal>

      <Modal
        open={enrolling} width={720} title="Ghi danh học sinh" onCancel={closeEnroll} destroyOnHidden mask={{ closable: false }}
        footer={(
          <>
            <Button onClick={closeEnroll}>Hủy</Button>
            <Button type="primary" loading={enrollSaving} disabled={chosen.length === 0} onClick={enrollMany}>
              {chosen.length > 0 ? `Thêm ${chosen.length} học sinh vào lớp` : 'Thêm vào lớp'}
            </Button>
          </>
        )}
      >
        <div className="roster-toolbar">
          <Input
            allowClear prefix={<Search size={14} />} placeholder="Tìm theo tên, mã hoặc số định danh" value={candidateSearch}
            onChange={(e) => setCandidateSearch(e.target.value)} style={{ flex: 1 }}
          />
          <label htmlFor="enroll-date">Ngày hiệu lực</label>
          <DatePicker
            id="enroll-date" format="DD/MM/YYYY" allowClear={false} value={enrollDate ?? defaultStart()}
            onChange={(d) => d && setEnrollDate(d)} disabledDate={outOfYear}
          />
        </div>
        <p className="muted">
          Chỉ hiện học sinh đang học và chưa có ghi danh còn hiệu lực trong năm học {year?.name}. Tích nhiều em rồi bấm Thêm; ghi danh kéo dài tới hết năm học.
        </p>
        <Table<StudentCandidate>
          className="data-table" size="small" rowKey="studentId" loading={candidates.isFetching} dataSource={candidates.data ?? []}
          pagination={false} scroll={{ y: 360 }}
          rowSelection={{
            selectedRowKeys: chosen,
            onChange: (keys) => setChosen([...chosen.filter((k) => !candidateIds.has(k)), ...(keys as number[])]),
          }}
          locale={{ emptyText: 'Không có học sinh phù hợp.' }}
          columns={[
            { title: 'Mã HS', dataIndex: 'personCode', width: 120, className: 'tabular' },
            { title: 'Họ tên', dataIndex: 'fullName' },
            { title: 'Ngày sinh', dataIndex: 'dateOfBirth', width: 120, className: 'tabular', render: (v: string | null) => formatDate(v) },
            { title: 'Giới tính', dataIndex: 'gender', width: 100, render: (v: string | null) => genderText(v) },
          ]}
        />
      </Modal>

      <FormModal<TransferValues>
        open={transferring !== null}
        title={transferring?.length === 1 ? `Chuyển lớp: ${transferring[0].fullName}` : `Chuyển lớp cho ${transferring?.length ?? 0} học sinh`}
        form={transferForm} okText="Chuyển lớp"
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
          extra={latestStart ? `Phải sau ngày bắt đầu ghi danh hiện tại (${formatDate(latestStart)}).` : undefined}
        >
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={outOfYear} />
        </Form.Item>
      </FormModal>

      <FormModal<CloseValues>
        open={closing !== null}
        title={closing?.length === 1 ? `Kết thúc ghi danh: ${closing[0].fullName}` : `Kết thúc ghi danh của ${closing?.length ?? 0} học sinh`}
        form={closeForm} okText="Kết thúc ghi danh"
        onClose={() => setClosing(null)} onSubmit={closeEnrollment}
        initialValues={{ reason: 'ClassTransfer' }}
      >
        <Form.Item label="Lý do" name="reason" rules={[{ required: true, message: 'Chọn lý do kết thúc ghi danh.' }]}>
          <Radio.Group options={CLOSE_REASONS} className="roster-reasons" />
        </Form.Item>
        <InlineAlert
          type="warning"
          title={reason === 'SchoolTransfer' || reason === 'Graduated'
            ? `Sẽ kết thúc ${closeCount} ghi danh ở lớp ${cls?.name} và khóa ${closeCount} tài khoản học sinh ngay lập tức. Học sinh chuyển sang trạng thái không còn học.`
            : `Sẽ kết thúc ${closeCount} ghi danh ở lớp ${cls?.name}. Học sinh chưa thuộc lớp nào cho tới khi được ghi danh hoặc chuyển vào lớp mới.`}
        />
        <p className="muted">Ngày kết thúc là hôm nay ({dayjs().format('DD/MM/YYYY')}).</p>
      </FormModal>
    </>
  );
}
