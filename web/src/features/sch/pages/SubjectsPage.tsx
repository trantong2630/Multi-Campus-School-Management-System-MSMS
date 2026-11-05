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
import { StatCard } from '@/shared/components/StatCard';
import { StatStrip } from '@/shared/components/StatStrip';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatNumber as fmt } from '@/shared/lib/format';
import { schApi } from '../api';
import { useCatalogStats, useDepartments } from '../hooks/useCatalogs';
import '../components/school.css';
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
  const stats = useCatalogStats();
  const deptStat = new Map((stats.data?.departments ?? []).map((d) => [d.departmentId, d]));
  const subjectStat = new Map((stats.data?.subjects ?? []).map((s) => [s.subjectId, s]));
  const activeDepts = departments.data?.filter((d) => d.isActive) ?? [];
  const activeSubjects = (departments.data ?? []).reduce((n, d) => n + (d.isActive ? d.subjectCount : 0), 0);
  const deptsWithoutHead = activeDepts.filter((d) => !deptStat.get(d.departmentId)?.headName).length;
  const teacherCount = activeDepts.reduce((n, d) => n + (deptStat.get(d.departmentId)?.teacherCount ?? 0), 0);
  const taughtSubjects = stats.data?.subjects.filter((s) => s.teacherCount > 0).length;
  const loaded = !!departments.data && !!stats.data;

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
        description={canWrite
          ? 'Viết tắt môn chỉ gồm chữ và số, duy nhất toàn trường — dùng trong ô MÔN-GV của file thời khóa biểu.'
          : 'Tổ chuyên môn, môn học và số giáo viên toàn trường. Phân công dạy theo thời khóa biểu đã phát hành.'}
        extra={addButton}
      />
      <StatStrip>
        <StatCard label="Tổ chuyên môn" value={fmt(loaded ? activeDepts.length : undefined)}
          attention={deptsWithoutHead > 0}
          hint={loaded && deptsWithoutHead ? `${deptsWithoutHead} tổ chưa có tổ trưởng` : undefined} />
        <StatCard label="Môn học đang dùng" value={fmt(loaded ? activeSubjects : undefined)} />
        <StatCard label="Giáo viên thuộc tổ" value={fmt(loaded ? teacherCount : undefined)} />
        <StatCard label="Môn đang có giáo viên dạy" value={fmt(taughtSubjects)}
          meter={taughtSubjects !== undefined && activeSubjects > 0 ? (taughtSubjects / activeSubjects) * 100 : null}
          hint={taughtSubjects !== undefined && activeSubjects > 0 ? `trên ${fmt(activeSubjects)} môn` : undefined} />
      </StatStrip>
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
                      {
                        title: 'Giáo viên dạy', key: 'teachers', width: 130, align: 'right', className: 'tabular',
                        render: (_: unknown, s: Subject) => fmt(subjectStat.get(s.subjectId)?.teacherCount ?? (stats.data ? 0 : undefined)),
                      },
                      {
                        title: 'Lớp đang học', key: 'classes', width: 130, align: 'right', className: 'tabular',
                        render: (_: unknown, s: Subject) => fmt(subjectStat.get(s.subjectId)?.classCount ?? (stats.data ? 0 : undefined)),
                      },
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
                    { title: 'Tổ chuyên môn', dataIndex: 'name', render: (v: string) => <b>{v}</b> },
                    {
                      title: 'Tổ trưởng', key: 'head',
                      render: (_: unknown, d: Department) => {
                        const head = deptStat.get(d.departmentId)?.headName;
                        if (head) return head;
                        return stats.data && d.isActive ? <span className="text-warn">Chưa có tổ trưởng</span> : <span className="muted">—</span>;
                      },
                    },
                    { title: 'Mô tả', dataIndex: 'description', render: (v: string | null) => v ?? <span className="muted">—</span> },
                    { title: 'Số môn', dataIndex: 'subjectCount', width: 100, align: 'right', className: 'tabular' },
                    {
                      title: 'Giáo viên', key: 'teachers', width: 110, align: 'right', className: 'tabular',
                      render: (_: unknown, d: Department) => fmt(deptStat.get(d.departmentId)?.teacherCount ?? (stats.data ? 0 : undefined)),
                    },
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
        variant="drawer" width={640}
        open={editingDept !== null} title={editingDept === 'new' ? 'Thêm tổ chuyên môn' : 'Sửa tổ chuyên môn'}
        form={deptForm} onClose={() => setEditingDept(null)} onSubmit={saveDept}
        initialValues={editingDept && editingDept !== 'new' ? editingDept : undefined}
      >
        <div className="form-grid">
          <Form.Item label="Tên tổ chuyên môn" name="name" style={{ gridColumn: '1 / -1' }} rules={[{ required: true, whitespace: true, message: 'Nhập tên tổ chuyên môn.' }]}>
            <Input maxLength={150} />
          </Form.Item>
          <Form.Item label="Mô tả" name="description" style={{ gridColumn: '1 / -1' }}><Input.TextArea rows={2} maxLength={255} /></Form.Item>
        </div>
      </FormModal>

      <FormModal<SubjectValues>
        variant="drawer" width={640}
        open={editingSubject !== null} title={editingSubject === 'new' ? 'Thêm môn học' : 'Sửa môn học'}
        form={subjectForm} onClose={() => setEditingSubject(null)} onSubmit={saveSubject}
        initialValues={editingSubject && editingSubject !== 'new' ? editingSubject : undefined}
      >
        <div className="form-grid">
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
        </div>
      </FormModal>
    </>
  );
}
