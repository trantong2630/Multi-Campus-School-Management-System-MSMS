import { useQuery } from '@tanstack/react-query';
import { Form, Input, Modal, Segmented, Select, Space } from 'antd';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { getErrorMessage } from '@/app/apiClient';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { accountApi } from '../accountApi';
import { STAFF_ACCOUNT_TYPES } from '../accountTypes';
import type { AccountDirectoryItem, BulkCreateResult } from '../types';

interface Props {
  mode: 'single' | 'bulk';
  onClose: () => void;
  onCreated: (result: BulkCreateResult) => void;
}

/** Chọn hồ sơ từ danh bạ, dùng chung API IAM với các màn hình hồ sơ. */
export function CreateAccountsModal({ mode, onClose, onCreated }: Props) {
  const [personType, setPersonType] = useState('Staff');
  const [accountType, setAccountType] = useState<AccountType>('Teacher');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<number[]>([]);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const directory = useQuery({
    queryKey: ['iam', 'directory', personType, search, page],
    queryFn: () => accountApi.directory({ personType, search: search || undefined, page, pageSize: 20 }),
  });

  const create = async () => {
    setCreating(true);
    setError(null);
    try {
      const type = personType === 'Staff' ? accountType : personType;
      const result = mode === 'single'
        ? { created: [await accountApi.create(selected[0], type)], skipped: [] }
        : await accountApi.bulkCreate(selected, type);
      // Chuyển ngay mật khẩu vừa nhận sang màn hình kết quả, không đưa vào query cache.
      onCreated(result);
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setCreating(false);
    }
  };

  return (
    <Modal open title={mode === 'single' ? 'Tạo tài khoản' : 'Tạo tài khoản hàng loạt'} width={840}
      onCancel={onClose} onOk={create} okText="Tạo tài khoản" cancelText="Đóng" confirmLoading={creating}
      okButtonProps={{ disabled: selected.length === 0 || directory.isError }} cancelButtonProps={{ disabled: creating }}
      closable={!creating} keyboard={!creating} mask={{ closable: false }}>
      <Space orientation="vertical" style={{ width: '100%' }} size="middle">
        <Segmented disabled={creating} value={personType} options={[
          { value: 'Staff', label: 'Cán bộ' }, { value: 'Student', label: 'Học sinh' }, { value: 'Parent', label: 'Phụ huynh' },
        ]} onChange={(value) => { setPersonType(value); setSelected([]); setPage(1); setError(null); }} />
        <Input.Search allowClear placeholder="Tìm theo tên hoặc mã hồ sơ" disabled={creating}
          onSearch={(value) => { setSearch(value); setSelected([]); setPage(1); }} />
        {personType === 'Staff' && (
          <Form layout="vertical">
            <Form.Item label="Loại tài khoản cho các hồ sơ đã chọn" style={{ marginBottom: 0 }}>
              <Select value={accountType} onChange={setAccountType} disabled={creating}
                options={STAFF_ACCOUNT_TYPES.map((type) => ({ value: type, label: accountTypeLabel[type] }))} />
            </Form.Item>
          </Form>
        )}
        {personType !== 'Staff' && <p>Loại tài khoản: {accountTypeLabel[personType as AccountType]}.</p>}
        <DataTable<AccountDirectoryItem> rowKey="personId" dataSource={directory.data?.items} loading={directory.isLoading}
          emptyText="Không có hồ sơ phù hợp." pagination={{ current: page, pageSize: 20, total: directory.data?.total, onChange: setPage }}
          rowSelection={{ type: mode === 'single' ? 'radio' : 'checkbox', selectedRowKeys: selected, preserveSelectedRowKeys: true,
            onChange: (keys) => {
              if (keys.length > 200) { setError('Mỗi lần chọn tối đa 200 hồ sơ.'); return; }
              setError(null);
              setSelected(keys as number[]);
            },
            getCheckboxProps: (person) => ({ disabled: creating || person.hasAccount || !person.isActive || (selected.length >= 200 && !selected.includes(person.personId)) }),
          }}
          columns={[
            { title: 'Mã hồ sơ', dataIndex: 'personCode', width: 120 },
            { title: 'Họ và tên', dataIndex: 'fullName' },
            { title: 'Phân hiệu chính', dataIndex: 'campusName', render: (v: string | null) => v ?? '—' },
            { title: 'Tài khoản', render: (_: unknown, person: AccountDirectoryItem) => person.hasAccount ? 'Đã có tài khoản' : !person.isActive ? 'Ngừng hoạt động' : 'Chưa có tài khoản' },
          ]} />
        <span>Đã chọn {selected.length} hồ sơ{mode === 'bulk' ? ' (tối đa 200 mỗi lần)' : ''}. Hồ sơ đã có tài khoản được bỏ qua.</span>
        {(error || directory.isError) && <InlineAlert type="error" title={error ?? getErrorMessage(directory.error)} />}
      </Space>
    </Modal>
  );
}
