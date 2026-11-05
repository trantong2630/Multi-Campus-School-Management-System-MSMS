import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, Select, Switch } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import { Pencil, Plus, Power, Upload, Users } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { StudentImportModal } from '@/features/ppl/components/StudentImportModal';
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
import { ClassRosterDrawer } from '../components/ClassRosterDrawer';
import { useCampuses, useCurrentYear, useYears } from '../hooks/useCatalogs';
import '../components/school.css';
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
  const [rosterOf, setRosterOf] = useState<SchoolClass | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [form] = Form.useForm<FormValues>();

  const effectiveYear = yearId ?? current?.academicYearId;
  const classes = useQuery({
    queryKey: ['sch', 'classes', effectiveYear, campusId, includeInactive],
    queryFn: () => schApi.classes.list({ yearId: effectiveYear, campusId, includeInactive }),
    enabled: effectiveYear !== undefined,
  });


  const navigate = useNavigate();
  // Hiệu phó chỉ đọc lớp của phân hiệu đang phụ trách (API đã lọc), nên ô chọn cũng chỉ còn các phân hiệu đó.
  const campusOptions = (campuses.data ?? [])
    .filter((c) => user?.accountType !== 'VicePrincipal' || user.scope?.ledCampusIds.includes(c.campusId))
    .map((c) => ({ value: c.campusId, label: c.name }));

  // Số liệu theo đúng bộ lọc đang chọn, chỉ tính lớp đang hoạt động.
  const active = classes.data?.filter((c) => c.isActive) ?? [];
  const studentCount = active.reduce((n, c) => n + c.studentCount, 0);
  const withoutHomeroom = active.filter((c) => !c.homeroomName).length;
  const grades = [6, 7, 8, 9].map((g) => {
    const of = active.filter((c) => c.gradeLevel === g);
    return { grade: g, classes: of.length, students: of.reduce((n, c) => n + c.studentCount, 0) };
  }).filter((g) => g.classes > 0);
  const loaded = !!classes.data;

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
        description={canWrite
          ? 'Tên lớp duy nhất trong phạm vi (phân hiệu, năm học). Ghi danh học sinh làm cùng danh bạ học sinh.'
          : 'Lớp, sĩ số và chủ nhiệm theo năm học và phân hiệu đã chọn. Bấm một lớp để xem danh sách học sinh.'}
        extra={canWrite && (
          <>
            <Button icon={<Upload size={16} />} onClick={() => setImportOpen(true)}>Import học sinh</Button>
            <Button type="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>Thêm lớp</Button>
          </>
        )}
      />
      <FilterBar>
        <Select
          placeholder="Năm học" value={effectiveYear} onChange={setYearId}
          options={years.data?.map((y) => ({ value: y.academicYearId, label: `Năm học ${y.name}` }))}
        />
        <Select
          allowClear placeholder={user?.accountType === 'VicePrincipal' ? 'Tất cả phân hiệu phụ trách' : 'Tất cả phân hiệu'}
          value={campusId} onChange={setCampusId} options={campusOptions}
        />
        <label className="inline-switch"><Switch checked={includeInactive} onChange={setIncludeInactive} /> Hiện cả lớp ngừng hoạt động</label>
      </FilterBar>
      <StatStrip>
        <StatCard label="Lớp đang hoạt động" value={fmt(loaded ? active.length : undefined)} />
        <StatCard label="Học sinh đang học" value={fmt(loaded ? studentCount : undefined)} />
        <StatCard label="Học sinh theo khối" value={grades.length ? null : '—'}>
          {grades.length > 0 && (
            <ul className="grade-bars">
              {grades.map((g) => (
                <li key={g.grade} title={`Khối ${g.grade}: ${g.students} học sinh, ${g.classes} lớp`}>
                  <b className="tabular">{fmt(g.students)}</b>
                  <i style={{ height: `${Math.max(8, (g.students / Math.max(1, ...grades.map((x) => x.students))) * 46)}px` }} />
                  <span>Khối {g.grade}</span>
                </li>
              ))}
            </ul>
          )}
        </StatCard>
        <StatCard label="Lớp chưa có chủ nhiệm" value={fmt(loaded ? withoutHomeroom : undefined)}
          attention={withoutHomeroom > 0}
          onClick={withoutHomeroom > 0 && user && isPathAllowed('/ppl/responsibilities', user) ? () => navigate('/ppl/responsibilities') : undefined} />
      </StatStrip>
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
            {
              title: 'Thao tác', key: 'actions', width: 140, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, c: SchoolClass) => (
                <RowActions actions={[
                  { key: 'roster', label: 'Danh sách học sinh', icon: <Users size={16} />, onClick: () => setRosterOf(c) },
                  { key: 'edit', label: 'Sửa', icon: <Pencil size={16} />, hidden: !canWrite, onClick: () => setEditing(c) },
                  {
                    key: 'toggle', label: c.isActive ? 'Ngừng hoạt động' : 'Kích hoạt lại', icon: <Power size={16} />, danger: c.isActive, hidden: !canWrite,
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
            },
          ]}
        />
      </Card>

      <StudentImportModal open={importOpen} onClose={() => setImportOpen(false)} yearId={effectiveYear} campusId={campusId} />

      <ClassRosterDrawer key={rosterOf?.classId} cls={rosterOf} canWrite={canWrite} onClose={() => setRosterOf(null)} />

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
