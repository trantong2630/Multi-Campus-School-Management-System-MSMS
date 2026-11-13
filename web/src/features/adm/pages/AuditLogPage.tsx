import { useQuery } from '@tanstack/react-query';
import { Card, DatePicker, Select } from 'antd';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonCell } from '@/shared/components/PersonCell';
import { formatDateTime } from '@/shared/lib/format';
import { auditActionLabel, auditCategoryLabel } from '@/shared/lib/labels';
import { admApi } from '../api';
import { AuditDetailDrawer } from '../components/AuditDetailDrawer';
import type { AuditLogItem } from '../types';

const PAGE_SIZE = 20;
const API_DATE = 'YYYY-MM-DD';

/** ADM-02 (task 4.9.8, 4.9.9): School Admin tra nhật ký, lọc theo nhóm, người thực hiện và thời gian (BR-28). */
export function AuditLogPage() {
  const [category, setCategory] = useState<string | undefined>();
  const [actorPersonId, setActorPersonId] = useState<number | undefined>();
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<number | null>(null);

  const filters = { category, actorPersonId, from: range?.[0].format(API_DATE), to: range?.[1].format(API_DATE) };
  const log = useQuery({
    queryKey: ['adm', 'audit-log', filters, page],
    queryFn: () => admApi.auditLog({ ...filters, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });
  const actors = useQuery({ queryKey: ['adm', 'audit-log', 'actors'], queryFn: admApi.auditActors });

  return (
    <>
      <PageHeader title="Nhật ký" />
      <FilterBar>
        <Select
          allowClear placeholder="Nhóm" value={category} onChange={(v) => { setCategory(v); setPage(1); }}
          options={Object.entries(auditCategoryLabel).map(([value, label]) => ({ value, label }))}
        />
        <Select
          allowClear showSearch optionFilterProp="label" placeholder="Người thực hiện" loading={actors.isLoading}
          value={actorPersonId} onChange={(v) => { setActorPersonId(v); setPage(1); }}
          options={actors.data?.map((a) => ({ value: a.personId, label: `${a.fullName} (${a.personCode})` }))}
        />
        <DatePicker.RangePicker
          format="DD/MM/YYYY" value={range}
          onChange={(v) => { setRange(v && v[0] && v[1] ? [v[0], v[1]] : null); setPage(1); }}
        />
      </FilterBar>

      <Card className="table-card">
        <DataTable<AuditLogItem>
          rowKey="auditId" loading={log.isLoading} dataSource={log.data?.items}
          emptyText="Chưa có bản ghi nào khớp bộ lọc."
          pagination={{ current: page, pageSize: PAGE_SIZE, total: log.data?.total, onChange: setPage }}
          onRow={(a) => ({ onClick: () => setOpenId(a.auditId), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Thời gian', dataIndex: 'createdAt', width: 170, className: 'tabular', render: (v: string) => formatDateTime(v) },
            { title: 'Người thực hiện', dataIndex: 'actorName', width: 240, render: (v: string, a: AuditLogItem) => <PersonCell name={v} sub={a.actorCode} /> },
            { title: 'Nhóm', dataIndex: 'category', width: 210, render: (v: string) => auditCategoryLabel[v] ?? v },
            { title: 'Hành động', dataIndex: 'action', render: (v: string) => auditActionLabel[v] ?? v },
            { title: 'Số đối tượng', dataIndex: 'targetCount', width: 120, align: 'right', className: 'tabular' },
          ]}
        />
      </Card>

      <AuditDetailDrawer auditId={openId} onClose={() => setOpenId(null)} />
    </>
  );
}
