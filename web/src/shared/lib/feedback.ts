import { isAxiosError } from 'axios';
import type { ReactNode } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { modal } from './antdStatic';

/**
 * Lỗi người dùng phải sửa thì không dùng toast tự tắt (Design_Web §8.7): hiện hộp thoại, nút đóng luôn là "Đóng".
 */
export function showError(error: unknown, title = 'Không thực hiện được') {
  modal.error({ title, content: getErrorMessage(error), okText: 'Đóng' });
}

let lastQueryErrorAt = 0;

/** Lỗi tải dữ liệu: hiện một hộp thoại, không dồn nhiều hộp khi nhiều truy vấn cùng hỏng. Lỗi 401 do apiClient đưa về trang đăng nhập. */
export function showQueryError(error: unknown) {
  if (isAxiosError(error) && error.response?.status === 401) return;
  const now = Date.now();
  if (now - lastQueryErrorAt < 4000) return;
  lastQueryErrorAt = now;
  showError(error, 'Không tải được dữ liệu');
}

interface ConfirmOptions {
  title: string;
  content: ReactNode;
  okText?: string;
  danger?: boolean;
  onOk: () => Promise<unknown> | void;
}

/** Hỏi xác nhận trước hành động không lùi được, nêu rõ tác động bằng con số nếu có (Design_Web §8.4). Nút đóng luôn là "Đóng". */
export function confirmAction({ title, content, okText = 'Xác nhận', danger = false, onOk }: ConfirmOptions) {
  modal.confirm({
    title,
    content,
    okText,
    cancelText: 'Đóng',
    okButtonProps: { danger },
    centered: true,
    onOk,
  });
}
