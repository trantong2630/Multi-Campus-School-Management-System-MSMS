import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, DatePicker, Form, Input, Modal, Radio, Select, Table, Tag, Tooltip } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { ArrowRightLeft, LogOut, Search, UserPlus } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { RowActions } from '@/shared/components/RowActions';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, toDateString } from '@/shared/lib/format';
import { schApi } from '../api';
import { classSizeNotice } from '../classSize';
import { gradeAgeNotice } from '../gradeAge';
import './school.css';
import { useYears } from '../hooks/useCatalogs';
import type { RosterItem, SchoolClass, StudentCandidate } from '../types';
import { defaultRosterDate, genderText, rosterColumns } from './rosterColumns';

interface Props {
  cls: SchoolClass;
  canWrite: boolean;
}

interface TransferValues { toClassId: number; effectiveDate: Dayjs }
interface CloseValues { reason: string }

const CLOSE_REASONS = [
  { value: 'ClassTransfer', label: 'Chuyển lớp (chưa xếp lớp mới)' },
  { value: 'SchoolTransfer', label: 'Chuyển trường' },
  { value: 'Graduated', label: 'Tốt nghiệp' },
];

/**
 * SCH-05: danh sách học sinh của một lớp tại một ngày, kèm xếp lớp, chuyển lớp, rời lớp (4.2.29–4.2.32).
 * Xếp lớp chọn nhiều em từ bảng; chuyển lớp và rời lớp làm được cho cả nhóm đã tích hoặc từng em ở cột thao tác.
 * Chuyển lớp chỉ sang lớp cùng khối, cùng năm học. Đổi lớp thì màn cha đặt lại `key` để bỏ trạng thái cũ.
 */
export function ClassRosterPanel({ cls, canWrite }: Props) {
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

  const year = years.data?.find((y) => y.academicYearId === cls.academicYearId);
  const outOfYear = (d: Dayjs) => !year || d.isBefore(year.startDate, 'day') || d.isAfter(year.endDate, 'day');
  const defaultStart = () => defaultRosterDate(year?.startDate);
  const date = picked ?? defaultStart();
  const dateKey = toDateString(date);
  const roster = useQuery({
    queryKey: ['sch', 'roster', cls.classId, dateKey],
    queryFn: () => schApi.enrollments.roster(cls.classId, dateKey),
  });
  const candidates = useQuery({
    queryKey: ['sch', 'candidates', cls.academicYearId, candidateSearch],
    queryFn: () => schApi.enrollments.candidates(cls.academicYearId, candidateSearch, 200),
    enabled: enrolling,
  });
  const otherClasses = useQuery({
    queryKey: ['sch', 'classes', cls.academicYearId, undefined, false],
    queryFn: () => schApi.classes.list({ yearId: cls.academicYearId }),
    enabled: transferring !== null,
  });
  // Chuyển lớp chỉ trong cùng khối: lên hay xuống khối khác là lên lớp hoặc ở lại lớp, làm bằng Kết chuyển năm học.
  const sameGradeClasses = otherClasses.data?.filter((c) => c.isActive && c.classId !== cls.classId && c.gradeLevel === cls.gradeLevel);

  const refresh = async () => {
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['sch', 'roster'] }),
      qc.invalidateQueries({ queryKey: ['sch', 'classes'] }),
      qc.invalidateQueries({ queryKey: ['sch', 'candidates'] }),
    ]);
  };

  // Nhớ học sinh đã từng hiện trong danh sách để cảnh báo cả những em tích ở lần tìm trước (ô tìm kiếm đổi thì danh sách đổi).
  const seenCandidates = useRef(new Map<number, StudentCandidate>());
  useEffect(() => {
    for (const c of candidates.data ?? []) seenCandidates.current.set(c.studentId, c);
  }, [candidates.data]);
  const ageNoticeOf = (c: StudentCandidate) => gradeAgeNotice(c.dateOfBirth, cls.gradeLevel, year?.startDate);

  const closeEnroll = () => { setEnrolling(false); setCandidateSearch(''); setChosen([]); setEnrollDate(null); seenCandidates.current.clear(); };
  // Lệch tuổi chỉ là cảnh báo: hỏi xác nhận rồi vẫn cho xếp (học sinh đi học muộn, học lại là chuyện có thật).
  const askEnroll = () => {
    const flagged = chosen.flatMap((id) => {
      const c = seenCandidates.current.get(id);
      const notice = c && ageNoticeOf(c);
      return c && notice ? [{ c, notice }] : [];
    });
    if (flagged.length === 0) { void enrollMany(); return; }
    confirmAction({
      title: `Xếp ${flagged.length} học sinh lệch tuổi vào lớp ${cls.name}?`,
      content: (
        <>
          <p>Kiểm tra lại ngày sinh. Nếu đúng (em đi học muộn hoặc học lại) thì xác nhận để tiếp tục.</p>
          <ul>
            {flagged.map(({ c, notice }) => <li key={c.studentId}><b>{c.fullName}</b>: {notice.text}</li>)}
          </ul>
        </>
      ),
      okText: 'Vẫn xếp lớp',
      onOk: enrollMany,
    });
  };
  const enrollMany = async () => {
    setEnrollSaving(true);
    try {
      await schApi.enrollments.enrollMany(cls.classId, { studentIds: chosen, startDate: toDateString(enrollDate ?? defaultStart()) });
      await refresh();
      message.success(`Đã xếp ${chosen.length} học sinh vào lớp.`);
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
    message.success(`Đã cho ${closing!.length} học sinh rời lớp.`);
  };

  const reason = Form.useWatch('reason', closeForm);
  const openRows = roster.data?.students.filter((r) => selected.includes(r.enrollmentId)) ?? [];
  const candidateIds = new Set((candidates.data ?? []).map((c) => c.studentId));
  const closeCount = closing?.length ?? 1;
  // Cảnh báo mềm sĩ số: không chặn, chỉ nhắc khi xếp hoặc chuyển thêm làm lớp vượt mức thường gặp.
  const enrollSizeNotice = classSizeNotice(cls.name, cls.studentCount, chosen.length);
  const toClassId = Form.useWatch('toClassId', transferForm);
  const targetClass = sameGradeClasses?.find((c) => c.classId === toClassId);
  const transferSizeNotice = targetClass && classSizeNotice(targetClass.name, targetClass.studentCount, transferring?.length ?? 0);

  return (
    <>
      <div className="roster-toolbar">
        <label htmlFor="roster-date">Xem tại ngày</label>
        <DatePicker id="roster-date" format="DD/MM/YYYY" allowClear={false} value={date} onChange={(d) => { if (d) { setPicked(d); setSelected([]); } }} />
        <span className="muted tabular">{roster.data ? `${roster.data.students.length} học sinh` : ''}</span>
        {canWrite && cls.isActive && (
          <Button type="primary" icon={<UserPlus size={16} />} onClick={() => setEnrolling(true)}>Xếp học sinh vào lớp</Button>
        )}
      </div>
      {canWrite && openRows.length > 0 && (
        <div className="roster-bulkbar">
          <span><b>{openRows.length}</b> học sinh được chọn</span>
          <Button icon={<ArrowRightLeft size={16} />} onClick={() => setTransferring(openRows)}>Chuyển lớp</Button>
          <Button danger icon={<LogOut size={16} />} onClick={() => setClosing(openRows)}>Rời lớp</Button>
          <Button type="link" onClick={() => setSelected([])}>Bỏ chọn</Button>
        </div>
      )}
      <DataTable<RosterItem>
        rowKey="enrollmentId" loading={roster.isLoading} dataSource={roster.data?.students} scroll={{}}
        emptyText="Lớp chưa có học sinh nào tại ngày này."
        rowSelection={canWrite ? {
          selectedRowKeys: selected,
          onChange: (k) => setSelected(k as number[]),
          getCheckboxProps: (r) => ({ disabled: r.endDate !== null }),
        } : undefined}
        columns={[
          ...rosterColumns,
          ...(canWrite ? [{
            title: 'Thao tác', key: 'actions', width: 84, align: 'center' as const,
            render: (_: unknown, r: RosterItem) => (
              <RowActions actions={[
                { key: 'transfer', label: 'Chuyển lớp', icon: <ArrowRightLeft size={16} />, hidden: r.endDate !== null, onClick: () => setTransferring([r]) },
                { key: 'close', label: 'Rời lớp', icon: <LogOut size={16} />, danger: true, hidden: r.endDate !== null, onClick: () => setClosing([r]) },
              ]} />
            ),
          }] : []),
        ]}
      />

      <Modal
        open={enrolling} width={720} title="Xếp lớp học sinh" onCancel={closeEnroll} destroyOnHidden mask={{ closable: false }}
        footer={(
          <>
            <Button onClick={closeEnroll}>Hủy</Button>
            <Button type="primary" loading={enrollSaving} disabled={chosen.length === 0} onClick={askEnroll}>
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
          Chỉ hiện học sinh đang học và chưa thuộc lớp nào trong năm học {year?.name}. Tích nhiều em rồi bấm Thêm; học sinh ở lớp tới hết năm học.
          Em lệch tuổi nhiều so với khối được gắn nhãn "Lệch tuổi"; chỉ là nhắc kiểm tra ngày sinh, vẫn xếp lớp được sau khi xác nhận.
        </p>
        {enrollSizeNotice && <InlineAlert type="warning" title={enrollSizeNotice} />}
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
            {
              title: 'Ngày sinh', dataIndex: 'dateOfBirth', width: 170, className: 'tabular',
              render: (v: string | null, c: StudentCandidate) => {
                const notice = ageNoticeOf(c);
                return (
                  <div>
                    <div>{formatDate(v)}</div>
                    {notice && <Tooltip title={notice.text}><Tag color="warning" style={{ marginInlineEnd: 0 }}>Lệch tuổi</Tag></Tooltip>}
                  </div>
                );
              },
            },
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
          title="Học sinh rời lớp hiện tại vào ngày liền trước ngày hiệu lực. Điểm danh và dữ liệu đã có ở lớp cũ được giữ nguyên."
        />
        <Form.Item
          label="Chuyển đến lớp" name="toClassId" rules={[{ required: true, message: 'Chọn lớp chuyển đến.' }]}
          extra={`Chỉ chuyển được sang lớp cùng khối (khối ${cls.gradeLevel}) trong năm học này.`}
        >
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn lớp" loading={otherClasses.isLoading}
            notFoundContent={`Chưa có lớp nào khác thuộc khối ${cls.gradeLevel}.`}
            options={sameGradeClasses?.map((c) => ({ value: c.classId, label: `${c.name} · ${c.campusName}` }))}
          />
        </Form.Item>
        {transferSizeNotice && <InlineAlert type="warning" title={transferSizeNotice} />}
        <Form.Item
          label="Ngày hiệu lực" name="effectiveDate" rules={[{ required: true, message: 'Chọn ngày hiệu lực.' }]}
        >
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={outOfYear} />
        </Form.Item>
      </FormModal>

      <FormModal<CloseValues>
        open={closing !== null}
        title={closing?.length === 1 ? `Rời lớp: ${closing[0].fullName}` : `Cho ${closing?.length ?? 0} học sinh rời lớp`}
        form={closeForm} okText="Rời lớp"
        onClose={() => setClosing(null)} onSubmit={closeEnrollment}
        initialValues={{ reason: 'ClassTransfer' }}
      >
        <Form.Item label="Lý do" name="reason" rules={[{ required: true, message: 'Chọn lý do rời lớp.' }]}>
          {/* Tốt nghiệp chỉ có ở khối cuối cấp (khối 9); khối thấp hơn mà em rời trường thì chọn Chuyển trường. */}
          <Radio.Group options={CLOSE_REASONS.filter((r) => r.value !== 'Graduated' || cls.gradeLevel === 9)} className="roster-reasons" />
        </Form.Item>
        <InlineAlert
          type="warning"
          title={reason === 'SchoolTransfer' || reason === 'Graduated'
            ? `Sẽ cho ${closeCount} học sinh rời lớp ${cls.name} và khóa ${closeCount} tài khoản học sinh ngay lập tức. Học sinh chuyển sang trạng thái không còn học.`
            : `Sẽ cho ${closeCount} học sinh rời lớp ${cls.name}. Học sinh chưa thuộc lớp nào cho tới khi được xếp lớp hoặc chuyển vào lớp mới.`}
        />
        <p className="muted">Ngày rời lớp là hôm nay ({dayjs().format('DD/MM/YYYY')}).</p>
      </FormModal>
    </>
  );
}
