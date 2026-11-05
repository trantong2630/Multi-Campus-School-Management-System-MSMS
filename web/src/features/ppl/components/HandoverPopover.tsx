import { Button, Popover } from 'antd';
import dayjs from 'dayjs';
import { X } from 'lucide-react';
import { useState } from 'react';
import { formatDate } from '@/shared/lib/format';
import type { ResponsibilityRow } from '../types';
import './handover.css';

interface Props {
  row: ResponsibilityRow;
  /** Động từ trước ngày kết thúc, ví dụ "Phụ trách", "Chủ nhiệm". */
  verb: string;
  /** Mở lịch sử đầy đủ của đích này. */
  onDetail: () => void;
}

/** Có bàn giao khi người đang giữ đã có ngày kết thúc, hoặc đã có người được hẹn nhận. */
export function hasHandover(r: ResponsibilityRow): boolean {
  return Boolean((r.staffId && (r.endDate || r.nextStaffName)) || (!r.staffId && r.nextStaffName));
}

/**
 * "Xem lịch bàn giao": chỉ hiện khi đích sắp đổi người. Popover nhỏ nói người hiện tại làm đến hết ngày nào và ai nhận tiếp
 * từ ngày nào; lịch sử đầy đủ nằm sau "Xem chi tiết".
 */
export function HandoverPopover({ row, verb, onDetail }: Props) {
  const [open, setOpen] = useState(false);
  const detail = () => { setOpen(false); onDetail(); };

  // Người bàn giao: người đang giữ, hoặc người vừa kết thúc ngay ngày liền trước ngày người mới bắt đầu (thay người có hẹn).
  // Trường hợp sau hệ thống không còn tính là "đang giữ" nhưng vẫn là người sắp bàn giao nên không thể ghi "Chưa có".
  const handedOver = row.endedStaffName && row.endedDate && row.nextStartDate
    && dayjs(row.nextStartDate).diff(dayjs(row.endedDate), 'day') === 1;
  const outgoing = row.staffId
    ? { name: row.staffName!, endDate: row.endDate, startDate: row.startDate }
    : handedOver ? { name: row.endedStaffName!, endDate: row.endedDate, startDate: null } : null;

  const content = (
    <div className="handover">
      <div className="handover__step handover__step--line">
        <span className="handover__dot" />
        <div>
          <span className="handover__label">Người hiện tại</span>
          <strong className="handover__name">{outgoing?.name ?? 'Chưa có'}</strong>
          {outgoing && (
            <span className="handover__sub">
              {outgoing.endDate ? `Kết thúc vào ${formatDate(outgoing.endDate)}` : `${verb} từ ${formatDate(outgoing.startDate)}`}
            </span>
          )}
        </div>
      </div>
      <div className="handover__step handover__step--next">
        <span className="handover__dot" />
        <div>
          <span className="handover__label">Người tiếp nhận</span>
          <strong className="handover__name">{row.nextStaffName ?? 'Chưa có người tiếp nhận'}</strong>
          {row.nextStaffName
            ? <span className="handover__sub">Bắt đầu từ {formatDate(row.nextStartDate)}</span>
            : <span className="handover__sub">Cần gán người trước ngày kết thúc</span>}
        </div>
      </div>
      <div className="handover__footer">
        <Button type="link" onClick={detail}>Xem chi tiết ›</Button>
      </div>
    </div>
  );

  return (
    <Popover
      open={open} onOpenChange={setOpen} trigger="click" placement="bottomLeft" content={content}
      title={(
        <div className="handover__title">
          <span>Lịch bàn giao</span>
          <Button type="text" size="small" className="icon-btn" aria-label="Đóng" icon={<X size={16} />} onClick={() => setOpen(false)} />
        </div>
      )}
    >
      <Button type="link" size="small" style={{ padding: 0, height: 'auto' }}>Xem lịch bàn giao</Button>
    </Popover>
  );
}