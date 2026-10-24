import { Button, Drawer, Form, Modal } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import type { FormInstance } from 'antd';
import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { getErrorMessage } from '@/app/apiClient';

interface Props<T> {
  open: boolean;
  title: string;
  form: FormInstance<T>;
  onClose: () => void;
  onSubmit: (values: T) => Promise<unknown>;
  children: ReactNode;
  width?: number;
  okText?: string;
  /** `drawer` cho form dài (khoảng 8 trường trở lên): trượt từ phải, nút ở chân cố định. Mặc định là hộp thoại giữa màn hình. */
  variant?: 'modal' | 'drawer';
  /** Giá trị ban đầu; null từ API được đổi thành undefined để ô nhập trống thay vì lỗi kiểu. */
  initialValues?: object;
}

/**
 * Form tạo/sửa chuẩn: nhãn trên ô nhập, nút "Đóng" bên trái "Lưu", lỗi nêu nguyên nhân và nằm yên trên màn hình
 * cho tới khi người dùng sửa (Design_Web §8.4). Nút đóng không bao giờ ghi "Hủy" vì "Hủy" trong MSMS là hủy tiết.
 */
export function FormModal<T extends object>({
  open, title, form, onClose, onSubmit, children, width = 520, okText = 'Lưu', variant = 'modal', initialValues,
}: Props<T>) {
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setError(null);
      form.resetFields();
      if (initialValues) {
        form.setFieldsValue(Object.fromEntries(Object.entries(initialValues).map(([k, v]) => [k, v === null ? undefined : v])) as never);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const handleOk = async () => {
    const values = await form.validateFields().catch(() => null);
    if (!values) return;
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(values);
      onClose();
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  const body = (
    <Form form={form} layout="vertical" requiredMark={false} onValuesChange={() => setError(null)} className={`form-${variant}`}>
      {children}
      {error && <InlineAlert type="error" title={error} />}
    </Form>
  );

  if (variant === 'drawer') {
    return (
      <Drawer
        open={open}
        title={title}
        size={width}
        onClose={onClose}
        mask={{ closable: false }}
        destroyOnHidden
        footer={(
          <div className="form-drawer__footer">
            <Button onClick={onClose}>Đóng</Button>
            <Button type="primary" loading={submitting} onClick={handleOk}>{okText}</Button>
          </div>
        )}
      >
        {body}
      </Drawer>
    );
  }

  return (
    <Modal
      open={open}
      title={title}
      width={width}
      okText={okText}
      cancelText="Đóng"
      confirmLoading={submitting}
      onOk={handleOk}
      onCancel={onClose}
      mask={{ closable: false }}
      destroyOnHidden
    >
      {body}
    </Modal>
  );
}
