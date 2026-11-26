import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Select, Switch, Tabs, Tag, Tooltip } from 'antd';
import type { Dayjs } from 'dayjs';
import { BadgeCheck, ClipboardCheck } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { schApi } from '@/features/sch/api';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, formatTime, sessionOfDayLabel, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import { ResolveModal } from '../components/AbsenceModals';
import type { ResolveTarget } from '../components/AbsenceModals';
import { absenceScopeLabel } from '../labels';
import type { Absence, PendingSession } from '../types';
import { useScdCampus } from '../useScdCampus';

/**
 * SCD-03: giáo viên vắng. Tổ trưởng khai báo và quyết định từng tiết; hiệu phó chỉ duyệt khi tổ trưởng đã quyết định hết (BR-16), hoặc xử lý tay từng tiết.
 * Hiệu trưởng xem được cả hai danh sách (toàn trường) nhưng không duyệt hay xử lý tiết: đó là việc của Hiệu phó.
 */
export function AbsencePage() {
  const qc = useQueryClient();
  const canManage = useAuth().user?.accountType === 'VicePrincipal';
  const { campuses, effective: effectiveCampus, select: campusSelect, head: campusHead } = useScdCampus();
  const [tab, setTab] = useState('absences');

  // Không làm mới gợi ý giáo viên: ô chọn trong hộp "Xử lý tiết" vẫn đang mở, gọi lại cho tiết vừa xử lý xong sẽ bị báo lỗi vì tiết không còn Chờ xử lý.
  const refresh = () => qc.invalidateQueries({ queryKey: ['scd'], predicate: (q) => q.queryKey[1] !== 'teacherAvailability' });

  return (
    <>
      <PageHeader title="Giáo viên vắng" />
      {campusHead}
      {campuses.isSuccess && campuses.data.length === 0 && canManage && (
        <InlineAlert type="warning" title="Chưa có phân hiệu nào để thao tác" description="Hiệu phó cần được gán phụ trách phân hiệu ở Hồ sơ → Trách nhiệm." />
      )}
      {effectiveCampus !== undefined && (
        <Tabs
          activeKey={tab} onChange={setTab}
          items={[
            { key: 'absences', label: 'Khai báo vắng', children: <AbsenceTab campusId={effectiveCampus} lead={campusSelect} canManage={canManage} onChanged={refresh} /> },
            { key: 'pending', label: 'Tiết chờ xử lý', children: <PendingTab campusId={effectiveCampus} lead={campusSelect} canManage={canManage} onChanged={refresh} /> },
          ]}
        />
      )}
    </>
  );
}

// ---------------------------------------------------------------- Tiết chờ xử lý
function PendingTab({ campusId, lead, canManage, onChanged }: { campusId: number; lead: React.ReactNode; canManage: boolean; onChanged: () => void }) {
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [classId, setClassId] = useState<number | undefined>();
  const [resolving, setResolving] = useState<ResolveTarget | null>(null);
  const from = range ? toDateString(range[0]) : undefined;
  const to = range ? toDateString(range[1]) : undefined;

  const classes = useQuery({
    queryKey: ['scd', 'adjustClasses', campusId],
    queryFn: async () => (await scdApi.classes(campusId)).map((c) => ({ value: c.classId, label: c.name })),
  });
  const pending = useQuery({
    queryKey: ['scd', 'pending', campusId, from, to, classId],
    queryFn: () => scdApi.pending(campusId, { from, to, classId }),
  });

  const resolve = (s: PendingSession) => setResolving({
    sessionId: s.sessionId, title: `${s.subjectName} — lớp ${s.className}`,
    summary: `${weekdayOfDate(s.date)} ${formatDate(s.date)}, ${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo}. ${s.absentTeacherName ? `Giáo viên vắng: ${s.absentTeacherName}.` : (s.note ?? '')}`,
    proposal: s.proposedAction === 'Cancel' ? 'Tổ trưởng đề xuất hủy tiết.' : s.proposedAction === 'Substitute' ? `Tổ trưởng đề xuất ${s.proposedTeacherName} dạy thay.` : null,
  });

  return (
    <>
      <FilterBar>
        {lead}
        <DatePicker.RangePicker
          format="DD/MM/YYYY" allowClear value={range} placeholder={['Từ ngày', 'Đến ngày']}
          onChange={(v) => setRange(v && v[0] && v[1] ? [v[0], v[1]] : null)}
        />
        <Select allowClear showSearch optionFilterProp="label" placeholder="Lớp" style={{ minWidth: 140 }} value={classId} onChange={setClassId} options={classes.data} />
      </FilterBar>
      <Card className="table-card">
        <DataTable<PendingSession>
          rowKey="sessionId" loading={pending.isLoading} dataSource={pending.data ?? []}
          emptyText="Không có tiết nào đang chờ xử lý."
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
            { title: 'Lớp', dataIndex: 'className', width: 100 },
            { title: 'Môn', dataIndex: 'subjectName', width: 160 },
            {
              title: 'Nguyên nhân', key: 'cause',
              render: (_: unknown, s: PendingSession) => s.absentTeacherName ? `${s.absentTeacherName} vắng` : (s.note ?? '—'),
            },
            {
              title: 'Quyết định của tổ trưởng', key: 'proposal', width: 210,
              render: (_: unknown, s: PendingSession) => s.proposedAction === 'Cancel' ? <Tag color="red">Hủy tiết</Tag>
                : s.proposedAction === 'Substitute' ? <Tag color="blue">Dạy thay: {s.proposedTeacherName}</Tag> : <span className="muted">Chưa quyết định</span>,
            },
            {
              title: 'Tình trạng', key: 'state', width: 130, align: 'center',
              render: () => <Tag color="gold">Chờ xử lý</Tag>,
            },
            ...(canManage ? [{
              title: 'Thao tác', key: 'actions', width: 90, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, s: PendingSession) => (
                <RowActions actions={[{ key: 'resolve', label: 'Xử lý tay', icon: <ClipboardCheck size={16} />, onClick: () => resolve(s) }]} />
              ),
            }] : []),
          ]}
        />
      </Card>
      <ResolveModal target={resolving} onClose={() => setResolving(null)} onDone={() => { setResolving(null); onChanged(); }} />
    </>
  );
}

// ---------------------------------------------------------------- Khai báo vắng
function AbsenceTab({ campusId, lead, canManage, onChanged }: { campusId: number; lead: React.ReactNode; canManage: boolean; onChanged: () => void }) {
  const absences = useQuery({ queryKey: ['scd', 'absences', campusId], queryFn: () => scdApi.absences(campusId) });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });
  const [showPast, setShowPast] = useState(false);
  const visible = (absences.data ?? []).filter((a) => showPast || !a.isPast);
  const hiddenPast = (absences.data ?? []).length - (absences.data ?? []).filter((a) => !a.isPast).length;

  // Duyệt: áp mọi quyết định của tổ trưởng cho tiết ở phân hiệu này cùng lúc, tất cả hoặc không. Chỉ duyệt được khi tổ trưởng đã quyết định hết mọi tiết.
  const approve = (a: Absence) => confirmAction({
    title: `Duyệt khai báo vắng của ${a.teacherName}?`,
    content: `${a.pendingCount} tiết ở phân hiệu này được áp theo quyết định của tổ trưởng (giáo viên dạy thay hoặc hủy tiết). Giáo viên dạy thay và học sinh của lớp được thông báo. Nếu một quyết định không còn hợp lệ thì không tiết nào được áp và bạn xử lý tay tiết đó trước.`,
    okText: 'Duyệt',
    onOk: async () => {
      try {
        const res = await scdApi.approveAbsence(a.absenceId);
        onChanged();
        message.success(`Đã duyệt: ${res.substitutedCount} tiết có người dạy thay, ${res.cancelledCount} tiết hủy${res.otherCampusPendingCount > 0 ? `. ${res.otherCampusPendingCount} tiết ở phân hiệu khác chờ hiệu phó đó duyệt` : ''}.`);
      } catch (e) {
        showError(e, 'Không duyệt được khai báo');
      }
    },
  });

  return (
    <>
      <FilterBar>
        {lead}
        <label className="filter-switch">
          <Switch size="small" checked={showPast} onChange={setShowPast} />
          <span>Hiện cả khai báo đã qua{hiddenPast > 0 && !showPast ? ` (${hiddenPast})` : ''}</span>
        </label>
      </FilterBar>
      <Card className="table-card">
        <DataTable<Absence>
          rowKey="absenceId" loading={absences.isLoading} dataSource={visible}
          emptyText={hiddenPast > 0 && !showPast ? 'Không có khai báo nào đang hiệu lực. Bật "Hiện cả khai báo đã qua" để tra cứu.' : 'Chưa có khai báo giáo viên vắng nào.'}
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
              title: 'Trạng thái', key: 'state', width: 130, align: 'center',
              render: (_: unknown, a: Absence) => a.isPast ? <Tag>Đã qua</Tag> : <Tag color="green">Đang hiệu lực</Tag>,
            },
            {
              title: 'Tổ trưởng đã quyết định', key: 'decided', width: 160, align: 'right', className: 'tabular',
              render: (_: unknown, a: Absence) => a.awaitingCount > 0 ? `${a.decidedCount}/${a.awaitingCount} tiết` : '—',
            },
            { title: 'Còn chờ xử lý', dataIndex: 'pendingCount', width: 120, align: 'right', className: 'tabular' },
            { title: 'Bỏ lỡ', dataIndex: 'missedCount', width: 90, align: 'right', className: 'tabular', render: (v: number) => v > 0 ? <span style={{ color: 'var(--c-amber)' }}>{v}</span> : v },
            { title: 'Đã xử lý', dataIndex: 'handledCount', width: 100, align: 'right', className: 'tabular' },
            ...(canManage ? [{
              title: 'Thao tác', key: 'actions', width: 90, align: 'center' as const, fixed: 'right' as const,
              // Khai báo đã qua không còn thao tác nào: để trống thay vì một nút tắt.
              render: (_: unknown, a: Absence) => a.isPast || a.pendingCount === 0 ? null : (
                <div className="row-actions">
                  <Tooltip title={a.canApprove ? 'Duyệt khai báo' : `Chờ tổ trưởng quyết định hết các tiết (${a.decidedCount}/${a.awaitingCount})`}>
                    <Button
                      type="text" className="icon-btn" disabled={!a.canApprove} aria-label="Duyệt khai báo"
                      icon={<BadgeCheck size={16} />} onClick={() => approve(a)}
                    />
                  </Tooltip>
                </div>
              ),
            }] : []),
          ]}
        />
      </Card>
    </>
  );
}
