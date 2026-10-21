import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Card, DatePicker, Drawer, Form, Select, Tabs } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { CircleOff, History, UserPlus, UserRoundCog } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { useCurrentYear } from '@/features/sch/hooks/useCatalogs';
import { DataTable } from '@/shared/components/DataTable';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { formatDate, toDateString } from '@/shared/lib/format';
import { pplApi } from '../api';
import type { ResponsibilityHistoryRow, ResponsibilityKind, ResponsibilityRow } from '../types';

interface AssignValues { staffId: number; startDate: Dayjs }
interface EndValues { endDate: Dayjs }

const KINDS: { key: ResponsibilityKind; label: string; target: string; holder: string; accountType: string; accountLabel: string }[] = [
  { key: 'campus', label: 'Phụ trách phân hiệu', target: 'Phân hiệu', holder: 'Hiệu phó phụ trách', accountType: 'VicePrincipal', accountLabel: 'Hiệu phó' },
  { key: 'department', label: 'Tổ trưởng', target: 'Tổ chuyên môn', holder: 'Tổ trưởng', accountType: 'Teacher', accountLabel: 'Giáo viên' },
  { key: 'homeroom', label: 'Chủ nhiệm', target: 'Lớp', holder: 'Chủ nhiệm', accountType: 'Teacher', accountLabel: 'Giáo viên' },
];

/** PPL-04: trách nhiệm có khoảng hiệu lực. Chỉ Principal và School Admin gán (spec 2.2). */
export function ResponsibilitiesPage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin' || user?.accountType === 'Principal';
  const [kind, setKind] = useState<ResponsibilityKind>('campus');

  return (
    <>
      <PageHeader
        title="Trách nhiệm"
        description="Mỗi đích có tối đa một người đang đảm nhiệm. Thay người thì bản cũ kết thúc vào ngày liền trước ngày bản mới bắt đầu."
      />
      <Tabs
        activeKey={kind}
        onChange={(k) => setKind(k as ResponsibilityKind)}
        items={KINDS.map((k) => ({ key: k.key, label: k.label, children: <KindTable kind={k.key} canWrite={canWrite} /> }))}
      />
    </>
  );
}

function KindTable({ kind, canWrite }: { kind: ResponsibilityKind; canWrite: boolean }) {
  const meta = KINDS.find((k) => k.key === kind)!;
  const qc = useQueryClient();
  const { current } = useCurrentYear();
  const rows = useQuery({
    queryKey: ['ppl', 'resp', kind, current?.academicYearId],
    queryFn: () => pplApi.responsibilities.list(kind, current?.academicYearId),
    enabled: kind !== 'homeroom' || current !== undefined,
  });
  const staff = useQuery({
    queryKey: ['ppl', 'staff-options', meta.accountType],
    queryFn: () => pplApi.staff.list({ accountType: meta.accountType, pageSize: 200 }),
  });

  const [assignForm] = Form.useForm<AssignValues>();
  const [endForm] = Form.useForm<EndValues>();
  const [assigning, setAssigning] = useState<ResponsibilityRow | null>(null);
  const [ending, setEnding] = useState<ResponsibilityRow | null>(null);
  const [history, setHistory] = useState<ResponsibilityRow | null>(null);
  const historyData = useQuery({
    queryKey: ['ppl', 'resp-history', kind, history?.targetId],
    queryFn: () => pplApi.responsibilities.history(kind, history!.targetId),
    enabled: history !== null,
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['ppl', 'resp'] });

  const assign = async (v: AssignValues) => {
    await pplApi.responsibilities.assign({ kind, targetId: assigning!.targetId, staffId: v.staffId, startDate: toDateString(v.startDate) });
    await refresh();
    message.success(assigning!.staffId ? 'Đã thay người đảm nhiệm.' : 'Đã gán trách nhiệm.');
  };
  const end = async (v: EndValues) => {
    await pplApi.responsibilities.end({ kind, recordId: ending!.recordId!, endDate: toDateString(v.endDate) });
    await refresh();
    message.success('Đã kết thúc trách nhiệm.');
  };

  return (
    <>
      <Card className="table-card">
        <DataTable<ResponsibilityRow>
          rowKey="targetId" loading={rows.isLoading} dataSource={rows.data}
          emptyText={kind === 'homeroom' ? 'Chưa có lớp nào trong năm học hiện hành.' : `Chưa có ${meta.target.toLowerCase()} nào.`}
          columns={[
            { title: meta.target, dataIndex: 'targetName', render: (v: string) => <strong>{v}</strong> },
            ...(kind === 'homeroom' ? [{ title: 'Phân hiệu', dataIndex: 'targetGroup', width: 180 }] : []),
            {
              title: meta.holder, dataIndex: 'staffName',
              render: (v: string | null) => v ?? <span className="muted">Chưa có</span>,
            },
            { title: 'Từ ngày', dataIndex: 'startDate', width: 130, className: 'tabular', render: (v: string | null) => formatDate(v) },
            {
              title: 'Thao tác', key: 'actions', width: 140, align: 'center', fixed: 'right',
              render: (_: unknown, r: ResponsibilityRow) => (
                <RowActions actions={[
                  {
                    key: 'assign', label: r.staffId ? 'Thay người' : 'Gán người đảm nhiệm', hidden: !canWrite,
                    icon: r.staffId ? <UserRoundCog size={16} /> : <UserPlus size={16} />,
                    onClick: () => { setAssigning(r); },
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
        open={assigning !== null}
        title={assigning?.staffId ? `Thay ${meta.holder.toLowerCase()} — ${assigning.targetName}` : `Gán ${meta.holder.toLowerCase()} — ${assigning?.targetName ?? ''}`}
        form={assignForm} onClose={() => setAssigning(null)} onSubmit={assign}
        initialValues={{ startDate: dayjs() }}
      >
        {assigning?.staffId && (
          <InlineAlert
            type="info"
            title={`Hiện do ${assigning.staffName} đảm nhiệm từ ${formatDate(assigning.startDate)}.`}
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
            options={staff.data?.items.filter((s) => s.isActive && s.staffId !== assigning?.staffId)
              .map((s) => ({ value: s.staffId, label: `${s.fullName} (${s.personCode})` }))}
          />
        </Form.Item>
        <Form.Item label="Ngày hiệu lực" name="startDate" rules={[{ required: true, message: 'Chọn ngày bắt đầu hiệu lực.' }]}>
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
        </Form.Item>
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
            { title: 'Từ ngày', dataIndex: 'startDate', width: 120, className: 'tabular', render: (v: string) => formatDate(v) },
            { title: 'Đến ngày', dataIndex: 'endDate', width: 120, className: 'tabular', render: (v: string | null) => (v ? formatDate(v) : 'Đang đảm nhiệm') },
            { title: '', dataIndex: 'isEffective', width: 140, align: 'center', render: (v: boolean) => (v ? <StatusBadge domain="catalog" value="Active" /> : null) },
          ]}
        />
      </Drawer>
    </>
  );
}
