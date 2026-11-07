import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Button, Card, DatePicker, Drawer, Form, Input, Select, Switch, Tabs } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { CircleOff, FileDown, History, Plus, Search, UserPlus, UserRoundCog } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { useCurrentYear, useYears } from '@/features/sch/hooks/useCatalogs';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonCell } from '@/shared/components/PersonCell';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { message } from '@/shared/lib/antdStatic';
import { blobErrorMessage, downloadFile } from '@/shared/lib/download';
import { formatDate, toDateString } from '@/shared/lib/format';
import { pplApi } from '../api';
import { HandoverPopover, hasHandover } from '../components/HandoverPopover';
import type { ResponsibilityHistoryRow, ResponsibilityKind, ResponsibilityRow } from '../types';

interface AssignValues { targetId?: number; staffId: number; startDate: Dayjs }
interface EndValues { endDate: Dayjs }

interface KindMeta {
  key: ResponsibilityKind; label: string; target: string; holder: string; verb: string; accountType: string; accountLabel: string;
  /** Cảnh báo khi đích chưa có người: "N <đích> <lacks>: ..." và hệ quả cho người dùng. */
  lacks: string;
  effect: string;
}

const KINDS: KindMeta[] = [
  {
    key: 'campus', label: 'Phụ trách phân hiệu', target: 'Phân hiệu', holder: 'Hiệu phó phụ trách', verb: 'Phụ trách', accountType: 'VicePrincipal', accountLabel: 'Hiệu phó',
    lacks: 'chưa có Hiệu phó phụ trách', effect: 'Chưa có Hiệu phó theo dõi các phân hiệu này cho tới khi có người được gán.',
  },
  {
    key: 'department', label: 'Tổ trưởng', target: 'Tổ chuyên môn', holder: 'Tổ trưởng', verb: 'Làm tổ trưởng', accountType: 'Teacher', accountLabel: 'Giáo viên',
    lacks: 'chưa có tổ trưởng', effect: 'Chưa có tổ trưởng theo dõi tiến độ bài tập rèn luyện của các tổ này cho tới khi có người được gán.',
  },
  {
    key: 'homeroom', label: 'Chủ nhiệm', target: 'Lớp', holder: 'Chủ nhiệm', verb: 'Chủ nhiệm', accountType: 'Teacher', accountLabel: 'Giáo viên',
    lacks: 'chưa có chủ nhiệm', effect: 'Đơn xin nghỉ của các lớp này nằm chờ tới khi có chủ nhiệm được gán.',
  },
];

type RespStatus = 'Assigned' | 'Upcoming' | 'Unassigned' | 'Ended';
const statusOf = (r: ResponsibilityRow): RespStatus =>
  r.staffId ? 'Assigned' : r.nextStaffName ? 'Upcoming' : r.endedStaffName ? 'Ended' : 'Unassigned';
const STATUS_OPTIONS: { value: RespStatus; label: string }[] = [
  { value: 'Assigned', label: 'Đã phân công' },
  { value: 'Upcoming', label: 'Sắp hiệu lực' },
  { value: 'Unassigned', label: 'Chưa phân công' },
  { value: 'Ended', label: 'Đã kết thúc' },
];
const dash = <span className="muted">—</span>;

/** Trạng thái một dòng trong lịch bàn giao: đã kết thúc từ hôm nay trở về trước thì quyền đã mất (BR-09). */
function handoverStatus(r: ResponsibilityHistoryRow): 'Current' | 'Upcoming' | 'Ended' {
  const today = dayjs().startOf('day');
  if (dayjs(r.startDate).isAfter(today)) return 'Upcoming';
  if (r.endDate && !dayjs(r.endDate).isAfter(today)) return 'Ended';
  return 'Current';
}

function useResponsibilities(kind: ResponsibilityKind, yearId?: number) {
  return useQuery({
    queryKey: ['ppl', 'resp', kind, kind === 'homeroom' ? yearId : null],
    queryFn: () => pplApi.responsibilities.list(kind, yearId),
    enabled: kind !== 'homeroom' || yearId !== undefined,
  });
}

/** PPL-04: trách nhiệm có khoảng hiệu lực. Chỉ Principal và School Admin gán (spec 2.2). */
export function ResponsibilitiesPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin' || user?.accountType === 'Principal';
  const { current } = useCurrentYear();
  const [kind, setKind] = useState<ResponsibilityKind>('campus');
  const [yearId, setYearId] = useState<number | undefined>();
  const effectiveYear = yearId ?? current?.academicYearId;

  const campus = useResponsibilities('campus');
  const department = useResponsibilities('department');
  const homeroom = useResponsibilities('homeroom', effectiveYear);
  const byKind = { campus, department, homeroom };

  return (
    <>
      <PageHeader
        title="Trách nhiệm"
        description="Mỗi đích có tối đa một người đang đảm nhiệm. Thay người thì bản cũ kết thúc vào ngày liền trước ngày bản mới bắt đầu."
      />
      <Tabs
        activeKey={kind}
        onChange={(k) => setKind(k as ResponsibilityKind)}
        items={KINDS.map((k) => {
          // Số đích chưa có người đảm nhiệm (và chưa có ai được hẹn): cần xử lý.
          const missing = byKind[k.key].data?.filter((r) => !r.staffId && !r.nextStaffName).length ?? 0;
          return {
            key: k.key,
            label: <span>{k.label}{missing > 0 && <Badge count={missing} color="#C26A0A" size="small" style={{ marginLeft: 8 }} />}</span>,
            children: (
              <KindTable
                kind={k.key} rows={byKind[k.key].data} loading={byKind[k.key].isLoading} canWrite={canWrite}
                yearId={effectiveYear} onYear={setYearId}
              />
            ),
          };
        })}
      />
    </>
  );
}

interface KindTableProps {
  kind: ResponsibilityKind;
  rows: ResponsibilityRow[] | undefined;
  loading: boolean;
  canWrite: boolean;
  yearId: number | undefined;
  onYear: (id: number | undefined) => void;
}

function KindTable({ kind, rows, loading, canWrite, yearId, onYear }: KindTableProps) {
  const meta = KINDS.find((k) => k.key === kind)!;
  const qc = useQueryClient();
  const years = useYears();
  const staff = useQuery({
    queryKey: ['ppl', 'staff-options', meta.accountType],
    queryFn: () => pplApi.staff.list({ accountType: meta.accountType, pageSize: 200 }),
  });

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<RespStatus | 'All'>('All');
  const [onlyMissing, setOnlyMissing] = useState(false);

  const [assignForm] = Form.useForm<AssignValues>();
  const [endForm] = Form.useForm<EndValues>();
  const [assigning, setAssigning] = useState<ResponsibilityRow | 'new' | null>(null);
  const [picked, setPicked] = useState<number | undefined>();
  const [ending, setEnding] = useState<ResponsibilityRow | null>(null);
  const [history, setHistory] = useState<ResponsibilityRow | null>(null);
  const historyData = useQuery({
    queryKey: ['ppl', 'resp-history', kind, history?.targetId],
    queryFn: () => pplApi.responsibilities.history(kind, history!.targetId),
    enabled: history !== null,
  });
  useEffect(() => { if (assigning === 'new') setPicked(undefined); }, [assigning]);

  const refresh = () => qc.invalidateQueries({ queryKey: ['ppl', 'resp'] });
  const target = assigning === 'new' ? rows?.find((r) => r.targetId === picked) : assigning;

  const assign = async (v: AssignValues) => {
    const targetId = assigning === 'new' ? v.targetId! : assigning!.targetId;
    await pplApi.responsibilities.assign({ kind, targetId, staffId: v.staffId, startDate: toDateString(v.startDate) });
    await refresh();
    message.success(target?.staffId ? 'Đã thay người đảm nhiệm.' : 'Đã gán trách nhiệm.');
  };
  const end = async (v: EndValues) => {
    await pplApi.responsibilities.end({ kind, recordId: ending!.recordId!, endDate: toDateString(v.endDate) });
    await refresh();
    message.success('Đã kết thúc trách nhiệm.');
  };
  const exportFile = async () => {
    try { await downloadFile('/ppl/responsibilities/export', { kind, yearId: kind === 'homeroom' ? yearId : undefined }); }
    catch (e) { message.error(await blobErrorMessage(e) ?? 'Không xuất được danh sách.'); }
  };

  // Đích chưa có người đảm nhiệm và chưa có ai được hẹn (4.3.34 cho lớp; tương tự cho phân hiệu và tổ chuyên môn).
  const missing = rows?.filter((r) => !r.staffId && !r.nextStaffName) ?? [];

  const term = search.trim().toLowerCase();
  const shown = (rows ?? []).filter((r) =>
    (!onlyMissing || (!r.staffId && !r.nextStaffName))
    && (status === 'All' || statusOf(r) === status)
    && (!term || [r.targetName, r.targetCode, r.targetGroup, r.staffName, r.staffCode, r.nextStaffName, r.endedStaffName]
      .some((x) => x?.toLowerCase().includes(term))));

  // Mỗi Hiệu phó một phân hiệu, mỗi giáo viên một tổ trưởng, mỗi giáo viên một lớp chủ nhiệm (cùng năm học): làm mờ người đang giữ đích khác (server vẫn kiểm lại, kể cả người được hẹn).
  // Gồm cả người đã được xếp cho đích khác nhưng chưa tới ngày hiệu lực: đã phân công thì không chọn thêm.
  const ledCampus = new Map<number, string>();
  if (kind === 'campus' || kind === 'homeroom' || kind === 'department') {
    const act = kind === 'campus' ? 'phụ trách' : kind === 'department' ? 'làm tổ trưởng' : 'chủ nhiệm';
    rows?.forEach((r) => {
      if (r.targetId === target?.targetId) return;
      if (r.staffId) ledCampus.set(r.staffId, `đang ${act} ${r.targetName}`);
      if (r.nextStaffId && !ledCampus.has(r.nextStaffId)) ledCampus.set(r.nextStaffId, `đã được xếp ${act} ${r.targetName} từ ${formatDate(r.nextStartDate)}`);
    });
  }

  return (
    <>
      <FilterBar>
        <Input
          allowClear prefix={<Search size={14} />} value={search} onChange={(e) => setSearch(e.target.value)}
          placeholder={`Tìm ${meta.target.toLowerCase()}, mã hoặc người phụ trách`}
        />
        <Select value={status} onChange={setStatus} options={[{ value: 'All', label: 'Tất cả trạng thái' }, ...STATUS_OPTIONS]} />
        {kind === 'homeroom' && (
          <Select
            placeholder="Năm học" value={yearId} onChange={onYear}
            options={years.data?.map((y) => ({ value: y.academicYearId, label: y.name }))}
          />
        )}
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <Button icon={<FileDown size={16} />} onClick={exportFile}>Xuất danh sách</Button>
          {canWrite && <Button type="primary" icon={<Plus size={16} />} onClick={() => setAssigning('new')}>Phân công</Button>}
        </div>
      </FilterBar>

      {missing.length > 0 && (
        <InlineAlert
          type="warning"
          title={`${missing.length} ${meta.target.toLowerCase()} ${meta.lacks}: ${missing.slice(0, 5).map((r) => r.targetName).join(', ')}${missing.length > 5 ? '…' : ''}`}
          description={meta.effect}
          action={<span>Chỉ hiện {meta.target.toLowerCase()} {meta.lacks} <Switch size="small" checked={onlyMissing} onChange={setOnlyMissing} /></span>}
        />
      )}

      <Card className="table-card">
        <DataTable<ResponsibilityRow>
          rowKey="targetId" loading={loading} dataSource={shown}
          emptyText={rows?.length ? 'Không có kết quả khớp bộ lọc.' : kind === 'homeroom' ? 'Chưa có lớp nào trong năm học này.' : `Chưa có ${meta.target.toLowerCase()} nào.`}
          columns={[
            ...(kind === 'campus' ? [{ title: 'Mã', dataIndex: 'targetCode', width: 110, className: 'tabular', render: (v: string | null) => v ?? dash }] : []),
            { title: meta.target, dataIndex: 'targetName', width: 200, render: (v: string) => <strong>{v}</strong> },
            ...(kind === 'homeroom' ? [{ title: 'Phân hiệu', dataIndex: 'targetGroup', width: 150 }] : []),
            {
              title: meta.holder, key: 'holder', width: 260,
              render: (_: unknown, r: ResponsibilityRow) => {
                if (r.staffId) {
                  return (
                    <div className="stack-cell">
                      <PersonCell name={r.staffName!} sub={r.staffCode ?? undefined} />
                      {r.nextStaffName && <span className="muted">Sắp nhận: {r.nextStaffName} từ {formatDate(r.nextStartDate)}</span>}
                    </div>
                  );
                }
                if (r.nextStaffName) return <PersonCell name={r.nextStaffName} sub={`Từ ${formatDate(r.nextStartDate)}`} />;
                if (r.endedStaffName) return <PersonCell name={r.endedStaffName} sub="Đã kết thúc" muted />;
                return <span className="muted">Chưa phân công</span>;
              },
            },
            {
              title: 'Thời gian hiệu lực', key: 'period', width: 210,
              render: (_: unknown, r: ResponsibilityRow) => {
                const range = r.staffId
                  ? `${formatDate(r.startDate)} – ${r.endDate ? formatDate(r.endDate) : 'hiện tại'}`
                  : r.nextStaffName ? `Từ ${formatDate(r.nextStartDate)}`
                    : r.endedStaffName ? `Đến ${formatDate(r.endedDate)}` : null;
                if (!range) return dash;
                // Chỉ khi đích sắp đổi người mới có "Xem lịch bàn giao"; lịch sử đầy đủ mở từ cột Thao tác hoặc "Xem chi tiết".
                return (
                  <div className="stack-cell">
                    <span className="tabular">{range}</span>
                    {hasHandover(r) && <HandoverPopover row={r} verb={meta.verb} onDetail={() => setHistory(r)} />}
                  </div>
                );
              },
            },
            { title: 'Trạng thái', key: 'status', width: 150, render: (_: unknown, r: ResponsibilityRow) => <StatusBadge domain="responsibility" value={statusOf(r)} /> },
            {
              title: 'Thao tác', key: 'actions', width: 140, align: 'center', fixed: 'right',
              render: (_: unknown, r: ResponsibilityRow) => (
                <RowActions actions={[
                  {
                    key: 'assign', label: r.staffId ? 'Thay người' : 'Gán người đảm nhiệm', hidden: !canWrite,
                    icon: r.staffId ? <UserRoundCog size={16} /> : <UserPlus size={16} />,
                    onClick: () => setAssigning(r),
                  },
                  { key: 'end', label: 'Kết thúc trách nhiệm', icon: <CircleOff size={16} />, danger: true, hidden: !canWrite || !r.staffId, onClick: () => setEnding(r) },
                  { key: 'history', label: 'Lịch sử', icon: <History size={16} />, onClick: () => setHistory(r) },

                ]} />
              ),
            },
          ]}
        />
      </Card>

      <FormModal<AssignValues>
        variant="drawer" width={640}
        open={assigning !== null}
        title={assigning === 'new'
          ? `Phân công ${meta.holder.toLowerCase()}`
          : assigning?.staffId ? `Thay ${meta.holder.toLowerCase()} — ${assigning.targetName}` : `Gán ${meta.holder.toLowerCase()} — ${assigning?.targetName ?? ''}`}
        form={assignForm} onClose={() => setAssigning(null)} onSubmit={assign}
        initialValues={{ startDate: dayjs() }}
      >
        <div className="form-grid">
          {assigning === 'new' && (
            <Form.Item label={meta.target} name="targetId" style={{ gridColumn: '1 / -1' }} rules={[{ required: true, message: `Chọn ${meta.target.toLowerCase()}.` }]}>
              <Select
                showSearch optionFilterProp="label" placeholder={`Chọn ${meta.target.toLowerCase()}`} onChange={setPicked}
                options={rows?.map((r) => ({ value: r.targetId, label: r.targetGroup ? `${r.targetName} · ${r.targetGroup}` : r.targetName }))}
              />
            </Form.Item>
          )}
          {target?.staffId && (
            <InlineAlert
              type="info" style={{ gridColumn: '1 / -1' }}
              title={`Hiện do ${target.staffName} đảm nhiệm từ ${formatDate(target.startDate)}.`}
              description="Người cũ kết thúc vào ngày liền trước ngày hiệu lực của người mới; dữ liệu đã phát sinh không bị đụng tới."
            />
          )}
          <Form.Item
            label={`Cán bộ (tài khoản loại ${meta.accountLabel})`} name="staffId"
            rules={[{ required: true, message: `Chọn cán bộ có tài khoản loại ${meta.accountLabel}.` }]}
            extra={staff.data?.items.length === 0 ? `Chưa có cán bộ nào có tài khoản loại ${meta.accountLabel}. Tạo tài khoản ở Hồ sơ → Cán bộ trước.` : undefined}
          >
            <Select
              showSearch optionFilterProp="label" placeholder="Chọn cán bộ"
              options={staff.data?.items.filter((s) => s.isActive && s.staffId !== target?.staffId)
                .map((s) => ({
                  value: s.staffId,
                  label: ledCampus.has(s.staffId)
                    ? `${s.fullName} (${s.personCode}) — ${ledCampus.get(s.staffId)}`
                    : `${s.fullName} (${s.personCode})`,
                  disabled: ledCampus.has(s.staffId),
                }))}
            />
          </Form.Item>
          <Form.Item label="Ngày hiệu lực" name="startDate" rules={[{ required: true, message: 'Chọn ngày bắt đầu hiệu lực.' }]}>
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
        </div>
      </FormModal>

      <FormModal<EndValues>
        open={ending !== null} title={`Kết thúc trách nhiệm — ${ending?.targetName ?? ''}`}
        form={endForm} onClose={() => setEnding(null)} onSubmit={end} okText="Kết thúc"
        initialValues={{ endDate: dayjs() }}
      >
        <InlineAlert
          type="warning"
          title={`${ending?.staffName ?? ''} sẽ mất quyền tương ứng ngay sau ngày kết thúc.`}
          description="Loại tài khoản không đổi."
        />
        <Form.Item label="Ngày kết thúc" name="endDate" rules={[{ required: true, message: 'Chọn ngày kết thúc.' }]}>
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
        </Form.Item>
      </FormModal>

      <Drawer open={history !== null} onClose={() => setHistory(null)} size={560} title={`Lịch sử — ${history?.targetName ?? ''}`} closable>
        <DataTable<ResponsibilityHistoryRow>
          rowKey="recordId" loading={historyData.isLoading} dataSource={historyData.data} pagination={false}
          emptyText="Chưa có ai từng đảm nhiệm."
          columns={[
            { title: 'Người đảm nhiệm', dataIndex: 'staffName' },
            { title: 'Từ ngày', dataIndex: 'startDate', width: 110, className: 'tabular', render: (v: string) => formatDate(v) },
            { title: 'Đến ngày', dataIndex: 'endDate', width: 110, className: 'tabular', render: (v: string | null) => (v ? formatDate(v) : 'Hiện tại') },
            { title: 'Trạng thái', key: 'status', width: 150, render: (_: unknown, r: ResponsibilityHistoryRow) => <StatusBadge domain="handover" value={handoverStatus(r)} /> },
          ]}
        />
      </Drawer>
    </>
  );
}