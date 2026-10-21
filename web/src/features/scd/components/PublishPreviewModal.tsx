import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Checkbox, Modal, Skeleton } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { Rocket } from 'lucide-react';
import { useState } from 'react';
import { DataTable } from '@/shared/components/DataTable';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, sessionOfDayLabel, weekdayLabel } from '@/shared/lib/format';
import { scdApi } from '../api';
import type { TeacherConflict } from '../types';
import './scd.css';

interface Props {
  versionId: number | null;
  onClose: () => void;
}

/**
 * Xem trước trước khi Phát hành (BR-14): số tiết xóa và tạo, cảnh báo tiết Chỉnh tay / tiết bù / tiết Chờ xử lý sẽ bị xóa,
 * danh sách xung đột giáo viên. Còn xung đột là chặn Phát hành. Xem trước không lưu gì; lúc phát hành hệ thống tính lại.
 */
export function PublishPreviewModal({ versionId, onClose }: Props) {
  const qc = useQueryClient();
  const [confirmed, setConfirmed] = useState(false);
  const [publishing, setPublishing] = useState(false);

  const preview = useQuery({
    queryKey: ['scd', 'preview', versionId],
    queryFn: () => scdApi.preview(versionId!),
    enabled: versionId !== null,
    gcTime: 0,
  });
  const p = preview.data;

  const close = () => { setConfirmed(false); onClose(); };

  const publish = async () => {
    if (!p) return;
    setPublishing(true);
    try {
      const res = await scdApi.publish(p.versionId, confirmed);
      await qc.invalidateQueries({ queryKey: ['scd'] });
      message.success(`Đã phát hành: xóa ${res.deletedCount} tiết, tạo ${res.createdCount} tiết, thông báo cho ${res.notifiedTeachers} giáo viên.`);
      close();
    } catch (e) {
      showError(e, 'Không phát hành được');
      void preview.refetch();
    } finally {
      setPublishing(false);
    }
  };

  const askPublish = () => {
    if (!p) return;
    confirmAction({
      title: `Phát hành thời khóa biểu ${p.campusName}, ${p.semesterName}?`,
      content: `Sẽ xóa ${p.deleteCount} tiết và tạo ${p.createCount} tiết từ ${formatDate(p.fromDate)} đến ${formatDate(p.toDate)}. Tiết đã qua và tiết đã điểm danh không bị đụng tới.`,
      okText: 'Phát hành',
      onOk: publish,
    });
  };

  const needsConfirm = p?.warnings.needsConfirmation ?? false;
  const canPublish = !!p && p.canPublish && (!needsConfirm || confirmed);

  return (
    <Modal
      open={versionId !== null}
      title={p ? `Xem trước phát hành — ${p.campusName}, ${p.semesterName}` : 'Xem trước phát hành'}
      width={820}
      onCancel={close}
      mask={{ closable: false }}
      footer={[
        <Button key="close" onClick={close}>Đóng</Button>,
        <Button key="publish" type="primary" icon={<Rocket size={16} />} disabled={!canPublish} loading={publishing} onClick={askPublish}>
          Phát hành
        </Button>,
      ]}
    >
      {preview.isLoading && <Skeleton active paragraph={{ rows: 6 }} />}
      {preview.isError && <InlineAlert type="error" title="Không dựng được bản xem trước. Đóng hộp thoại rồi thử lại." />}
      {p && (
        <>
          <div className="stat-row">
            <div className="stat-tile"><div className="stat-tile__value tabular">{p.deleteCount}</div><div className="stat-tile__label">tiết sẽ xóa</div></div>
            <div className="stat-tile"><div className="stat-tile__value tabular">{p.createCount}</div><div className="stat-tile__label">tiết sẽ tạo</div></div>
            <div className="stat-tile">
              <div className="stat-tile__value tabular" style={{ fontSize: 18 }}>{formatDate(p.fromDate)} – {formatDate(p.toDate)}</div>
              <div className="stat-tile__label">khoảng ngày sinh lại</div>
            </div>
          </div>

          {p.blockingIssues.map((issue) => <InlineAlert key={issue} type="error" title={issue} />)}

          {p.conflicts.length > 0 && (
            <div style={{ marginBottom: 16 }}>
              <InlineAlert
                type="error"
                title={`Còn ${p.conflicts.length} xung đột giáo viên — chặn phát hành`}
                description="Giáo viên đang có tiết ở phân hiệu khác cùng khung giờ. Hiệu phó phân hiệu kia sửa lịch, hoặc quản trị hệ thống gỡ, rồi xem trước lại."
              />
              <DataTable<TeacherConflict>
                rowKey={(c) => `${c.teacherAbbr}|${c.dayOfWeek}|${c.sessionOfDay}|${c.periodNo}|${c.occupiedClassName}`}
                dataSource={p.conflicts} pagination={{ pageSize: 5 }}
                columns={[
                  { title: 'Giáo viên', key: 't', width: 160, render: (_: unknown, c: TeacherConflict) => <>{c.teacherName} <code className="mono">{c.teacherAbbr}</code></> },
                  { title: 'Khung giờ', key: 'slot', width: 150, render: (_: unknown, c: TeacherConflict) => `${weekdayLabel(c.dayOfWeek)}, ${sessionOfDayLabel(c.sessionOfDay)} tiết ${c.periodNo}` },
                  { title: 'Lớp trong file', dataIndex: 'newClassName', width: 110 },
                  { title: 'Đang chiếm chỗ', key: 'occ', render: (_: unknown, c: TeacherConflict) => `${c.occupiedSubject} ${c.occupiedClassName} · ${c.occupiedCampusName}` },
                  { title: 'Hiệu phó phụ trách', dataIndex: 'occupiedViceName', width: 160, render: (v: string | null) => v ?? <span className="muted">Chưa có</span> },
                  { title: 'Số buổi', dataIndex: 'sessionCount', width: 90, align: 'right', className: 'tabular' },
                ]}
              />
            </div>
          )}

          {needsConfirm && (
            <InlineAlert
              type="warning"
              title="Phát hành sẽ ghi đè và xóa cả tiết đã chỉnh tay"
              description={
                <>
                  <p style={{ margin: '0 0 8px' }}>
                    Sẽ xóa {p.warnings.manualEditedCount} tiết Chỉnh tay, {p.warnings.makeUpCount} tiết bù và {p.warnings.pendingCount} tiết Chờ xử lý.
                  </p>
                  <Checkbox checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)}>
                    Đã hiểu, xác nhận xóa các tiết trên
                  </Checkbox>
                </>
              }
            />
          )}

          <DataTable<{ className: string; deleteCount: number; createCount: number }>
            rowKey="className" dataSource={p.byClass} pagination={{ pageSize: 8 }}
            emptyText="Không có tiết nào thay đổi."
            columns={[
              { title: 'Lớp', dataIndex: 'className' },
              { title: 'Tiết sẽ xóa', dataIndex: 'deleteCount', width: 140, align: 'right', className: 'tabular' },
              { title: 'Tiết sẽ tạo', dataIndex: 'createCount', width: 140, align: 'right', className: 'tabular' },
            ]}
          />
        </>
      )}
    </Modal>
  );
}
