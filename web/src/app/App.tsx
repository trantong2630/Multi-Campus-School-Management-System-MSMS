import { QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App as AntApp, ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { useEffect } from 'react';
import { RouterProvider } from 'react-router-dom';
import { useSchool } from '@/features/sch/hooks/useSchool';
import { MinWidthGuard } from '@/shared/components/MinWidthGuard';
import { showQueryError } from '@/shared/lib/feedback';
import { AntdBridge } from './AntdBridge';
import { antdTheme } from './antdTheme';
import { AuthProvider } from './AuthProvider';
import { router } from './router';

dayjs.locale('vi');

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: (error, query) => { if (!query.meta?.silent) showQueryError(error); } }),
  defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
});

/** Tên trường trên tab trình duyệt. */
function DocumentTitle() {
  const school = useSchool();
  useEffect(() => {
    document.title = school.hasName ? `${school.name} — MSMS` : 'MSMS';
  }, [school.name, school.hasName]);
  return null;
}

export function App() {
  return (
    <ConfigProvider theme={antdTheme} locale={viVN}>
      <AntApp>
        <AntdBridge />
        <QueryClientProvider client={queryClient}>
          <DocumentTitle />
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
