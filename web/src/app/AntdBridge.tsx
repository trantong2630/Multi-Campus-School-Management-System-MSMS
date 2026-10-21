import { App as AntApp } from 'antd';
import { bindAntdApp } from '@/shared/lib/antdStatic';

/** Nối message/modal của <AntApp> vào các hàm dùng chung ngoài component. */
export function AntdBridge() {
  const { message, modal } = AntApp.useApp();
  bindAntdApp(message, modal);
  return null;
}
