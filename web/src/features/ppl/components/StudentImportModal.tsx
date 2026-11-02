import { useQueryClient } from '@tanstack/react-query';
import { Button, Modal } from 'antd';
import { FileDown, FileUp } from 'lucide-react';
import { useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { FileButton } from '@/shared/components/FileButton';
import { ImportErrorPanel } from '@/shared/components/ImportErrorPanel';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { blobErrorMessage, downloadFile } from '@/shared/lib/download';
import { pplApi } from '../api';
import type { StudentImportResult } from '../types';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Năm học và phân hiệu đang chọn ở màn hình gọi: điền sẵn vào hai ô meta của file mẫu. */
  yearId?: number;
  campusId?: number;
}

/**
 * Import học sinh một bước (BR-11): gửi file, validate toàn bộ rồi mới ghi. Có một lỗi là không ghi gì.
 * Một dòng là một học sinh kèm phụ huynh đại diện; cột Tên lớp có giá trị thì ghi danh luôn.
 */
export function StudentImportModal({ open, onClose, yearId, campusId }: Props) {
  const qc = useQueryClient();
  const [uploading, setUploading] = useState(false);
  const [result, setResult] = useState<StudentImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const close = () => { setResult(null); setError(null); onClose(); };

  const template = async () => {
    try { await downloadFile('/ppl/students/import-template', { yearId, campusId }); }
    catch (e) { message.error(await blobErrorMessage(e) ?? 'Không tải được file mẫu.'); }
  };

  const upload = async (file: File) => {
    setUploading(true);
    setResult(null);
    setError(null);
    try {
      const res = await pplApi.students.import(file);
      setResult(res);
      if (res.accepted) {
        // Sĩ số, danh sách lớp và ô tìm học sinh để ghi danh đều đổi theo.
        void qc.invalidateQueries({ queryKey: ['sch'] });
        void qc.invalidateQueries({ queryKey: ['ppl'] });
        if (res.students > 0) message.success(`Đã tạo ${res.students} học sinh.`);
      }
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setUploading(false);
    }
  };

  return (
    <Modal
      open={open} title="Import học sinh từ Excel" width={820} onCancel={close} mask={{ closable: false }}
      footer={<Button onClick={close}>Đóng</Button>}
    >
      <p className="import-modal__hint">
        Tải file mẫu, điền rồi tải lên. Mỗi dòng là một học sinh mới kèm phụ huynh đại diện; mỗi file một phân hiệu và một năm học.
        Cột Tên lớp có giá trị thì ghi danh luôn từ ngày bắt đầu năm học (lớp phải tạo trước). Học sinh đã có (trùng số định danh) bị bỏ qua
        cả dòng; import chỉ tạo hồ sơ, không tạo tài khoản.
      </p>
      <div className="import-modal__actions">
        <Button icon={<FileDown size={16} />} onClick={template}>Tải file mẫu</Button>
        <FileButton type="primary" icon={<FileUp size={16} />} loading={uploading} onFile={upload}>Tải lên</FileButton>
      </div>

      {error && <InlineAlert type="error" title={error} />}
      {result?.accepted && (
        <InlineAlert
          type="success"
          title={
            `Đã tạo ${result.students} học sinh, ${result.enrollments} ghi danh và ${result.parents} phụ huynh mới`
            + (result.skipped.length ? `, bỏ qua ${result.skipped.length} học sinh đã có.` : '.')
          }
        />
      )}
      {result && (!result.accepted || result.skipped.length > 0) && (
        <ImportErrorPanel errors={result.errors} totalErrors={result.totalErrors} skipped={result.skipped} />
      )}
    </Modal>
  );
}
