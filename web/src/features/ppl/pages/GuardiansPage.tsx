import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, Modal, Select } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import { Eye, KeyRound, Pencil, Plus } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { accountApi } from '@/features/iam/accountApi';
import { TempPasswordModal } from '@/features/iam/components/TempPasswordModal';
import type { BulkCreateResult } from '@/features/iam/types';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonCell } from '@/shared/components/PersonCell';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { showError } from '@/shared/lib/feedback';
import { pplApi } from '../api';
import type { Guardian, GuardianChild } from '../types';

interface FormValues {
  fullName: string;
  phoneNumber: string;
  email?: string;
  address?: string;
  gender?: string;
}

const PAGE_SIZE = 20;
const GENDERS = [{ value: 'Male', label: 'Nam' }, { value: 'Female', label: 'Nữ' }, { value: 'Other', label: 'Khác' }];
const dash = <span className="muted">—</span>;

/** PPL-03: danh bạ phụ huynh (4.3.22–4.3.25). Số điện thoại là khóa nhận diện. Chỉ School Admin tạo và sửa. */
export function GuardiansPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();

  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const guardians = useQuery({
    queryKey: ['ppl', 'guardians', search, page],
    queryFn: () => pplApi.guardians.list({ search: search || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });

  const [form] = Form.useForm<FormValues>();
  const [editing, setEditing] = useState<Guardian | 'new' | null>(null);
  const [viewing, setViewing] = useState<Guardian | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const [accountOpen, setAccountOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [created, setCreated] = useState<BulkCreateResult | null>(null);

  // Tạo tài khoản Parent hàng loạt cho phụ huynh chưa có tài khoản; mật khẩu tạm hiện một lần.
  const createAccounts = async () => {
    setCreating(true);
    try {
      const res = await accountApi.bulkCreate(selected);
      setAccountOpen(false);
      setSelected([]);
      setCreated(res);
      void qc.invalidateQueries({ queryKey: ['ppl', 'guardians'] });
    } catch (e) {
      showError(e);
    } finally {
      setCreating(false);
    }
  };
  const children = useQuery({
    queryKey: ['ppl', 'guardian-children', viewing?.parentId],
    queryFn: () => pplApi.guardians.children(viewing!.parentId),
    enabled: viewing !== null,
  });

  const save = async (v: FormValues) => {
    if (editing === 'new') await pplApi.guardians.create(v);
    else if (editing) await pplApi.guardians.update(editing.parentId, v);
    await qc.invalidateQueries({ queryKey: ['ppl', 'guardians'] });
    message.success('Đã lưu hồ sơ phụ huynh.');
  };

  return (
    <>
      <PageHeader
        title="Phụ huynh"
        description="Danh bạ phụ huynh và các con được liên kết. Số điện thoại bắt buộc, duy nhất và là khóa nhận diện khi import."
        extra={canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm phụ huynh</Button>}
      />
      <FilterBar>
        <Input.Search
          allowClear placeholder="Tên, mã hồ sơ hoặc số điện thoại" enterButton="Tìm kiếm"
          onSearch={(v) => { setSearch(v); setPage(1); }}
        />
      </FilterBar>

      <Card className="table-card">
        <DataTable<Guardian>
          rowKey="parentId" loading={guardians.isLoading} dataSource={guardians.data?.items}
          emptyText="Không có phụ huynh nào khớp bộ lọc."
          pagination={{ current: page, pageSize: PAGE_SIZE, total: guardians.data?.total, onChange: setPage }}
          rowSelection={canWrite ? {
            selectedRowKeys: selected,
            onChange: (keys) => setSelected(keys as number[]),
            getCheckboxProps: (g) => ({ disabled: g.accountId !== null }),
          } : undefined}
          columns={[
            { title: 'Mã hồ sơ', dataIndex: 'personCode', width: 110, className: 'tabular' },
            { title: 'Họ và tên', dataIndex: 'fullName', width: 210, render: (v: string) => <PersonCell name={v} /> },
            { title: 'Giới tính', dataIndex: 'gender', width: 90, render: (v: string | null) => GENDERS.find((x) => x.value === v)?.label ?? '—' },
            { title: 'Số điện thoại', dataIndex: 'phoneNumber', width: 130, className: 'tabular', render: (v: string | null) => v ?? dash },
            { title: 'Email', dataIndex: 'email', width: 180, render: (v: string | null) => v ?? dash },
            {
              title: 'Các con', key: 'children', width: 240,
              render: (_: unknown, g: Guardian) => g.childCount === 0 ? dash : (
                <div className="stack-cell">
                  <span>{g.childrenSummary}{g.childCount > 3 ? '…' : ''}</span>
                  <span className="muted">{g.childCount} học sinh</span>
                </div>
              ),
            },
            {
              title: 'Tài khoản', key: 'account', width: 140,
              render: (_: unknown, g: Guardian) => g.accountId === null ? <span className="muted">Chưa có</span> : <StatusBadge domain="account" value={g.accountStatus ?? ''} />,
            },
            {
              title: 'Thao tác', key: 'actions', width: 96, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, g: Guardian) => (
                <RowActions actions={[
                  { key: 'view', label: 'Xem các con', icon: <Eye size={16} />, onClick: () => setViewing(g) },
                  ...(canWrite ? [{ key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, onClick: () => setEditing(g) }] : []),
                ]} />
              ),
            },
          ]}
        />
      </Card>

      {selected.length > 0 && (
        <div className="floating-bar">
          <span>Đã chọn <strong>{selected.length}</strong> phụ huynh chưa có tài khoản</span>
          <Button onClick={() => setSelected([])}>Bỏ chọn</Button>
          <Button type="primary" icon={<KeyRound size={16} />} onClick={() => setAccountOpen(true)}>Tạo tài khoản</Button>
        </div>
      )}

      <FormModal<FormValues>
        open={editing !== null} title={editing === 'new' ? 'Thêm phụ huynh' : 'Sửa hồ sơ phụ huynh'} width={560} variant="drawer"
        form={form} onClose={() => setEditing(null)} onSubmit={save}
        initialValues={editing && editing !== 'new'
          ? { ...editing, phoneNumber: editing.phoneNumber ?? undefined, email: editing.email ?? undefined, address: editing.address ?? undefined } as Partial<FormValues>
          : undefined}
      >
        <div className="form-grid">
          <Form.Item label="Họ và tên" name="fullName" rules={[{ required: true, whitespace: true, message: 'Nhập họ và tên đầy đủ.' }]}>
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item label="Số điện thoại" name="phoneNumber" extra="Duy nhất giữa các phụ huynh. 9 đến 15 chữ số."
            rules={[{ required: true, message: 'Nhập số điện thoại.' }, { pattern: /^[0-9+\s.-]{9,20}$/, message: 'Số điện thoại chưa đúng định dạng.' }]}>
            <Input maxLength={20} inputMode="tel" />
          </Form.Item>
          <Form.Item label="Giới tính" name="gender"><Select allowClear options={GENDERS} /></Form.Item>
          <Form.Item label="Email" name="email" rules={[{ type: 'email', message: 'Email chưa đúng định dạng.' }]}>
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item label="Địa chỉ" name="address"><Input maxLength={255} /></Form.Item>
        </div>
      </FormModal>

      <Modal
        open={viewing !== null} title={viewing ? `Các con của ${viewing.fullName}` : ''} width={720}
        footer={<Button onClick={() => setViewing(null)}>Đóng</Button>} onCancel={() => setViewing(null)}
      >
        <DataTable<GuardianChild>
          rowKey="studentId" loading={children.isLoading} dataSource={children.data} pagination={false}
          emptyText="Phụ huynh này chưa liên kết với học sinh nào."
          columns={[
            { title: 'Mã hồ sơ', dataIndex: 'personCode', width: 110, className: 'tabular' },
            { title: 'Họ và tên', dataIndex: 'fullName', render: (v: string) => <PersonCell name={v} /> },
            { title: 'Lớp', dataIndex: 'className', width: 80, render: (v: string | null) => v ?? dash },
            { title: 'Phân hiệu', dataIndex: 'campusName', width: 120, render: (v: string | null) => v ?? dash },
            { title: 'Quan hệ', dataIndex: 'relationship', width: 110, render: (v: string | null) => v ?? dash },
            { title: 'Trạng thái', dataIndex: 'studentStatus', width: 140, render: (v: string) => <StatusBadge domain="student" value={v} /> },
          ]}
        />
      </Modal>
      <Modal
        open={accountOpen} title="Tạo tài khoản hàng loạt" okText="Tạo tài khoản" cancelText="Đóng"
        confirmLoading={creating} onOk={createAccounts} onCancel={() => setAccountOpen(false)} mask={{ closable: false }}
      >
        <p>Tạo tài khoản Phụ huynh cho {selected.length} hồ sơ đã chọn. Mỗi tài khoản nhận mật khẩu tạm sinh ngẫu nhiên, hiện một lần ở bước sau.</p>
      </Modal>

      <TempPasswordModal
        open={created !== null} title="Tài khoản vừa tạo"
        items={created?.created ?? []} skipped={created?.skipped} onClose={() => setCreated(null)}
      />
    </>
  );
}