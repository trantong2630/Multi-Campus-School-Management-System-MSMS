import { useMutation, useQueryClient, useQuery } from '@tanstack/react-query';
import { Button, Card, Form, Input, Select, Tabs } from 'antd';
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
import { useDepartments } from '../hooks/useCatalogs';
import type { Department, Subject } from '../types';

interface DeptValues { name: string; description?: string }
interface SubjectValues { departmentId: number; name: string; abbreviation: string }

export function SubjectsPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const qc = useQueryClient();
  const [tab, setTab] = useState<'subjects' | 'departments'>('subjects');
  const [deptFilter, setDeptFilter] = useState<number | undefined>();
  const departments = useDepartments();
  const subjects = useQuery({ queryKey: ['sch', 'subjects', deptFilter], queryFn: () => schApi.subjects.list(deptFilter) });

  const [deptForm] = Form.useForm<DeptValues>();
  const [subjectForm] = Form.useForm<SubjectValues>();
  const [editingDept, setEditingDept] = useState<Department | 'new' | null>(null);
  const [editingSubject, setEditingSubject] = useState<Subject | 'new' | null>(null);

  const refresh = () => qc.invalidateQueries({ queryKey: ['sch'] });
  const toggleDept = useMutation({
    mutationFn: (d: Department) => schApi.departments.setActive(d.departmentId, !d.isActive),
    onSuccess: () => { void refresh(); message.success('Đã cập nhật trạng thái tổ chuyên môn.'); },
    onError: (e) => showError(e),
  });
  const toggleSubject = useMutation({
    mutationFn: (s: Subject) => schApi.subjects.setActive(s.subjectId, !s.isActive),
    onSuccess: () => { void refresh(); message.success('Đã cập nhật trạng thái môn học.'); },
    onError: (e) => showError(e),
  });

  const saveDept = async (v: DeptValues) => {
    if (editingDept === 'new') await schApi.departments.create(v);
    else if (editingDept) await schApi.departments.update(editingDept.departmentId, v);
    await refresh();
    message.success('Đã lưu tổ chuyên môn.');
  };
  const saveSubject = async (v: SubjectValues) => {
    if (editingSubject === 'new') await schApi.subjects.create(v);
    else if (editingSubject) await schApi.subjects.update(editingSubject.subjectId, v);
    await refresh();
    message.success('Đã lưu môn học.');
  };

  const addButton = canWrite && (
    tab === 'subjects'
      ? <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditingSubject('new')}>Thêm môn học</Button>
      : <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditingDept('new')}>Thêm tổ chuyên môn</Button>
  );

  return (
    <>
      <PageHeader
        title="Tổ chuyên môn & Môn học"
        description="Viết tắt môn chỉ gồm chữ và số, duy nhất toàn trường — dùng trong ô MÔN-GV của file thời khóa biểu."
        extra={addButton}
      />
      <Tabs
        activeKey={tab}
        onChange={(k) => setTab(k as typeof tab)}
        items={[
          {
            key: 'subjects',
            label: 'Môn học',
            children: (
              <>
                <FilterBar>
                  <Select
                    allowClear placeholder="Tất cả tổ chuyên môn" value={deptFilter} onChange={setDeptFilter}
                    options={departments.data?.map((d) => ({ value: d.departmentId, label: d.name }))}
                  />
                </FilterBar>
                <Card className="table-card">
                  <DataTable<Subject>
                    rowKey="subjectId" loading={subjects.isLoading} dataSource={subjects.data}
                    emptyText="Chưa có môn học nào."
                    columns={[
                      { title: 'Viết tắt', dataIndex: 'abbreviation', width: 120, render: (v: string) => <code className="mono">{v}</code> },
                      { title: 'Môn học', dataIndex: 'name' },
                      { title: 'Tổ chuyên môn', dataIndex: 'departmentName' },
                      { title: 'Trạng thái', dataIndex: 'isActive', width: 160, align: 'center', render: (v: boolean) => <StatusBadge domain="catalog" value={v ? 'Active' : 'Inactive'} /> },
                      ...(canWrite ? [{
                        title: 'Thao tác', key: 'actions', width: 120, align: 'center' as const, fixed: 'right' as const,
                        render: (_: unknown, s: Subject) => (
                          <RowActions actions={[
                            { key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, onClick: () => setEditingSubject(s) },
                            {
                              key: 'toggle', label: s.isActive ? 'Ngừng hoạt động' : 'Kích hoạt lại', icon: <Power size={16} />, danger: s.isActive,
                              onClick: () => s.isActive
                                ? confirmAction({ title: `Ngừng hoạt động môn ${s.name}?`, content: 'Môn sẽ không còn xuất hiện khi khai báo và import thời khóa biểu mới. Dữ liệu cũ được giữ nguyên.', okText: 'Ngừng hoạt động', danger: true, onOk: () => toggleSubject.mutateAsync(s).catch(() => undefined) })
                                : toggleSubject.mutate(s),
                            },
                          ]} />
                        ),
                      }] : []),
                    ]}
                  />
                </Card>
              </>
            ),
          },
          {
            key: 'departments',
            label: 'Tổ chuyên môn',
            children: (
              <Card className="table-card">
                <DataTable<Department>
                  rowKey="departmentId" loading={departments.isLoading} dataSource={departments.data}
                  emptyText="Chưa có tổ chuyên môn nào."
                  columns={[
                    { title: 'Tổ chuyên môn', dataIndex: 'name' },
                    { title: 'Mô tả', dataIndex: 'description', render: (v: string | null) => v ?? <span className="muted">—</span> },
                    { title: 'Số môn', dataIndex: 'subjectCount', width: 100, align: 'right', className: 'tabular' },
                    { title: 'Trạng thái', dataIndex: 'isActive', width: 160, align: 'center', render: (v: boolean) => <StatusBadge domain="catalog" value={v ? 'Active' : 'Inactive'} /> },
                    ...(canWrite ? [{
                      title: 'Thao tác', key: 'actions', width: 120, align: 'center' as const, fixed: 'right' as const,
                      render: (_: unknown, d: Department) => (
                        <RowActions actions={[
                          { key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, onClick: () => setEditingDept(d) },
                          {
                            key: 'toggle', label: d.isActive ? 'Ngừng hoạt động' : 'Kích hoạt lại', icon: <Power size={16} />, danger: d.isActive,
                            onClick: () => d.isActive
                              ? confirmAction({ title: `Ngừng hoạt động ${d.name}?`, content: d.subjectCount > 0 ? `Tổ còn ${d.subjectCount} môn đang hoạt động nên hệ thống sẽ từ chối. Ngừng các môn đó trước.` : 'Tổ sẽ không còn xuất hiện trong các ô chọn.', okText: 'Ngừng hoạt động', danger: true, onOk: () => toggleDept.mutateAsync(d).catch(() => undefined) })
                              : toggleDept.mutate(d),
                          },
                        ]} />
                      ),
                    }] : []),
                  ]}
                />
              </Card>
            ),
          },
        ]}
      />

      <FormModal<DeptValues>
        open={editingDept !== null} title={editingDept === 'new' ? 'Thêm tổ chuyên môn' : 'Sửa tổ chuyên môn'}
        form={deptForm} onClose={() => setEditingDept(null)} onSubmit={saveDept}
        initialValues={editingDept && editingDept !== 'new' ? editingDept : undefined}
      >
        <Form.Item label="Tên tổ chuyên môn" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên tổ chuyên môn.' }]}>
          <Input maxLength={150} />
        </Form.Item>
        <Form.Item label="Mô tả" name="description"><Input.TextArea rows={2} maxLength={255} /></Form.Item>
      </FormModal>

      <FormModal<SubjectValues>
        open={editingSubject !== null} title={editingSubject === 'new' ? 'Thêm môn học' : 'Sửa môn học'}
        form={subjectForm} onClose={() => setEditingSubject(null)} onSubmit={saveSubject}
        initialValues={editingSubject && editingSubject !== 'new' ? editingSubject : undefined}
      >
        <Form.Item label="Tổ chuyên môn" name="departmentId" rules={[{ required: true, message: 'Chọn tổ chuyên môn của môn học.' }]}>
          <Select options={departments.data?.filter((d) => d.isActive).map((d) => ({ value: d.departmentId, label: d.name }))} />
        </Form.Item>
        <Form.Item label="Tên môn học" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên môn học.' }]}>
          <Input maxLength={150} />
        </Form.Item>
        <Form.Item
          label="Viết tắt môn" name="abbreviation"
          extra="Chỉ gồm chữ và số, không dấu cách, không dấu gạch. Ví dụ: TOAN, VAN, TA."
          rules={[
            { required: true, whitespace: true, message: 'Nhập viết tắt môn.' },
            { pattern: /^[A-Za-z0-9]+$/, message: 'Viết tắt chỉ gồm chữ và số, không dấu cách, không dấu gạch.' },
          ]}
        >
          <Input maxLength={10} />
        </Form.Item>
      </FormModal>
    </>
  );
}
