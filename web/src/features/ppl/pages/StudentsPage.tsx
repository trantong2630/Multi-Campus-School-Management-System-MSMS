import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input, Select } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { FileDown, GraduationCap, LogOut, Pencil, Plus, Upload, UserRoundCog } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { schApi } from '@/features/sch/api';
import { useCampuses, useCurrentYear, useYears } from '@/features/sch/hooks/useCatalogs';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonCell } from '@/shared/components/PersonCell';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { blobErrorMessage, downloadFile } from '@/shared/lib/download';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, toDateString } from '@/shared/lib/format';
import { pplApi } from '../api';
import { ChangeGuardianModal } from '../components/ChangeGuardianModal';
import { StudentImportModal } from '../components/StudentImportModal';
import type { Student } from '../types';

interface FormValues {
  fullName: string;
  dateOfBirth: Dayjs;
  nationalId?: string;
  gender?: string;
  phoneNumber?: string;
  address?: string;
  note?: string;
}

const PAGE_SIZE = 20;
const GENDERS = [{ value: 'Male', label: 'Nam' }, { value: 'Female', label: 'Nữ' }, { value: 'Other', label: 'Khác' }];
const STATUSES = [
  { value: 'Studying', label: 'Đang học' },
  { value: 'Transferred', label: 'Đã chuyển trường' },
  { value: 'Graduated', label: 'Đã tốt nghiệp' },
];
const dash = <span className="muted">—</span>;

/** PPL-02: danh bạ học sinh (4.3.12–4.3.16). Chỉ School Admin tạo, sửa và import; Principal và Hiệu phó chỉ xem. */
export function StudentsPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const campuses = useCampuses();
  const years = useYears();
  const { current } = useCurrentYear();

  const [search, setSearch] = useState('');
  const [yearId, setYearId] = useState<number | undefined>();
  const [campusId, setCampusId] = useState<number | undefined>();
  const [classId, setClassId] = useState<number | undefined>();
  const [status, setStatus] = useState<string | undefined>();
  const [page, setPage] = useState(1);

  // Lọc theo lớp cần năm học: mặc định năm hiện hành khi chọn phân hiệu hoặc lớp.
  const effectiveYearId = yearId ?? (campusId || classId ? current?.academicYearId : undefined);
  const classes = useQuery({
    queryKey: ['sch', 'classes', { campusId, yearId: effectiveYearId }],
    queryFn: () => schApi.classes.list({ campusId, yearId: effectiveYearId }),
    enabled: effectiveYearId !== undefined,
  });

  const filters = { search: search || undefined, yearId: effectiveYearId, campusId, classId, status };
  const students = useQuery({
    queryKey: ['ppl', 'students', filters, page],
    queryFn: () => pplApi.students.list({ ...filters, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });

  const [form] = Form.useForm<FormValues>();
  const [editing, setEditing] = useState<Student | 'new' | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [guardianOf, setGuardianOf] = useState<Student | null>(null);
  const [selected, setSelected] = useState<number[]>([]);

  // 4.3.19–4.3.20: cùng một thao tác cho một hoặc nhiều học sinh; lỗi thì không em nào bị đổi.
  const leave = (reason: 'SchoolTransfer' | 'Graduated') => {
    const graduating = reason === 'Graduated';
    confirmAction({
      title: `${graduating ? 'Cho tốt nghiệp' : 'Chuyển trường cho'} ${selected.length} học sinh?`,
      content: 'Hệ thống đóng ghi danh đang mở, đổi trạng thái hồ sơ và khóa tài khoản học sinh trong cùng thao tác. '
        + (graduating ? 'Chỉ áp dụng cho học sinh khối cuối cấp.' : 'Không hoàn tác từ màn hình này.'),
      okText: graduating ? 'Tốt nghiệp' : 'Chuyển trường', danger: !graduating,
      onOk: async () => {
        try {
          await pplApi.students.leave(selected, reason);
          setSelected([]);
          await refresh();
          message.success(graduating ? 'Đã cho tốt nghiệp.' : 'Đã chuyển trường.');
        } catch (e) { showError(e); }
      },
    });
  };

  const refresh = () => qc.invalidateQueries({ queryKey: ['ppl', 'students'] });
  const reset = () => setPage(1);

  const save = async (v: FormValues) => {
    const body = { ...v, dateOfBirth: toDateString(v.dateOfBirth) };
    if (editing === 'new') await pplApi.students.create(body);
    else if (editing) await pplApi.students.update(editing.studentId, body);
    await refresh();
    message.success('Đã lưu hồ sơ học sinh.');
  };

  const template = async () => {
    try { await downloadFile('/ppl/students/import-template', { yearId: effectiveYearId, campusId }); }
    catch (e) { message.error(await blobErrorMessage(e) ?? 'Không tải được file mẫu.'); }
  };

  return (
    <>
      <PageHeader
        title="Học sinh"
        description="Danh bạ học sinh toàn trường. Lớp hiển thị là lớp đang học tại năm được chọn (mặc định: ghi danh đang mở hôm nay)."
        extra={canWrite && (
          <>
            <Button icon={<FileDown size={16} />} onClick={template}>Tải file mẫu</Button>
            <Button icon={<Upload size={16} />} onClick={() => setImportOpen(true)}>Import Excel</Button>
            <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm học sinh</Button>
          </>
        )}
      />
      <FilterBar>
        <Input.Search
          allowClear placeholder="Tên hoặc mã hồ sơ" enterButton="Tìm kiếm"
          onSearch={(v) => { setSearch(v); reset(); }}
        />
        <Select allowClear placeholder="Năm học" value={yearId} onChange={(v) => { setYearId(v); setClassId(undefined); reset(); }}
          options={years.data?.map((y) => ({ value: y.academicYearId, label: y.name }))} />
        <Select allowClear placeholder="Phân hiệu" value={campusId} onChange={(v) => { setCampusId(v); setClassId(undefined); reset(); }}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))} />
        <Select allowClear showSearch optionFilterProp="label" placeholder="Lớp" value={classId} onChange={(v) => { setClassId(v); reset(); }}
          disabled={effectiveYearId === undefined && !campusId}
          options={classes.data?.map((c) => ({ value: c.classId, label: `${c.name} · ${c.campusName}` }))} />
        <Select allowClear placeholder="Trạng thái" value={status} onChange={(v) => { setStatus(v); reset(); }} options={STATUSES} />
      </FilterBar>

      <Card className="table-card">
        <DataTable<Student>
          rowKey="studentId" loading={students.isLoading} dataSource={students.data?.items}
          emptyText="Không có học sinh nào khớp bộ lọc."
          pagination={{ current: page, pageSize: PAGE_SIZE, total: students.data?.total, onChange: setPage }}
          rowSelection={canWrite ? {
            selectedRowKeys: selected,
            onChange: (keys) => setSelected(keys as number[]),
            getCheckboxProps: (s) => ({ disabled: s.status !== 'Studying' }),
          } : undefined}
          columns={[
            { title: 'Mã hồ sơ', dataIndex: 'personCode', width: 110, className: 'tabular' },
            { title: 'Họ và tên', dataIndex: 'fullName', width: 210, render: (v: string, s: Student) => <PersonCell name={v} muted={s.status !== 'Studying'} /> },
            { title: 'Ngày sinh', dataIndex: 'dateOfBirth', width: 104, className: 'tabular', render: (v: string | null) => formatDate(v) },
            { title: 'Số định danh', dataIndex: 'nationalId', width: 120, className: 'tabular' },
            { title: 'Lớp', dataIndex: 'className', width: 90, render: (v: string | null) => v ?? dash },
            { title: 'Phân hiệu', dataIndex: 'campusName', width: 120, render: (v: string | null) => v ?? dash },
            {
              title: 'Phụ huynh đại diện', key: 'guardian', width: 170,
              render: (_: unknown, s: Student) => s.guardianName
                ? <div className="stack-cell"><span>{s.guardianName}</span><span className="muted tabular">{s.guardianPhone}</span></div>
                : dash,
            },
            { title: 'Trạng thái', dataIndex: 'status', width: 140, render: (v: string) => <StatusBadge domain="student" value={v} /> },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 80, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, s: Student) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, onClick: () => setEditing(s) },
                  { key: 'guardian', label: 'Đổi phụ huynh đại diện', icon: <UserRoundCog size={16} />, onClick: () => setGuardianOf(s) },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      {selected.length > 0 && (
        <div className="floating-bar">
          <span>Đã chọn <strong>{selected.length}</strong> học sinh đang học</span>
          <Button onClick={() => setSelected([])}>Bỏ chọn</Button>
          <Button danger icon={<LogOut size={16} />} onClick={() => leave('SchoolTransfer')}>Chuyển trường</Button>
          <Button type="primary" icon={<GraduationCap size={16} />} onClick={() => leave('Graduated')}>Tốt nghiệp</Button>
        </div>
      )}

      <FormModal<FormValues>
        open={editing !== null} title={editing === 'new' ? 'Thêm học sinh' : 'Sửa hồ sơ học sinh'} width={640} variant="drawer"
        form={form} onClose={() => setEditing(null)} onSubmit={save}
        initialValues={editing && editing !== 'new'
          ? {
            ...editing, nationalId: editing.nationalId, dateOfBirth: editing.dateOfBirth ? dayjs(editing.dateOfBirth) : undefined,
            gender: editing.gender ?? undefined, phoneNumber: editing.phoneNumber ?? undefined, address: editing.address ?? undefined,
            note: editing.note ?? undefined,
          } as Partial<FormValues>
          : undefined}
      >
        <div className="form-grid">
          <Form.Item label="Họ và tên" name="fullName" rules={[{ required: true, whitespace: true, message: 'Nhập họ và tên đầy đủ.' }]}>
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item label="Ngày sinh" name="dateOfBirth" rules={[{ required: true, message: 'Chọn ngày sinh.' }]}>
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={(d) => d.isAfter(dayjs())} />
          </Form.Item>
          <Form.Item label="Số định danh cá nhân" name="nationalId"
            extra={editing === 'new' ? 'Đủ 12 chữ số, duy nhất toàn trường, không sửa được sau khi tạo.' : 'Không sửa được sau khi tạo.'}
            rules={editing === 'new' ? [{ required: true, message: 'Nhập số định danh cá nhân.' }, { pattern: /^\d{12}$/, message: 'Số định danh gồm đúng 12 chữ số.' }] : []}>
            <Input maxLength={12} inputMode="numeric" disabled={editing !== 'new'} />
          </Form.Item>
          <Form.Item label="Giới tính" name="gender"><Select allowClear options={GENDERS} /></Form.Item>
          <Form.Item label="Số điện thoại" name="phoneNumber"><Input maxLength={20} /></Form.Item>
          <Form.Item label="Địa chỉ" name="address"><Input maxLength={255} /></Form.Item>
          <Form.Item label="Ghi chú" name="note"><Input.TextArea rows={3} maxLength={500} /></Form.Item>
        </div>
      </FormModal>

      <ChangeGuardianModal student={guardianOf} onClose={() => setGuardianOf(null)} />
      <StudentImportModal open={importOpen} onClose={() => setImportOpen(false)} yearId={effectiveYearId} campusId={campusId} />
    </>
  );
}