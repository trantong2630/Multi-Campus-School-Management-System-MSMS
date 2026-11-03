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
import { StatCard } from '@/shared/components/StatCard';
import { StatStrip } from '@/shared/components/StatStrip';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatNumber as fmt } from '@/shared/lib/format';
import { schApi } from '../api';
import { useCampuses, useCampusStats } from '../hooks/useCatalogs';
import type { Campus, CampusStat } from '../types';
import '../components/school.css';

type CampusRow = Campus & { stat: CampusStat };

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
  const campuses = useCampuses();
  const stats = useCampusStats();
  const [form] = Form.useForm<FormValues>();
  const [editing, setEditing] = useState<Campus | 'new' | null>(null);

  // Thống kê đã lọc theo phạm vi ở API: Hiệu phó chỉ nhận phân hiệu mình phụ trách, nên bảng cũng chỉ giữ các phân hiệu đó.
  const statOf = new Map((stats.data ?? []).map((s) => [s.campusId, s]));
  const data: CampusRow[] | undefined = campuses.data && stats.data
    ? campuses.data.filter((c) => statOf.has(c.campusId)).map((c) => ({ ...c, stat: statOf.get(c.campusId)! }))
    : undefined;
  const active = data?.filter((c) => c.isActive) ?? [];
  const sum = (pick: (s: CampusStat) => number) => active.reduce((n, c) => n + pick(c.stat), 0);
  const studentCount = sum((s) => s.studentCount);
  const classCount = sum((s) => s.classCount);
  const withoutLeader = active.filter((c) => !c.stat.leaders.length).length;

  const refresh = () => Promise.all([
    qc.invalidateQueries({ queryKey: ['sch', 'campuses'] }),
    qc.invalidateQueries({ queryKey: ['sch', 'stats', 'campuses'] }),
  ]);
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
        description={canWrite
          ? 'Mã và tên phân hiệu là khóa tra cứu của các file import nên không được trùng.'
          : 'Quy mô từng phân hiệu trong năm học hiện hành, tính tại hôm nay.'}
        extra={canWrite && (
          <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm phân hiệu</Button>
        )}
      />
      <StatStrip>
        <StatCard label="Phân hiệu đang hoạt động" value={fmt(data && active.length)}
          hint={data && data.length > active.length ? `${data.length - active.length} phân hiệu ngừng hoạt động` : undefined} />
        <StatCard label="Lớp đang hoạt động" value={fmt(data && classCount)} />
        <StatCard label="Học sinh đang học" value={fmt(data && studentCount)} />
        <StatCard label="Cán bộ đang công tác" value={fmt(data && sum((s) => s.staffCount))} />
      </StatStrip>
      <Card className="table-card">
        <DataTable<CampusRow>
          rowKey="campusId"
          loading={campuses.isLoading || stats.isLoading}
          dataSource={data}
          emptyText="Chưa có phân hiệu nào."
          columns={[
            { title: 'Mã', dataIndex: 'code', width: 100, render: (v: string) => <span className="tabular">{v}</span> },
            {
              title: 'Tên phân hiệu', dataIndex: 'name',
              render: (v: string, c: CampusRow) => (
                <div className="cell-stack">
                  <b>{v}</b>
                  <span className={c.stat.leaders.length || !c.isActive ? 'muted' : 'text-warn'}>
                    {c.stat.leaders.length ? `Hiệu phó: ${c.stat.leaders.join(', ')}` : 'Chưa có hiệu phó phụ trách'}
                  </span>
                </div>
              ),
            },
            { title: 'Địa chỉ', dataIndex: 'address', render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Số điện thoại', dataIndex: 'phoneNumber', width: 140, render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Lớp', key: 'classes', width: 80, align: 'right', className: 'tabular', render: (_: unknown, c: CampusRow) => fmt(c.stat.classCount) },
            { title: 'Học sinh', key: 'students', width: 100, align: 'right', className: 'tabular', render: (_: unknown, c: CampusRow) => fmt(c.stat.studentCount) },
            { title: 'Cán bộ', key: 'staff', width: 90, align: 'right', className: 'tabular', render: (_: unknown, c: CampusRow) => fmt(c.stat.staffCount) },
            {
              title: 'Trạng thái', dataIndex: 'isActive', width: 160, align: 'center',
              render: (v: boolean) => <StatusBadge domain="catalog" value={v ? 'Active' : 'Inactive'} />,
            },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 120, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, c: CampusRow) => (
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
