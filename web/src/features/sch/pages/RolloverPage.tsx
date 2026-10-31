import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Select } from 'antd';
import { ArrowRightLeft, Plus } from 'lucide-react';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate } from '@/shared/lib/format';
import { schApi } from '../api';
import { RolloverSuggestModal } from '../components/RolloverSuggestModal';
import { useCampuses, useYears } from '../hooks/useCatalogs';
import type { RolloverRow } from '../types';

/** Trạng thái chuyển học sinh của một lớp: lớp chưa tạo, chưa chuyển, chuyển một phần hoặc đã chuyển hết. */
const moveStatus = (r: RolloverRow) => {
  if (!r.target) return <span className="muted">—</span>;
  if (r.studentCount === 0) return <span className="muted">Không có học sinh</span>;
  if (r.alreadyEnrolled === 0) return 'Chưa chuyển';
  if (r.alreadyEnrolled >= r.studentCount) return `Đã chuyển hết (${r.alreadyEnrolled})`;
  return `Đã chuyển ${r.alreadyEnrolled}/${r.studentCount}`;
};

/** Còn học sinh chưa có ghi danh năm mới thì mới chọn để chuyển được. */
const movable = (r: RolloverRow) => r.target !== null && r.target.isActive && r.studentCount - r.alreadyEnrolled > 0;

/**
 * SCH-06 Kết chuyển năm học: bước 1 tạo lớp năm mới từ lớp năm cũ, bước 2 chuyển nguyên học sinh của các lớp đã chọn (BR-09).
 * Không tự xét lên lớp hay lưu ban, không sao chép thời khóa biểu, phân công hay trách nhiệm.
 */
export function RolloverPage() {
  const qc = useQueryClient();
  const years = useYears();
  const campuses = useCampuses();
  const [fromPick, setFromPick] = useState<number | undefined>();
  const [toPick, setToPick] = useState<number | undefined>();
  const [campusId, setCampusId] = useState<number | undefined>();
  const [selected, setSelected] = useState<number[]>([]);
  const [suggestOpen, setSuggestOpen] = useState(false);

  const list = years.data ?? [];
  const fromYear = list.find((y) => y.academicYearId === fromPick) ?? list.find((y) => y.isCurrent) ?? list[0];
  const later = fromYear ? list.filter((y) => y.startDate > fromYear.startDate).sort((a, b) => a.startDate.localeCompare(b.startDate)) : [];
  const toYear = later.find((y) => y.academicYearId === toPick) ?? later[0];

  const overview = useQuery({
    queryKey: ['sch', 'rollover', fromYear?.academicYearId, toYear?.academicYearId, campusId],
    queryFn: () => schApi.rollover.overview({ fromYearId: fromYear!.academicYearId, toYearId: toYear!.academicYearId, campusId }),
    enabled: fromYear !== undefined && toYear !== undefined,
  });
  const rows = overview.data ?? [];
  const withoutTarget = rows.filter((r) => r.target === null);
  const chosen = rows.filter((r) => selected.includes(r.sourceClassId) && movable(r));

  const move = useMutation({
    mutationFn: () => schApi.rollover.move({ pairs: chosen.map((r) => ({ sourceClassId: r.sourceClassId, targetClassId: r.target!.classId })) }),
    onSuccess: (res) => {
      void qc.invalidateQueries({ queryKey: ['sch'] });
      setSelected([]);
      message.success(`Đã chuyển ${res.moved} học sinh sang năm học ${toYear?.name}${res.skipped > 0 ? `, bỏ qua ${res.skipped} em đã có ghi danh` : ''}.`);
    },
    onError: (e) => showError(e),
  });

  const askMove = () => {
    if (!toYear) return;
    const students = chosen.reduce((sum, r) => sum + r.studentCount - r.alreadyEnrolled, 0);
    confirmAction({
      title: `Chuyển học sinh sang năm học ${toYear.name}?`,
      content: (
        <>
          <p>
            Sẽ chuyển <b>{students}</b> học sinh của <b>{chosen.length}</b> lớp sang lớp tương ứng của năm học {toYear.name},
            ghi danh có hiệu lực từ <b>{formatDate(toYear.startDate)}</b>.
          </p>
          <p>Học sinh đã có ghi danh năm học mới được bỏ qua. Dữ liệu năm học cũ giữ nguyên. Hệ thống không tự xét lên lớp hay lưu ban.</p>
        </>
      ),
      okText: 'Chuyển học sinh',
      onOk: () => move.mutateAsync().catch(() => undefined),
    });
  };

  const noLaterYear = fromYear !== undefined && later.length === 0;

  return (
    <>
      <PageHeader
        title="Kết chuyển năm học"
        description="Bước 1: tạo lớp cho năm học mới từ lớp năm cũ. Bước 2: chọn các lớp rồi chuyển học sinh sang. Hệ thống không tự xét lên lớp hay lưu ban; thời khóa biểu, phân công và trách nhiệm của năm cũ không được sao chép."
        extra={(
          <>
            <Button icon={<Plus size={16} />} disabled={withoutTarget.length === 0} onClick={() => setSuggestOpen(true)}>Tạo lớp năm mới</Button>
            <Button type="primary" icon={<ArrowRightLeft size={16} />} disabled={chosen.length === 0} onClick={askMove}>
              {chosen.length > 0 ? `Chuyển học sinh (${chosen.length} lớp)` : 'Chuyển học sinh'}
            </Button>
          </>
        )}
      />
      <FilterBar>
        <Select
          placeholder="Từ năm học" value={fromYear?.academicYearId}
          onChange={(v) => { setFromPick(v); setToPick(undefined); setSelected([]); }}
          options={list.map((y) => ({ value: y.academicYearId, label: `Từ năm học ${y.name}` }))}
        />
        <Select
          placeholder="Sang năm học" value={toYear?.academicYearId} disabled={later.length === 0}
          onChange={(v) => { setToPick(v); setSelected([]); }}
          options={later.map((y) => ({ value: y.academicYearId, label: `Sang năm học ${y.name}` }))}
        />
        <Select
          allowClear placeholder="Tất cả phân hiệu" value={campusId} onChange={(v) => { setCampusId(v); setSelected([]); }}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
      </FilterBar>

      {noLaterYear && (
        <InlineAlert
          type="info"
          title={`Chưa có năm học nào sau năm học ${fromYear.name}. Tạo năm học mới ở Thiết lập → Năm học & Học kỳ trước.`}
        />
      )}

      {toYear && (
        <Card className="table-card">
          <DataTable<RolloverRow>
            rowKey="sourceClassId" loading={overview.isLoading || years.isLoading} dataSource={rows}
            emptyText={`Năm học ${fromYear?.name} chưa có lớp đang hoạt động nào trong phạm vi đã chọn.`}
            rowSelection={{
              selectedRowKeys: selected,
              onChange: (keys) => setSelected(keys as number[]),
              getCheckboxProps: (r) => ({ disabled: !movable(r) }),
            }}
            columns={[
              { title: 'Lớp năm cũ', dataIndex: 'sourceName', width: 130, render: (v: string) => <strong>{v}</strong> },
              { title: 'Phân hiệu', dataIndex: 'campusName' },
              { title: 'Khối', dataIndex: 'gradeLevel', width: 80, className: 'tabular' },
              { title: 'Sĩ số cuối năm', dataIndex: 'studentCount', width: 130, align: 'right', className: 'tabular' },
              {
                title: `Lớp năm học ${toYear.name}`, key: 'target', width: 190,
                render: (_: unknown, r: RolloverRow) => r.target
                  ? <><strong>{r.target.name}</strong> <span className="muted">· Khối {r.target.gradeLevel}</span></>
                  : <span className="muted">Chưa tạo</span>,
              },
              { title: 'Học sinh đã chuyển', key: 'moved', width: 190, className: 'tabular', render: (_: unknown, r: RolloverRow) => moveStatus(r) },
            ]}
          />
        </Card>
      )}

      {suggestOpen && fromYear && toYear && (
        <RolloverSuggestModal key={`${fromYear.academicYearId}-${toYear.academicYearId}`} fromYear={fromYear} toYear={toYear} rows={withoutTarget} onClose={() => setSuggestOpen(false)} />
      )}
    </>
  );
}
