import { Alert } from 'antd';
import type { AlertProps } from 'antd';
import './shared.css';

/** Cảnh báo trong trang: luôn cách khối bên dưới 16px, không tự đặt style.marginBottom (Design_Web §8.7). */
export function InlineAlert({ className, ...rest }: AlertProps) {
  return <Alert showIcon className={['inline-alert', className].filter(Boolean).join(' ')} {...rest} />;
}
