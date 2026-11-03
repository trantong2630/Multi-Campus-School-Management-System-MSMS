import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, Segmented, Select, Space } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import { KeyRound, Lock, LockOpen, Repeat } from 'lucide-react';
import { useState } from 'react';
import { useCampuses } from '@/features/sch/hooks/useCatalogs';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDateTime } from '@/shared/lib/format';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { PersonCell } from '@/shared/components/PersonCell';
import { accountApi } from '../accountApi';
import { STAFF_ACCOUNT_TYPES } from '../accountTypes';
import { TempPasswordModal } from '../components/TempPasswordModal';
import { CreateAccountsModal } from '../components/CreateAccountsModal';
import { AccountImpact } from '../components/AccountImpact';
import type { Account, LockImpact, TempPasswordItem } from '../types';

const ALL_TYPES: AccountType[] = ['SchoolAdmin', 'Principal', 'VicePrincipal', 'Teacher', 'Student', 'Parent'];
const STATUSES = ['Active', 'PendingPasswordChange', 'Locked'];
const STATUS_LABEL: Record<string, string> = { Active: 'Đang hoạt động', PendingPasswordChange: 'Chờ đổi mật khẩu', Locked: 'Đã khóa' };
const PAGE_SIZE = 20;

interface TypeValues { accountType: string }

/** IAM-02: quản lý vòng đời tài khoản từ các hồ sơ sẵn có. */
export function AccountsPage() {
  const qc = useQueryClient();
  const campuses = useCampuses();
  const [search, setSearch] = useState('');
  const [accountType, setAccountType] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();
  const [campusId, setCampusId] = useState<number | undefined>();
  const [page, setPage] = useState(1);
  const [form] = Form.useForm<TypeValues>();
  const [changing, setChanging] = useState<Account | null>(null);
  const [changeImpact, setChangeImpact] = useState<LockImpact | null>(null);
  const [createMode, setCreateMode] = useState<'single' | 'bulk' | null>(null);
  const [issued, setIssued] = useState<TempPasswordItem[] | null>(null);
  const [skipped, setSkipped] = useState<{ personId: number; fullName: string; reason: string }[]>([]);

  const filters = { search: search || undefined, accountType, status, campusId };
  const accounts = useQuery({
    queryKey: ['iam', 'accounts', filters, page],
    queryFn: () => accountApi.list({ ...filters, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });
  const refresh = () => Promise.all([
    qc.invalidateQueries({ queryKey: ['iam', 'accounts'] }),
    qc.invalidateQueries({ queryKey: ['iam', 'directory'] }),
    qc.invalidateQueries({ queryKey: ['ppl'] }),
  ]);
  const reset = () => setPage(1);

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action();
      await refresh();
      message.success(success);
    } catch (e) {
      showError(e);
    }
  };

  const toItem = (a: Account, r: { username: string; tempPassword: string }, type?: string): TempPasswordItem => ({
    personId: a.personId, personCode: a.personCode, fullName: a.fullName, username: r.username,
    accountType: type ?? a.accountType, tempPassword: r.tempPassword,
  });

  const changeType = async (v: TypeValues) => {
    const res = await accountApi.changeType(changing!.accountId, v.accountType);
    setIssued([toItem(changing!, res, v.accountType)]);
    void refresh();
  };

  const allowedTypes = (a: Account) =>
    (a.personType === 'Staff' ? STAFF_ACCOUNT_TYPES : [a.accountType]).filter((t) => t !== a.accountType);

  return (
    <>
      <PageHeader
        title="Tài khoản"
        description="Quản lý tài khoản cán bộ, học sinh và phụ huynh. Mỗi hồ sơ có đúng một tài khoản."
        extra={<Space><Button onClick={() => setCreateMode('bulk')}>Tạo hàng loạt</Button><Button type="primary" onClick={() => setCreateMode('single')}>Tạo tài khoản</Button></Space>}
      />
      <FilterBar>
        <Segmented<string>
          value={status ?? 'All'} onChange={(v) => { setStatus(v === 'All' ? undefined : v); reset(); }}
          options={[{ value: 'All', label: 'Tất cả' }, ...STATUSES.map((s) => ({ value: s, label: STATUS_LABEL[s] }))]}
        />
        <Input.Search
          allowClear placeholder="Tên, mã hồ sơ hoặc tên đăng nhập" enterButton="Tìm kiếm"
          onSearch={(v) => { setSearch(v); reset(); }}
        />
        <Select allowClear placeholder="Loại tài khoản" value={accountType} onChange={(v) => { setAccountType(v); reset(); }}
          options={ALL_TYPES.map((t) => ({ value: t, label: accountTypeLabel[t] }))} />
        <Select allowClear placeholder="Phân hiệu chính" value={campusId} onChange={(v) => { setCampusId(v); reset(); }}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))} />
      </FilterBar>

      <Card className="table-card">
        <DataTable<Account>
          rowKey="accountId" loading={accounts.isLoading} dataSource={accounts.data?.items}
          emptyText="Chưa có tài khoản nào khớp bộ lọc."
          pagination={{ current: page, pageSize: PAGE_SIZE, total: accounts.data?.total, onChange: setPage }}
          columns={[
            { title: 'Họ và tên', dataIndex: 'fullName', width: 220, render: (v: string, a: Account) => <PersonCell name={v} sub={a.personCode} /> },
            { title: 'Tên đăng nhập', dataIndex: 'username', width: 170, render: (v: string) => <code className="mono">{v}</code> },
            { title: 'Loại tài khoản', dataIndex: 'accountType', width: 140, render: (v: AccountType) => accountTypeLabel[v] },
            { title: 'Phân hiệu', dataIndex: 'campusName', width: 120, render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Đăng nhập gần nhất', dataIndex: 'lastLoginAt', width: 150, className: 'tabular', render: (v: string | null) => (v ? formatDateTime(v) : <span className="muted">Chưa đăng nhập</span>) },
            { title: 'Trạng thái', dataIndex: 'status', width: 150, align: 'center', render: (v: string) => <StatusBadge domain="account" value={v} /> },
            {
              title: 'Thao tác', key: 'actions', width: 120, align: 'center', fixed: 'right',
              render: (_: unknown, a: Account) => (
                <RowActions actions={[
                  a.status === 'Locked'
                    ? {
                      key: 'unlock', label: 'Mở khóa tài khoản', icon: <LockOpen size={16} />,
                      onClick: () => void run(() => accountApi.unlock(a.accountId), 'Đã mở khóa tài khoản.'),
                    }
                    : {
                      key: 'lock', label: 'Khóa tài khoản', icon: <Lock size={16} />, danger: true,
                      onClick: async () => {
                        let impact: LockImpact | null = null;
                        try { impact = await accountApi.lockImpact(a.accountId); } catch (e) { showError(e); return; }
                        confirmAction({
                          title: `Khóa tài khoản ${a.username}?`,
                          content: (
                            <div>
                              <p>Người này không đăng nhập được và phiên đang mở bị từ chối ở thao tác kế tiếp. Hồ sơ và lịch sử giữ nguyên.</p>
                              <AccountImpact impact={impact} />
                            </div>
                          ),
                          okText: 'Khóa tài khoản', danger: true,
                          onOk: () => run(() => accountApi.lock(a.accountId), 'Đã khóa tài khoản.'),
                        });
                      },
                    },
                  {
                    key: 'reset', label: 'Cấp lại mật khẩu', icon: <KeyRound size={16} />,
                    onClick: () => confirmAction({
                      title: `Cấp lại mật khẩu cho ${a.fullName}?`,
                      content: 'Hệ thống sinh mật khẩu tạm mới, hiện một lần. Tài khoản quay về Chờ đổi mật khẩu; mọi phiên đăng nhập và mã OTP cũ mất hiệu lực.',
                      okText: 'Cấp lại mật khẩu',
                      onOk: async () => {
                        try {
                          const res = await accountApi.resetPassword(a.accountId);
                          setIssued([toItem(a, res)]);
                          void refresh();
                        } catch (e) { showError(e); throw e; }
                      },
                    }),
                  },
                  { key: 'type', label: 'Đổi loại tài khoản', icon: <Repeat size={16} />, hidden: allowedTypes(a).length === 0,
                    onClick: async () => {
                      try {
                        const impact = await accountApi.lockImpact(a.accountId);
                        setChangeImpact(impact);
                        setChanging(a);
                      } catch (e) { showError(e); }
                    },
                  },
                ]} />
              ),
            },
          ]}
        />
      </Card>

      <FormModal<TypeValues>
        open={changing !== null} title={`Đổi loại tài khoản — ${changing?.fullName ?? ''}`}
        form={form} onClose={() => setChanging(null)} onSubmit={changeType} okText="Đổi loại"
      >
        <p style={{ marginTop: 0, color: 'var(--c-text2)' }}>
          Chỉ dùng khi cấp sai loại. Hệ thống ghi nhật ký, cấp lại mật khẩu tạm và đưa tài khoản về Chờ đổi mật khẩu. Hồ sơ và lịch sử nghiệp vụ giữ nguyên.
        </p>
        <p>Mọi phiên đăng nhập sẽ bị thu hồi.</p>
        {changeImpact && <AccountImpact impact={changeImpact} />}
        <Form.Item label="Loại tài khoản mới" name="accountType" rules={[{ required: true, message: 'Chọn loại tài khoản mới.' }]}>
          <Select options={(changing ? allowedTypes(changing) : []).map((t) => ({ value: t, label: accountTypeLabel[t as AccountType] }))} />
        </Form.Item>
      </FormModal>

      {createMode && <CreateAccountsModal mode={createMode} onClose={() => setCreateMode(null)} onCreated={(result) => {
        setCreateMode(null);
        setIssued(result.created);
        setSkipped(result.skipped);
        void refresh();
      }} />}
      <TempPasswordModal open={issued !== null} title="Kết quả cấp tài khoản và mật khẩu tạm" items={issued ?? []}
        skipped={skipped} onClose={() => { setIssued(null); setSkipped([]); }} />
    </>
  );
}
