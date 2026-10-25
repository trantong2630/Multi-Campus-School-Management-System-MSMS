import { useQuery } from '@tanstack/react-query';
import { Select } from 'antd';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { scdApi } from '../api';
import { subjectColorStyle } from '../subjectColor';
import type { ResponsibilityItem, ResponsibilityTeacher } from '../types';
import '../components/scd.css';

/** SCD-04: phụ trách chuyên môn tính từ thời khóa biểu đã phát hành, chỉ đọc. Muốn đổi thì sửa thời khóa biểu rồi Phát hành lại (BR-14). */
export function ResponsibilityPage() {
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number | undefined>();
  const [subjectId, setSubjectId] = useState<number | undefined>();
  const effectiveCampus = campusId ?? campuses.data?.[0]?.campusId;

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

  return (
    <>
      <PageHeader
        title="Phụ trách chuyên môn"
        description={list.data?.semesterName
          ? `Tính từ thời khóa biểu đã phát hành của ${list.data.semesterName}. Muốn đổi, sửa thời khóa biểu rồi Phát hành lại.`
          : 'Tính từ thời khóa biểu đã phát hành. Muốn đổi, sửa thời khóa biểu rồi Phát hành lại.'}
      />
      {campuses.isSuccess && campuses.data.length === 0 && (
        <InlineAlert type="warning" title="Chưa có phân hiệu nào để xem" description="Hiệu phó cần được gán phụ trách phân hiệu ở Hồ sơ → Trách nhiệm." />
      )}
      <FilterBar>
        <Select
          placeholder="Phân hiệu" value={effectiveCampus} onChange={setCampusId}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
        <Select
          allowClear showSearch optionFilterProp="label" placeholder="Môn" value={subjectId} onChange={setSubjectId}
          style={{ minWidth: 180 }}
          options={subjects.data?.subjects.map((s) => ({ value: s.subjectId, label: s.name }))}
        />
      </FilterBar>
      <DataTable<ResponsibilityTeacher>
        rowKey="teacherId" loading={list.isLoading} dataSource={list.data?.teachers} pagination={false}
        emptyText="Chưa có thời khóa biểu nào được phát hành, hoặc không có giáo viên khớp bộ lọc."
        columns={[
          { title: 'Giáo viên', dataIndex: 'teacherName', width: 220 },
          { title: 'Phụ trách', key: 'items', render: (_: unknown, t) => <ItemBoxes items={t.items} /> },
          { title: 'Tổng tiết', dataIndex: 'totalPeriods', width: 110, align: 'right', className: 'tabular' },
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
