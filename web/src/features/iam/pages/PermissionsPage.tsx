import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Input, Segmented, Space, Switch, Tooltip } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { Lock } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDateTime } from '@/shared/lib/format';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { iamApi } from '../api';
import type { FeatureCell, FeatureChange, FeatureImpact, FeatureRow } from '../types';
import './permissions.css';

type View = 'all' | 'off';

const cellKey = (type: AccountType, feature: string) => `${type}|${feature}`;

/** Phân quyền động: School Admin bật/tắt chức năng theo loại tài khoản, chỉ trong phạm vi nghiệp vụ của từng loại. */
export function PermissionsPage() {
  const qc = useQueryClient();
  const { refresh } = useAuth();
  const matrix = useQuery({ queryKey: ['iam', 'permissions'], queryFn: iamApi.permissions.get });
  // Chỉ giữ các ô đã đổi so với dữ liệu server; bỏ lưu là xóa hết.
  const [edits, setEdits] = useState<Record<string, boolean>>({});
  const [search, setSearch] = useState('');
  const [view, setView] = useState<View>('all');
  const [saving, setSaving] = useState(false);
  const [checking, setChecking] = useState(false);

  const valueOf = (row: FeatureRow, c: FeatureCell) => edits[cellKey(c.accountType, row.key)] ?? c.enabled;

  const toggle = (row: FeatureRow, c: FeatureCell, next: boolean) => {
    const key = cellKey(c.accountType, row.key);
    setEdits((prev) => {
      const copy = { ...prev };
      if (next === c.enabled) delete copy[key];
      else copy[key] = next;
      return copy;
    });
  };

  const changes: FeatureChange[] = useMemo(() => Object.entries(edits).map(([k, enabled]) => {
    const [accountType, featureKey] = k.split('|');
    return { accountType: accountType as AccountType, featureKey, enabled };
  }), [edits]);

  const rows = useMemo(() => {
    const all = matrix.data?.features ?? [];
    const term = search.trim().toLowerCase();
    return all.filter((r) => (!term || r.label.toLowerCase().includes(term) || r.group.toLowerCase().includes(term))
      && (view === 'all' || r.cells.some((c) => c.allowed && !(edits[cellKey(c.accountType, r.key)] ?? c.enabled))));
  }, [matrix.data, search, view, edits]);

  // Ô "Nhóm" gộp các dòng liền nhau cùng nhóm.
  const groupSpan = useMemo(() => rows.map((r, i) => {
    if (i > 0 && rows[i - 1].group === r.group) return 0;
    let n = 1;
    while (i + n < rows.length && rows[i + n].group === r.group) n++;
    return n;
  }), [rows]);

  const offCount = useMemo(() => (matrix.data?.features ?? []).reduce((sum, r) =>
    sum + r.cells.filter((c) => c.allowed && !c.locked && !(edits[cellKey(c.accountType, r.key)] ?? c.enabled)).length, 0), [matrix.data, edits]);

  const save = () => {
    const turningOff = changes.filter((c) => !c.enabled);
    const doSave = async () => {
      setSaving(true);
      try {
        const data = await iamApi.permissions.update(changes);
        qc.setQueryData(['iam', 'permissions'], data);
        setEdits({});
        // Menu của chính School Admin cũng đổi theo nếu Admin tắt chức năng của loại mình.
        await refresh().catch(() => {});
        message.success('Đã lưu phân quyền.');
      } catch (e) {
        showError(e);
      } finally {
        setSaving(false);
      }
    };
    if (turningOff.length === 0) { void doSave(); return; }
    void (async () => {
      // Đếm việc đang dở trước khi hỏi, để Admin biết tắt lúc này ảnh hưởng gì.
      setChecking(true);
      let impacts: FeatureImpact[] = [];
      try { impacts = await iamApi.permissions.impact(changes); } catch (e) { showError(e); return; } finally { setChecking(false); }
      confirmAction({
        title: `Tắt ${turningOff.length} chức năng?`,
        content: (
          <div className="perm-confirm">
            <p>
              Người dùng thuộc loại tài khoản bị tắt sẽ không còn thấy mục này trên menu và không gọi được chức năng ngay sau khi lưu.
              Dữ liệu đã có vẫn giữ nguyên; bật lại là dùng được như cũ.
            </p>
            {impacts.length > 0 && (
              <>
                <p className="perm-confirm__lead">Việc đang dở cần lưu ý:</p>
                <ul className="perm-confirm__list">
                  {impacts.map((i) => (
                    <li key={`${i.accountType}|${i.featureKey}`}>
                      <strong>{i.label} · {accountTypeLabel[i.accountType]}</strong>
                      <span>{i.message}</span>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>
        ),
        okText: 'Lưu phân quyền',
        danger: true,
        onOk: doSave,
      });
    })();
  };

  const types = matrix.data?.accountTypes ?? [];
  const columns: ColumnsType<FeatureRow> = [
    {
      title: 'Nhóm', dataIndex: 'group', width: 170, fixed: 'left',
      onCell: (_, i) => ({ rowSpan: groupSpan[i ?? 0] }),
      render: (v: string) => <span className="perm-group">{v}</span>,
    },
    { title: 'Chức năng', dataIndex: 'label', width: 220, fixed: 'left' },
    ...types.map((t) => ({
      title: accountTypeLabel[t], key: t, width: 120, align: 'center' as const,
      render: (_: unknown, row: FeatureRow) => {
        const c = row.cells.find((x) => x.accountType === t);
        // Cuộn xuống thì khó nhìn tiêu đề cột: di chuột vào ô nào cũng hiện loại tài khoản và chức năng của ô đó.
        const tip = (state: string) => (
          <div className="perm-tip">
            <strong>{accountTypeLabel[t]}</strong>
            <span>{row.label}</span>
            <span className="perm-tip__state">{state}</span>
          </div>
        );
        if (!c || !c.allowed) {
          return (
            <Tooltip title={tip('Không thuộc nghiệp vụ của loại tài khoản này')} mouseEnterDelay={0.15}>
              <span className="perm-hit"><span className="perm-na">—</span></span>
            </Tooltip>
          );
        }
        if (c.locked) {
          return (
            <Tooltip title={tip('Luôn bật, không tắt được')} mouseEnterDelay={0.15}>
              <span className="perm-hit"><span className="perm-locked"><Switch size="small" checked disabled /><Lock size={13} /></span></span>
            </Tooltip>
          );
        }
        const changed = edits[cellKey(t, row.key)] !== undefined;
        const on = valueOf(row, c);
        return (
          <Tooltip title={tip(`${on ? 'Đang bật' : 'Đang tắt'}${changed ? ' · chưa lưu' : ''}`)} mouseEnterDelay={0.15}>
            <span className="perm-hit">
              <span className={changed ? 'perm-cell perm-cell--changed' : 'perm-cell'}>
                <Switch
                  size="small" checked={on} aria-label={`${row.label} — ${accountTypeLabel[t]}`}
                  onChange={(next) => toggle(row, c, next)}
                />
              </span>
            </span>
          </Tooltip>
        );
      },
    })),
  ];

  const updated = matrix.data?.updatedAt
    ? `Cập nhật lần cuối ${formatDateTime(matrix.data.updatedAt)}${matrix.data.updatedByName ? ` bởi ${matrix.data.updatedByName}` : ''}.`
    : 'Chưa có thay đổi nào: mọi chức năng đang bật theo nghiệp vụ mặc định.';

  return (
    <>
      <PageHeader
        title="Phân quyền"
        extra={(
          <Space>
            <Button disabled={!changes.length || saving || checking} onClick={() => setEdits({})}>Hoàn tác</Button>
            <Button type="primary" disabled={!changes.length} loading={saving || checking} onClick={save}>
              {changes.length ? `Lưu thay đổi (${changes.length})` : 'Lưu thay đổi'}
            </Button>
          </Space>
        )}
      />
      <InlineAlert
        type="info"
        message="Bật hoặc tắt chức năng cho từng loại tài khoản"
        description={(
          <>
            Chỉ tắt được chức năng vốn thuộc nghiệp vụ của loại đó; ô “—” là chức năng loại tài khoản này không có.
            Quyền theo phạm vi (phân hiệu phụ trách, lớp chủ nhiệm, tổ trưởng, phụ trách môn) vẫn áp dụng như cũ. {updated}
          </>
        )}
      />
      <FilterBar>
        <Segmented<View>
          value={view} onChange={setView}
          options={[{ value: 'all', label: 'Tất cả chức năng' }, { value: 'off', label: `Đang tắt (${offCount})` }]}
        />
        <Input.Search allowClear placeholder="Tên chức năng hoặc nhóm" enterButton="Tìm kiếm" onSearch={setSearch} />
      </FilterBar>
      <Card className="table-card">
        <DataTable<FeatureRow>
          rowKey="key" loading={matrix.isLoading} dataSource={rows} columns={columns} pagination={false}
          emptyText={view === 'off' ? 'Không có chức năng nào đang tắt.' : 'Không có chức năng nào khớp từ khóa.'}
        />
      </Card>
    </>
  );
}
