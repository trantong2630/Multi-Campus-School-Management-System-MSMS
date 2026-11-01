import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Select, Table } from 'antd';
import { ArrowLeft, ArrowRight, ArrowRightLeft, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { schApi } from '../api';
import { RolloverSuggestModal } from '../components/RolloverSuggestModal';
import { useCampuses, useYears } from '../hooks/useCatalogs';
import type { RolloverStudent, RosterItem, SchoolClass } from '../types';
import '../components/school.css';

const GRADES = [6, 7, 8, 9];

/** Gợi ý lớp đích: lớp của khối đích có tên giống tên lớp nguồn sau khi thay số khối (6A1 → 7A1). */
const guessTarget = (source: SchoolClass, options: SchoolClass[], grade: number) => {
  const suggested = source.name.replace(new RegExp(`(?<!\\d)${source.gradeLevel}(?!\\d)`), String(grade)).toLowerCase();
  return (options.find((c) => c.name.toLowerCase() === suggested) ?? options[0])?.classId;
};

/** Cột giữa: kết quả xếp lớp của từng học sinh so với khối của lớp cũ. */
const outcome = (s: RolloverStudent, newClass: SchoolClass | undefined, oldGrade: number) => {
  if (s.graduated) return <span className="rollover-result rollover-result--up">Tốt nghiệp</span>;
  if (!s.newClassId) return <span className="muted">Chưa xếp</span>;
  if (!newClass) return s.newClassName;
  return (
    <span className={newClass.gradeLevel > oldGrade ? 'rollover-result rollover-result--up' : 'rollover-result rollover-result--stay'}>
      {newClass.gradeLevel > oldGrade ? 'Lên lớp' : 'Lưu ban'} · {newClass.name}
    </span>
  );
};

interface PanelProps {
  title: string;
  options: SchoolClass[];
  classId: number | undefined;
  onClassChange: (id: number | undefined) => void;
  roster: RosterItem[];
  loading: boolean;
  onRemove: (enrollmentIds: number[]) => void;
  disabledHint?: string;
}

/** Cột trái / phải: chọn lớp năm mới, xem học sinh đã xếp vào lớp, gỡ học sinh ra khỏi lớp. */
function TargetPanel({ title, options, classId, onClassChange, roster, loading, onRemove, disabledHint }: PanelProps) {
  const [picked, setPicked] = useState<number[]>([]);
  return (
    <Card className="rollover-panel" title={title}>
      <div className="rollover-panel__bar">
        <Select
          placeholder="Chọn lớp" value={classId} disabled={options.length === 0} className="rollover-panel__select"
          onChange={(v) => { onClassChange(v); setPicked([]); }}
          options={options.map((c) => ({ value: c.classId, label: c.name }))}
          notFoundContent="Chưa có lớp"
        />
        <Button danger icon={<Trash2 size={14} />} disabled={picked.length === 0} onClick={() => { onRemove(picked); setPicked([]); }}>
          Gỡ{picked.length > 0 ? ` (${picked.length})` : ''}
        </Button>
      </div>
      {disabledHint && <p className="muted rollover-panel__hint">{disabledHint}</p>}
      <Table<RosterItem>
        className="data-table" size="small" rowKey="enrollmentId" loading={loading} dataSource={classId ? roster : []} pagination={false}
        scroll={{ y: 420 }}
        rowSelection={{ selectedRowKeys: picked, onChange: (k) => setPicked(k as number[]) }}
        locale={{ emptyText: classId ? 'Lớp chưa có học sinh.' : 'Chọn lớp để xem học sinh.' }}
        columns={[
          { title: 'Mã HS', dataIndex: 'personCode', width: 110, className: 'tabular' },
          { title: 'Họ tên', dataIndex: 'fullName' },
        ]}
      />
      <div className="rollover-panel__foot muted">{classId ? `${roster.length} học sinh` : ''}</div>
    </Card>
  );
}

/**
 * SCH-06 Kết chuyển năm học: chọn lớp năm cũ ở giữa, chọn lớp lên lớp / lưu ban ở hai bên rồi chuyển từng học sinh bằng mũi tên.
 * Người dùng tự quyết lên lớp hay lưu ban (hệ thống không tự xét); không sao chép thời khóa biểu, phân công hay trách nhiệm.
 */
export function RolloverPage() {
  const qc = useQueryClient();
  const years = useYears();
  const campuses = useCampuses();
  const [fromPick, setFromPick] = useState<number | undefined>();
  const [toPick, setToPick] = useState<number | undefined>();
  const [campusId, setCampusId] = useState<number | undefined>();
  const [grade, setGrade] = useState<number | undefined>();
  const [sourceId, setSourceId] = useState<number | undefined>();
  const [retainPick, setRetainPick] = useState<number | undefined>();
  const [promotePick, setPromotePick] = useState<number | undefined>();
  const [picked, setPicked] = useState<number[]>([]);
  const [suggestOpen, setSuggestOpen] = useState(false);
  /** Ba bảng chỉ hiện sau khi tạo lớp năm mới hoặc bấm "Chuyển học sinh". */
  const [started, setStarted] = useState(false);
  /** Lớp năm cũ vừa được tạo lớp mới: bảng giữa chỉ hiện các lớp này. null = hiện mọi lớp (bấm "Chuyển học sinh"). */
  const [scope, setScope] = useState<number[] | null>(null);

  const list = years.data ?? [];
  const fromYear = list.find((y) => y.academicYearId === fromPick) ?? list.find((y) => y.isCurrent) ?? list[0];
  const later = fromYear ? list.filter((y) => y.startDate > fromYear.startDate).sort((a, b) => a.startDate.localeCompare(b.startDate)) : [];
  const toYear = later.find((y) => y.academicYearId === toPick) ?? later[0];
  const ready = fromYear !== undefined && toYear !== undefined;

  const fromClasses = useQuery({
    queryKey: ['sch', 'classes', 'rollover-from', fromYear?.academicYearId, campusId],
    queryFn: () => schApi.classes.list({ yearId: fromYear!.academicYearId, campusId }),
    enabled: ready,
  });
  const toClasses = useQuery({
    queryKey: ['sch', 'classes', 'rollover-to', toYear?.academicYearId, campusId],
    queryFn: () => schApi.classes.list({ yearId: toYear!.academicYearId, campusId }),
    enabled: ready,
  });
  const overview = useQuery({
    queryKey: ['sch', 'rollover', fromYear?.academicYearId, toYear?.academicYearId, campusId],
    queryFn: () => schApi.rollover.overview({ fromYearId: fromYear!.academicYearId, toYearId: toYear!.academicYearId, campusId }),
    enabled: ready,
  });
  const withoutTarget = (overview.data ?? []).filter((r) => r.target === null);

  const sourceOptions = (fromClasses.data ?? []).filter((c) => c.isActive && (scope === null || scope.includes(c.classId)) && (grade === undefined || c.gradeLevel === grade));
  const source = sourceOptions.find((c) => c.classId === sourceId);
  const newYearClasses = (toClasses.data ?? []).filter((c) => c.isActive);
  const retainOptions = source ? newYearClasses.filter((c) => c.campusId === source.campusId && c.gradeLevel === source.gradeLevel) : [];
  const promoteOptions = source ? newYearClasses.filter((c) => c.campusId === source.campusId && c.gradeLevel === source.gradeLevel + 1) : [];
  const retainId = retainOptions.some((c) => c.classId === retainPick) ? retainPick : undefined;
  const promoteId = promoteOptions.some((c) => c.classId === promotePick) ? promotePick : (source ? guessTarget(source, promoteOptions, source.gradeLevel + 1) : undefined);

  const students = useQuery({
    queryKey: ['sch', 'rollover', 'students', sourceId, toYear?.academicYearId],
    queryFn: () => schApi.rollover.students(sourceId!, toYear!.academicYearId),
    enabled: sourceId !== undefined && toYear !== undefined && source !== undefined,
  });
  const retainRoster = useQuery({
    queryKey: ['sch', 'rollover', 'roster', retainId],
    queryFn: () => schApi.enrollments.roster(retainId!, toYear!.startDate),
    enabled: retainId !== undefined && toYear !== undefined,
  });
  const promoteRoster = useQuery({
    queryKey: ['sch', 'rollover', 'roster', promoteId],
    queryFn: () => schApi.enrollments.roster(promoteId!, toYear!.startDate),
    enabled: promoteId !== undefined && toYear !== undefined,
  });

  const classById = new Map(newYearClasses.map((c) => [c.classId, c]));
  const list2 = students.data ?? [];
  const selectable = list2.filter((s) => s.newClassId === null && !s.graduated);
  const graduates = list2.filter((s) => s.graduated);
  const isFinal = source !== undefined && source.gradeLevel >= 9;
  const unplaced = selectable.length;

  const refresh = () => { void qc.invalidateQueries({ queryKey: ['sch'] }); };
  const move = useMutation({
    mutationFn: (toClassId: number) => schApi.rollover.moveStudents({ fromClassId: sourceId!, toClassId, studentIds: picked }),
    onSuccess: (res, toClassId) => {
      refresh();
      setPicked([]);
      message.success(`Đã chuyển ${res.moved} học sinh sang lớp ${classById.get(toClassId)?.name}${res.skipped > 0 ? `, bỏ qua ${res.skipped} em đã có lớp` : ''}.`);
    },
    onError: (e) => showError(e),
  });
  const graduate = useMutation({
    mutationFn: () => schApi.rollover.graduate(sourceId!, picked),
    onSuccess: (n) => { refresh(); setPicked([]); message.success(`Đã cho ${n} học sinh tốt nghiệp.`); },
    onError: (e) => showError(e),
  });
  const askGraduate = () => confirmAction({
    title: `Cho ${picked.length} học sinh lớp ${source?.name} tốt nghiệp?`,
    content: (
      <>
        <p>Hồ sơ học sinh chuyển sang <b>Đã tốt nghiệp</b>, ghi danh kết thúc vào cuối năm học {fromYear?.name} và <b>tài khoản học sinh bị khóa</b>.</p>
        <p>Thao tác này không hoàn tác được từ màn kết chuyển.</p>
      </>
    ),
    okText: 'Cho tốt nghiệp',
    onOk: () => graduate.mutateAsync().catch(() => undefined),
  });
  const undo = useMutation({
    mutationFn: (ids: number[]) => schApi.rollover.undo(ids),
    onSuccess: (n) => { refresh(); message.success(`Đã gỡ ${n} học sinh khỏi lớp.`); },
    onError: (e) => showError(e),
  });
  const askUndo = (ids: number[]) => confirmAction({
    title: `Gỡ ${ids.length} học sinh khỏi lớp năm học ${toYear?.name}?`,
    content: <p>Học sinh sẽ quay về danh sách chưa xếp lớp ở giữa, dữ liệu năm học cũ giữ nguyên.</p>,
    okText: 'Gỡ',
    onOk: () => undo.mutateAsync(ids).catch(() => undefined),
  });

  const resetPicks = () => { setStarted(false); setSourceId(undefined); setPicked([]); setRetainPick(undefined); setPromotePick(undefined); };
  const noLaterYear = fromYear !== undefined && later.length === 0;

  return (
    <>
      <PageHeader
        title="Kết chuyển năm học"
        description="Chọn năm học, khối và lớp cũ ở giữa; chọn lớp lưu ban (trái) hoặc lớp được lên (phải) rồi bấm mũi tên để chuyển các học sinh đã tích. Hệ thống không tự xét lên lớp hay lưu ban; thời khóa biểu, phân công và trách nhiệm của năm cũ không được sao chép."
        extra={(
          <>
            <Button icon={<Plus size={16} />} disabled={withoutTarget.length === 0} onClick={() => setSuggestOpen(true)}>Tạo lớp năm mới</Button>
            <Button type="primary" icon={<ArrowRightLeft size={16} />} disabled={!ready} onClick={() => { setScope(null); resetPicks(); setStarted(true); }}>Chuyển học sinh</Button>
          </>
        )}
      />
      <FilterBar>
        <Select
          placeholder="Từ năm học" value={fromYear?.academicYearId}
          onChange={(v) => { setFromPick(v); setToPick(undefined); resetPicks(); }}
          options={list.map((y) => ({ value: y.academicYearId, label: `Từ năm học ${y.name}` }))}
        />
        <Select
          placeholder="Sang năm học" value={toYear?.academicYearId} disabled={later.length === 0}
          onChange={(v) => { setToPick(v); resetPicks(); }}
          options={later.map((y) => ({ value: y.academicYearId, label: `Sang năm học ${y.name}` }))}
        />
        <Select
          allowClear placeholder="Tất cả phân hiệu" value={campusId} onChange={(v) => { setCampusId(v); resetPicks(); }}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
      </FilterBar>

      {noLaterYear && (
        <InlineAlert
          type="info"
          title={`Chưa có năm học nào sau năm học ${fromYear.name}. Tạo năm học mới ở Thiết lập → Năm học & Học kỳ trước.`}
        />
      )}

      {ready && started && (
        <div className="rollover-board">
          <TargetPanel
            title={`Lưu ban · năm học ${toYear.name}`} options={retainOptions} classId={retainId} onClassChange={setRetainPick}
            roster={retainRoster.data?.students ?? []} loading={retainRoster.isFetching} onRemove={askUndo}
            disabledHint={source && retainOptions.length === 0 ? `Chưa có lớp khối ${source.gradeLevel} ở năm học ${toYear.name}. Tạo lớp ở màn Lớp & Ghi danh trước.` : undefined}
          />
          <div className="rollover-arrows">
            <Button
              aria-label="Chuyển sang lớp lưu ban" icon={<ArrowLeft size={16} />}
              disabled={picked.length === 0 || retainId === undefined} loading={move.isPending}
              onClick={() => move.mutate(retainId!)}
            />
          </div>

          <Card className="rollover-panel rollover-panel--source" title={`Năm học ${fromYear.name}`}>
            <div className="rollover-panel__bar">
              <Select
                allowClear placeholder="Khối" value={grade} className="rollover-grade"
                onChange={(v) => { setGrade(v); setSourceId(undefined); setPicked([]); setRetainPick(undefined); setPromotePick(undefined); }}
                options={GRADES.map((g) => ({ value: g, label: `Khối ${g}` }))}
              />
              <Select
                placeholder="Chọn lớp" value={sourceId} className="rollover-panel__select" notFoundContent="Chưa có lớp"
                onChange={(v) => { setSourceId(v); setPicked([]); setRetainPick(undefined); setPromotePick(undefined); }}
                options={sourceOptions.map((c) => ({ value: c.classId, label: campusId ? c.name : `${c.name} · ${c.campusName}` }))}
              />
            </div>
            <Table<RolloverStudent>
              className="data-table" size="small" rowKey="studentId" loading={students.isFetching} dataSource={sourceId ? list2 : []} pagination={false}
              scroll={{ y: 420 }}
              rowSelection={{
                selectedRowKeys: picked,
                onChange: (k) => setPicked(k as number[]),
                getCheckboxProps: (s) => ({ disabled: s.newClassId !== null || s.graduated }),
              }}
              locale={{ emptyText: sourceId ? 'Lớp không có học sinh còn học ở cuối năm.' : 'Chọn khối và lớp để xem học sinh.' }}
              columns={[
                { title: 'STT', key: 'i', width: 56, className: 'tabular', render: (_: unknown, __: RolloverStudent, i: number) => i + 1 },
                { title: 'Mã HS', dataIndex: 'personCode', width: 110, className: 'tabular' },
                { title: 'Họ tên', dataIndex: 'fullName' },
                {
                  title: 'Kết quả', key: 'result', width: 170,
                  render: (_: unknown, s: RolloverStudent) => outcome(s, s.newClassId ? classById.get(s.newClassId) : undefined, source?.gradeLevel ?? 0),
                },
              ]}
            />
            <div className="rollover-panel__foot muted">
              {sourceId ? `${list2.length} học sinh · ${unplaced} chưa xếp lớp` : ''}
              {selectable.length > 0 && (
                <Button type="link" size="small" onClick={() => setPicked(picked.length === selectable.length ? [] : selectable.map((s) => s.studentId))}>
                  {picked.length === selectable.length ? 'Bỏ chọn tất cả' : 'Chọn tất cả chưa xếp'}
                </Button>
              )}
            </div>
          </Card>

          <div className="rollover-arrows">
            <Button
              type="primary" aria-label={isFinal ? 'Cho tốt nghiệp' : 'Chuyển sang lớp được lên'} icon={<ArrowRight size={16} />}
              disabled={picked.length === 0 || (!isFinal && promoteId === undefined)} loading={move.isPending || graduate.isPending}
              onClick={() => (isFinal ? askGraduate() : move.mutate(promoteId!))}
            />
          </div>
          {isFinal ? (
            <Card className="rollover-panel" title="Tốt nghiệp">
              <p className="muted rollover-panel__hint">Khối 9 không có khối 10: học sinh được chọn rồi bấm → sẽ tốt nghiệp.</p>
              <Table<RolloverStudent>
                className="data-table" size="small" rowKey="studentId" dataSource={graduates} pagination={false} scroll={{ y: 420 }}
                locale={{ emptyText: 'Chưa có học sinh tốt nghiệp.' }}
                columns={[
                  { title: 'Mã HS', dataIndex: 'personCode', width: 110, className: 'tabular' },
                  { title: 'Họ tên', dataIndex: 'fullName' },
                ]}
              />
              <div className="rollover-panel__foot muted">{`${graduates.length} học sinh`}</div>
            </Card>
          ) : (
          <TargetPanel
            title={`Được lên lớp · năm học ${toYear.name}`} options={promoteOptions} classId={promoteId} onClassChange={setPromotePick}
            roster={promoteRoster.data?.students ?? []} loading={promoteRoster.isFetching} onRemove={askUndo}
            disabledHint={source && promoteOptions.length === 0 ? `Chưa có lớp khối ${source.gradeLevel + 1} ở năm học ${toYear.name}. Bấm Tạo lớp năm mới.` : undefined}
          />
          )}
        </div>
      )}

      {suggestOpen && fromYear && toYear && (
        <RolloverSuggestModal key={`${fromYear.academicYearId}-${toYear.academicYearId}`} fromYear={fromYear} toYear={toYear} rows={withoutTarget} onClose={() => setSuggestOpen(false)} onCreated={(ids) => { resetPicks(); setScope(ids); setStarted(true); }} />
      )}
    </>
  );
}
