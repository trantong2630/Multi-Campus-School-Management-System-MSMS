import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Form, Input, Modal, Select, Switch } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { FileDown, KeyRound, Pencil, Plus, Power, Upload } from 'lucide-react';
import { useState } from 'react';
import { PersonCell } from '@/shared/components/PersonCell';
import { useAuth } from '@/app/AuthProvider';
import { accountApi } from '@/features/iam/accountApi';
import { STAFF_ACCOUNT_TYPES } from '@/features/iam/accountTypes';
import { TempPasswordModal } from '@/features/iam/components/TempPasswordModal';
import type { BulkCreateResult } from '@/features/iam/types';
import { useCampuses, useDepartments } from '@/features/sch/hooks/useCatalogs';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { blobErrorMessage, downloadFile } from '@/shared/lib/download';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, toDateString } from '@/shared/lib/format';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { pplApi } from '../api';
import { StaffImportModal } from '../components/StaffImportModal';
import type { Staff } from '../types';

interface FormValues {
  fullName: string;
  dateOfBirth: Dayjs;
  gender?: string;
  phoneNumber?: string;
  email?: string;
  primaryCampusId: number;
  departmentId?: number;
  teacherAbbr?: string;
  position?: string;
}

const PAGE_SIZE = 20;
const GENDERS = [{ value: 'Male', label: 'Nam' }, { value: 'Female', label: 'Nữ' }, { value: 'Other', label: 'Khác' }];

export function StaffPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const campuses = useCampuses();
  const departments = useDepartments();

  const [search, setSearch] = useState('');
  const [campusId, setCampusId] = useState<number | undefined>();
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [accountType, setAccountType] = useState<string | undefined>();
  const [hasAbbr, setHasAbbr] = useState<boolean | undefined>();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [page, setPage] = useState(1);

  const filters = { search: search || undefined, campusId, departmentId, accountType, hasAbbr, includeInactive };
  const staff = useQuery({
    queryKey: ['ppl', 'staff', filters, page],
    queryFn: () => pplApi.staff.list({ ...filters, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });

  const [form] = Form.useForm<FormValues>();
  const [editing, setEditing] = useState<Staff | 'new' | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [selected, setSelected] = useState<number[]>([]);
  const [accountOpen, setAccountOpen] = useState(false);
  const [bulkType, setBulkType] = useState<AccountType>('Teacher');
  const [creating, setCreating] = useState(false);
  const [created, setCreated] = useState<BulkCreateResult | null>(null);

  const refresh = () => qc.invalidateQueries({ queryKey: ['ppl', 'staff'] });
  const toggle = useMutation({
    mutationFn: (s: Staff) => pplApi.staff.setActive(s.staffId, !s.isActive),
    onSuccess: () => { void refresh(); message.success('Đã cập nhật trạng thái hồ sơ cán bộ.'); },
    onError: (e) => showError(e),
  });

  const save = async (v: FormValues) => {
    const body = { ...v, dateOfBirth: toDateString(v.dateOfBirth) };
    if (editing === 'new') await pplApi.staff.create(body);
    else if (editing) await pplApi.staff.update(editing.staffId, body);
    await refresh();
    message.success('Đã lưu hồ sơ cán bộ.');
  };

  const template = async () => {
    try { await downloadFile('/ppl/staff/import-template'); }
    catch (e) { message.error(await blobErrorMessage(e) ?? 'Không tải được file mẫu.'); }
  };

  const createAccounts = async () => {
    setCreating(true);
    try {
      const res = await accountApi.bulkCreate(selected, bulkType);
      setAccountOpen(false);
      setSelected([]);
      setCreated(res);
      void refresh();
    } catch (e) {
      showError(e);
    } finally {
      setCreating(false);
    }
  };

  const reset = () => setPage(1);

  return (
    <>
      <PageHeader
        title="Cán bộ"
        description="Danh bạ cán bộ gồm cả Hiệu trưởng và Hiệu phó. Viết tắt GV chỉ điền cho giáo viên, duy nhất toàn trường."
        extra={canWrite && (
          <>
            <Button icon={<FileDown size={16} />} onClick={template}>Tải file mẫu</Button>
            <Button icon={<Upload size={16} />} onClick={() => setImportOpen(true)}>Import Excel</Button>
            <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm cán bộ</Button>
          </>
        )}
      />
      <FilterBar>
        <Input.Search
          allowClear placeholder="Tên, mã hồ sơ hoặc viết tắt" enterButton="Tìm kiếm"
          onSearch={(v) => { setSearch(v); reset(); }}
        />
        <Select allowClear placeholder="Phân hiệu chính" value={campusId} onChange={(v) => { setCampusId(v); reset(); }}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))} />
        <Select allowClear placeholder="Tổ chuyên môn" value={departmentId} onChange={(v) => { setDepartmentId(v); reset(); }}
          options={departments.data?.map((d) => ({ value: d.departmentId, label: d.name }))} />
        <Select allowClear placeholder="Loại tài khoản" value={accountType} onChange={(v) => { setAccountType(v); reset(); }}
          options={STAFF_ACCOUNT_TYPES.map((t) => ({ value: t, label: accountTypeLabel[t] }))} />
        <Select allowClear placeholder="Viết tắt GV" value={hasAbbr} onChange={(v) => { setHasAbbr(v); reset(); }}
          options={[{ value: true, label: 'Đã có viết tắt' }, { value: false, label: 'Chưa có viết tắt' }]} />
        <label className="inline-switch"><Switch checked={includeInactive} onChange={(v) => { setIncludeInactive(v); reset(); }} /> Hiện cả hồ sơ ngừng hoạt động</label>
      </FilterBar>

      <Card className="table-card">
        <DataTable<Staff>
          rowKey="staffId" loading={staff.isLoading} dataSource={staff.data?.items}
          emptyText="Không có cán bộ nào khớp bộ lọc."
          pagination={{ current: page, pageSize: PAGE_SIZE, total: staff.data?.total, onChange: setPage }}
          rowSelection={canWrite ? {
            selectedRowKeys: selected,
            onChange: (keys) => setSelected(keys as number[]),
            getCheckboxProps: (s) => ({ disabled: s.accountId !== null || !s.isActive }),
          } : undefined}
          columns={[
            { title: 'Họ và tên', dataIndex: 'fullName', width: 260, render: (v: string, s: Staff) => <PersonCell name={v} sub={s.personCode} muted={!s.isActive} /> },
            { title: 'Ngày sinh', dataIndex: 'dateOfBirth', width: 110, className: 'tabular', render: (v: string | null) => formatDate(v) },
            { title: 'Phân hiệu chính', dataIndex: 'primaryCampusName', width: 140 },
            { title: 'Tổ chuyên môn', dataIndex: 'departmentName', width: 150, render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Viết tắt GV', dataIndex: 'teacherAbbr', width: 100, render: (v: string | null) => v ? <code className="mono">{v}</code> : <span className="muted">Chưa có</span> },
            {
              title: 'Tài khoản', key: 'account', width: 170,
              render: (_: unknown, s: Staff) => s.accountId === null
                ? <span className="muted">Chưa có</span>
                : <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  <span>{accountTypeLabel[s.accountType as AccountType]}</span>
                  <StatusBadge domain="account" value={s.accountStatus ?? ''} />
                </div>,
            },
            ...(includeInactive ? [{ title: 'Hồ sơ', dataIndex: 'isActive', width: 150, align: 'center' as const, render: (v: boolean) => <StatusBadge domain="catalog" value={v ? 'Active' : 'Inactive'} /> }] : []),
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 110, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, s: Staff) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, onClick: () => setEditing(s) },
                  {
                    key: 'toggle', label: s.isActive ? 'Ngừng hoạt động' : 'Kích hoạt lại', icon: <Power size={16} />, danger: s.isActive,
                    onClick: () => s.isActive
                      ? confirmAction({
                        title: `Ngừng hoạt động hồ sơ ${s.fullName}?`,
                        content: 'Hệ thống từ chối nếu cán bộ còn trách nhiệm, phụ trách chuyên môn hoặc tiết dạy sắp tới chưa chuyển giao.',
                        okText: 'Ngừng hoạt động', danger: true, onOk: () => toggle.mutateAsync(s).catch(() => undefined),
                      })
                      : toggle.mutate(s),
                  },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      {selected.length > 0 && (
        <div className="floating-bar">
          <span>Đã chọn <strong>{selected.length}</strong> cán bộ chưa có tài khoản</span>
          <Button onClick={() => setSelected([])}>Bỏ chọn</Button>
          <Button type="primary" icon={<KeyRound size={16} />} onClick={() => setAccountOpen(true)}>Tạo tài khoản</Button>
        </div>
      )}

      <FormModal<FormValues>
        open={editing !== null} title={editing === 'new' ? 'Thêm cán bộ' : 'Sửa hồ sơ cán bộ'} width={640}
        form={form} onClose={() => setEditing(null)} onSubmit={save}
        initialValues={editing && editing !== 'new'
          ? { ...editing, dateOfBirth: editing.dateOfBirth ? dayjs(editing.dateOfBirth) : undefined, gender: editing.gender ?? undefined,
            phoneNumber: editing.phoneNumber ?? undefined, email: editing.email ?? undefined, departmentId: editing.departmentId ?? undefined,
            teacherAbbr: editing.teacherAbbr ?? undefined, position: editing.position ?? undefined } as Partial<FormValues>
          : undefined}
      >
        <div className="form-grid">
          <Form.Item label="Họ và tên" name="fullName" rules={[{ required: true, whitespace: true, message: 'Nhập họ và tên đầy đủ.' }]}>
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item label="Ngày sinh" name="dateOfBirth" rules={[{ required: true, message: 'Chọn ngày sinh. Họ tên và ngày sinh là khóa nhận diện khi import.' }]}>
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabledDate={(d) => d.isAfter(dayjs())} />
          </Form.Item>
          <Form.Item label="Phân hiệu chính" name="primaryCampusId" rules={[{ required: true, message: 'Chọn phân hiệu chính.' }]}>
            <Select options={campuses.data?.filter((c) => c.isActive).map((c) => ({ value: c.campusId, label: c.name }))} />
          </Form.Item>
          <Form.Item label="Tổ chuyên môn" name="departmentId">
            <Select allowClear options={departments.data?.filter((d) => d.isActive).map((d) => ({ value: d.departmentId, label: d.name }))} />
          </Form.Item>
          <Form.Item label="Giới tính" name="gender"><Select allowClear options={GENDERS} /></Form.Item>
          <Form.Item label="Chức danh" name="position"><Input maxLength={100} /></Form.Item>
          <Form.Item label="Số điện thoại" name="phoneNumber"><Input maxLength={20} /></Form.Item>
          <Form.Item label="Email nhận mã OTP" name="email" rules={[{ type: 'email', message: 'Email chưa đúng định dạng.' }]}>
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item
            label="Viết tắt GV" name="teacherAbbr"
            extra="Chỉ gồm chữ và số, duy nhất toàn trường. Không có viết tắt thì không xuất hiện được trong file thời khóa biểu."
            rules={[{ pattern: /^[A-Za-z0-9]*$/, message: 'Viết tắt chỉ gồm chữ và số, không dấu cách, không dấu gạch.' }]}
          >
            <Input maxLength={10} />
          </Form.Item>
        </div>
      </FormModal>

      <StaffImportModal open={importOpen} onClose={() => setImportOpen(false)} />

      <Modal
        open={accountOpen} title="Tạo tài khoản hàng loạt" okText="Tạo tài khoản" cancelText="Đóng"
        confirmLoading={creating} onOk={createAccounts} onCancel={() => setAccountOpen(false)} mask={{ closable: false }}
      >
        <p>Chọn một loại tài khoản cho cả lô {selected.length} cán bộ. Mỗi tài khoản nhận mật khẩu tạm sinh ngẫu nhiên, hiện một lần ở bước sau.</p>
        <Form layout="vertical" requiredMark={false}>
          <Form.Item label="Loại tài khoản">
            <Select
              value={bulkType} onChange={setBulkType}
              options={STAFF_ACCOUNT_TYPES.map((t) => ({ value: t, label: accountTypeLabel[t] }))}
            />
          </Form.Item>
        </Form>
      </Modal>

      <TempPasswordModal
        open={created !== null} title="Tài khoản vừa tạo"
        items={created?.created ?? []} skipped={created?.skipped} onClose={() => setCreated(null)}
      />
    </>
  );
}
