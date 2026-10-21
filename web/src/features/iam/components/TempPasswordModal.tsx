import { Button, Modal } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { Download } from 'lucide-react';
import { useState } from 'react';
import { blobErrorMessage, downloadFile } from '@/shared/lib/download';
import { DataTable } from '@/shared/components/DataTable';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { accountApi } from '../accountApi';
import type { TempPasswordItem } from '../types';

interface Props {
  open: boolean;
  title: string;
  items: TempPasswordItem[];
  skipped?: { personId: number; fullName: string; reason: string }[];
  onClose: () => void;
}

/**
 * Mật khẩu tạm hiện MỘT lần ở đây kèm nút tải Excel để bàn giao. File sinh tức thời từ kết quả vừa tạo,
 * không lưu và không xuất lại được; mất thì phải cấp lại mật khẩu (BR-04).
 */
export function TempPasswordModal({ open, title, items, skipped = [], onClose }: Props) {
  const [downloading, setDownloading] = useState(false);

  const download = async () => {
    setDownloading(true);
    try {
      await downloadFile(accountApi.sheetUrl, undefined, 'post', { items });
    } catch (e) {
      message.error(await blobErrorMessage(e) ?? 'Không tải được file. Thử lại.');
    } finally {
      setDownloading(false);
    }
  };

  return (
    <Modal
      open={open}
      title={title}
      width={720}
      onCancel={onClose}
      mask={{ closable: false }}
      footer={[
        <Button key="close" onClick={onClose}>Đóng</Button>,
        items.length > 0 && (
          <Button key="download" type="primary" icon={<Download size={16} />} loading={downloading} onClick={download}>
            Tải file Excel
          </Button>
        ),
      ]}
    >
      {items.length > 0 && (
        <InlineAlert
          type="warning"
          title="Mật khẩu tạm chỉ hiện một lần"
          description="Tải file Excel để bàn giao ngay. Đóng hộp thoại này thì không xem lại được; mất thì phải cấp lại mật khẩu."
        />
      )}
      <DataTable<TempPasswordItem>
        rowKey="personId" dataSource={items} pagination={false} scroll={{ y: 320 }}
        emptyText="Không tạo được tài khoản nào."
        columns={[
          { title: 'Mã hồ sơ', dataIndex: 'personCode', width: 110, render: (v: string) => <span className="tabular">{v}</span> },
          { title: 'Họ và tên', dataIndex: 'fullName' },
          { title: 'Loại tài khoản', dataIndex: 'accountType', width: 140, render: (v: string) => accountTypeLabel[v as AccountType] ?? v },
          { title: 'Tên đăng nhập', dataIndex: 'username', width: 170, render: (v: string) => <code className="mono">{v}</code> },
          { title: 'Mật khẩu tạm', dataIndex: 'tempPassword', width: 150, render: (v: string) => <code className="mono">{v}</code> },
        ]}
      />
      {skipped.length > 0 && (
        <InlineAlert
          style={{ marginTop: 16 }} type="info" title={`${skipped.length} hồ sơ bị bỏ qua`}
          description={<ul style={{ margin: 0, paddingLeft: 18 }}>{skipped.map((s) => <li key={s.personId}>{s.fullName}: {s.reason}</li>)}</ul>}
        />
      )}
    </Modal>
  );
}
