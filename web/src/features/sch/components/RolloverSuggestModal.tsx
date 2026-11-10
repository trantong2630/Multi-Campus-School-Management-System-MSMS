import { useQueryClient } from '@tanstack/react-query';
import { Button, Checkbox, Input, Modal, Select } from 'antd';
import { useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { DataTable } from '@/shared/components/DataTable';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { schApi } from '../api';
import type { AcademicYear, RolloverRow } from '../types';
import './school.css';

interface Props {
  fromYear: AcademicYear;
  toYear: AcademicYear;
  /** Các lớp năm cũ chưa có lớp tương ứng ở năm mới. */
  rows: RolloverRow[];
  onClose: () => void;
  /** Gọi sau khi tạo lớp thành công (trước onClose). */
  onCreated?: (sourceClassIds: number[]) => void;
}

interface Draft { include: boolean; name: string; grade: number }

const GRADES = [6, 7, 8, 9].map((g) => ({ value: g, label: `Khối ${g}` }));

/**
 * SCH-06 (4.2.35–4.2.36): danh sách lớp gợi ý cho năm học mới. Sửa tên, đổi khối hoặc bỏ bớt lớp ngay tại đây rồi mới tạo.
 * Khối 9 không có khối 10 (học sinh tốt nghiệp) nên không được chọn sẵn, chỉ tích khi có em học lại lớp 9. Chỉ tạo lớp, không sao chép thời khóa biểu hay trách nhiệm.
 */
export function RolloverSuggestModal({ fromYear, toYear, rows, onClose, onCreated }: Props) {
  const qc = useQueryClient();
  const [drafts, setDrafts] = useState<Record<number, Draft>>(() => Object.fromEntries(
    rows.map((r) => [r.sourceClassId, { include: r.suggestedGrade !== null, name: r.suggestedName, grade: r.suggestedGrade ?? r.gradeLevel }]),
  ));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const chosen = rows.filter((r) => drafts[r.sourceClassId].include);
  // Tên lớp phải duy nhất trong cùng phân hiệu (không phân biệt hoa thường): báo ngay tại đây thay vì đợi bấm Tạo.
  const seen = new Map<string, number>();
  for (const r of chosen) {
    const key = `${r.campusId}|${drafts[r.sourceClassId].name.trim().toLowerCase()}`;
    seen.set(key, (seen.get(key) ?? 0) + 1);
  }
  const isDuplicate = (r: RolloverRow) => (seen.get(`${r.campusId}|${drafts[r.sourceClassId].name.trim().toLowerCase()}`) ?? 0) > 1;
  const hasDuplicate = chosen.some(isDuplicate);
  const canSave = chosen.length > 0 && !hasDuplicate && chosen.every((r) => drafts[r.sourceClassId].name.trim().length > 0);
  // Chọn tất cả chỉ áp cho các lớp có khối kế tiếp; lớp khối 9 (tốt nghiệp) luôn do người dùng tự tích.
  const selectable = rows.filter((r) => r.suggestedGrade !== null);
  const selectedCount = selectable.filter((r) => drafts[r.sourceClassId].include).length;
  const toggleAll = (on: boolean) => {
    setDrafts((d) => Object.fromEntries(Object.entries(d).map(([id, v]) => [id, selectable.some((r) => r.sourceClassId === Number(id)) ? { ...v, include: on } : v])));
    setError(null);
  };
  const patch = (id: number, p: Partial<Draft>) => {
    setDrafts((d) => ({ ...d, [id]: { ...d[id], ...p } }));
    setError(null);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const res = await schApi.rollover.createClasses({
        fromYearId: fromYear.academicYearId,
        toYearId: toYear.academicYearId,
        items: chosen.map((r) => ({ sourceClassId: r.sourceClassId, name: drafts[r.sourceClassId].name, gradeLevel: drafts[r.sourceClassId].grade })),
      });
      await qc.invalidateQueries({ queryKey: ['sch'] });
      message.success(`Đã tạo ${res.created} lớp cho năm học ${toYear.name}.`);
      onCreated?.(chosen.map((r) => r.sourceClassId));
      onClose();
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open width={720} title={`Tạo lớp cho năm học ${toYear.name}`} onCancel={onClose} mask={{ closable: false }} destroyOnHidden
      footer={(
        <>
          <Button onClick={onClose}>Đóng</Button>
          <Button type="primary" loading={saving} disabled={!canSave} onClick={save}>
            {chosen.length > 0 ? `Tạo ${chosen.length} lớp` : 'Tạo lớp'}
          </Button>
        </>
      )}
    >
      <p className="import-modal__hint">
        Gợi ý lấy từ lớp năm học {fromYear.name}: tên và khối tăng 1. Sửa tên hoặc bỏ tích những lớp không tạo, rồi bấm Tạo.
        Chỉ tạo lớp, thời khóa biểu, chủ nhiệm và trách nhiệm của năm cũ không được sao chép.
      </p>
      <DataTable<RolloverRow>
        rowKey="sourceClassId" dataSource={rows} pagination={false}
        columns={[
          {
            title: (
              <Checkbox
                checked={selectable.length > 0 && selectedCount === selectable.length}
                indeterminate={selectedCount > 0 && selectedCount < selectable.length}
                disabled={selectable.length === 0} onChange={(e) => toggleAll(e.target.checked)} aria-label="Chọn tất cả lớp"
              />
            ),
            key: 'include', width: 48,
            render: (_: unknown, r: RolloverRow) => (
              <Checkbox checked={drafts[r.sourceClassId].include} onChange={(e) => patch(r.sourceClassId, { include: e.target.checked })} aria-label={`Tạo lớp cho ${r.sourceName}`} />
            ),
          },
          {
            title: 'Lớp năm cũ', key: 'source',
            render: (_: unknown, r: RolloverRow) => (
              <>
                <strong>{r.sourceName}</strong>
                <div className="muted">Khối {r.gradeLevel} · {r.campusName}</div>
              </>
            ),
          },
          {
            title: 'Tên lớp mới', key: 'name', width: 200,
            render: (_: unknown, r: RolloverRow) => (
              <Input
                status={drafts[r.sourceClassId].include && isDuplicate(r) ? 'error' : undefined}
                maxLength={50} value={drafts[r.sourceClassId].name} disabled={!drafts[r.sourceClassId].include}
                onChange={(e) => patch(r.sourceClassId, { name: e.target.value })}
              />
            ),
          },
          {
            title: 'Khối', key: 'grade', width: 130,
            render: (_: unknown, r: RolloverRow) => (
              <Select
                className="rollover-grade" options={GRADES} value={drafts[r.sourceClassId].grade} disabled={!drafts[r.sourceClassId].include}
                onChange={(g) => patch(r.sourceClassId, { grade: g })}
              />
            ),
          },
        ]}
      />
      {rows.some((r) => r.suggestedGrade === null) && (
        <p className="muted">
          Lớp khối 9 không có khối 10 (học sinh tốt nghiệp) nên không được chọn sẵn. Học sinh lớp 9 lưu ban vẫn xếp được vào lớp 9 sinh từ lớp 8,
          nên chỉ tích hàng này khi muốn mở một lớp 9 riêng cho các em học lại, và đặt tên khác (ví dụ 9A2).
        </p>
      )}
      {hasDuplicate &&<InlineAlert type="warning" title="Có hai lớp trùng tên trong cùng phân hiệu (ô viền đỏ). Đổi tên hoặc bỏ tích một trong hai." />}
      {error && <InlineAlert type="error" title={error} />}
    </Modal>
  );
}
