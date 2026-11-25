import { useQuery } from '@tanstack/react-query';
import { Card, Select, Switch } from 'antd';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { FilterBar } from '@/shared/components/FilterBar';
import { PageHeader } from '@/shared/components/PageHeader';
import { formatNumber as fmt } from '@/shared/lib/format';
import { schApi } from '../api';
import { ClassRosterPanel } from '../components/ClassRosterPanel';
import { useCampuses, useCurrentYear, useYears } from '../hooks/useCatalogs';
import '../components/school.css';
import type { SchoolClass } from '../types';

/**
 * SCH-05: màn Xếp lớp. Cột trái là danh sách lớp của năm học và phân hiệu đang lọc, nhóm theo khối; bấm một lớp thì cột phải hiện
 * học sinh của lớp đó để xếp lớp, chuyển lớp, cho rời lớp. Lớp ngừng hoạt động ẩn sau công tắc (xem lại được, không xếp thêm học sinh).
 * Chỉ School Admin sửa; Hiệu trưởng, Hiệu phó chỉ xem.
 */
export function ClassPlacementPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin';
  const years = useYears();
  const { current } = useCurrentYear();
  const campuses = useCampuses();
  const [yearId, setYearId] = useState<number | undefined>();
  const [campusId, setCampusId] = useState<number | undefined>();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [classId, setClassId] = useState<number | undefined>();

  const effectiveYear = yearId ?? current?.academicYearId;
  const classes = useQuery({
    queryKey: ['sch', 'classes', effectiveYear, campusId, includeInactive],
    queryFn: () => schApi.classes.list({ yearId: effectiveYear, campusId, includeInactive }),
    enabled: effectiveYear !== undefined,
  });
  const cls = classes.data?.find((c) => c.classId === classId);

  // Hiệu phó chỉ nhận lớp của phân hiệu mình phụ trách nên ô lọc chỉ có các phân hiệu đó.
  const ledCampusIds = user?.accountType === 'VicePrincipal' ? user.scope?.ledCampusIds ?? [] : null;
  const campusOptions = (campuses.data ?? [])
    .filter((c) => !ledCampusIds || ledCampusIds.includes(c.campusId))
    .map((c) => ({ value: c.campusId, label: c.name }));

  const byGrade = new Map<number, SchoolClass[]>();
  for (const c of classes.data ?? []) byGrade.set(c.gradeLevel, [...(byGrade.get(c.gradeLevel) ?? []), c]);
  const groups = [...byGrade.entries()].sort(([a], [b]) => a - b);

  return (
    <>
      <PageHeader title="Xếp lớp" />
      <FilterBar>
        <Select
          placeholder="Năm học" value={effectiveYear}
          onChange={(v) => { setYearId(v); setClassId(undefined); }}
          options={years.data?.map((y) => ({ value: y.academicYearId, label: `Năm học ${y.name}` }))}
        />
        {/* Hiệu phó phụ trách một phân hiệu thì không có gì để lọc. */}
        {(!ledCampusIds || ledCampusIds.length > 1) && (
          <Select
            allowClear placeholder="Tất cả phân hiệu" value={campusId}
            onChange={(v) => { setCampusId(v); setClassId(undefined); }} options={campusOptions}
          />
        )}
        <label className="inline-switch"><Switch checked={includeInactive} onChange={setIncludeInactive} /> Hiện cả lớp ngừng hoạt động</label>
      </FilterBar>
      <div className="placement">
        <Card className="placement-side" loading={classes.isLoading}>
          {groups.length === 0 && <p className="muted">Chưa có lớp nào trong phạm vi đã chọn.</p>}
          {groups.map(([grade, items]) => (
            <section key={grade} className="placement-group">
              <h3 className="placement-group__title">
                <span>Khối {grade}</span>
                <span className="tabular">{fmt(items.reduce((n, c) => n + c.studentCount, 0))} học sinh</span>
              </h3>
              {items.map((c) => (
                <button
                  key={c.classId} type="button" aria-pressed={c.classId === classId}
                  className={`placement-item${c.classId === classId ? ' is-active' : ''}${c.isActive ? '' : ' is-muted'}`}
                  onClick={() => setClassId(c.classId)}
                >
                  <span>
                    <span className="placement-item__name">{c.name}</span>
                    <span className="placement-item__sub">
                      {c.homeroomName ?? 'Chưa có chủ nhiệm'}{c.isActive ? '' : ' · Ngừng hoạt động'}
                      {campusId === undefined && ` · ${c.campusName}`}
                    </span>
                  </span>
                  <span className="placement-item__count">{fmt(c.studentCount)}</span>
                </button>
              ))}
            </section>
          ))}
        </Card>
        <Card className="table-card">
          {cls ? (
            <>
              <h3 className="placement-title">Lớp {cls.name} · {cls.campusName} · Năm học {cls.academicYearName}</h3>
              <ClassRosterPanel key={cls.classId} cls={cls} canWrite={canWrite} />
            </>
          ) : (
            <div className="empty-state"><p className="empty-state__title">Chọn một lớp ở bên trái để xem danh sách học sinh và xếp lớp.</p></div>
          )}
        </Card>
      </div>
    </>
  );
}
