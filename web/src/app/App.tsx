import { QueryClientProvider } from '@tanstack/react-query';
import { App as AntApp, ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { RouterProvider } from 'react-router-dom';
import { MinWidthGuard } from '@/shared/components/MinWidthGuard';
import { AntdBridge } from './AntdBridge';
import { antdTheme } from './antdTheme';
import { AuthProvider } from './AuthProvider';
import { queryClient } from './queryClient';
import { router } from './router';

dayjs.locale('vi');

export function App() {
  return (
    <ConfigProvider theme={antdTheme} locale={viVN}>
      <AntApp>
        <AntdBridge />
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <MinWidthGuard>
              <RouterProvider router={router} />
            </MinWidthGuard>
          </AuthProvider>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  );
}
