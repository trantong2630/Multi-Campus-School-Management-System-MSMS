import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import { Pencil, Plus, Power } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { AddressInput } from '@/shared/components/AddressInput';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { schApi } from '../api';
import { useCampuses } from '../hooks/useCatalogs';
import type { Campus } from '../types';

interface FormValues {
  code: string;
  name: string;
  address?: string;
  phoneNumber?: string;
}

export function CampusPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const { data, isLoading } = useCampuses();
  const [form] = Form.useForm<FormValues>();
  const [editing, setEditing] = useState<Campus | 'new' | null>(null);

  const refresh = () => qc.invalidateQueries({ queryKey: ['sch', 'campuses'] });
  const toggle = useMutation({
    mutationFn: (c: Campus) => schApi.campuses.setActive(c.campusId, !c.isActive),
    onSuccess: () => { void refresh(); message.success('Đã cập nhật trạng thái phân hiệu.'); },
    onError: (e) => showError(e),
  });

  const save = async (values: FormValues) => {
    if (editing === 'new') await schApi.campuses.create(values);
    else if (editing) await schApi.campuses.update(editing.campusId, values);
    await refresh();
    message.success('Đã lưu phân hiệu.');
  };

  return (
    <>
      <PageHeader
        title="Phân hiệu"
        description="Mã và tên phân hiệu là khóa tra cứu của các file import nên không được trùng."
        extra={canWrite && (
          <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm phân hiệu</Button>
        )}
      />
      <Card className="table-card">
        <DataTable<Campus>
          rowKey="campusId"
          loading={isLoading}
          dataSource={data}
          emptyText="Chưa có phân hiệu nào."
          columns={[
            { title: 'Mã', dataIndex: 'code', width: 120, render: (v: string) => <span className="tabular">{v}</span> },
            { title: 'Tên phân hiệu', dataIndex: 'name' },
            { title: 'Địa chỉ', dataIndex: 'address', render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Số điện thoại', dataIndex: 'phoneNumber', width: 150, render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Số lớp', dataIndex: 'activeClassCount', width: 100, align: 'right', className: 'tabular' },
            {
              title: 'Trạng thái', dataIndex: 'isActive', width: 160, align: 'center',
              render: (v: boolean) => <StatusBadge domain="catalog" value={v ? 'Active' : 'Inactive'} />,
            },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 120, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, c: Campus) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, onClick: () => setEditing(c) },
                  {
                    key: 'toggle', label: c.isActive ? 'Ngừng hoạt động' : 'Kích hoạt lại', icon: <Power size={16} />, danger: c.isActive,
                    onClick: () => c.isActive
                      ? confirmAction({
                        title: `Ngừng hoạt động ${c.name}?`,
                        content: c.activeClassCount > 0
                          ? `Phân hiệu còn ${c.activeClassCount} lớp đang hoạt động nên hệ thống sẽ từ chối. Ngừng các lớp đó trước.`
                          : 'Phân hiệu sẽ không còn xuất hiện trong các ô chọn. Dữ liệu cũ được giữ nguyên.',
                        okText: 'Ngừng hoạt động', danger: true, onOk: () => toggle.mutateAsync(c).catch(() => undefined),
                      })
                      : toggle.mutate(c),
                  },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      <FormModal<FormValues>
        open={editing !== null}
        title={editing === 'new' ? 'Thêm phân hiệu' : 'Sửa phân hiệu'}
        form={form}
        onClose={() => setEditing(null)}
        onSubmit={save}
        initialValues={editing && editing !== 'new' ? editing : undefined}
      >
        <Form.Item label="Mã phân hiệu" name="code" rules={[{ required: true, whitespace: true, message: 'Nhập mã phân hiệu, ví dụ PH1.' }]}>
          <Input maxLength={20} />
        </Form.Item>
        <Form.Item label="Tên phân hiệu" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên phân hiệu.' }]}>
          <Input maxLength={150} />
        </Form.Item>
        <Form.Item label="Địa chỉ" name="address" rules={[{ max: 255, message: 'Địa chỉ tối đa 255 ký tự. Rút gọn số nhà, đường.' }]}>
          <AddressInput />
        </Form.Item>
        <Form.Item label="Số điện thoại" name="phoneNumber"><Input maxLength={20} /></Form.Item>
      </FormModal>
    </>
  );
}
