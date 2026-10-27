import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Modal, Select, Skeleton } from 'antd';
import { Rocket, RotateCcw } from 'lucide-react';
import { useState } from 'react';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { FormModal } from '@/shared/components/FormModal';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, sessionOfDayLabel, weekdayLabel } from '@/shared/lib/format';
import { scdApi } from '../api';
import type { GridCell, TeacherConflict, TimetableVersion, VersionGrid } from '../types';
import { TimetableGrid } from './TimetableGrid';
import type { CellPos } from './TimetableGrid';
import { PublishPreviewModal } from './PublishPreviewModal';
import './scd.css';

interface Props {
  version: TimetableVersion | null;
  onClose: () => void;
}

/**
 * Sửa bản Nháp trước khi Phát hành (BR-12): kéo tiết trong cột của cùng một lớp (ô trống thì chuyển, ô có tiết thì đổi chỗ),
 * nhấp vào ô để thêm tiết, đổi giáo viên hoặc xóa tiết. Thao tác gây xung đột vẫn được lưu và ô được viền đỏ; còn xung đột thì chặn Phát hành.
 * Mỗi thao tác lưu ngay vào bản Nháp, không đụng lịch dạy.
 */
export function DraftEditorModal({ version, onClose }: Props) {
  const qc = useQueryClient();
  const versionId = version?.versionId ?? null;
  const gridKey = ['scd', 'grid', versionId];

  const grid = useQuery({ queryKey: gridKey, queryFn: () => scdApi.versionGrid(versionId!), enabled: versionId !== null, gcTime: 0 });
  const options = useQuery({
    queryKey: ['scd', 'draftOptions', version?.campusId],
    queryFn: () => scdApi.draftOptions(version!.campusId),
    enabled: version !== null,
  });

  const [editing, setEditing] = useState<{ pos: CellPos; cell: GridCell | null } | null>(null);
  const [publishId, setPublishId] = useState<number | null>(null);

  const g = grid.data;
  const apply = (next: VersionGrid) => qc.setQueryData(gridKey, next);

  const close = () => {
    void qc.invalidateQueries({ queryKey: ['scd', 'versions'] });
    onClose();
  };

  const move = async (from: CellPos, to: { day: number; slotId: number }) => {
    try {
      apply(await scdApi.moveEntry(versionId!, {
        classId: from.classId, fromDayOfWeek: from.day, fromPeriodSlotId: from.slotId, toDayOfWeek: to.day, toPeriodSlotId: to.slotId,
      }));
    } catch (e) {
      showError(e, 'Không chuyển được tiết');
    }
  };

  const reset = () => confirmAction({
    title: 'Khôi phục bản Nháp về file import?',
    content: 'Mọi thay đổi bạn đã sửa trên lưới này sẽ mất, bản Nháp trở về đúng nội dung file đã tải lên.',
    okText: 'Khôi phục',
    danger: true,
    onOk: async () => {
      try { apply(await scdApi.resetDraft(versionId!)); } catch (e) { showError(e, 'Không khôi phục được'); }
    },
  });

  const conflictCount = g?.conflicts.length ?? 0;

  return (
    <>
      <Modal
        open={version !== null} width={1360} centered onCancel={close} mask={{ closable: false }}
        title={version ? `Sửa bản Nháp #${version.versionId} — ${version.campusName}, ${version.semesterName}` : ''}
        footer={[
          <Button key="reset" icon={<RotateCcw size={16} />} disabled={!g?.canReset} onClick={reset}>Khôi phục về file import</Button>,
          <Button key="close" onClick={close}>Đóng</Button>,
          <Button key="publish" type="primary" icon={<Rocket size={16} />} disabled={!g || conflictCount > 0} onClick={() => setPublishId(versionId)}>
            Phát hành
          </Button>,
        ]}
      >
        <InlineAlert
          type="info"
          title="Kéo tiết sang ô khác trong cùng cột lớp (ô trống thì chuyển, ô có tiết thì đổi chỗ). Nhấp vào ô để thêm tiết, đổi giáo viên hoặc xóa."
          description="Ô viền đỏ là giáo viên đang bị trùng lịch; ô có chấm vàng khác với file đã tải lên. Mọi thay đổi lưu ngay vào bản Nháp, chưa ảnh hưởng lịch dạy cho tới khi Phát hành."
        />
        {grid.isLoading && <Skeleton active paragraph={{ rows: 8 }} />}
        {grid.isError && <InlineAlert type="error" title="Không tải được lưới bản Nháp. Đóng hộp thoại rồi thử lại." />}
        {g && (
          <div className="tt-editor">
            <div className="tt-editor__grid">
              {g.classes.length === 0
                ? <InlineAlert type="warning" title="Phiên bản này không có lớp nào." />
                : <TimetableGrid grid={g} edit={{ onMove: move, onCellClick: (pos, cell) => setEditing({ pos, cell }) }} />}
            </div>
            {conflictCount > 0 && (
              <div className="tt-editor__side">
                <InlineAlert type="error" title={`Còn ${conflictCount} xung đột giáo viên — chặn phát hành`} />
                {g.conflicts.map((c) => <ConflictItem key={`${c.teacherAbbr}|${c.dayOfWeek}|${c.periodNo}|${c.sessionOfDay}|${c.newClassName}|${c.occupiedClassName}`} c={c} />)}
              </div>
            )}
          </div>
        )}
      </Modal>

      <CellDialog
        versionId={versionId} grid={g} editing={editing} options={options.data}
        onClose={() => setEditing(null)} onChanged={apply}
      />
      <PublishPreviewModal versionId={publishId} onClose={() => setPublishId(null)} onPublished={close} />
    </>
  );
}

function ConflictItem({ c }: { c: TeacherConflict }) {
  return (
    <div className="tt-conflict-item">
      <div><strong>{c.teacherName}</strong> <code className="mono">{c.teacherAbbr}</code></div>
      <div>{weekdayLabel(c.dayOfWeek)}, {sessionOfDayLabel(c.sessionOfDay)} tiết {c.periodNo} — lớp {c.newClassName}</div>
      <div className="muted">Đang chiếm chỗ: {c.occupiedSubject} {c.occupiedClassName} · {c.occupiedCampusName}</div>
      {c.sessionCount > 0 && <div className="muted tabular">{c.sessionCount} buổi, {formatDate(c.firstDate)} – {formatDate(c.lastDate)}</div>}
    </div>
  );
}

interface DialogProps {
  versionId: number | null;
  grid: VersionGrid | undefined;
  editing: { pos: CellPos; cell: GridCell | null } | null;
  options: { subjects: { subjectId: number; abbr: string; name: string }[]; teachers: { teacherId: number; name: string; abbr: string | null }[] } | undefined;
  onClose: () => void;
  onChanged: (next: VersionGrid) => void;
}

/** Nhấp vào ô trống: thêm tiết (môn + giáo viên). Nhấp vào ô có tiết: đổi giáo viên hoặc xóa tiết. */
function CellDialog({ versionId, grid, editing, options, onClose, onChanged }: DialogProps) {
  const [form] = Form.useForm<{ subjectId?: number; teacherId: number }>();
  const pos = editing?.pos;
  const cell = editing?.cell ?? null;
  const className = grid?.classes.find((c) => c.classId === pos?.classId)?.name ?? '';
  const slot = grid?.rows.find((r) => r.dayOfWeek === pos?.day && r.periodSlotId === pos?.slotId);
  const where = slot && pos ? `${className} · ${weekdayLabel(pos.day)}, ${sessionOfDayLabel(slot.sessionOfDay)} tiết ${slot.periodNo}` : '';

  const teacherOptions = options?.teachers.map((t) => ({ value: t.teacherId, label: `${t.name} (${t.abbr})` }));

  const submit = async (v: { subjectId?: number; teacherId: number }) => {
    if (!pos || versionId === null) return;
    onChanged(cell
      ? await scdApi.changeEntryTeacher(versionId, { classId: pos.classId, dayOfWeek: pos.day, periodSlotId: pos.slotId, teacherId: v.teacherId })
      : await scdApi.addEntry(versionId, { classId: pos.classId, dayOfWeek: pos.day, periodSlotId: pos.slotId, subjectId: v.subjectId!, teacherId: v.teacherId }));
  };

  const remove = () => {
    if (!pos || versionId === null) return;
    confirmAction({
      title: 'Xóa tiết này khỏi bản Nháp?',
      content: `${cell?.subjectAbbr} — ${where}. Ô sẽ trở thành tiết trống.`,
      okText: 'Xóa tiết',
      danger: true,
      onOk: async () => {
        try {
          onChanged(await scdApi.removeEntry(versionId, { classId: pos.classId, dayOfWeek: pos.day, periodSlotId: pos.slotId }));
          onClose();
        } catch (e) {
          showError(e, 'Không xóa được tiết');
        }
      },
    });
  };

  return (
    <FormModal
      open={editing !== null} form={form} onClose={onClose} onSubmit={submit}
      title={cell ? `Tiết ${cell.subjectAbbr} — ${where}` : `Thêm tiết — ${where}`}
      okText={cell ? 'Lưu' : 'Thêm tiết'}
      initialValues={{ teacherId: cell?.teacherId }}
    >
      {!cell && (
        <Form.Item name="subjectId" label="Môn" rules={[{ required: true, message: 'Chọn môn' }]}>
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn môn"
            options={options?.subjects.map((s) => ({ value: s.subjectId, label: `${s.name} (${s.abbr})` }))}
          />
        </Form.Item>
      )}
      <Form.Item name="teacherId" label="Giáo viên" rules={[{ required: true, message: 'Chọn giáo viên' }]}>
        <Select showSearch optionFilterProp="label" placeholder="Chọn giáo viên" options={teacherOptions} />
      </Form.Item>
      {cell && <Button danger type="text" onClick={remove}>Xóa tiết khỏi bản Nháp</Button>}
    </FormModal>
  );
}
