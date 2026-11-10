import { useQuery } from '@tanstack/react-query';
import { Input, Select } from 'antd';
import { useMemo, useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { scdApi } from '../api';
import { subjectColorStyle } from '../subjectColor';
import type { ResponsibilityItem, ResponsibilityTeacher } from '../types';
import { useScdCampus } from '../useScdCampus';
import '../components/scd.css';

const fold = (v: string) => v.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase();

/** SCD-04: phụ trách chuyên môn tính từ thời khóa biểu đã phát hành, chỉ đọc. Muốn đổi thì sửa thời khóa biểu rồi Phát hành lại (BR-14). */
export function ResponsibilityPage() {
  const { campuses, effective: effectiveCampus, select: campusSelect, head: campusHead } = useScdCampus();
  const [subjectId, setSubjectId] = useState<number | undefined>();
  const [search, setSearch] = useState('');

  const subjects = useQuery({
    queryKey: ['scd', 'draft-options', effectiveCampus],
    queryFn: () => scdApi.draftOptions(effectiveCampus!),
    enabled: effectiveCampus !== undefined,
  });
  const list = useQuery({
    queryKey: ['scd', 'responsibilities', effectiveCampus, subjectId],
    queryFn: () => scdApi.responsibilities(effectiveCampus, subjectId),
    enabled: campuses.isSuccess,
  });

  /** Tìm theo tên giáo viên, môn hoặc lớp đang phụ trách. */
  const teachers = useMemo(() => {
    const key = fold(search.trim());
    if (!key) return list.data?.teachers;
    return list.data?.teachers.filter((t) => fold(`${t.teacherName} ${t.items.map((i) => `${i.subjectName} ${i.className}`).join(' ')}`).includes(key));
  }, [list.data, search]);

  return (
    <>
      <PageHeader
        title="Phụ trách chuyên môn"
      />
      {campusHead}
      {campuses.isSuccess && campuses.data.length === 0 && (
        <InlineAlert type="warning" title="Chưa có phân hiệu nào để xem" description="Hiệu phó cần được gán phụ trách phân hiệu ở Hồ sơ → Trách nhiệm." />
      )}
      <FilterBar>
        {campusSelect}
        <Input allowClear placeholder="Tìm giáo viên, môn hoặc lớp" value={search} onChange={(e) => setSearch(e.target.value)} style={{ width: 260 }} />
        <Select
          allowClear showSearch optionFilterProp="label" placeholder="Môn" value={subjectId} onChange={setSubjectId}
          style={{ minWidth: 180 }}
          options={subjects.data?.subjects.map((s) => ({ value: s.subjectId, label: s.name }))}
        />
      </FilterBar>
      <DataTable<ResponsibilityTeacher>
        rowKey="teacherId" loading={list.isLoading} dataSource={teachers} pagination={false}
        emptyText="Chưa có thời khóa biểu nào được phát hành, hoặc không có giáo viên khớp bộ lọc."
        columns={[
          { title: 'Giáo viên', dataIndex: 'teacherName', width: 220 },
          { title: 'Phụ trách', key: 'items', render: (_: unknown, t) => <ItemBoxes items={t.items} /> },
          { title: 'Tổng tiết/tuần', dataIndex: 'totalPeriods', width: 140, align: 'right', className: 'tabular' },
        ]}
      />
    </>
  );
}

/** Mỗi mục một hộp "Môn Lớp — số tiết", tự xuống dòng. */
function ItemBoxes({ items }: { items: ResponsibilityItem[] }) {
  return (
    <div className="resp-boxes">
      {items.map((i) => (
        <span key={`${i.subjectId}-${i.classId}`} className="resp-box" style={subjectColorStyle(i.subjectId)}>
          <strong>{i.subjectName}</strong> {i.className} — {i.periodsPerWeek} tiết
        </span>
      ))}
    </div>
  );
}
