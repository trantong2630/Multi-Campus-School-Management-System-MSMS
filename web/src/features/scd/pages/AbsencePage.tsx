import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Select, Switch, Tabs, Tag, Tooltip } from 'antd';
import type { Dayjs } from 'dayjs';
import { BadgeCheck, Undo2 } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { Key } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { schApi } from '@/features/sch/api';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, formatTime, sessionOfDayLabel, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import { ReturnAbsenceModal } from '../components/AbsenceModals';
import { absenceScopeLabel } from '../labels';
import type { Absence, PendingSession } from '../types';
import { useScdCampus } from '../useScdCampus';

/**
 * SCD-03: giáo viên vắng. Tổ trưởng khai báo, quyết định từng tiết rồi gửi duyệt; hiệu phó thấy khai báo đã gửi, bấm một khai báo để xem các tiết kèm quyết định của tổ trưởng, rồi Duyệt hoặc Trả lại kèm lý do (BR-16). Xử lý riêng một tiết thì dùng lưới Lịch dạy ở chế độ Điều chỉnh, không có nút riêng ở đây.
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
            { key: 'pending', label: 'Tiết chờ xử lý', children: <PendingTab campusId={effectiveCampus} lead={campusSelect} canManage={canManage} /> },
          ]}
        />
      )}
    </>
  );
}

// ---------------------------------------------------------------- Tiết chờ xử lý
function PendingTab({ campusId, lead, canManage }: { campusId: number; lead: React.ReactNode; canManage: boolean }) {
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [classId, setClassId] = useState<number | undefined>();
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

  return (
    <>
      {canManage && (
        <InlineAlert type="info" title="Muốn xử lý riêng một tiết" description="Mở Lịch dạy, bấm Điều chỉnh, chọn lớp và tuần rồi bấm vào tiết đang Chờ xử lý: chọn giáo viên dạy thay hoặc hủy tiết. Tiết tổ trưởng đang quyết định (chưa gửi duyệt) thì chờ tổ trưởng gửi rồi mới xử lý được." />
      )}
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
              title: 'Tình trạng', key: 'state', width: 130, align: 'center',
              render: () => <Tag color="gold">Chờ xử lý</Tag>,
            },
          ]}
        />
      </Card>
    </>
  );
}

// ---------------------------------------------------------------- Khai báo vắng
/** Khai báo tổ trưởng còn đang soạn hoặc bị trả lại: hiệu phó chưa phải làm gì nên không hiện trong danh sách, chỉ hiện khi tổ trưởng gửi duyệt. */
const inProgress = (a: Absence) => !a.isPast && a.pendingCount > 0 && a.status !== 'Submitted';

function StateTag({ a }: { a: Absence }) {
  if (a.isPast) return <Tag>Đã qua</Tag>;
  if (a.pendingCount === 0) return <Tag color="green">Đã xử lý</Tag>;
  if (a.status === 'Submitted') return <Tag color="blue">Chờ duyệt</Tag>;
  if (a.status === 'Returned') return <Tag color="red">Đã trả lại tổ trưởng</Tag>;
  return <Tag color="gold">Tổ trưởng đang quyết định</Tag>;
}

function AbsenceTab({ campusId, lead, canManage, onChanged }: { campusId: number; lead: React.ReactNode; canManage: boolean; onChanged: () => void }) {
  const absences = useQuery({ queryKey: ['scd', 'absences', campusId], queryFn: () => scdApi.absences(campusId) });
  const pending = useQuery({ queryKey: ['scd', 'pending', campusId, undefined, undefined, undefined], queryFn: () => scdApi.pending(campusId, {}) });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });
  const [showPast, setShowPast] = useState(false);
  const [returning, setReturning] = useState<{ absenceId: number; title: string } | null>(null);
  const [expanded, setExpanded] = useState<Key[]>([]);
  const openNeedingApproval = useRef(true);   // mở sẵn các khai báo chờ duyệt: lần đầu có dữ liệu

  const all = absences.data ?? [];
  const visible = all.filter((a) => (showPast || !a.isPast) && !inProgress(a));
  const hiddenPast = all.filter((a) => a.isPast).length;

  const sessionsByAbsence = useMemo(() => {
    const map = new Map<number, PendingSession[]>();
    for (const s of pending.data ?? []) {
      if (s.absenceId === null) continue;
      map.set(s.absenceId, [...(map.get(s.absenceId) ?? []), s]);
    }
    return map;
  }, [pending.data]);

  useEffect(() => {
    if (!openNeedingApproval.current || !absences.data) return;
    openNeedingApproval.current = false;
    setExpanded(absences.data.filter((a) => a.canApprove).map((a) => a.absenceId));
  }, [absences.data]);

  // Duyệt: áp mọi đề xuất của tổ trưởng cho tiết ở phân hiệu này cùng lúc, tất cả hoặc không. Chỉ duyệt được khi tổ trưởng đã gửi khai báo.
  const approve = (a: Absence) => confirmAction({
    title: `Duyệt khai báo vắng của ${a.teacherName}?`,
    content: 'Các tiết ở phân hiệu này có đề xuất được áp theo quyết định của tổ trưởng (giáo viên dạy thay hoặc hủy tiết). Giáo viên dạy thay và học sinh của lớp được thông báo, giáo viên dạy thay còn nhận email nếu hồ sơ có email. Tiết chưa có đề xuất (môn của tổ khác) bạn xử lý riêng trên lưới Điều chỉnh.',
    okText: 'Duyệt',
    onOk: async () => {
      try {
        const res = await scdApi.approveAbsence(a.absenceId);
        onChanged();
        const extra = [
          res.leftPendingCount > 0 ? `${res.leftPendingCount} tiết chưa có đề xuất bạn xử lý riêng trên lưới Điều chỉnh` : '',
          res.otherCampusPendingCount > 0 ? `${res.otherCampusPendingCount} tiết ở phân hiệu khác chờ hiệu phó đó duyệt` : '',
        ].filter(Boolean).join('; ');
        message.success(`Đã duyệt: ${res.substitutedCount} tiết có người dạy thay, ${res.cancelledCount} tiết hủy${extra ? `. ${extra}` : ''}.`);
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
          emptyText={hiddenPast > 0 && !showPast
            ? 'Chưa có khai báo nào chờ bạn duyệt. Bật "Hiện cả khai báo đã qua" để tra cứu.'
            : 'Chưa có khai báo nào chờ bạn duyệt.'}
          expandable={{
            expandedRowKeys: expanded,
            onExpandedRowsChange: (keys) => setExpanded([...keys]),
            expandRowByClick: true,
            rowExpandable: (a) => (sessionsByAbsence.get(a.absenceId)?.length ?? 0) > 0,
            expandedRowRender: (a) => <AbsenceSessions sessions={sessionsByAbsence.get(a.absenceId) ?? []} />,
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
              title: 'Trạng thái', key: 'state', width: 190, align: 'center',
              render: (_: unknown, a: Absence) => <StateTag a={a} />,
            },
            {
              title: 'Tổ trưởng đã quyết định', key: 'decided', width: 160, align: 'right', className: 'tabular',
              render: (_: unknown, a: Absence) => a.decidableCount > 0 ? `${a.decidedCount}/${a.decidableCount} tiết` : '—',
            },
            { title: 'Còn chờ xử lý', dataIndex: 'pendingCount', width: 120, align: 'right', className: 'tabular' },
            { title: 'Bỏ lỡ', dataIndex: 'missedCount', width: 90, align: 'right', className: 'tabular', render: (v: number) => v > 0 ? <span style={{ color: 'var(--c-amber)' }}>{v}</span> : v },
            { title: 'Đã xử lý', dataIndex: 'handledCount', width: 100, align: 'right', className: 'tabular' },
            ...(canManage ? [{
              title: 'Thao tác', key: 'actions', width: 110, align: 'center' as const, fixed: 'right' as const,
              // Khai báo đã qua không còn thao tác nào: để trống thay vì một nút tắt. Bấm nút không làm mở rộng hay thu gọn dòng.
              render: (_: unknown, a: Absence) => a.isPast || a.pendingCount === 0 ? null : (
                <div className="row-actions" onClick={(e) => e.stopPropagation()}>
                  <Tooltip title={a.canApprove ? 'Duyệt khai báo' : a.status === 'Submitted' ? 'Không có đề xuất ở phân hiệu này: xử lý riêng các tiết trên lưới Điều chỉnh' : 'Chờ tổ trưởng gửi duyệt'}>
                    <Button
                      type="text" className="icon-btn" disabled={!a.canApprove} aria-label="Duyệt khai báo"
                      icon={<BadgeCheck size={16} />} onClick={() => approve(a)}
                    />
                  </Tooltip>
                  <Tooltip title={a.canReturn ? 'Trả lại tổ trưởng kèm lý do' : 'Chỉ trả lại được khai báo tổ trưởng đã gửi'}>
                    <Button
                      type="text" className="icon-btn" disabled={!a.canReturn} aria-label="Trả lại tổ trưởng"
                      icon={<Undo2 size={16} />} onClick={() => setReturning({ absenceId: a.absenceId, title: a.teacherName })}
                    />
                  </Tooltip>
                </div>
              ),
            }] : []),
          ]}
        />
      </Card>
      <ReturnAbsenceModal target={returning} onClose={() => setReturning(null)} onDone={() => { setReturning(null); onChanged(); }} />
    </>
  );
}

/** Các tiết Chờ xử lý của một khai báo ở phân hiệu này, kèm quyết định của tổ trưởng (chỉ xem). */
function AbsenceSessions({ sessions }: { sessions: PendingSession[] }) {
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
        {
          title: 'Quyết định của tổ trưởng', key: 'decision',
          render: (_: unknown, s: PendingSession) => s.proposedAction === 'Cancel' ? <Tag color="red">Hủy tiết</Tag>
            : s.proposedAction === 'Substitute' ? <Tag color="blue">Dạy thay: {s.proposedTeacherName}</Tag>
              : <span className="muted">Chưa có đề xuất — bạn xử lý riêng trên lưới Điều chỉnh</span>,
        },
      ]}
    />
  );
}
