import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Card, Form, Input, Select } from 'antd';
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
import type { Account, LockImpact, TempPasswordItem } from '../types';

const ALL_TYPES: AccountType[] = ['SchoolAdmin', 'Principal', 'VicePrincipal', 'Teacher', 'Student', 'Parent'];
const dutyLabel: Record<string, string> = {
  Campus: 'Phụ trách phân hiệu', Department: 'Tổ trưởng', Homeroom: 'Chủ nhiệm lớp', Teaching: 'Phụ trách chuyên môn',
};
const STATUSES = ['PendingPasswordChange', 'Active', 'Locked'];
const PAGE_SIZE = 20;

interface TypeValues { accountType: string }

/** IAM-02: quản lý tài khoản. Tạo tài khoản làm trên danh bạ (chọn nhiều dòng), màn hình này lo khóa, mở, cấp lại, đổi loại. */
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
  const [issued, setIssued] = useState<TempPasswordItem[] | null>(null);

  const filters = { search: search || undefined, accountType, status, campusId };
  const accounts = useQuery({
    queryKey: ['iam', 'accounts', filters, page],
    queryFn: () => accountApi.list({ ...filters, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });
  const refresh = () => qc.invalidateQueries({ queryKey: ['iam', 'accounts'] });
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
    await refresh();
    setIssued([toItem(changing!, res, v.accountType)]);
  };

  const allowedTypes = (a: Account) =>
    (a.personType === 'Staff' ? STAFF_ACCOUNT_TYPES : [a.accountType]).filter((t) => t !== a.accountType);

  return (
    <>
      <PageHeader
        title="Tài khoản"
        description="Mỗi hồ sơ có đúng một tài khoản. Tạo tài khoản bằng cách chọn nhiều dòng ở Hồ sơ → Cán bộ."
      />
      <FilterBar>
        <Input.Search
          allowClear placeholder="Tên, mã hồ sơ hoặc tên đăng nhập" enterButton="Tìm kiếm"
          onSearch={(v) => { setSearch(v); reset(); }}
        />
        <Select allowClear placeholder="Loại tài khoản" value={accountType} onChange={(v) => { setAccountType(v); reset(); }}
          options={ALL_TYPES.map((t) => ({ value: t, label: accountTypeLabel[t] }))} />
        <Select allowClear placeholder="Trạng thái" value={status} onChange={(v) => { setStatus(v); reset(); }}
          options={STATUSES.map((s) => ({ value: s, label: { PendingPasswordChange: 'Chờ đổi mật khẩu', Active: 'Đang hoạt động', Locked: 'Đã khóa' }[s] }))} />
        <Select allowClear placeholder="Phân hiệu chính" value={campusId} onChange={(v) => { setCampusId(v); reset(); }}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))} />
      </FilterBar>

      <Card className="table-card">
        <DataTable<Account>
          rowKey="accountId" loading={accounts.isLoading} dataSource={accounts.data?.items}
          emptyText="Chưa có tài khoản nào khớp bộ lọc."
          pagination={{ current: page, pageSize: PAGE_SIZE, total: accounts.data?.total, onChange: setPage }}
          columns={[
            { title: 'Họ và tên', dataIndex: 'fullName', width: 260, render: (v: string, a: Account) => <PersonCell name={v} sub={a.personCode} /> },
            { title: 'Tên đăng nhập', dataIndex: 'username', width: 190, render: (v: string) => <code className="mono">{v}</code> },
            { title: 'Loại tài khoản', dataIndex: 'accountType', width: 160, render: (v: AccountType) => accountTypeLabel[v] },
            { title: 'Phân hiệu', dataIndex: 'campusName', width: 160, render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Đăng nhập gần nhất', dataIndex: 'lastLoginAt', width: 170, className: 'tabular', render: (v: string | null) => (v ? formatDateTime(v) : <span className="muted">Chưa đăng nhập</span>) },
            { title: 'Trạng thái', dataIndex: 'status', width: 170, align: 'center', render: (v: string) => <StatusBadge domain="account" value={v} /> },
            {
              title: 'Thao tác', key: 'actions', width: 130, align: 'center', fixed: 'right',
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
                              {impact.sessionCount > 0 && (
                                <p>
                                  {impact.sessionCount} tiết từ hôm nay trở đi sẽ chuyển sang <b>Chờ xử lý</b> ({impact.campuses.map((c) => `${c.campus}: ${c.count}`).join(', ')}).
                                  Hiệu phó phân hiệu sẽ nhận thông báo để đổi giáo viên hoặc hủy tiết.
                                </p>
                              )}
                              {impact.duties.length > 0 && (
                                <p>
                                  Trách nhiệm sẽ kết thúc vào hôm nay: {impact.duties.map((d) => `${dutyLabel[d.kind] ?? d.kind} ${d.targetName}`).join('; ')}.
                                </p>
                              )}
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
                      content: 'Hệ thống sinh mật khẩu tạm mới, hiện một lần. Tài khoản quay về Chờ đổi mật khẩu.',
                      okText: 'Cấp lại mật khẩu',
                      onOk: async () => {
                        try {
                          const res = await accountApi.resetPassword(a.accountId);
                          await refresh();
                          setIssued([toItem(a, res)]);
                        } catch (e) { showError(e); }
                      },
                    }),
                  },
                  { key: 'type', label: 'Đổi loại tài khoản', icon: <Repeat size={16} />, hidden: allowedTypes(a).length === 0, onClick: () => setChanging(a) },
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
        <Form.Item label="Loại tài khoản mới" name="accountType" rules={[{ required: true, message: 'Chọn loại tài khoản mới.' }]}>
          <Select options={(changing ? allowedTypes(changing) : []).map((t) => ({ value: t, label: accountTypeLabel[t as AccountType] }))} />
        </Form.Item>
      </FormModal>

      <TempPasswordModal open={issued !== null} title="Mật khẩu tạm mới" items={issued ?? []} onClose={() => setIssued(null)} />
    </>
  );
}
