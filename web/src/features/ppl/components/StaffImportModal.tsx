import { useQueryClient } from '@tanstack/react-query';
import { Button, Modal } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { FileDown, FileUp } from 'lucide-react';
import { useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { FileButton } from '@/shared/components/FileButton';
import { ImportErrorPanel } from '@/shared/components/ImportErrorPanel';
import { blobErrorMessage, downloadFile } from '@/shared/lib/download';
import { pplApi } from '../api';
import type { StaffImportResult } from '../types';

/** Import một bước (BR-11): gửi file, validate toàn bộ rồi mới ghi. Có một lỗi là không ghi gì. */
export function StaffImportModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const [uploading, setUploading] = useState(false);
  const [result, setResult] = useState<StaffImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const close = () => { setResult(null); setError(null); onClose(); };

  const template = async () => {
    try { await downloadFile('/ppl/staff/import-template'); }
    catch (e) { message.error(await blobErrorMessage(e) ?? 'Không tải được file mẫu.'); }
  };

  const upload = async (file: File) => {
    setUploading(true);
    setResult(null);
    setError(null);
    try {
      const res = await pplApi.staff.import(file);
      setResult(res);
      if (res.accepted) {
        void qc.invalidateQueries({ queryKey: ['ppl', 'staff'] });
        message.success(`Đã tạo ${res.created} hồ sơ cán bộ.`);
      }
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setUploading(false);
    }
  };

  return (
    <Modal
      open={open} title="Import cán bộ từ Excel" width={820} onCancel={close} mask={{ closable: false }}
      footer={<Button onClick={close}>Đóng</Button>}
    >
      <p className="import-modal__hint">
        Tải file mẫu, điền rồi tải lên. Người đã có (trùng họ tên và ngày sinh) bị bỏ qua cả dòng; import chỉ tạo hồ sơ, không tạo tài khoản.
      </p>
      <div className="import-modal__actions">
        <Button icon={<FileDown size={16} />} onClick={template}>Tải file mẫu</Button>
        <FileButton type="primary" icon={<FileUp size={16} />} loading={uploading} onFile={upload}>Tải lên</FileButton>
      </div>

      {error && <InlineAlert type="error" title={error} />}
      {result?.accepted && (
        <InlineAlert
          type="success"
          title={`Đã tạo ${result.created} hồ sơ cán bộ${result.skipped.length ? `, bỏ qua ${result.skipped.length} người đã có` : ''}.`}
        />
      )}
      {result && (!result.accepted || result.skipped.length > 0) && (
        <ImportErrorPanel errors={result.errors} totalErrors={result.totalErrors} skipped={result.skipped} />
      )}
    </Modal>
  );
}
