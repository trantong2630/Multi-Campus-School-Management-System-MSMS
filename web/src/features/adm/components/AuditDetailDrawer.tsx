import { useQuery } from '@tanstack/react-query';
import { Descriptions, Drawer, Skeleton } from 'antd';
import { formatDate, formatDateTime } from '@/shared/lib/format';
import {
  accountTypeLabel, auditActionLabel, auditCategoryLabel, auditFieldLabel, auditTargetKindLabel, enrollmentCloseReasonLabel,
} from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { admApi } from '../api';

interface Props {
  auditId: number | null;
  onClose: () => void;
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** Giá trị còn ở dạng mã thì đổi sang nhãn: ngày, lý do kết thúc ghi danh, loại tài khoản (đổi loại lưu ở from và to). */
function showValue(key: string, value: string) {
  if (ISO_DATE.test(value)) return formatDate(value);
  if (key === 'reason') return enrollmentCloseReasonLabel[value] ?? value;
  if (key === 'from' || key === 'to') return accountTypeLabel[value as AccountType] ?? value;
  return value;
}

/** ADM-02 (task 4.9.10): nội dung đầy đủ của một bản ghi, gồm danh sách ID của thao tác hàng loạt. */
export function AuditDetailDrawer({ auditId, onClose }: Props) {
  const q = useQuery({
    queryKey: ['adm', 'audit-log', 'entry', auditId],
    queryFn: () => admApi.auditEntry(auditId!),
    enabled: auditId !== null,
  });
  const d = q.data;

  return (
    <Drawer
      open={auditId !== null} size={640} onClose={onClose} mask={{ closable: false }} destroyOnHidden
      title={d ? auditActionLabel[d.action] ?? d.action : 'Bản ghi nhật ký'}
    >
      {q.isLoading || !d ? <Skeleton active paragraph={{ rows: 8 }} /> : (
        <Descriptions column={1} size="small">
          <Descriptions.Item label="Thời gian"><span className="tabular">{formatDateTime(d.createdAt)}</span></Descriptions.Item>
          <Descriptions.Item label="Người thực hiện">{d.actorName} ({d.actorCode})</Descriptions.Item>
          <Descriptions.Item label="Nhóm">{auditCategoryLabel[d.category] ?? d.category}</Descriptions.Item>
          {d.targets.length > 0 && (
            <Descriptions.Item label={`${auditTargetKindLabel[d.targetKind] ?? d.targetKind} (${d.targets.length})`}>
              <div>
                {d.targets.map((t) => (
                  <div key={t.id} className="tabular">{t.label ? `${t.label} · #${t.id}` : `#${t.id}`}</div>
                ))}
              </div>
            </Descriptions.Item>
          )}
          {d.fields.map((f) => (
            <Descriptions.Item key={f.key} label={auditFieldLabel[f.key] ?? f.key}>{showValue(f.key, f.value)}</Descriptions.Item>
          ))}
        </Descriptions>
      )}
    </Drawer>
  );
}
