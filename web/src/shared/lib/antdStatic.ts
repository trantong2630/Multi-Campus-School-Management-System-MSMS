import { Modal, message as staticMessage } from 'antd';
import type { MessageInstance } from 'antd/es/message/interface';
import type { HookAPI } from 'antd/es/modal/useModal';

let appMessage: MessageInstance | null = null;
let appModal: HookAPI | null = null;

/** Gọi một lần trong <AntApp> để toast và hộp thoại dùng đúng theme của ConfigProvider (hàm tĩnh của AntD không đọc được context). */
export function bindAntdApp(message: MessageInstance, modal: HookAPI) {
  appMessage = message;
  appModal = modal;
}

export const message = {
  success: (content: string) => (appMessage ?? staticMessage).success(content),
  error: (content: string) => (appMessage ?? staticMessage).error(content),
  info: (content: string) => (appMessage ?? staticMessage).info(content),
  warning: (content: string) => (appMessage ?? staticMessage).warning(content),
};

export const modal = {
  confirm: (props: Parameters<typeof Modal.confirm>[0]) => (appModal ?? Modal).confirm(props),
  error: (props: Parameters<typeof Modal.error>[0]) => (appModal ?? Modal).error(props),
};
