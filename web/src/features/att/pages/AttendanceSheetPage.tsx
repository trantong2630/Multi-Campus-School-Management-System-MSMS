import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Input, Skeleton } from 'antd';
import { ArrowLeft, CheckCheck, Save } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonCell } from '@/shared/components/PersonCell';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { message } from '@/shared/lib/antdStatic';
import { formatDate, formatDateTime } from '@/shared/lib/format';
import { statusGroup } from '@/shared/lib/labels';
import { badgeColor } from '@/shared/lib/tokens';
import { attApi } from '../api';
import { dayText, labelCountItems, slotText } from '../attFormat';
import { CountStrip } from '../components/AttendanceParts';
import type { AttendanceSheetRow, AttendanceStatus } from '../types';
import '../components/att.css';

interface Draft {
  status: AttendanceStatus | null;
  note: string;
}

const CHOICES: { value: AttendanceStatus; label: string; labelKey: string }[] = [
  { value: 'Present', label: 'Có mặt', labelKey: 'Present' },
  { value: 'Late', label: 'Muộn', labelKey: 'Late' },
  { value: 'Absent', label: 'Vắng', labelKey: 'UnexcusedAbsent' },
];


function Trail({ row }: { row: AttendanceSheetRow }) {
  if (!row.recordedByName) return null;
  const parts = [`${row.isBackfilled ? 'Nhập bù' : 'Ghi'} bởi ${row.recordedByName}, ${formatDateTime(row.recordedAt)}`];
  if (row.updatedByName) parts.push(`sửa bởi ${row.updatedByName}, ${formatDateTime(row.updatedAt)}`);
  return <span className="att-row__trail">{parts.join(' · ')}</span>;
}

/**
 * ATT-01 bảng điểm danh của một tiết (Design_Web §9.3): học sinh là thẻ dòng, ba nút Có mặt / Muộn / Vắng bên phải.
 * - Giáo viên của tiết: ghi và sửa trong ngày của tiết (4.5.3, 4.5.4).
 * - School Admin: nhập bù, sửa tiết đã qua, banner hổ phách ghi nhận người sửa (4.5.7, 4.5.8).
 * - Hiệu trưởng, Hiệu phó, chủ nhiệm, giáo viên sau ngày của tiết: chỉ xem; Backend vẫn chặn độc lập (4.5.5).
 */
export function AttendanceSheetPage() {
  const { sessionId } = useParams();
  const id = Number(sessionId);
  const navigate = useNavigate();
  const qc = useQueryClient();
  // Chỉ giữ phần người dùng đã đổi trên màn; dòng chưa đổi lấy thẳng từ bản đã lưu. Lưu xong thì xóa phần đã đổi.
  const [edits, setEdits] = useState<Record<number, Draft>>({});
  const [saveError, setSaveError] = useState<string | null>(null);

  const sheet = useQuery({ queryKey: ['att', 'sheet', id], queryFn: () => attApi.sheet(id), enabled: Number.isFinite(id) });
  const data = sheet.data;
  const editable = !!data && data.readOnlyReason === null;
  const draftOf = (row: AttendanceSheetRow): Draft => edits[row.studentId] ?? { status: row.status, note: row.note ?? '' };

  const save = useMutation({
    mutationFn: () => attApi.save(id, data!.students
      .map((s) => ({ s, d: draftOf(s) }))
      .filter(({ d }) => d.status)
      .map(({ s, d }) => ({ studentId: s.studentId, status: d.status!, note: d.note.trim() || null }))),
    onSuccess: (next) => {
      qc.setQueryData(['att', 'sheet', id], next);
      void qc.invalidateQueries({ queryKey: ['att'], predicate: (q) => q.queryKey[1] !== 'sheet' });
      setEdits({});
      setSaveError(null);
      message.success('Đã lưu điểm danh.');
    },
    onError: (e) => setSaveError(getErrorMessage(e)),
  });

  // Số đếm sống theo lựa chọn đang có trên màn (chưa lưu): Vắng chưa biết có phép hay không nên đếm chung.
  const liveCounts = { Present: 0, Late: 0, Absent: 0, NotMarked: 0 };
  for (const s of data?.students ?? []) liveCounts[draftOf(s).status ?? 'NotMarked']++;

  const dirty = !!data && data.students.some((s) => {
    const d = edits[s.studentId];
    return d && (d.status !== s.status || d.note.trim() !== (s.note ?? ''));
  });

  const set = (row: AttendanceSheetRow, patch: Partial<Draft>) => {
    setSaveError(null);
    setEdits((prev) => ({ ...prev, [row.studentId]: { ...(prev[row.studentId] ?? { status: row.status, note: row.note ?? '' }), ...patch } }));
  };

  const markAllPresent = () => {
    setSaveError(null);
    setEdits((prev) => {
      const next = { ...prev };
      for (const s of data?.students ?? []) {
        const d = next[s.studentId] ?? { status: s.status, note: s.note ?? '' };
        if (!d.status) next[s.studentId] = { ...d, status: 'Present' };
      }
      return next;
    });
  };

  const back = () => navigate('/att');

  if (sheet.isLoading) {
    return (<><PageHeader title="Điểm danh" /><Skeleton active paragraph={{ rows: 8 }} /></>);
  }
  if (sheet.error || !data) {
    return (
      <>
        <PageHeader title="Điểm danh" extra={<Button icon={<ArrowLeft size={16} />} onClick={back}>Quay lại</Button>} />
        <InlineAlert type="error" title="Không mở được bảng điểm danh" description={getErrorMessage(sheet.error)} />
      </>
    );
  }

  const s = data.session;
  const unmarked = liveCounts.NotMarked;

  return (
    <>
      <PageHeader
        title={`${s.subjectName} — lớp ${s.className}`}
        description={editable
          ? 'Chọn Có mặt, Muộn hoặc Vắng cho từng học sinh rồi bấm Lưu. Lưu lại là cập nhật bản ghi đã có.'
          : 'Bảng điểm danh chỉ xem.'}
        extra={(
          <div className="att-sheet-actions">
            <Button icon={<ArrowLeft size={16} />} onClick={back}>Quay lại</Button>
            {editable && (
              <>
                <Button icon={<CheckCheck size={16} />} onClick={markAllPresent} disabled={unmarked === 0}>Còn lại có mặt</Button>
                <Button type="primary" icon={<Save size={16} />} loading={save.isPending} disabled={!dirty} onClick={() => save.mutate()}>Lưu</Button>
              </>
            )}
          </div>
        )}
      />

      <div className="att-sheet-meta">
        <span><strong>{dayText(s.date)}</strong></span>
        <span>{slotText(s)}</span>
        <span>Giáo viên: <strong>{s.teacherName ?? 'Chưa có'}</strong></span>
        <span>{s.campusName}</span>
        <StatusBadge domain="session" value={s.status} />
      </div>

      {data.isAdminMode && (
        <InlineAlert type="warning" title={`Nhập bù cho tiết ngày ${formatDate(s.date).slice(0, 5)} — ghi nhận người sửa.`} />
      )}
      {data.readOnlyReason && <InlineAlert type="info" title={data.readOnlyReason} />}
      {saveError && <InlineAlert type="error" title={saveError} />}

      {editable ? (
        <CountStrip
          total={data.students.length}
          items={[
            { key: 'p', label: 'Có mặt', value: liveCounts.Present, group: 'active' },
            { key: 'l', label: 'Muộn', value: liveCounts.Late, group: 'info' },
            { key: 'a', label: 'Vắng', value: liveCounts.Absent, group: 'danger' },
            { key: 'n', label: 'Chưa điểm danh', value: liveCounts.NotMarked, group: 'neutral' },
          ]}
        />
      ) : (
        s.status === 'Scheduled' && <CountStrip total={data.students.length} items={labelCountItems(data.counts)} />
      )}

      {data.students.length === 0 ? (
        <div className="empty-state"><p className="empty-state__title">Lớp không có học sinh nào có ghi danh vào ngày của tiết.</p></div>
      ) : (
        <div className="att-rows">
          {data.students.map((row, index) => {
            const d = draftOf(row);
            return (
              <div key={row.studentId} className="att-row">
                <span className="att-row__no tabular">{index + 1}</span>
                <div className="att-row__side">
                  <PersonCell name={row.fullName} sub={row.studentCode} />
                </div>
                {editable ? (
                  <div className="att-choice" role="radiogroup" aria-label={`Điểm danh ${row.fullName}`}>
                    {CHOICES.map((c) => {
                      const on = d.status === c.value;
                      const bg = badgeColor[statusGroup('attendance', c.labelKey)].bg;
                      return (
                        <button
                          key={c.value}
                          type="button"
                          role="radio"
                          aria-checked={on}
                          className={`att-choice__btn${on ? ' att-choice__btn--on' : ''}`}
                          style={on ? { background: bg } : undefined}
                          onClick={() => set(row, { status: c.value })}
                        >
                          {c.label}
                        </button>
                      );
                    })}
                  </div>
                ) : (
                  <StatusBadge domain="attendance" value={row.label} />
                )}
                <div className="att-row__side">
                  {editable ? (
                    <Input
                      size="small" maxLength={255} placeholder="Ghi chú (không bắt buộc)" value={d.note}
                      onChange={(e) => set(row, { note: e.target.value })}
                    />
                  ) : (
                    row.note && <span>{row.note}</span>
                  )}
                  {editable && row.status === 'Absent' && row.label === 'ExcusedAbsent' && (
                    <span className="att-row__trail">Có đơn nghỉ đã duyệt — tính là Vắng có phép.</span>
                  )}
                  <Trail row={row} />
                </div>
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}
