import { Navigate, createBrowserRouter } from 'react-router-dom';
import { admRoutes } from '@/features/adm/routes';
import { iamRoutes } from '@/features/iam/routes';
import { pplRoutes } from '@/features/ppl/routes';
import { schRoutes } from '@/features/sch/routes';
import { scdRoutes } from '@/features/scd/routes';
import { LoginPage } from '@/features/iam/pages/LoginPage';
import { AppLayout } from './AppLayout';
import { RequireAuth } from './RequireAuth';

// Mỗi feature export routes của mình; file này chỉ ghép lại.
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        path: '/',
        element: <AppLayout />,
        children: [...admRoutes, ...iamRoutes, ...schRoutes, ...pplRoutes, ...scdRoutes],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
