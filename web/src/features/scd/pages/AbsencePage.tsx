import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, DatePicker, Select, Tabs, Tag, Tooltip } from 'antd';
import type { Dayjs } from 'dayjs';
import { ClipboardCheck, Trash2, UserRoundX } from 'lucide-react';
import { useState } from 'react';
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
import { DeclareAbsenceModal, ResolveModal } from '../components/AbsenceModals';
import type { ResolveTarget } from '../components/AbsenceModals';
import { absenceScopeLabel } from '../labels';
import type { Absence, PendingSession } from '../types';

/** SCD-03: giáo viên vắng. Khai báo không tự đổi lịch — tiết bị ảnh hưởng về Chờ xử lý, hiệu phó quyết định từng tiết (BR-16). */
export function AbsencePage() {
  const qc = useQueryClient();
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number | undefined>();
  const effectiveCampus = campusId ?? campuses.data?.[0]?.campusId;
  const [declareOpen, setDeclareOpen] = useState(false);
  const [tab, setTab] = useState('pending');

  const refresh = () => qc.invalidateQueries({ queryKey: ['scd'] });
  const campusSelect = (
    <Select
      placeholder="Phân hiệu" value={effectiveCampus} onChange={setCampusId}
      options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
    />
  );

  return (
    <>
      <PageHeader
        title="Giáo viên vắng"
        description="Khai báo giáo viên vắng theo ngày hoặc theo tiết, rồi xử lý từng tiết Chờ xử lý: chọn giáo viên dạy thay hoặc hủy. Đây không phải đơn xin nghỉ của cán bộ."
        extra={
          <Button type="primary" icon={<UserRoundX size={16} />} disabled={effectiveCampus === undefined} onClick={() => setDeclareOpen(true)}>
            Khai báo vắng
          </Button>
        }
      />
      {campuses.isSuccess && campuses.data.length === 0 && (
        <InlineAlert type="warning" title="Chưa có phân hiệu nào để thao tác" description="Hiệu phó cần được gán phụ trách phân hiệu ở Hồ sơ → Trách nhiệm." />
      )}
      {effectiveCampus !== undefined && (
        <Tabs
          activeKey={tab} onChange={setTab}
          items={[
            { key: 'pending', label: 'Tiết chờ xử lý', children: <PendingTab campusId={effectiveCampus} lead={campusSelect} onChanged={refresh} /> },
            { key: 'absences', label: 'Khai báo vắng', children: <AbsenceTab campusId={effectiveCampus} lead={campusSelect} onChanged={refresh} /> },
          ]}
        />
      )}
      {effectiveCampus !== undefined && (
        <DeclareAbsenceModal
          open={declareOpen} campusId={effectiveCampus} onClose={() => setDeclareOpen(false)}
          onDone={() => { setDeclareOpen(false); setTab('pending'); }}
        />
      )}
    </>
  );
}

// ---------------------------------------------------------------- Tiết chờ xử lý
function PendingTab({ campusId, lead, onChanged }: { campusId: number; lead: React.ReactNode; onChanged: () => void }) {
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
            { title: 'Ngày', dataIndex: 'date', width: 150, className: 'tabular', render: (v: string) => `${weekdayOfDate(v)} ${formatDate(v)}` },
            {
              title: 'Tiết', key: 'slot', width: 170,
              render: (_: unknown, s: PendingSession) => `${sessionOfDayLabel(s.sessionOfDay)} tiết ${s.periodNo} (${formatTime(s.startTime)}–${formatTime(s.endTime)})`,
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
            {
              title: 'Thao tác', key: 'actions', width: 90, align: 'center', fixed: 'right',
              render: (_: unknown, s: PendingSession) => (
                <RowActions actions={[{ key: 'resolve', label: 'Xử lý tiết', icon: <ClipboardCheck size={16} />, onClick: () => resolve(s) }]} />
              ),
            },
          ]}
        />
      </Card>
      <ResolveModal target={resolving} onClose={() => setResolving(null)} onDone={() => { setResolving(null); onChanged(); }} />
    </>
  );
}

// ---------------------------------------------------------------- Khai báo vắng
function AbsenceTab({ campusId, lead, onChanged }: { campusId: number; lead: React.ReactNode; onChanged: () => void }) {
  const absences = useQuery({ queryKey: ['scd', 'absences', campusId], queryFn: () => scdApi.absences(campusId) });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });

  const remove = (a: Absence) => confirmAction({
    title: `Xóa khai báo vắng của ${a.teacherName}?`,
    content: `${a.pendingCount} tiết đang Chờ xử lý sẽ trở về ${a.teacherName}. Nếu giáo viên đã có tiết khác trùng lịch ở một tiết nào đó thì không xóa được, và bạn xử lý tiết đó trước.`,
    okText: 'Xóa khai báo',
    danger: true,
    onOk: async () => {
      try {
        const res = await scdApi.deleteAbsence(a.absenceId);
        onChanged();
        message.success(`Đã xóa khai báo, ${res.restoredCount} tiết trở về giáo viên.`);
      } catch (e) {
        showError(e, 'Không xóa được khai báo');
      }
    },
  });

  return (
    <>
      <FilterBar>{lead}</FilterBar>
      <Card className="table-card">
        <DataTable<Absence>
          rowKey="absenceId" loading={absences.isLoading} dataSource={absences.data ?? []}
          emptyText="Chưa có khai báo giáo viên vắng nào."
          columns={[
            { title: 'Giáo viên', key: 't', width: 200, render: (_: unknown, a: Absence) => <>{a.teacherName} {a.teacherAbbr && <code className="mono">{a.teacherAbbr}</code>}</> },
            {
              title: 'Khoảng ngày', key: 'range', width: 190, className: 'tabular',
              render: (_: unknown, a: Absence) => a.fromDate === a.toDate ? formatDate(a.fromDate) : `${formatDate(a.fromDate)} – ${formatDate(a.toDate)}`,
            },
            { title: 'Phạm vi', key: 'scope', render: (_: unknown, a: Absence) => absenceScopeLabel(a.slotIds, slots.data) },
            { title: 'Lý do', dataIndex: 'reason', render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Người khai', dataIndex: 'declaredByName', width: 150, render: (v: string | null) => v ?? '—' },
            { title: 'Còn chờ xử lý', dataIndex: 'pendingCount', width: 120, align: 'right', className: 'tabular' },
            { title: 'Đã xử lý', dataIndex: 'handledCount', width: 100, align: 'right', className: 'tabular' },
            {
              title: 'Thao tác', key: 'actions', width: 90, align: 'center', fixed: 'right',
              render: (_: unknown, a: Absence) => (
                <div className="row-actions">
                  <Tooltip title={a.canDelete ? 'Xóa khai báo' : 'Đã có tiết được xử lý (dạy thay hoặc hủy), không xóa được'}>
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
    </>
  );
}
