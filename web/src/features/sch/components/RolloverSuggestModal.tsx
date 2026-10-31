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
}

interface Draft { include: boolean; name: string; grade: number }

const GRADES = [6, 7, 8, 9].map((g) => ({ value: g, label: `Khối ${g}` }));

/**
 * SCH-06 (4.2.35–4.2.36): danh sách lớp gợi ý cho năm học mới. Sửa tên, đổi khối hoặc bỏ bớt lớp ngay tại đây rồi mới tạo.
 * Khối 9 không có khối 10 (học sinh tốt nghiệp) nên không được chọn sẵn. Chỉ tạo lớp, không sao chép thời khóa biểu hay trách nhiệm.
 */
export function RolloverSuggestModal({ fromYear, toYear, rows, onClose }: Props) {
  const qc = useQueryClient();
  const [drafts, setDrafts] = useState<Record<number, Draft>>(() => Object.fromEntries(
    rows.map((r) => [r.sourceClassId, { include: r.suggestedGrade !== null, name: r.suggestedName, grade: r.suggestedGrade ?? r.gradeLevel }]),
  ));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const chosen = rows.filter((r) => drafts[r.sourceClassId].include);
  const canSave = chosen.length > 0 && chosen.every((r) => drafts[r.sourceClassId].name.trim().length > 0);
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
      onClose();
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open width={720} title={`Tạo lớp năm học ${toYear.name}`} onCancel={onClose} mask={{ closable: false }} destroyOnHidden
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
            title: '', key: 'include', width: 48,
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
        <p className="muted">Khối 9 không có khối 10 (học sinh tốt nghiệp) nên các lớp khối 9 không được chọn sẵn. Tích chọn nếu muốn tạo lại một lớp khối 9.</p>
      )}
      {error && <InlineAlert type="error" title={error} />}
    </Modal>
  );
}
