import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Switch, Tag, Tooltip } from 'antd';
import { ClipboardPen, Trash2, UserRoundMinus, UserRoundX } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { Key } from 'react';
import { schApi } from '@/features/sch/api';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, formatTime, sessionOfDayLabel, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import { DeclareAbsenceModal, ProposeModal } from '../components/AbsenceModals';
import { absenceScopeLabel } from '../labels';
import type { Absence, PendingSession } from '../types';

/**
 * SCD-03 cho tổ trưởng: một danh sách khai báo vắng của tổ. Bấm một khai báo thì dòng mở rộng, hiện các tiết của khai báo đó; mỗi tiết quyết định
 * trong ngăn kéo bên phải bằng một đề xuất (người dạy thay hoặc hủy tiết). Đề xuất chưa đổi lịch: hiệu phó của phân hiệu có tiết chỉ duyệt, và chỉ duyệt
 * khi tổ trưởng đã quyết định hết mọi tiết của khai báo (BR-16).
 */
export function DepartmentAbsencePage() {
  const qc = useQueryClient();
  const [declareOpen, setDeclareOpen] = useState(false);
  const [showPast, setShowPast] = useState(false);
  const [deciding, setDeciding] = useState<PendingSession | null>(null);
  const [expanded, setExpanded] = useState<Key[]>([]);
  const openNeedingDecision = useRef(true);   // mở sẵn các khai báo còn việc: lần đầu có dữ liệu và sau mỗi lần khai báo mới

  const absences = useQuery({ queryKey: ['scd', 'departmentAbsences'], queryFn: scdApi.departmentAbsences });
  const pending = useQuery({ queryKey: ['scd', 'departmentPending'], queryFn: scdApi.departmentPending });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });

  // Không làm mới gợi ý giáo viên: ô chọn trong ngăn kéo quyết định vẫn đang mở.
  const refresh = () => qc.invalidateQueries({ queryKey: ['scd'], predicate: (q) => q.queryKey[1] !== 'teacherAvailability' });

  const sessionsByAbsence = useMemo(() => {
    const map = new Map<number, PendingSession[]>();
    for (const s of pending.data ?? []) {
      if (s.absenceId === null) continue;
      map.set(s.absenceId, [...(map.get(s.absenceId) ?? []), s]);
    }
    return map;
  }, [pending.data]);

  const all = absences.data ?? [];
  const visible = all.filter((a) => showPast || !a.isPast);
  const hiddenPast = all.length - all.filter((a) => !a.isPast).length;
  const decided = (pending.data ?? []).filter((s) => s.proposedAction).length;

  // Mở sẵn các khai báo còn tiết chưa quyết định, để vào trang là thấy ngay việc cần làm.
  useEffect(() => {
    if (!openNeedingDecision.current || !absences.data) return;
    openNeedingDecision.current = false;
    setExpanded(absences.data.filter((a) => !a.isPast && a.decidedCount < a.awaitingCount).map((a) => a.absenceId));
  }, [absences.data]);

  const remove = (a: Absence) => confirmAction({
    title: `Xóa khai báo vắng của ${a.teacherName}?`,
    content: `${a.pendingCount} tiết đang Chờ xử lý sẽ trở về ${a.teacherName}, các quyết định của bạn cho những tiết đó bị bỏ. Nếu giáo viên đã có tiết khác trùng lịch ở một tiết nào đó thì không xóa được.`,
    okText: 'Xóa khai báo',
    danger: true,
    onOk: async () => {
      try {
        const res = await scdApi.deleteDepartmentAbsence(a.absenceId);
        refresh();
        message.success(`Đã xóa khai báo, ${res.restoredCount} tiết trở về giáo viên.`);
      } catch (e) {
        showError(e, 'Không xóa được khai báo');
      }
    },
  });

  const withdraw = (s: PendingSession) => confirmAction({
    title: `Bỏ quyết định cho tiết ${s.subjectName} lớp ${s.className}?`,
    content: 'Tiết quay về chưa quyết định. Hiệu phó chưa duyệt được khai báo cho tới khi bạn quyết định lại.',
    okText: 'Bỏ quyết định',
    onOk: async () => {
      try {
        await scdApi.proposeSession(s.sessionId, { action: 'Clear' });
        refresh();
        message.success('Đã bỏ quyết định.');
      } catch (e) {
        showError(e, 'Không bỏ được quyết định');
      }
    },
  });

  return (
    <>
      <PageHeader
        title="Giáo viên vắng của tổ"
        extra={<Button type="primary" icon={<UserRoundX size={16} />} onClick={() => setDeclareOpen(true)}>Khai báo vắng</Button>}
      />
      <FilterBar>
        <label className="filter-switch">
          <Switch size="small" checked={showPast} onChange={setShowPast} />
          <span>Hiện cả khai báo đã qua{hiddenPast > 0 && !showPast ? ` (${hiddenPast})` : ''}</span>
        </label>
      </FilterBar>
      <Card className="table-card">
        <DataTable<Absence>
          rowKey="absenceId" loading={absences.isLoading} dataSource={visible}
          emptyText={hiddenPast > 0 && !showPast ? 'Không có khai báo nào đang hiệu lực. Bật "Hiện cả khai báo đã qua" để tra cứu.' : 'Chưa có khai báo giáo viên vắng nào của tổ.'}
          expandable={{
            expandedRowKeys: expanded,
            onExpandedRowsChange: (keys) => setExpanded([...keys]),
            expandRowByClick: true,
            rowExpandable: (a) => !a.isPast && (sessionsByAbsence.get(a.absenceId)?.length ?? 0) > 0,
            expandedRowRender: (a) => (
              <SessionsTable sessions={sessionsByAbsence.get(a.absenceId) ?? []} onDecide={setDeciding} onWithdraw={withdraw} />
            ),
          }}
          columns={[
            { title: 'Giáo viên', key: 't', width: 200, render: (_: unknown, a: Absence) => <>{a.teacherName} {a.teacherAbbr && <code className="mono">{a.teacherAbbr}</code>}</> },
            {
              title: 'Khoảng ngày', key: 'range', width: 190, className: 'tabular',
              render: (_: unknown, a: Absence) => a.fromDate === a.toDate ? formatDate(a.fromDate) : `${formatDate(a.fromDate)} – ${formatDate(a.toDate)}`,
            },
            { title: 'Phạm vi', key: 'scope', render: (_: unknown, a: Absence) => absenceScopeLabel(a.slotIds, slots.data) },
            { title: 'Lý do', dataIndex: 'reason', render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Người khai', dataIndex: 'declaredByName', width: 150, render: (v: string | null) => v ?? '—' },
            {
              title: 'Trạng thái', key: 'state', width: 170, align: 'center',
              render: (_: unknown, a: Absence) => a.isPast ? <Tag>Đã qua</Tag>
                : a.awaitingCount === 0 ? <Tag color="green">Đã duyệt</Tag>
                  : a.decidedCount < a.awaitingCount ? <Tag color="gold">Đang quyết định</Tag> : <Tag color="blue">Chờ hiệu phó duyệt</Tag>,
            },
            {
              title: 'Đã quyết định', key: 'decided', width: 130, align: 'right', className: 'tabular',
              render: (_: unknown, a: Absence) => a.awaitingCount > 0 ? `${a.decidedCount}/${a.awaitingCount} tiết` : '—',
            },
            { title: 'Đã xử lý', dataIndex: 'handledCount', width: 100, align: 'right', className: 'tabular' },
            {
              title: 'Thao tác', key: 'actions', width: 90, align: 'center', fixed: 'right',
              render: (_: unknown, a: Absence) => a.isPast ? null : (
                // Bấm nút không làm mở rộng hay thu gọn dòng.
                <div className="row-actions" onClick={(e) => e.stopPropagation()}>
                  <Tooltip title={a.canDelete ? 'Xóa khai báo' : 'Chỉ xóa được khai báo do bạn khai và chưa có tiết nào được xử lý'}>
                    <Button
                      type="text" className="icon-btn" danger disabled={!a.canDelete} aria-label="Xóa khai báo"
                      icon={<Trash2 size={16} />} onClick={() => remove(a)}
                    />
                  </Tooltip>
                </div>
              ),
            },
          ]}
        />
      </Card>
      <DeclareAbsenceModal
        open={declareOpen} onClose={() => setDeclareOpen(false)}
        onDone={() => { openNeedingDecision.current = true; setDeclareOpen(false); }}
      />
      <ProposeModal
        target={deciding} decided={decided} total={(pending.data ?? []).length}
        onClose={() => setDeciding(null)} onDone={() => { setDeciding(null); refresh(); }}
      />
    </>
  );
}

// ---------------------------------------------------------------- Các tiết của một khai báo
function DecisionTag({ s }: { s: PendingSession }) {
  if (s.proposedAction === 'Cancel') return <Tag color="red">Hủy tiết</Tag>;
  if (s.proposedAction === 'Substitute') return <Tag color="blue">Dạy thay: {s.proposedTeacherName}</Tag>;
  return <span className="muted">Chưa quyết định</span>;
}

function SessionsTable({ sessions, onDecide, onWithdraw }: {
  sessions: PendingSession[];
  onDecide: (s: PendingSession) => void;
  onWithdraw: (s: PendingSession) => void;
}) {
  return (
    <DataTable<PendingSession>
      rowKey="sessionId" dataSource={sessions} pagination={false} sticky={false} size="small"
      columns={[
        {
          title: 'Ngày', dataIndex: 'date', width: 150, className: 'tabular',
          render: (v: string) => <>{weekdayOfDate(v)}<span className="cell-sub cell-sub--date">{formatDate(v)}</span></>,
        },
        {
          title: 'Tiết', key: 'slot', width: 170,
          render: (_: unknown, s: PendingSession) => (
            <>{sessionOfDayLabel(s.sessionOfDay)} · Tiết {s.periodNo}<span className="cell-sub">{formatTime(s.startTime)}–{formatTime(s.endTime)}</span></>
          ),
        },
        { title: 'Lớp', dataIndex: 'className', width: 90 },
        { title: 'Môn', dataIndex: 'subjectName', width: 140 },
        { title: 'Phân hiệu', dataIndex: 'campusName', width: 150 },
        { title: 'Quyết định', key: 'decision', render: (_: unknown, s: PendingSession) => <DecisionTag s={s} /> },
        {
          title: 'Thao tác', key: 'actions', width: 100, align: 'center',
          render: (_: unknown, s: PendingSession) => (
            <RowActions actions={[
              { key: 'decide', label: s.proposedAction ? 'Đổi quyết định' : 'Quyết định', icon: <ClipboardPen size={16} />, onClick: () => onDecide(s) },
              { key: 'withdraw', label: 'Bỏ quyết định', icon: <UserRoundMinus size={16} />, hidden: !s.proposedAction, onClick: () => onWithdraw(s) },
            ]}
            />
          ),
        },
      ]}
    />
  );
}
