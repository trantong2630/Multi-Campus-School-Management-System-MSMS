import { apiClient } from '@/app/apiClient';

/** Tải file nhị phân từ API (kèm token) rồi lưu xuống máy. Tên file lấy từ Content-Disposition, Backend đã bỏ dấu. */
export async function downloadFile(url: string, params?: Record<string, unknown>, method: 'get' | 'post' = 'get', body?: unknown) {
  const res = await apiClient.request<Blob>({ url, method, params, data: body, responseType: 'blob' });
  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  const fileName = match ? decodeURIComponent(match[1]) : 'msms.xlsx';
  const href = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = href;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(href);
}

/** Lỗi khi tải file trả về Blob nên phải đọc ngược ra JSON để lấy message tiếng Việt. */
export async function blobErrorMessage(error: unknown): Promise<string | null> {
  const data = (error as { response?: { data?: unknown } }).response?.data;
  if (data instanceof Blob) {
    try {
      return (JSON.parse(await data.text()) as { message?: string }).message ?? null;
    } catch {
      return null;
    }
  }
  return null;
}
