import { modal } from '@/shared/lib/antdStatic';
import { blobErrorMessage } from '@/shared/lib/download';
import { showError } from '@/shared/lib/feedback';

/** Lỗi khi tải file: Backend trả lỗi dạng JSON nhưng axios giữ nó trong Blob, nên đọc ngược ra để hiện đúng lời nhắn tiếng Việt. */
export async function showDownloadError(error: unknown) {
  const text = await blobErrorMessage(error);
  if (text) modal.error({ title: 'Không thực hiện được', content: text, okText: 'Đóng' });
  else showError(error);
}
