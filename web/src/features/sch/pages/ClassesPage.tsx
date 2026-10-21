import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, Select, Switch } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import { Pencil, Plus, Power } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { schApi } from '../api';
import { useCampuses, useCurrentYear, useYears } from '../hooks/useCatalogs';
import type { SchoolClass } from '../types';

interface FormValues { campusId: number; academicYearId: number; name: string; gradeLevel: number }

const GRADES = [6, 7, 8, 9].map((g) => ({ value: g, label: `Khối ${g}` }));

export function ClassesPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const years = useYears();
  const { current } = useCurrentYear();
  const campuses = useCampuses();
  const [yearId, setYearId] = useState<number | undefined>();
  const [campusId, setCampusId] = useState<number | undefined>();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<SchoolClass | 'new' | null>(null);
  const [form] = Form.useForm<FormValues>();

  const effectiveYear = yearId ?? current?.academicYearId;
  const classes = useQuery({
    queryKey: ['sch', 'classes', effectiveYear, campusId, includeInactive],
    queryFn: () => schApi.classes.list({ yearId: effectiveYear, campusId, includeInactive }),
    enabled: effectiveYear !== undefined,
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['sch', 'classes'] });
  const toggle = useMutation({
    mutationFn: (c: SchoolClass) => schApi.classes.setActive(c.classId, !c.isActive),
    onSuccess: () => { void refresh(); message.success('Đã cập nhật trạng thái lớp.'); },
    onError: (e) => showError(e),
  });

  const save = async (v: FormValues) => {
    if (editing === 'new') await schApi.classes.create(v);
    else if (editing) await schApi.classes.update(editing.classId, { name: v.name, gradeLevel: v.gradeLevel });
    await refresh();
    message.success('Đã lưu lớp.');
  };

  return (
    <>
      <PageHeader
        title="Lớp & Ghi danh"
        description="Tên lớp duy nhất trong phạm vi (phân hiệu, năm học). Ghi danh học sinh làm cùng danh bạ học sinh."
        extra={canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm lớp</Button>}
      />
      <FilterBar>
        <Select
          placeholder="Năm học" value={effectiveYear} onChange={setYearId}
          options={years.data?.map((y) => ({ value: y.academicYearId, label: `Năm học ${y.name}` }))}
        />
        <Select
          allowClear placeholder="Tất cả phân hiệu" value={campusId} onChange={setCampusId}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
        <label className="inline-switch"><Switch checked={includeInactive} onChange={setIncludeInactive} /> Hiện cả lớp ngừng hoạt động</label>
      </FilterBar>
      <Card className="table-card">
        <DataTable<SchoolClass>
          rowKey="classId" loading={classes.isLoading || years.isLoading} dataSource={classes.data}
          emptyText="Chưa có lớp nào trong phạm vi đã chọn."
          columns={[
            { title: 'Lớp', dataIndex: 'name', width: 120, render: (v: string) => <strong>{v}</strong> },
            { title: 'Khối', dataIndex: 'gradeLevel', width: 90, className: 'tabular' },
            { title: 'Phân hiệu', dataIndex: 'campusName' },
            { title: 'Năm học', dataIndex: 'academicYearName', width: 120, className: 'tabular' },
            { title: 'Sĩ số', dataIndex: 'studentCount', width: 100, align: 'right', className: 'tabular' },
            { title: 'Chủ nhiệm', dataIndex: 'homeroomName', render: (v: string | null) => v ?? <span className="muted">Chưa có</span> },
            { title: 'Trạng thái', dataIndex: 'isActive', width: 160, align: 'center', render: (v: boolean) => <StatusBadge domain="catalog" value={v ? 'Active' : 'Inactive'} /> },
            ...(canWrite ? [{
              title: 'Thao tác', key: 'actions', width: 120, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, c: SchoolClass) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, onClick: () => setEditing(c) },
                  {
                    key: 'toggle', label: c.isActive ? 'Ngừng hoạt động' : 'Kích hoạt lại', icon: <Power size={16} />, danger: c.isActive,
                    onClick: () => c.isActive
                      ? confirmAction({
                        title: `Ngừng hoạt động lớp ${c.name}?`,
                        content: c.studentCount > 0 ? `Lớp còn ${c.studentCount} học sinh có ghi danh đang mở nên hệ thống sẽ từ chối.` : 'Lớp sẽ không còn xuất hiện trong file thời khóa biểu mới. Dữ liệu đã phát sinh được giữ nguyên.',
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
        open={editing !== null} title={editing === 'new' ? 'Thêm lớp' : 'Sửa lớp'}
        form={form} onClose={() => setEditing(null)} onSubmit={save}
        initialValues={editing && editing !== 'new' ? editing : { academicYearId: effectiveYear, campusId }}
      >
        <Form.Item label="Phân hiệu" name="campusId" rules={[{ required: true, message: 'Chọn phân hiệu của lớp.' }]}>
          <Select disabled={editing !== 'new'} options={campuses.data?.filter((c) => c.isActive).map((c) => ({ value: c.campusId, label: c.name }))} />
        </Form.Item>
        <Form.Item label="Năm học" name="academicYearId" rules={[{ required: true, message: 'Chọn năm học của lớp.' }]}>
          <Select disabled={editing !== 'new'} options={years.data?.map((y) => ({ value: y.academicYearId, label: y.name }))} />
        </Form.Item>
        <Form.Item label="Tên lớp" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên lớp, ví dụ 6A1.' }]}>
          <Input maxLength={50} />
        </Form.Item>
        <Form.Item label="Khối" name="gradeLevel" rules={[{ required: true, message: 'Chọn khối từ 6 đến 9.' }]}>
          <Select options={GRADES} />
        </Form.Item>
      </FormModal>
    </>
  );
}
